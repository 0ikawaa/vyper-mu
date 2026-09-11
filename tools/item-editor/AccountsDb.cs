// Cuentas, personajes, inventario y baul (esquema "data" de OpenMU).
//
// Slots (InventoryConstants de OpenMU): 0-11 equipo (0 mano izq, 1 mano der, 2 casco, 3 armadura,
// 4 pantalon, 5 guantes, 6 botas, 7 alas, 8 mascota, 9 pendiente, 10/11 anillos); 12-75 inventario
// 8x8; 76-203 extensiones de 4 filas (segun Character.InventoryExtensions); 204-235 tienda personal.
// Baul: 0-119 (8x15), o 0-239 si Account.IsVaultExtended.
//
// Opciones de un item = filas de data.ItemOptionLink -> config.IncreasableItemOption (luck, option,
// excelentes, alas, harmony, sockets, bonus ancient); un item ancient ademas tiene una fila en
// data.ItemItemOfItemSet -> config.ItemOfItemSet. Igual que arma los items el comando /item de OpenMU.

using Npgsql;

namespace VyperMu.ItemEditor;

public sealed class AccountSummary
{
    public Guid Id { get; set; }
    public string LoginName { get; set; } = string.Empty;
    public string? EMail { get; set; }
    public int State { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public bool IsVaultExtended { get; set; }
    public Guid? VaultId { get; set; }
    public int Characters { get; set; }
    public List<string> CharacterNames { get; set; } = new();
}

public sealed class StoredItem
{
    public Guid Id { get; set; }
    public Guid StorageId { get; set; }
    public int Slot { get; set; }
    public Guid DefinitionId { get; set; }
    public byte Group { get; set; }
    public short Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte Width { get; set; }
    public byte Height { get; set; }
    public byte MaximumItemLevel { get; set; }
    public byte Level { get; set; }
    public double Durability { get; set; }
    public bool HasSkill { get; set; }
    public int SocketCount { get; set; }
    public int? StorePrice { get; set; }
    public int PetExperience { get; set; }
    public List<ItemOptionInstance> Options { get; set; } = new();
    public Guid? ItemOfItemSetId { get; set; }
}

public sealed class ItemOptionInstance
{
    public Guid? Id { get; set; }
    public Guid ItemOptionId { get; set; }
    public int Level { get; set; }
    public int Index { get; set; }
    public string? TypeName { get; set; }
    public int Number { get; set; }
    public string? Label { get; set; }
}

public sealed class CharacterInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Slot { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public byte ClassNumber { get; set; }
    public int Level { get; set; }
    public int MasterLevel { get; set; }
    public long Experience { get; set; }
    public int LevelUpPoints { get; set; }
    public int MasterLevelUpPoints { get; set; }
    public int Money { get; set; }
    public Guid InventoryId { get; set; }
    public int InventoryExtensions { get; set; }
    public string? MapName { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public int State { get; set; }
    public int CharacterStatus { get; set; }
    public int PlayerKillCount { get; set; }
    public DateTime? CreateDate { get; set; }
    public bool IsStoreOpened { get; set; }
    public string? StoreName { get; set; }
    public Dictionary<string, float> Stats { get; set; } = new();
    public List<StoredItem> Items { get; set; } = new();
}

public sealed class AccountDetail
{
    public AccountSummary Account { get; set; } = new();
    public List<CharacterInfo> Characters { get; set; } = new();
    public Guid? VaultId { get; set; }
    public int VaultMoney { get; set; }
    public List<StoredItem> Vault { get; set; } = new();
}

public sealed class OptionChoice
{
    public Guid Id { get; set; }
    public Guid TypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Label { get; set; } = string.Empty;
    public int MaxLevel { get; set; }
}

public sealed class AncientChoice
{
    public Guid ItemOfItemSetId { get; set; }
    public string SetName { get; set; } = string.Empty;
    public Guid? BonusOptionId { get; set; }
    public int Discriminator { get; set; }
}

public sealed class DefinitionOptions
{
    public Guid DefinitionId { get; set; }
    public bool HasSkill { get; set; }
    public int MaximumSockets { get; set; }
    public byte MaximumItemLevel { get; set; }
    public byte Durability { get; set; }
    public byte Width { get; set; }
    public byte Height { get; set; }
    public List<OptionChoice> Options { get; set; } = new();
    public List<AncientChoice> AncientSets { get; set; } = new();
}

public sealed class ItemWrite
{
    public Guid DefinitionId { get; set; }
    public Guid StorageId { get; set; }
    public int Slot { get; set; }
    public byte Level { get; set; }
    public double Durability { get; set; }
    public bool HasSkill { get; set; }
    public int SocketCount { get; set; }
    public int? StorePrice { get; set; }
    public int PetExperience { get; set; }
    public List<ItemOptionInstance> Options { get; set; } = new();
    public Guid? ItemOfItemSetId { get; set; }
}

public sealed record MoveRequest(Guid StorageId, int Slot);

public sealed class AccountsDb
{
    private const string LevelAttributeId = "560931ad-0901-4342-b7f4-fd2e2fcc0563";
    private const string MasterLevelAttributeId = "70cd8c10-391a-4c51-9aa4-a854600e3a9f";
    private readonly Db _db;

