// Lector/escritor de Data\Local\Eng\Item_eng.bmd del cliente MuMain.
//
// Formato (ver MuMain: Data/DataHandler/ItemData/ItemDataLoader.cpp y
// Data/GameData/ItemData/ItemFieldDefs.h):
//   - 8192 registros (16 grupos x 512 indices), indice = grupo * 512 + numero
//   - cada registro cifrado con XOR de 3 bytes (BuxConvert: FC CF AB)
//   - al final un DWORD de checksum (GenerateCheckSum2, clave 0xE2F1) calculado
//     sobre el buffer cifrado
//   - dos layouts: "legacy" (nombre de 30 bytes, 84 bytes por registro) y
//     "nuevo" (nombre de 50 bytes, 104 bytes por registro). Se detecta por tamano.

using System.Text;

namespace VyperMu.ItemEditor;

public sealed class ClientItem
{
    public int Index { get; set; }
    public int Group => Index / 512;
    public int Number => Index % 512;
    public string Name { get; set; } = string.Empty;
    public bool TwoHand { get; set; }
    public ushort Level { get; set; }
    public byte Slot { get; set; }
    public ushort SkillIndex { get; set; }
    public byte Width { get; set; }
    public byte Height { get; set; }
    public byte DamageMin { get; set; }
    public byte DamageMax { get; set; }
    public byte SuccessfulBlocking { get; set; }
    public byte Defense { get; set; }
    public byte MagicDefense { get; set; }
    public byte WeaponSpeed { get; set; }
    public byte WalkSpeed { get; set; }
    public byte Durability { get; set; }
    public byte MagicDur { get; set; }
    public byte MagicPower { get; set; }
    public ushort RequireStrength { get; set; }
    public ushort RequireDexterity { get; set; }
    public ushort RequireEnergy { get; set; }
    public ushort RequireVitality { get; set; }
    public ushort RequireCharisma { get; set; }
    public ushort RequireLevel { get; set; }
    public byte Value { get; set; }
    public int Zen { get; set; }
    public byte AttType { get; set; }
    /// <summary>DW, DK, ELF, MG, DL, SUM, RF: 0 = no, 1 = clase base, 2 = 2da evolucion, 3 = 3ra.</summary>
    public List<byte> RequireClass { get; set; } = new(new byte[7]);
    public List<byte> Resistance { get; set; } = new(new byte[8]);

    public bool IsEmpty => string.IsNullOrEmpty(Name);
}

public sealed class ClientItemFile
{
    private const int MaxItems = 16 * 512;
    private const ushort ChecksumKey = 0xE2F1;
    private static readonly byte[] BuxCode = { 0xFC, 0xCF, 0xAB };

    private readonly byte[] _records;

    public string Path { get; }
    public int NameLength { get; }
    public int RecordSize { get; }

    private ClientItemFile(string path, byte[] records, int nameLength)
    {
        Path = path;
        _records = records;
        NameLength = nameLength;
        RecordSize = nameLength + 54;
    }

    public static bool Exists(string path) => File.Exists(path);

    public static ClientItemFile Load(string path)
    {
        var data = File.ReadAllBytes(path);
        int nameLength = (data.Length - 4) switch
        {
            84 * MaxItems => 30,
            104 * MaxItems => 50,
            _ => throw new InvalidDataException($"Tamano inesperado de {path}: {data.Length} bytes"),
        };

        var body = data.AsSpan(0, data.Length - 4).ToArray();
        var expected = BitConverter.ToUInt32(data, data.Length - 4);
        var actual = Checksum(body, ChecksumKey);
        if (expected != actual)
        {
            throw new InvalidDataException($"Checksum invalido en {path} (esperado {expected:X8}, calculado {actual:X8})");
        }

        Bux(body);
        return new ClientItemFile(path, body, nameLength);
    }

    public IEnumerable<ClientItem> ReadAll()
    {
        for (int i = 0; i < MaxItems; i++)
        {
            yield return Read(i);
        }
    }

