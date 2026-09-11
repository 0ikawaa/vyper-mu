// Acceso directo a la base de OpenMU (esquema "config"). Se escribe con SQL
// plano y transacciones; OpenMU carga la GameConfiguration al arrancar, asi
// que despues de guardar hay que reiniciar el servidor para que tome cambios.

using System.Data;
using Npgsql;

namespace VyperMu.ItemEditor;

public sealed class Db
{
    private readonly string _connectionString;

    public Db(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    public async Task PingAsync()
    {
        await using var conn = await OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM config.\"ItemDefinition\"", conn);
        await cmd.ExecuteScalarAsync();
    }

    // ------------------------------------------------------------ meta

    public async Task<Meta> GetMetaAsync()
    {
        await using var conn = await OpenAsync();

        var gameConfigId = (Guid)(await Scalar(conn, "SELECT \"Id\" FROM config.\"GameConfiguration\" LIMIT 1"))!;

        var attributes = await Query(conn,
            "SELECT \"Id\", \"Designation\", \"Description\" FROM config.\"AttributeDefinition\" ORDER BY \"Designation\"",
            r => new AttributeRef(r.GetGuid(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)));

        var classes = await Query(conn,
            "SELECT \"Id\", \"Name\", \"Number\" FROM config.\"CharacterClass\" ORDER BY \"Number\"",
            r => new ClassRef(r.GetGuid(0), r.GetString(1), (byte)r.GetInt16(2)));

        var slots = await Query(conn,
            "SELECT \"Id\", \"Description\", \"ItemSlots\" FROM config.\"ItemSlotType\" ORDER BY \"ItemSlots\"",
            r => new SlotRef(r.GetGuid(0), r.IsDBNull(1) ? string.Empty : r.GetString(1), r.IsDBNull(2) ? string.Empty : r.GetString(2)));

        var options = await Query(conn,
            "SELECT \"Id\", \"Name\" FROM config.\"ItemOptionDefinition\" ORDER BY \"Name\"",
            r => new NamedRef(r.GetGuid(0), r.IsDBNull(1) ? string.Empty : r.GetString(1)));

        var setGroups = await Query(conn,
            "SELECT \"Id\", \"Name\", \"SetLevel\" FROM config.\"ItemSetGroup\" ORDER BY \"Name\", \"SetLevel\"",
            r => new SetGroupRef(r.GetGuid(0), r.IsDBNull(1) ? string.Empty : r.GetString(1), r.GetInt32(2)));

        var skills = await Query(conn,
            "SELECT \"Id\", \"Name\", \"Number\" FROM config.\"Skill\" ORDER BY \"Number\"",
            r => new SkillRef(r.GetGuid(0), r.IsDBNull(1) ? string.Empty : r.GetString(1), r.GetInt16(2)));

        var effects = await Query(conn,
            "SELECT \"Id\", \"Name\" FROM config.\"MagicEffectDefinition\" ORDER BY \"Number\"",
            r => new NamedRef(r.GetGuid(0), r.IsDBNull(1) ? string.Empty : r.GetString(1)));

        var bonusTables = await Query(conn,
            "SELECT \"Id\", \"Name\" FROM config.\"ItemLevelBonusTable\" ORDER BY \"Name\"",
            r => new NamedRef(r.GetGuid(0), r.IsDBNull(1) ? string.Empty : r.GetString(1)));

        var dropGroups = await Query(conn,
            "SELECT \"Id\", \"Description\" FROM config.\"DropItemGroup\" ORDER BY \"Description\"",
            r => new NamedRef(r.GetGuid(0), r.IsDBNull(1) ? string.Empty : r.GetString(1)));

        return new Meta(gameConfigId, attributes, classes, slots, options, setGroups, skills, effects, bonusTables, dropGroups);
    }

    // ------------------------------------------------------------ items