    public AccountsDb(Db db)
    {
        _db = db;
    }

    // ------------------------------------------------------------ lectura

    public async Task<List<AccountSummary>> ListAsync()
    {
        await using var conn = await _db.OpenAsync();
        var list = new List<AccountSummary>();
        await using var cmd = new NpgsqlCommand("""
            SELECT a."Id", a."LoginName", a."EMail", a."State", a."RegistrationDate", a."IsVaultExtended", a."VaultId",
                   (SELECT count(*) FROM data."Character" c WHERE c."AccountId" = a."Id"),
                   (SELECT string_agg(c."Name", ',' ORDER BY c."CharacterSlot") FROM data."Character" c WHERE c."AccountId" = a."Id")
            FROM data."Account" a WHERE a."IsTemplate" = false ORDER BY a."LoginName"
            """, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new AccountSummary
            {
                Id = r.GetGuid(0), LoginName = r.GetString(1), EMail = r.IsDBNull(2) ? null : r.GetString(2), State = r.GetInt32(3),
                RegistrationDate = r.IsDBNull(4) ? null : r.GetDateTime(4), IsVaultExtended = r.GetBoolean(5), VaultId = r.IsDBNull(6) ? null : r.GetGuid(6),
                Characters = (int)r.GetInt64(7), CharacterNames = r.IsDBNull(8) ? new() : r.GetString(8).Split(',').ToList(),
            });
        }

        return list;
    }