    public ClientItem Read(int index)
    {
        if (index is < 0 or >= MaxItems) throw new ArgumentOutOfRangeException(nameof(index));
        var s = _records.AsSpan(index * RecordSize, RecordSize);
        int o = NameLength;
        var nameBytes = s[..NameLength];
        int nul = nameBytes.IndexOf((byte)0);
        if (nul >= 0) nameBytes = nameBytes[..nul];

        var item = new ClientItem
        {
            Index = index,
            Name = Encoding.UTF8.GetString(nameBytes),
            TwoHand = s[o] != 0,
            Level = BitConverter.ToUInt16(s[(o + 2)..]),
            Slot = s[o + 4],
            SkillIndex = BitConverter.ToUInt16(s[(o + 6)..]),
            Width = s[o + 8],
            Height = s[o + 9],
            DamageMin = s[o + 10],
            DamageMax = s[o + 11],
            SuccessfulBlocking = s[o + 12],
            Defense = s[o + 13],
            MagicDefense = s[o + 14],
            WeaponSpeed = s[o + 15],
            WalkSpeed = s[o + 16],
            Durability = s[o + 17],
            MagicDur = s[o + 18],
            MagicPower = s[o + 19],
            RequireStrength = BitConverter.ToUInt16(s[(o + 20)..]),
            RequireDexterity = BitConverter.ToUInt16(s[(o + 22)..]),
            RequireEnergy = BitConverter.ToUInt16(s[(o + 24)..]),
            RequireVitality = BitConverter.ToUInt16(s[(o + 26)..]),
            RequireCharisma = BitConverter.ToUInt16(s[(o + 28)..]),
            RequireLevel = BitConverter.ToUInt16(s[(o + 30)..]),
            Value = s[o + 32],
            Zen = BitConverter.ToInt32(s[(o + 34)..]),
            AttType = s[o + 38],
            RequireClass = new List<byte>(s.Slice(o + 39, 7).ToArray()),
            Resistance = new List<byte>(s.Slice(o + 46, 8).ToArray()),
        };
        return item;
    }

    public void Write(ClientItem item)
    {
        if (item.Index is < 0 or >= MaxItems) throw new ArgumentOutOfRangeException(nameof(item));
        var s = _records.AsSpan(item.Index * RecordSize, RecordSize);
        s.Clear();
        int o = NameLength;

        var name = Encoding.UTF8.GetBytes(item.Name ?? string.Empty);
        if (name.Length >= NameLength)
        {
            // Recortar sin partir una secuencia UTF-8 y dejando el terminador nulo.
            int len = NameLength - 1;
            while (len > 0 && (name[len] & 0xC0) == 0x80) len--;
            name = name[..len];
        }
        name.CopyTo(s);

        s[o] = (byte)(item.TwoHand ? 1 : 0);
        BitConverter.TryWriteBytes(s[(o + 2)..], item.Level);
        s[o + 4] = item.Slot;
        BitConverter.TryWriteBytes(s[(o + 6)..], item.SkillIndex);
        s[o + 8] = item.Width;
        s[o + 9] = item.Height;
        s[o + 10] = item.DamageMin;
        s[o + 11] = item.DamageMax;
        s[o + 12] = item.SuccessfulBlocking;
        s[o + 13] = item.Defense;
        s[o + 14] = item.MagicDefense;
        s[o + 15] = item.WeaponSpeed;
        s[o + 16] = item.WalkSpeed;
        s[o + 17] = item.Durability;
        s[o + 18] = item.MagicDur;
        s[o + 19] = item.MagicPower;
        BitConverter.TryWriteBytes(s[(o + 20)..], item.RequireStrength);
        BitConverter.TryWriteBytes(s[(o + 22)..], item.RequireDexterity);
        BitConverter.TryWriteBytes(s[(o + 24)..], item.RequireEnergy);
        BitConverter.TryWriteBytes(s[(o + 26)..], item.RequireVitality);
        BitConverter.TryWriteBytes(s[(o + 28)..], item.RequireCharisma);
        BitConverter.TryWriteBytes(s[(o + 30)..], item.RequireLevel);
        s[o + 32] = item.Value;
        BitConverter.TryWriteBytes(s[(o + 34)..], item.Zen);
        s[o + 38] = item.AttType;
        Pad(item.RequireClass, 7).CopyTo(s.Slice(o + 39, 7));
        Pad(item.Resistance, 8).CopyTo(s.Slice(o + 46, 8));
    }

    public void Clear(int index)
    {
        _records.AsSpan(index * RecordSize, RecordSize).Clear();
    }

    /// <summary>Guarda el archivo. Escribe primero a un temporal y despues lo reemplaza.</summary>
    public void Save()
    {
        var body = (byte[])_records.Clone();
        Bux(body);
        var output = new byte[body.Length + 4];
        body.CopyTo(output, 0);
        BitConverter.TryWriteBytes(output.AsSpan(body.Length), Checksum(body, ChecksumKey));

        var tmp = Path + ".tmp";
        File.WriteAllBytes(tmp, output);
        File.Move(tmp, Path, overwrite: true);
    }

    private static byte[] Pad(List<byte>? source, int length)
    {
        var result = new byte[length];
        if (source is null) return result;
        for (int i = 0; i < Math.Min(source.Count, length); i++) result[i] = source[i];
        return result;
    }

    private static void Bux(Span<byte> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] ^= BuxCode[i % 3];
        }
    }

    private static uint Checksum(ReadOnlySpan<byte> buffer, ushort key)
    {
        uint dwKey = key;
        uint result = dwKey << 9;
        for (uint i = 0; i + 4 <= buffer.Length; i += 4)
        {
            uint temp = BitConverter.ToUInt32(buffer[(int)i..]);
            if (((i / 4 + key) % 2) == 0) result ^= temp;
            else result += temp;

            if (i % 16 == 0)
            {
                result ^= (dwKey + result) >> (int)((i / 4) % 8 + 1);
            }
        }

        return result;
    }
}