    private const string ItemColumns =
        "\"Id\", \"Group\", \"Number\", \"Name\", \"Width\", \"Height\", \"DropLevel\", \"MaximumDropLevel\", " +
        "\"MaximumItemLevel\", \"Durability\", \"Value\", \"ItemSlotId\", \"DropsFromMonsters\", \"IsAmmunition\", " +
        "\"IsBoundToCharacter\", \"IsQuestItem\", \"StorageLimitPerCharacter\", \"MaximumSockets\", \"SkillId\", " +
        "\"ConsumeEffectId\", \"PetExperienceFormula\"";

    /// <summary>Devuelve todos los items con sus colecciones, cargadas en bloque (7 consultas en total).</summary>
    public async Task<List<ItemDetail>> GetItemsAsync()
    {
        await using var conn = await OpenAsync();
        var items = await Query(conn, $"SELECT {ItemColumns} FROM config.\"ItemDefinition\" ORDER BY \"Group\", \"Number\"", ReadDetail);
        var byId = items.ToDictionary(i => i.Id);

        await Bulk(conn, "SELECT \"ItemDefinitionId\", \"Id\", \"AttributeId\", \"MinimumValue\" FROM config.\"AttributeRequirement\" WHERE \"ItemDefinitionId\" IS NOT NULL",
            r => byId.GetValueOrDefault(r.GetGuid(0))?.Requirements.Add(new Requirement { Id = r.GetGuid(1), AttributeId = r.IsDBNull(2) ? Guid.Empty : r.GetGuid(2), MinimumValue = r.GetInt32(3) }));

        await Bulk(conn, "SELECT \"ItemDefinitionId\", \"Id\", \"TargetAttributeId\", \"BaseValue\", \"AggregateType\", \"BonusPerLevelTableId\" FROM config.\"ItemBasePowerUpDefinition\" WHERE \"ItemDefinitionId\" IS NOT NULL",
            r => byId.GetValueOrDefault(r.GetGuid(0))?.PowerUps.Add(new PowerUp
            {
                Id = r.GetGuid(1),
                TargetAttributeId = r.IsDBNull(2) ? Guid.Empty : r.GetGuid(2),
                BaseValue = r.GetFloat(3),
                AggregateType = r.GetInt32(4),
                BonusPerLevelTableId = r.IsDBNull(5) ? null : r.GetGuid(5),
            }));

        await Bulk(conn, "SELECT \"ItemDefinitionId\", \"CharacterClassId\" FROM config.\"ItemDefinitionCharacterClass\"", r => byId.GetValueOrDefault(r.GetGuid(0))?.ClassIds.Add(r.GetGuid(1)));
        await Bulk(conn, "SELECT \"ItemDefinitionId\", \"ItemOptionDefinitionId\" FROM config.\"ItemDefinitionItemOptionDefinition\"", r => byId.GetValueOrDefault(r.GetGuid(0))?.OptionIds.Add(r.GetGuid(1)));
        await Bulk(conn, "SELECT \"ItemDefinitionId\", \"ItemSetGroupId\" FROM config.\"ItemDefinitionItemSetGroup\"", r => byId.GetValueOrDefault(r.GetGuid(0))?.SetGroupIds.Add(r.GetGuid(1)));
        await Bulk(conn, "SELECT \"ItemDefinitionId\", \"DropItemGroupId\" FROM config.\"DropItemGroupItemDefinition\"", r => byId.GetValueOrDefault(r.GetGuid(0))?.DropGroupIds.Add(r.GetGuid(1)));

        foreach (var item in items)
        {
            item.Requirements.Sort((a, b) => b.MinimumValue.CompareTo(a.MinimumValue));
            item.PowerUps.Sort((a, b) => b.BaseValue.CompareTo(a.BaseValue));
        }

        return items;
    }