    public async Task<AccountDetail?> GetAsync(Guid accountId)
    {
        await using var conn = await _db.OpenAsync();
        var accounts = await Query(conn, """
            SELECT a."Id", a."LoginName", a."EMail", a."State", a."RegistrationDate", a."IsVaultExtended", a."VaultId"
            FROM data."Account" a WHERE a."Id" = @id
            """, r => new AccountSummary
        {
            Id = r.GetGuid(0), LoginName = r.GetString(1), EMail = r.IsDBNull(2) ? null : r.GetString(2), State = r.GetInt32(3),
            RegistrationDate = r.IsDBNull(4) ? null : r.GetDateTime(4), IsVaultExtended = r.GetBoolean(5), VaultId = r.IsDBNull(6) ? null : r.GetGuid(6),
        }, ("id", accountId));
        var account = accounts.SingleOrDefault();
        if (account is null) return null;

        var detail = new AccountDetail { Account = account, VaultId = account.VaultId };
        detail.Characters = await Query(conn, """
            SELECT c."Id", c."Name", c."CharacterSlot", cc."Name", cc."Number", c."Experience", c."LevelUpPoints", c."MasterLevelUpPoints",
                   COALESCE(s."Money", 0), c."InventoryId", c."InventoryExtensions", m."Name", c."PositionX", c."PositionY", c."State",
                   c."CharacterStatus", c."PlayerKillCount", c."CreateDate", c."IsStoreOpened", c."StoreName"
            FROM data."Character" c
            LEFT JOIN config."CharacterClass" cc ON cc."Id" = c."CharacterClassId"
            LEFT JOIN data."ItemStorage" s ON s."Id" = c."InventoryId"
            LEFT JOIN config."GameMapDefinition" m ON m."Id" = c."CurrentMapId"
            WHERE c."AccountId" = @id ORDER BY c."CharacterSlot"
            """, r => new CharacterInfo
        {
            Id = r.GetGuid(0), Name = r.GetString(1), Slot = r.GetInt16(2), ClassName = r.IsDBNull(3) ? "?" : r.GetString(3),
            ClassNumber = r.IsDBNull(4) ? (byte)0 : (byte)r.GetInt16(4), Experience = r.GetInt64(5), LevelUpPoints = r.GetInt32(6),
            MasterLevelUpPoints = r.GetInt32(7), Money = r.GetInt32(8), InventoryId = r.GetGuid(9), InventoryExtensions = r.GetInt32(10),
            MapName = r.IsDBNull(11) ? null : r.GetString(11), PositionX = r.GetInt16(12), PositionY = r.GetInt16(13), State = r.GetInt32(14),
            CharacterStatus = r.GetInt32(15), PlayerKillCount = r.GetInt32(16), CreateDate = r.IsDBNull(17) ? null : r.GetDateTime(17),
            IsStoreOpened = r.GetBoolean(18), StoreName = r.IsDBNull(19) ? null : r.GetString(19),
        }, ("id", accountId));

        foreach (var c in detail.Characters)
        {
            var stats = await Query(conn, """
                SELECT ad."Designation", sa."Value", sa."DefinitionId" FROM data."StatAttribute" sa
                JOIN config."AttributeDefinition" ad ON ad."Id" = sa."DefinitionId" WHERE sa."CharacterId" = @id
                """, r => (r.GetString(0), r.GetFloat(1), r.GetGuid(2)), ("id", c.Id));
            foreach (var (name, value, defId) in stats)
            {
                c.Stats[name] = value;
                if (defId.ToString() == LevelAttributeId) c.Level = (int)value;
                if (defId.ToString() == MasterLevelAttributeId) c.MasterLevel = (int)value;
            }

            c.Items = await LoadItems(conn, c.InventoryId);
        }

        if (account.VaultId is { } vaultId)
        {
            detail.VaultMoney = (int)((await Scalar(conn, "SELECT \"Money\" FROM data.\"ItemStorage\" WHERE \"Id\" = @id", ("id", vaultId))) ?? 0);
            detail.Vault = await LoadItems(conn, vaultId);
        }

        return detail;
    }

