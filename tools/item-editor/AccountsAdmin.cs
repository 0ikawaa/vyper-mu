// Alta, edicion y baja de cuentas y personajes.
//
// Cuentas: LoginName (1-10), PasswordHash con BCrypt (igual que OpenMU: BCrypt.Net-Next), baul
// (ItemStorage) obligatorio. Estados (AccountState): 0 Normal, 1 Spectator, 2 GameMaster,
// 3 GameMasterInvisible, 4 Banned, 5 TemporarilyBanned.
//
// Personajes: como CreateCharacterAction de OpenMU: StatAttribute por cada
// StatAttributeDefinition de la clase, mapa inicial de la clase, posicion en una puerta de
// spawn, inventario nuevo, KeyConfiguration por defecto. Estado (CharacterState): 0 Normal,
// 1 Banned, 32 GameMaster. La experiencia del nivel sale de la formula de OpenMU.

using Npgsql;

namespace VyperMu.ItemEditor;

public sealed record NewAccount(string LoginName, string Password, string? EMail, int State);

public sealed record AccountPatch(string? EMail, int? State, bool? IsVaultExtended, string? Password);

public sealed record NewCharacter(string Name, byte ClassNumber, int? Slot);

public sealed class CharacterPatch
{
    public string? Name { get; set; }
    public int? Level { get; set; }
    public int? LevelUpPoints { get; set; }
    public int? State { get; set; }
    public int? InventoryExtensions { get; set; }
    public int? PlayerKillCount { get; set; }
    public Dictionary<string, float>? Stats { get; set; }
}

public sealed record CreatableClass(Guid Id, string Name, byte Number, bool CanGetCreated);

public sealed class AccountsAdmin
{
    private const string LevelAttributeId = "560931ad-0901-4342-b7f4-fd2e2fcc0563";
    private const string PointsPerLevelAttribute = "Points per Level up";
    private readonly Db _db;

    public AccountsAdmin(Db db)
    {
        _db = db;
    }

    // ------------------------------------------------------------ cuentas

    public async Task<Guid> CreateAccountAsync(NewAccount a)
    {
        ValidateLogin(a.LoginName);
        ValidatePassword(a.Password);
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var exists = (bool)(await Scalar(conn, tx, "SELECT EXISTS (SELECT 1 FROM data.\"Account\" WHERE lower(\"LoginName\") = lower(@n))", ("n", a.LoginName)))!;
        if (exists) throw new InvalidOperationException("Ya existe una cuenta con ese nombre.");

        var vaultId = Guid.NewGuid();
        await Exec(conn, tx, "INSERT INTO data.\"ItemStorage\" (\"Id\", \"Money\") VALUES (@id, 0)", ("id", vaultId));
        var id = Guid.NewGuid();
        await Exec(conn, tx, """
            INSERT INTO data."Account" ("Id", "VaultId", "LoginName", "PasswordHash", "SecurityCode", "EMail", "RegistrationDate", "State", "TimeZone",
                "VaultPassword", "IsVaultExtended", "IsTemplate", "IsBot", "IsNetworkObservationActive")
            VALUES (@id, @vault, @login, @hash, '', @mail, now(), @state, 0, '', false, false, false, false)
            """, ("id", id), ("vault", vaultId), ("login", a.LoginName), ("hash", BCrypt.Net.BCrypt.HashPassword(a.Password)),
            ("mail", a.EMail ?? string.Empty), ("state", a.State));
        await tx.CommitAsync();
        return id;
    }

    public async Task UpdateAccountAsync(Guid id, AccountPatch p)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        if (p.EMail is not null) await Exec(conn, tx, "UPDATE data.\"Account\" SET \"EMail\" = @v WHERE \"Id\" = @id", ("id", id), ("v", p.EMail));
        if (p.State is { } state) await Exec(conn, tx, "UPDATE data.\"Account\" SET \"State\" = @v WHERE \"Id\" = @id", ("id", id), ("v", state));
        if (p.IsVaultExtended is { } ext) await Exec(conn, tx, "UPDATE data.\"Account\" SET \"IsVaultExtended\" = @v WHERE \"Id\" = @id", ("id", id), ("v", ext));
        if (!string.IsNullOrEmpty(p.Password))
        {
            ValidatePassword(p.Password);
            await Exec(conn, tx, "UPDATE data.\"Account\" SET \"PasswordHash\" = @v WHERE \"Id\" = @id", ("id", id), ("v", BCrypt.Net.BCrypt.HashPassword(p.Password)));
        }