    private static async Task Bulk(NpgsqlConnection conn, string sql, Action<NpgsqlDataReader> each)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) each(reader);
    }

    public async Task<ItemDetail?> GetItemAsync(Guid id)
    {
        await using var conn = await OpenAsync();
        return await GetItemAsync(conn, id);
    }

    private async Task<ItemDetail?> GetItemAsync(NpgsqlConnection conn, Guid id, NpgsqlTransaction? tx = null)
    {
        var items = await Query(conn, $"SELECT {ItemColumns} FROM config.\"ItemDefinition\" WHERE \"Id\" = @id", ReadDetail, tx, ("id", id));
        var item = items.SingleOrDefault();
        if (item is null) return null;

        item.Requirements = await Query(conn,
            "SELECT \"Id\", \"AttributeId\", \"MinimumValue\" FROM config.\"AttributeRequirement\" WHERE \"ItemDefinitionId\" = @id ORDER BY \"MinimumValue\" DESC",
            r => new Requirement { Id = r.GetGuid(0), AttributeId = r.IsDBNull(1) ? Guid.Empty : r.GetGuid(1), MinimumValue = r.GetInt32(2) }, tx, ("id", id));

        item.PowerUps = await Query(conn,
            "SELECT \"Id\", \"TargetAttributeId\", \"BaseValue\", \"AggregateType\", \"BonusPerLevelTableId\" FROM config.\"ItemBasePowerUpDefinition\" WHERE \"ItemDefinitionId\" = @id ORDER BY \"BaseValue\" DESC",
            r => new PowerUp
            {
                Id = r.GetGuid(0),
                TargetAttributeId = r.IsDBNull(1) ? Guid.Empty : r.GetGuid(1),
                BaseValue = r.GetFloat(2),
                AggregateType = r.GetInt32(3),
                BonusPerLevelTableId = r.IsDBNull(4) ? null : r.GetGuid(4),
            }, tx, ("id", id));

        item.ClassIds = await Query(conn, "SELECT \"CharacterClassId\" FROM config.\"ItemDefinitionCharacterClass\" WHERE \"ItemDefinitionId\" = @id", r => r.GetGuid(0), tx, ("id", id));
        item.OptionIds = await Query(conn, "SELECT \"ItemOptionDefinitionId\" FROM config.\"ItemDefinitionItemOptionDefinition\" WHERE \"ItemDefinitionId\" = @id", r => r.GetGuid(0), tx, ("id", id));
        item.SetGroupIds = await Query(conn, "SELECT \"ItemSetGroupId\" FROM config.\"ItemDefinitionItemSetGroup\" WHERE \"ItemDefinitionId\" = @id", r => r.GetGuid(0), tx, ("id", id));
        item.DropGroupIds = await Query(conn, "SELECT \"DropItemGroupId\" FROM config.\"DropItemGroupItemDefinition\" WHERE \"ItemDefinitionId\" = @id", r => r.GetGuid(0), tx, ("id", id));
        return item;
    }

    public async Task<bool> ExistsGroupNumberAsync(byte group, short number, Guid? exceptId)
    {
        await using var conn = await OpenAsync();
        var count = (long)(await Scalar(conn,
            "SELECT count(*) FROM config.\"ItemDefinition\" WHERE \"Group\" = @g AND \"Number\" = @n AND (\"Id\" <> @id)",
            null, ("g", (short)group), ("n", number), ("id", exceptId ?? Guid.Empty)))!;
        return count > 0;
    }

    public async Task<ItemDetail> UpdateItemAsync(ItemDetail item)
    {
        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var affected = await Exec(conn, """
            UPDATE config."ItemDefinition" SET
                "Group" = @group, "Number" = @number, "Name" = @name, "Width" = @width, "Height" = @height,
                "DropLevel" = @dropLevel, "MaximumDropLevel" = @maxDropLevel, "MaximumItemLevel" = @maxItemLevel,
                "Durability" = @durability, "Value" = @value, "ItemSlotId" = @slotId, "DropsFromMonsters" = @drops,
                "IsAmmunition" = @ammo, "IsBoundToCharacter" = @bound, "IsQuestItem" = @quest,
                "StorageLimitPerCharacter" = @storageLimit, "MaximumSockets" = @sockets, "SkillId" = @skillId,
                "ConsumeEffectId" = @effectId, "PetExperienceFormula" = @petFormula
            WHERE "Id" = @id
            """, tx, ItemParams(item));
        if (affected != 1) throw new InvalidOperationException("El item no existe.");

        await SyncRequirements(conn, tx, item);
        await SyncPowerUps(conn, tx, item);
        await SyncJoin(conn, tx, "ItemDefinitionCharacterClass", "CharacterClassId", item.Id, item.ClassIds);
        await SyncJoin(conn, tx, "ItemDefinitionItemOptionDefinition", "ItemOptionDefinitionId", item.Id, item.OptionIds);
        await SyncJoin(conn, tx, "ItemDefinitionItemSetGroup", "ItemSetGroupId", item.Id, item.SetGroupIds);
        await SyncJoin(conn, tx, "DropItemGroupItemDefinition", "DropItemGroupId", item.Id, item.DropGroupIds);

        var result = await GetItemAsync(conn, item.Id, tx);
        await tx.CommitAsync();
        return result!;
    }

    public async Task<ItemDetail> CloneItemAsync(Guid sourceId, CloneRequest request, Guid gameConfigurationId)
    {
        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var source = await GetItemAsync(conn, sourceId, tx) ?? throw new InvalidOperationException("El item original no existe.");
        source.Id = Guid.NewGuid();
        source.Group = request.Group;
        source.Number = request.Number;
        if (!string.IsNullOrWhiteSpace(request.Name)) source.Name = request.Name.Trim();
        foreach (var r in source.Requirements) r.Id = null;
        foreach (var p in source.PowerUps) p.Id = null;
        source.DropGroupIds.Clear(); // un clon no entra solo en los grupos de drop

        await Exec(conn, """
            INSERT INTO config."ItemDefinition" ("Id", "GameConfigurationId", "Group", "Number", "Name", "Width", "Height",
                "DropLevel", "MaximumDropLevel", "MaximumItemLevel", "Durability", "Value", "ItemSlotId", "DropsFromMonsters",
                "IsAmmunition", "IsBoundToCharacter", "IsQuestItem", "StorageLimitPerCharacter", "MaximumSockets", "SkillId",
                "ConsumeEffectId", "PetExperienceFormula")
            VALUES (@id, @gameConfigId, @group, @number, @name, @width, @height, @dropLevel, @maxDropLevel, @maxItemLevel,
                @durability, @value, @slotId, @drops, @ammo, @bound, @quest, @storageLimit, @sockets, @skillId, @effectId, @petFormula)
            """, tx, ItemParams(source).Append(("gameConfigId", gameConfigurationId)).ToArray());

        await SyncRequirements(conn, tx, source);
        await SyncPowerUps(conn, tx, source);
        await SyncJoin(conn, tx, "ItemDefinitionCharacterClass", "CharacterClassId", source.Id, source.ClassIds);
        await SyncJoin(conn, tx, "ItemDefinitionItemOptionDefinition", "ItemOptionDefinitionId", source.Id, source.OptionIds);
        await SyncJoin(conn, tx, "ItemDefinitionItemSetGroup", "ItemSetGroupId", source.Id, source.SetGroupIds);

        var result = await GetItemAsync(conn, source.Id, tx);
        await tx.CommitAsync();
        return result!;
    }

    /// <summary>Borra un item. Si algo mas lo referencia (un personaje lo tiene, una receta lo usa) falla y no borra nada.</summary>
    public async Task DeleteItemAsync(Guid id)
    {
        await using var conn = await OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        var owned = (long)(await Scalar(conn, "SELECT count(*) FROM data.\"Item\" WHERE \"DefinitionId\" = @id", tx, ("id", id)))!;
        if (owned > 0)
        {
            throw new InvalidOperationException($"No se puede borrar: hay {owned} item(s) de este tipo en inventarios, baules o tiendas de personajes.");
        }

        foreach (var table in new[] { "AttributeRequirement", "ItemBasePowerUpDefinition", "ItemDefinitionCharacterClass",
                     "ItemDefinitionItemOptionDefinition", "ItemDefinitionItemSetGroup", "DropItemGroupItemDefinition" })
        {
            await Exec(conn, $"DELETE FROM config.\"{table}\" WHERE \"ItemDefinitionId\" = @id", tx, ("id", id));
        }

        try
        {
            await Exec(conn, "DELETE FROM config.\"ItemDefinition\" WHERE \"Id\" = @id", tx, ("id", id));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
        {
            throw new InvalidOperationException($"No se puede borrar: lo referencia otra configuracion ({ex.ConstraintName}).");
        }

        await tx.CommitAsync();
    }

    // ------------------------------------------------------------ helpers

    private static ItemDetail ReadDetail(NpgsqlDataReader r) => new()
    {
        Id = r.GetGuid(0),
        Group = (byte)r.GetInt16(1),
        Number = r.GetInt16(2),
        Name = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        Width = (byte)r.GetInt16(4),
        Height = (byte)r.GetInt16(5),
        DropLevel = (byte)r.GetInt16(6),
        MaximumDropLevel = r.IsDBNull(7) ? null : (byte)r.GetInt16(7),
        MaximumItemLevel = (byte)r.GetInt16(8),
        Durability = (byte)r.GetInt16(9),
        Value = r.GetInt32(10),
        ItemSlotId = r.IsDBNull(11) ? null : r.GetGuid(11),
        DropsFromMonsters = r.GetBoolean(12),
        IsAmmunition = r.GetBoolean(13),
        IsBoundToCharacter = r.GetBoolean(14),
        IsQuestItem = r.GetBoolean(15),
        StorageLimitPerCharacter = r.GetInt32(16),
        MaximumSockets = r.GetInt32(17),
        SkillId = r.IsDBNull(18) ? null : r.GetGuid(18),
        ConsumeEffectId = r.IsDBNull(19) ? null : r.GetGuid(19),
        PetExperienceFormula = r.IsDBNull(20) ? null : r.GetString(20),
    };

    private static (string, object?)[] ItemParams(ItemDetail i) => new (string, object?)[]
    {
        ("id", i.Id), ("group", (short)i.Group), ("number", i.Number), ("name", i.Name ?? string.Empty),
        ("width", (short)i.Width), ("height", (short)i.Height), ("dropLevel", (short)i.DropLevel),
        ("maxDropLevel", i.MaximumDropLevel.HasValue ? (short)i.MaximumDropLevel.Value : null),
        ("maxItemLevel", (short)i.MaximumItemLevel), ("durability", (short)i.Durability), ("value", i.Value),
        ("slotId", i.ItemSlotId), ("drops", i.DropsFromMonsters), ("ammo", i.IsAmmunition), ("bound", i.IsBoundToCharacter),
        ("quest", i.IsQuestItem), ("storageLimit", i.StorageLimitPerCharacter), ("sockets", i.MaximumSockets),
        ("skillId", i.SkillId), ("effectId", i.ConsumeEffectId), ("petFormula", i.PetExperienceFormula),
    };

    private static async Task SyncRequirements(NpgsqlConnection conn, NpgsqlTransaction tx, ItemDetail item)
    {
        var existing = await Query(conn, "SELECT \"Id\" FROM config.\"AttributeRequirement\" WHERE \"ItemDefinitionId\" = @id", r => r.GetGuid(0), tx, ("id", item.Id));
        var keep = new HashSet<Guid>();
        foreach (var req in item.Requirements.Where(r => r.AttributeId != Guid.Empty))
        {
            if (req.Id is { } rid && existing.Contains(rid))
            {
                await Exec(conn, "UPDATE config.\"AttributeRequirement\" SET \"AttributeId\" = @attr, \"MinimumValue\" = @min WHERE \"Id\" = @rid", tx,
                    ("attr", req.AttributeId), ("min", req.MinimumValue), ("rid", rid));
                keep.Add(rid);
            }
            else
            {
                var newId = Guid.NewGuid();
                await Exec(conn, "INSERT INTO config.\"AttributeRequirement\" (\"Id\", \"ItemDefinitionId\", \"AttributeId\", \"MinimumValue\") VALUES (@rid, @id, @attr, @min)", tx,
                    ("rid", newId), ("id", item.Id), ("attr", req.AttributeId), ("min", req.MinimumValue));
                keep.Add(newId);
            }
        }

        foreach (var gone in existing.Where(e => !keep.Contains(e)))
        {
            await Exec(conn, "DELETE FROM config.\"AttributeRequirement\" WHERE \"Id\" = @rid", tx, ("rid", gone));
        }
    }

    private static async Task SyncPowerUps(NpgsqlConnection conn, NpgsqlTransaction tx, ItemDetail item)
    {
        var existing = await Query(conn, "SELECT \"Id\" FROM config.\"ItemBasePowerUpDefinition\" WHERE \"ItemDefinitionId\" = @id", r => r.GetGuid(0), tx, ("id", item.Id));
        var keep = new HashSet<Guid>();
        foreach (var pu in item.PowerUps.Where(p => p.TargetAttributeId != Guid.Empty))
        {
            if (pu.Id is { } pid && existing.Contains(pid))
            {
                await Exec(conn, "UPDATE config.\"ItemBasePowerUpDefinition\" SET \"TargetAttributeId\" = @attr, \"BaseValue\" = @val, \"AggregateType\" = @agg, \"BonusPerLevelTableId\" = @table WHERE \"Id\" = @pid", tx,
                    ("attr", pu.TargetAttributeId), ("val", pu.BaseValue), ("agg", pu.AggregateType), ("table", pu.BonusPerLevelTableId), ("pid", pid));
                keep.Add(pid);
            }
            else
            {
                var newId = Guid.NewGuid();
                await Exec(conn, "INSERT INTO config.\"ItemBasePowerUpDefinition\" (\"Id\", \"ItemDefinitionId\", \"TargetAttributeId\", \"BaseValue\", \"AggregateType\", \"BonusPerLevelTableId\") VALUES (@pid, @id, @attr, @val, @agg, @table)", tx,
                    ("pid", newId), ("id", item.Id), ("attr", pu.TargetAttributeId), ("val", pu.BaseValue), ("agg", pu.AggregateType), ("table", pu.BonusPerLevelTableId));
                keep.Add(newId);
            }
        }

        foreach (var gone in existing.Where(e => !keep.Contains(e)))
        {
            await Exec(conn, "DELETE FROM config.\"ItemBasePowerUpDefinition\" WHERE \"Id\" = @pid", tx, ("pid", gone));
        }
    }

    private static async Task SyncJoin(NpgsqlConnection conn, NpgsqlTransaction tx, string table, string otherColumn, Guid itemId, List<Guid> wanted)
    {
        var existing = await Query(conn, $"SELECT \"{otherColumn}\" FROM config.\"{table}\" WHERE \"ItemDefinitionId\" = @id", r => r.GetGuid(0), tx, ("id", itemId));
        var wantedSet = wanted.ToHashSet();
        foreach (var add in wantedSet.Where(w => !existing.Contains(w)))
        {
            await Exec(conn, $"INSERT INTO config.\"{table}\" (\"ItemDefinitionId\", \"{otherColumn}\") VALUES (@id, @other)", tx, ("id", itemId), ("other", add));
        }

        foreach (var remove in existing.Where(e => !wantedSet.Contains(e)))
        {
            await Exec(conn, $"DELETE FROM config.\"{table}\" WHERE \"ItemDefinitionId\" = @id AND \"{otherColumn}\" = @other", tx, ("id", itemId), ("other", remove));
        }
    }

    private static async Task<List<T>> Query<T>(NpgsqlConnection conn, string sql, Func<NpgsqlDataReader, T> map, NpgsqlTransaction? tx = null, params (string, object?)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        AddParams(cmd, args);
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<T>();
        while (await reader.ReadAsync()) list.Add(map(reader));
        return list;
    }

    private static async Task<object?> Scalar(NpgsqlConnection conn, string sql, NpgsqlTransaction? tx = null, params (string, object?)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        AddParams(cmd, args);
        return await cmd.ExecuteScalarAsync();
    }

    private static async Task<int> Exec(NpgsqlConnection conn, string sql, NpgsqlTransaction? tx = null, params (string, object?)[] args)
    {
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        AddParams(cmd, args);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static void AddParams(NpgsqlCommand cmd, (string, object?)[] args)
    {
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }
}