    private static async Task<List<StoredItem>> LoadItems(NpgsqlConnection conn, Guid storageId)
    {
        var items = await Query(conn, """
            SELECT i."Id", i."ItemStorageId", i."ItemSlot", i."DefinitionId", d."Group", d."Number", d."Name", d."Width", d."Height",
                   d."MaximumItemLevel", i."Level", i."Durability", i."HasSkill", i."SocketCount", i."StorePrice", i."PetExperience",
                   (SELECT s."ItemOfItemSetId" FROM data."ItemItemOfItemSet" s WHERE s."ItemId" = i."Id" LIMIT 1)
            FROM data."Item" i LEFT JOIN config."ItemDefinition" d ON d."Id" = i."DefinitionId"
            WHERE i."ItemStorageId" = @id ORDER BY i."ItemSlot"
            """, r => new StoredItem
        {
            Id = r.GetGuid(0), StorageId = r.GetGuid(1), Slot = r.GetInt16(2), DefinitionId = r.IsDBNull(3) ? Guid.Empty : r.GetGuid(3),
            Group = r.IsDBNull(4) ? (byte)0 : (byte)r.GetInt16(4), Number = r.IsDBNull(5) ? (short)0 : r.GetInt16(5),
            Name = r.IsDBNull(6) ? "(definicion desconocida)" : r.GetString(6), Width = r.IsDBNull(7) ? (byte)1 : (byte)r.GetInt16(7),
            Height = r.IsDBNull(8) ? (byte)1 : (byte)r.GetInt16(8), MaximumItemLevel = r.IsDBNull(9) ? (byte)0 : (byte)r.GetInt16(9),
            Level = (byte)r.GetInt16(10), Durability = r.GetDouble(11), HasSkill = r.GetBoolean(12), SocketCount = r.GetInt32(13),
            StorePrice = r.IsDBNull(14) ? null : r.GetInt32(14), PetExperience = r.GetInt32(15), ItemOfItemSetId = r.IsDBNull(16) ? null : r.GetGuid(16),
        }, ("id", storageId));

        var byId = items.ToDictionary(i => i.Id);
        await using var cmd = new NpgsqlCommand("""
            SELECT l."ItemId", l."Id", l."ItemOptionId", l."Level", l."Index", t."Name", o."Number", ad."Designation", pv."Value"
            FROM data."ItemOptionLink" l
            JOIN data."Item" i ON i."Id" = l."ItemId"
            LEFT JOIN config."IncreasableItemOption" o ON o."Id" = l."ItemOptionId"
            LEFT JOIN config."ItemOptionType" t ON t."Id" = o."OptionTypeId"
            LEFT JOIN config."PowerUpDefinition" p ON p."Id" = o."PowerUpDefinitionId"
            LEFT JOIN config."AttributeDefinition" ad ON ad."Id" = p."TargetAttributeId"
            LEFT JOIN config."PowerUpDefinitionValue" pv ON pv."Id" = p."BoostId"
            WHERE i."ItemStorageId" = @id ORDER BY l."Index"
            """, conn);
        cmd.Parameters.AddWithValue("id", storageId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            if (!byId.TryGetValue(r.GetGuid(0), out var item)) continue;
            item.Options.Add(new ItemOptionInstance
            {
                Id = r.GetGuid(1), ItemOptionId = r.IsDBNull(2) ? Guid.Empty : r.GetGuid(2), Level = r.GetInt32(3), Index = r.GetInt32(4),
                TypeName = r.IsDBNull(5) ? null : r.GetString(5), Number = r.IsDBNull(6) ? 0 : r.GetInt32(6),
                Label = MakeLabel(r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetFloat(8)),
            });
        }

        return items;
    }

    private static string MakeLabel(string? attribute, float? value)
    {
        if (attribute is null) return string.Empty;
        return value is { } v && v != 0 ? $"{attribute} {(v > 0 ? "+" : "")}{v:0.##}" : attribute;
    }

    // ------------------------------------------------------------ opciones posibles de una definicion