        // sin baul (cuentas creadas por otras vias): se lo creamos
        var vault = await Scalar(conn, tx, "SELECT \"VaultId\" FROM data.\"Account\" WHERE \"Id\" = @id", ("id", id));
        if (vault is null)
        {
            var vaultId = Guid.NewGuid();
            await Exec(conn, tx, "INSERT INTO data.\"ItemStorage\" (\"Id\", \"Money\") VALUES (@id, 0)", ("id", vaultId));
            await Exec(conn, tx, "UPDATE data.\"Account\" SET \"VaultId\" = @v WHERE \"Id\" = @id", ("id", id), ("v", vaultId));
        }

        await tx.CommitAsync();
    }

    public async Task DeleteAccountAsync(Guid id)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var storages = await Query(conn, tx, "SELECT \"InventoryId\" FROM data.\"Character\" WHERE \"AccountId\" = @id AND \"InventoryId\" IS NOT NULL", r => r.GetGuid(0), ("id", id));
        var vault = await Scalar(conn, tx, "SELECT \"VaultId\" FROM data.\"Account\" WHERE \"Id\" = @id", ("id", id));
        // el FK Character -> Account es ON DELETE CASCADE y arrastra items, stats, skills, quests, cartas
        var n = await Exec(conn, tx, "DELETE FROM data.\"Account\" WHERE \"Id\" = @id", ("id", id));
        if (n != 1) throw new InvalidOperationException("La cuenta no existe.");
        foreach (var s in storages) await Exec(conn, tx, "DELETE FROM data.\"ItemStorage\" WHERE \"Id\" = @id", ("id", s));
        if (vault is Guid v) await Exec(conn, tx, "DELETE FROM data.\"ItemStorage\" WHERE \"Id\" = @id", ("id", v));
        await tx.CommitAsync();
    }

    // ------------------------------------------------------------ personajes

    public async Task<List<CreatableClass>> ClassesAsync()
    {
        await using var conn = await _db.OpenAsync();
        return await Query(conn, null, "SELECT \"Id\", \"Name\", \"Number\", \"CanGetCreated\" FROM config.\"CharacterClass\" ORDER BY \"Number\"",
            r => new CreatableClass(r.GetGuid(0), r.GetString(1), (byte)r.GetInt16(2), r.GetBoolean(3)));
    }

    public async Task<Guid> CreateCharacterAsync(Guid accountId, NewCharacter c)
    {
        if (string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 10 || !c.Name.All(char.IsLetterOrDigit))
            throw new InvalidOperationException("El nombre tiene que tener de 1 a 10 letras o numeros.");

        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        if ((bool)(await Scalar(conn, tx, "SELECT EXISTS (SELECT 1 FROM data.\"Character\" WHERE lower(\"Name\") = lower(@n))", ("n", c.Name)))!)
            throw new InvalidOperationException("Ya existe un personaje con ese nombre.");

        var cls = (await Query(conn, tx, "SELECT \"Id\", \"HomeMapId\" FROM config.\"CharacterClass\" WHERE \"Number\" = @n", r => (r.GetGuid(0), r.IsDBNull(1) ? (Guid?)null : r.GetGuid(1)), ("n", (short)c.ClassNumber))).FirstOrDefault();
        if (cls.Item1 == Guid.Empty) throw new InvalidOperationException("Clase desconocida.");

        var used = await Query(conn, tx, "SELECT \"CharacterSlot\" FROM data.\"Character\" WHERE \"AccountId\" = @id", r => (int)r.GetInt16(0), ("id", accountId));
        int slot = c.Slot ?? Enumerable.Range(0, 5).FirstOrDefault(s => !used.Contains(s), -1);
        if (slot < 0 || slot > 4 || used.Contains(slot)) throw new InvalidOperationException("No hay slot libre (maximo 5 personajes por cuenta).");

        var (x, y) = (125, 125);
        if (cls.Item2 is { } map)
        {
            var gate = (await Query(conn, tx, "SELECT \"X1\", \"Y1\", \"X2\", \"Y2\" FROM config.\"ExitGate\" WHERE \"MapId\" = @m AND \"IsSpawnGate\" ORDER BY random() LIMIT 1",
                r => (r.GetInt16(0), r.GetInt16(1), r.GetInt16(2), r.GetInt16(3)), ("m", map))).FirstOrDefault();
            if (gate != default) { x = (gate.Item1 + gate.Item3) / 2; y = (gate.Item2 + gate.Item4) / 2; }
        }

        var inventoryId = Guid.NewGuid();
        await Exec(conn, tx, "INSERT INTO data.\"ItemStorage\" (\"Id\", \"Money\") VALUES (@id, 0)", ("id", inventoryId));

        var keys = new byte[30];
        keys[21] = 1; keys[22] = 4; keys[23] = 0xFF; keys[25] = 0xFF;
        var id = Guid.NewGuid();
        await Exec(conn, tx, """
            INSERT INTO data."Character" ("Id", "CharacterClassId", "CurrentMapId", "InventoryId", "AccountId", "Name", "CharacterSlot", "CreateDate",
                "Experience", "MasterExperience", "LevelUpPoints", "MasterLevelUpPoints", "PositionX", "PositionY", "PlayerKillCount", "StateRemainingSeconds",
                "State", "CharacterStatus", "Pose", "UsedFruitPoints", "UsedNegFruitPoints", "InventoryExtensions", "KeyConfiguration", "IsStoreOpened")
            VALUES (@id, @cls, @map, @inv, @acc, @name, @slot, now(), 0, 0, 0, 0, @x, @y, 0, 0, 0, 0, 0, 0, 0, 0, @keys, false)
            """, ("id", id), ("cls", cls.Item1), ("map", cls.Item2), ("inv", inventoryId), ("acc", accountId), ("name", c.Name), ("slot", (short)slot),
            ("x", (short)x), ("y", (short)y), ("keys", keys));

        var stats = await Query(conn, tx, "SELECT \"AttributeId\", \"BaseValue\" FROM config.\"StatAttributeDefinition\" WHERE \"CharacterClassId\" = @c AND \"AttributeId\" IS NOT NULL",
            r => (r.GetGuid(0), r.GetFloat(1)), ("c", cls.Item1));
        foreach (var (attr, value) in stats)
        {
            await Exec(conn, tx, "INSERT INTO data.\"StatAttribute\" (\"Id\", \"DefinitionId\", \"CharacterId\", \"Value\") VALUES (@id, @def, @ch, @v)",
                ("id", Guid.NewGuid()), ("def", attr), ("ch", id), ("v", value));
        }

        await tx.CommitAsync();
        return id;
    }

    public async Task UpdateCharacterAsync(Guid id, CharacterPatch p)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var classId = (Guid?)await Scalar(conn, tx, "SELECT \"CharacterClassId\" FROM data.\"Character\" WHERE \"Id\" = @id", ("id", id)) ?? throw new InvalidOperationException("El personaje no existe.");

        if (p.Name is not null)
        {
            if (p.Name.Length is < 1 or > 10 || !p.Name.All(char.IsLetterOrDigit)) throw new InvalidOperationException("El nombre tiene que tener de 1 a 10 letras o numeros.");
            if ((bool)(await Scalar(conn, tx, "SELECT EXISTS (SELECT 1 FROM data.\"Character\" WHERE lower(\"Name\") = lower(@n) AND \"Id\" <> @id)", ("n", p.Name), ("id", id)))!)
                throw new InvalidOperationException("Ya existe un personaje con ese nombre.");
            await Exec(conn, tx, "UPDATE data.\"Character\" SET \"Name\" = @v WHERE \"Id\" = @id", ("id", id), ("v", p.Name));
        }

        if (p.Level is { } level)
        {
            if (level is < 1 or > 400) throw new InvalidOperationException("El nivel tiene que estar entre 1 y 400.");
            var current = (float?)await Scalar(conn, tx, "SELECT \"Value\" FROM data.\"StatAttribute\" WHERE \"CharacterId\" = @id AND \"DefinitionId\" = @def", ("id", id), ("def", Guid.Parse(LevelAttributeId))) ?? 1f;
            await SetStat(conn, tx, id, Guid.Parse(LevelAttributeId), level);
            await Exec(conn, tx, "UPDATE data.\"Character\" SET \"Experience\" = @v WHERE \"Id\" = @id", ("id", id), ("v", NeededExperience(level)));
            if (p.LevelUpPoints is null && level != (int)current)
            {
                var perLevel = (float?)await Scalar(conn, tx, """
                    SELECT s."BaseValue" FROM config."StatAttributeDefinition" s JOIN config."AttributeDefinition" a ON a."Id" = s."AttributeId"
                    WHERE s."CharacterClassId" = @c AND a."Designation" = @d
                    """, ("c", classId), ("d", PointsPerLevelAttribute)) ?? 5f;
                await Exec(conn, tx, "UPDATE data.\"Character\" SET \"LevelUpPoints\" = GREATEST(0, \"LevelUpPoints\" + @d) WHERE \"Id\" = @id", ("id", id), ("d", (int)((level - (int)current) * perLevel)));
            }
        }

        if (p.LevelUpPoints is { } pts) await Exec(conn, tx, "UPDATE data.\"Character\" SET \"LevelUpPoints\" = @v WHERE \"Id\" = @id", ("id", id), ("v", Math.Max(0, pts)));
        if (p.State is { } st) await Exec(conn, tx, "UPDATE data.\"Character\" SET \"State\" = @v WHERE \"Id\" = @id", ("id", id), ("v", st));
        if (p.InventoryExtensions is { } ext) await Exec(conn, tx, "UPDATE data.\"Character\" SET \"InventoryExtensions\" = @v WHERE \"Id\" = @id", ("id", id), ("v", Math.Clamp(ext, 0, 4)));
        if (p.PlayerKillCount is { } pk) await Exec(conn, tx, "UPDATE data.\"Character\" SET \"PlayerKillCount\" = @v WHERE \"Id\" = @id", ("id", id), ("v", Math.Max(0, pk)));

        if (p.Stats is not null)
        {
            foreach (var (designation, value) in p.Stats)
            {
                var def = (Guid?)await Scalar(conn, tx, "SELECT \"Id\" FROM config.\"AttributeDefinition\" WHERE \"Designation\" = @d", ("d", designation));
                if (def is { } attr) await SetStat(conn, tx, id, attr, value);
            }
        }

        await tx.CommitAsync();
    }

    public async Task DeleteCharacterAsync(Guid id)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var inv = await Scalar(conn, tx, "SELECT \"InventoryId\" FROM data.\"Character\" WHERE \"Id\" = @id", ("id", id));
        var n = await Exec(conn, tx, "DELETE FROM data.\"Character\" WHERE \"Id\" = @id", ("id", id));
        if (n != 1) throw new InvalidOperationException("El personaje no existe.");
        if (inv is Guid s) await Exec(conn, tx, "DELETE FROM data.\"ItemStorage\" WHERE \"Id\" = @id", ("id", s));
        await tx.CommitAsync();
    }

    /// <summary>Formula de experiencia de OpenMU (GameConfigurationInitializerBase.CalculateNeededExperience).</summary>
    public static long NeededExperience(long level)
    {
        if (level <= 0) return 0;
        if (level < 256) return 10 * (level + 8) * (level - 1) * (level - 1);
        return (10 * (level + 8) * (level - 1) * (level - 1)) + (1000 * (level - 247) * (level - 256) * (level - 256));
    }

    // ------------------------------------------------------------ helpers

    private static void ValidateLogin(string login)
    {
        if (string.IsNullOrWhiteSpace(login) || login.Length > 10 || !login.All(char.IsLetterOrDigit))
            throw new InvalidOperationException("El nombre de cuenta tiene que tener de 1 a 10 letras o numeros (el cliente no admite mas).");
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 20)
            throw new InvalidOperationException("La contrasena tiene que tener de 1 a 20 caracteres.");
    }

    private static async Task SetStat(NpgsqlConnection conn, NpgsqlTransaction tx, Guid characterId, Guid attribute, float value)
    {
        var n = await Exec(conn, tx, "UPDATE data.\"StatAttribute\" SET \"Value\" = @v WHERE \"CharacterId\" = @id AND \"DefinitionId\" = @def", ("id", characterId), ("def", attribute), ("v", value));
        if (n == 0)
        {
            await Exec(conn, tx, "INSERT INTO data.\"StatAttribute\" (\"Id\", \"DefinitionId\", \"CharacterId\", \"Value\") VALUES (@nid, @def, @id, @v)", ("nid", Guid.NewGuid()), ("def", attribute), ("id", characterId), ("v", value));
        }
    }

    private static async Task<List<T>> Query<T>(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, Func<NpgsqlDataReader, T> map, params (string, object?)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        var list = new List<T>();
        while (await r.ReadAsync()) list.Add(map(r));
        return list;
    }

    private static async Task<object?> Scalar(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, params (string, object?)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        var v2 = await cmd.ExecuteScalarAsync();
        return v2 is DBNull ? null : v2;
    }

    private static async Task<int> Exec(NpgsqlConnection conn, NpgsqlTransaction? tx, string sql, params (string, object?)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync();
    }
}
