namespace VyperMu.ItemEditor;

public sealed record NamedRef(Guid Id, string Name);

public sealed record AttributeRef(Guid Id, string Designation, string? Description);

public sealed record ClassRef(Guid Id, string Name, byte Number);

public sealed record SlotRef(Guid Id, string Description, string RawItemSlots);

public sealed record SetGroupRef(Guid Id, string Name, int SetLevel);

public sealed record SkillRef(Guid Id, string Name, short Number);

public sealed record Meta(
    Guid GameConfigurationId,
    List<AttributeRef> Attributes,
    List<ClassRef> Classes,
    List<SlotRef> Slots,
    List<NamedRef> Options,
    List<SetGroupRef> SetGroups,
    List<SkillRef> Skills,
    List<NamedRef> Effects,
    List<NamedRef> BonusTables,
    List<NamedRef> DropGroups);

public sealed class Requirement
{
    public Guid? Id { get; set; }
    public Guid AttributeId { get; set; }
    public int MinimumValue { get; set; }
}

public sealed class PowerUp
{
    public Guid? Id { get; set; }
    public Guid TargetAttributeId { get; set; }
    public float BaseValue { get; set; }
    public int AggregateType { get; set; }
    public Guid? BonusPerLevelTableId { get; set; }
}

public sealed class ItemDetail
{
    public Guid Id { get; set; }
    public byte Group { get; set; }
    public short Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte Width { get; set; }
    public byte Height { get; set; }
    public byte DropLevel { get; set; }
    public byte? MaximumDropLevel { get; set; }
    public byte MaximumItemLevel { get; set; }
    public byte Durability { get; set; }
    public int Value { get; set; }
    public Guid? ItemSlotId { get; set; }
    public bool DropsFromMonsters { get; set; }
    public bool IsAmmunition { get; set; }
    public bool IsBoundToCharacter { get; set; }
    public bool IsQuestItem { get; set; }
    public int StorageLimitPerCharacter { get; set; }
    public int MaximumSockets { get; set; }
    public Guid? SkillId { get; set; }
    public Guid? ConsumeEffectId { get; set; }
    public string? PetExperienceFormula { get; set; }
    public List<Requirement> Requirements { get; set; } = new();
    public List<PowerUp> PowerUps { get; set; } = new();
    public List<Guid> ClassIds { get; set; } = new();
    public List<Guid> OptionIds { get; set; } = new();
    public List<Guid> SetGroupIds { get; set; } = new();
    public List<Guid> DropGroupIds { get; set; } = new();
}

public sealed record CloneRequest(byte Group, short Number, string? Name);

public sealed record Status(
    bool DbOk,
    string? DbError,
    bool ClientFileOk,
    string ClientFilePath,
    int ClientNameLength,
    bool ServerRunning,
    string Root,
    string BackupDir);