    public async Task<DefinitionOptions?> GetDefinitionOptionsAsync(Guid definitionId)
    {
        await using var conn = await _db.OpenAsync();
        var defs = await Query(conn, "SELECT \"SkillId\", \"MaximumSockets\", \"MaximumItemLevel\", \"Durability\", \"Width\", \"Height\" FROM config.\"ItemDefinition\" WHERE \"Id\" = @id",
            r => new DefinitionOptions
            {
                DefinitionId = definitionId, HasSkill = !r.IsDBNull(0), MaximumSockets = r.GetInt32(1), MaximumItemLevel = (byte)r.GetInt16(2),
                Durability = (byte)r.GetInt16(3), Width = (byte)r.GetInt16(4), Height = (byte)r.GetInt16(5),
            }, ("id", definitionId));
        var result = defs.SingleOrDefault();
        if (result is null) return null;

        result.Options = await Query(conn, """
            SELECT o."Id", t."Id", t."Name", o."Number",
                   COALESCE(ad."Designation", (SELECT ad2."Designation" FROM config."ItemOptionOfLevel" lv
                        JOIN config."PowerUpDefinition" p2 ON p2."Id" = lv."PowerUpDefinitionId"
                        JOIN config."AttributeDefinition" ad2 ON ad2."Id" = p2."TargetAttributeId"
                        WHERE lv."IncreasableItemOptionId" = o."Id" ORDER BY lv."Level" LIMIT 1)),
                   pv."Value",
                   COALESCE((SELECT max(lv."Level") FROM config."ItemOptionOfLevel" lv WHERE lv."IncreasableItemOptionId" = o."Id"), 1)
            FROM config."ItemDefinitionItemOptionDefinition" x
            JOIN config."IncreasableItemOption" o ON o."ItemOptionDefinitionId" = x."ItemOptionDefinitionId"
            LEFT JOIN config."ItemOptionType" t ON t."Id" = o."OptionTypeId"
            LEFT JOIN config."PowerUpDefinition" p ON p."Id" = o."PowerUpDefinitionId"
            LEFT JOIN config."AttributeDefinition" ad ON ad."Id" = p."TargetAttributeId"
            LEFT JOIN config."PowerUpDefinitionValue" pv ON pv."Id" = p."BoostId"
            WHERE x."ItemDefinitionId" = @id ORDER BY t."Name", o."Number", o."SubOptionType"
            """, r => new OptionChoice
        {
            Id = r.GetGuid(0), TypeId = r.IsDBNull(1) ? Guid.Empty : r.GetGuid(1), TypeName = r.IsDBNull(2) ? "?" : r.GetString(2),
            Number = r.GetInt32(3), Label = MakeLabel(r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? null : r.GetFloat(5)),
            MaxLevel = r.GetInt32(6),
        }, ("id", definitionId));

        result.AncientSets = await Query(conn, """
            SELECT s."Id", g."Name", s."BonusOptionId", s."AncientSetDiscriminator"
            FROM config."ItemOfItemSet" s JOIN config."ItemSetGroup" g ON g."Id" = s."ItemSetGroupId"
            WHERE s."ItemDefinitionId" = @id AND s."BonusOptionId" IS NOT NULL ORDER BY g."Name"
            """, r => new AncientChoice { ItemOfItemSetId = r.GetGuid(0), SetName = r.GetString(1), BonusOptionId = r.IsDBNull(2) ? null : r.GetGuid(2), Discriminator = r.GetInt32(3) }, ("id", definitionId));

        return result;
    }

    // ------------------------------------------------------------ escritura

    public async Task<Guid> CreateItemAsync(ItemWrite w)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await EnsureSlotFree(conn, tx, w.StorageId, w.Slot, w.DefinitionId, null);
        var id = Guid.NewGuid();
        await Exec(conn, """
            INSERT INTO data."Item" ("Id", "ItemStorageId", "DefinitionId", "ItemSlot", "Level", "Durability", "HasSkill", "SocketCount", "StorePrice", "PetExperience")
            VALUES (@id, @storage, @def, @slot, @level, @dur, @skill, @sockets, @price, @pet)
            """, tx, ("id", id), ("storage", w.StorageId), ("def", w.DefinitionId), ("slot", (short)w.Slot), ("level", (short)w.Level),
            ("dur", w.Durability), ("skill", w.HasSkill), ("sockets", w.SocketCount), ("price", w.StorePrice), ("pet", w.PetExperience));
        await WriteOptions(conn, tx, id, w.Options, w.ItemOfItemSetId);
        await tx.CommitAsync();
        return id;
    }

    public async Task UpdateItemAsync(Guid id, ItemWrite w)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await EnsureSlotFree(conn, tx, w.StorageId, w.Slot, w.DefinitionId, id);
        var n = await Exec(conn, """
            UPDATE data."Item" SET "ItemStorageId" = @storage, "DefinitionId" = @def, "ItemSlot" = @slot, "Level" = @level, "Durability" = @dur,
                "HasSkill" = @skill, "SocketCount" = @sockets, "StorePrice" = @price, "PetExperience" = @pet WHERE "Id" = @id
            """, tx, ("id", id), ("storage", w.StorageId), ("def", w.DefinitionId), ("slot", (short)w.Slot), ("level", (short)w.Level),
            ("dur", w.Durability), ("skill", w.HasSkill), ("sockets", w.SocketCount), ("price", w.StorePrice), ("pet", w.PetExperience));
        if (n != 1) throw new InvalidOperationException("El item ya no existe.");
        await Exec(conn, "DELETE FROM data.\"ItemOptionLink\" WHERE \"ItemId\" = @id", tx, ("id", id));
        await Exec(conn, "DELETE FROM data.\"ItemItemOfItemSet\" WHERE \"ItemId\" = @id", tx, ("id", id));
        await WriteOptions(conn, tx, id, w.Options, w.ItemOfItemSetId);
        await tx.CommitAsync();
    }

    public async Task MoveItemAsync(Guid id, MoveRequest move)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        var def = (Guid?)await Scalar(conn, "SELECT \"DefinitionId\" FROM data.\"Item\" WHERE \"Id\" = @id", ("id", id), tx) ?? throw new InvalidOperationException("El item no existe.");
        await EnsureSlotFree(conn, tx, move.StorageId, move.Slot, def, id);
        await Exec(conn, "UPDATE data.\"Item\" SET \"ItemStorageId\" = @storage, \"ItemSlot\" = @slot WHERE \"Id\" = @id", tx, ("id", id), ("storage", move.StorageId), ("slot", (short)move.Slot));
        await tx.CommitAsync();
    }

    public async Task DeleteItemAsync(Guid id)
    {
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        await Exec(conn, "DELETE FROM data.\"ItemOptionLink\" WHERE \"ItemId\" = @id", tx, ("id", id));
        await Exec(conn, "DELETE FROM data.\"ItemItemOfItemSet\" WHERE \"ItemId\" = @id", tx, ("id", id));
        await Exec(conn, "DELETE FROM data.\"Item\" WHERE \"Id\" = @id", tx, ("id", id));
        await tx.CommitAsync();
    }

    public async Task SetMoneyAsync(Guid storageId, int money)
    {
        await using var conn = await _db.OpenAsync();
        await Exec(conn, "UPDATE data.\"ItemStorage\" SET \"Money\" = @m WHERE \"Id\" = @id", null, ("id", storageId), ("m", money));
    }

    public async Task<StoredItem?> GetItemAsync(Guid id)
    {
        await using var conn = await _db.OpenAsync();
        var storage = (Guid?)await Scalar(conn, "SELECT \"ItemStorageId\" FROM data.\"Item\" WHERE \"Id\" = @id", ("id", id));
        if (storage is null) return null;
        return (await LoadItems(conn, storage.Value)).FirstOrDefault(i => i.Id == id);
    }

    private static async Task WriteOptions(NpgsqlConnection conn, NpgsqlTransaction tx, Guid itemId, List<ItemOptionInstance> options, Guid? itemOfItemSetId)
    {
        foreach (var o in options.Where(o => o.ItemOptionId != Guid.Empty))
        {
            await Exec(conn, "INSERT INTO data.\"ItemOptionLink\" (\"Id\", \"ItemId\", \"ItemOptionId\", \"Level\", \"Index\") VALUES (@id, @item, @opt, @level, @index)", tx,
                ("id", Guid.NewGuid()), ("item", itemId), ("opt", o.ItemOptionId), ("level", o.Level), ("index", o.Index));
        }

        if (itemOfItemSetId is { } setId)
        {
            await Exec(conn, "INSERT INTO data.\"ItemItemOfItemSet\" (\"ItemId\", \"ItemOfItemSetId\") VALUES (@item, @set)", tx, ("item", itemId), ("set", setId));
        }
    }

    /// <summary>Comprueba que el item entre en el slot sin pisar otro (grilla de 8 columnas; los slots 0-11 del inventario son de equipo).</summary>
    private static async Task EnsureSlotFree(NpgsqlConnection conn, NpgsqlTransaction tx, Guid storageId, int slot, Guid definitionId, Guid? exceptItemId)
    {
        var dims = await Query(conn, "SELECT \"Width\", \"Height\" FROM config.\"ItemDefinition\" WHERE \"Id\" = @id", r => (r.GetInt16(0), r.GetInt16(1)), ("id", definitionId), tx);
        if (dims.Count == 0) throw new InvalidOperationException("La definicion del item no existe.");
        var (w, h) = dims[0];

        var isInventory = (bool)(await Scalar(conn, "SELECT EXISTS (SELECT 1 FROM data.\"Character\" c WHERE c.\"InventoryId\" = @id)", ("id", storageId), tx))!;
        var others = await Query(conn, """
            SELECT i."ItemSlot", d."Width", d."Height" FROM data."Item" i JOIN config."ItemDefinition" d ON d."Id" = i."DefinitionId"
            WHERE i."ItemStorageId" = @id AND i."Id" <> @except
            """, r => (Slot: (int)r.GetInt16(0), W: (int)r.GetInt16(1), H: (int)r.GetInt16(2)), ("id", storageId), ("except", exceptItemId ?? Guid.Empty), tx);

        if (isInventory && slot < 12)
        {
            if (others.Any(o => o.Slot == slot)) throw new InvalidOperationException("Ese slot de equipo ya esta ocupado.");
            return;
        }

        int baseSlot = isInventory ? 12 : 0;
        if (slot < baseSlot) throw new InvalidOperationException("Slot invalido.");
        var (col, row) = ((slot - baseSlot) % 8, (slot - baseSlot) / 8);
        if (col + w > 8) throw new InvalidOperationException("El item no entra en esa posicion (se sale por la derecha).");
        foreach (var o in others.Where(o => o.Slot >= baseSlot))
        {
            var (oc, or) = ((o.Slot - baseSlot) % 8, (o.Slot - baseSlot) / 8);
            bool overlap = col < oc + o.W && oc < col + w && row < or + o.H && or < row + h;
            if (overlap) throw new InvalidOperationException("Esa posicion pisa otro item.");
        }
    }

    // ------------------------------------------------------------ helpers

    private static async Task<List<T>> Query<T>(NpgsqlConnection conn, string sql, Func<NpgsqlDataReader, T> map, params object[] args)
    {
        NpgsqlTransaction? tx = args.OfType<NpgsqlTransaction>().FirstOrDefault();
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        AddArgs(cmd, args);
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<T>();
        while (await reader.ReadAsync()) list.Add(map(reader));
        return list;
    }

    private static async Task<object?> Scalar(NpgsqlConnection conn, string sql, params object[] args)
    {
        NpgsqlTransaction? tx = args.OfType<NpgsqlTransaction>().FirstOrDefault();
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        AddArgs(cmd, args);
        var v = await cmd.ExecuteScalarAsync();
        return v is DBNull ? null : v;
    }

    private static void AddArgs(NpgsqlCommand cmd, object[] args)
    {
        foreach (var a in args)
        {
            if (a is System.Runtime.CompilerServices.ITuple t && t.Length == 2 && t[0] is string name)
            {
                cmd.Parameters.AddWithValue(name, t[1] ?? DBNull.Value);
            }
        }
    }

    private static async Task<int> Exec(NpgsqlConnection conn, string sql, NpgsqlTransaction? tx, params (string, object?)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return await cmd.ExecuteNonQueryAsync();
    }
}
