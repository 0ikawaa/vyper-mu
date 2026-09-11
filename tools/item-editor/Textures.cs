// Texturas del cliente: .OZJ = JPEG con 24 bytes de cabecera, .OZT = TGA (32 bits sin
// comprimir) con 4 bytes, .OZB = BMP con 4 bytes. Ver GlobalBitmap.cpp de MuMain.
// El TGA se convierte a PNG aca porque el navegador no lo decodifica.

using System.Buffers.Binary;
using System.IO.Compression;

namespace VyperMu.ItemEditor;

public static class Textures
{
    /// <summary>Busca el archivo de textura que corresponde al nombre guardado en el BMD (ej. "sword01.jpg").</summary>
    public static string? Resolve(string directory, string textureName)
    {
        if (string.IsNullOrWhiteSpace(textureName) || !Directory.Exists(directory)) return null;
        var stem = Path.GetFileNameWithoutExtension(textureName);
        var ext = Path.GetExtension(textureName).ToLowerInvariant();
        var candidates = ext switch
        {
            ".jpg" or ".jpeg" => new[] { ".ozj", ".ozt", ".ozb" },
            ".tga" => new[] { ".ozt", ".ozj", ".ozb" },
            ".bmp" => new[] { ".ozb", ".ozj", ".ozt" },
            _ => new[] { ".ozj", ".ozt", ".ozb" },
        };

        var files = Directory.EnumerateFiles(directory).ToDictionary(f => Path.GetFileName(f).ToLowerInvariant(), f => f);
        foreach (var c in candidates)
        {
            if (files.TryGetValue((stem + c).ToLowerInvariant(), out var found)) return found;
        }

        return null;
    }

    /// <summary>Devuelve (contentType, bytes) listos para el navegador.</summary>
    public static (string ContentType, byte[] Data)? Decode(string path)
    {
        var bytes = File.ReadAllBytes(path);
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".ozj":
                return bytes.Length > 24 ? ("image/jpeg", bytes[24..]) : null;
            case ".ozb":
                return bytes.Length > 4 ? ("image/bmp", bytes[4..]) : null;
            case ".ozt":
                return bytes.Length > 22 ? ("image/png", TgaToPng(bytes.AsSpan(4))) : null;
            default:
                return null;
        }
    }

    private static byte[] TgaToPng(ReadOnlySpan<byte> tga)
    {
        int idLength = tga[0];
        int imageType = tga[2];
        int width = BinaryPrimitives.ReadInt16LittleEndian(tga[12..]);
        int height = BinaryPrimitives.ReadInt16LittleEndian(tga[14..]);
        int bpp = tga[16];
        bool topDown = (tga[17] & 0x20) != 0;
        if (width <= 0 || height <= 0 || (bpp != 32 && bpp != 24)) throw new InvalidDataException($"TGA no soportado ({bpp} bpp, tipo {imageType}).");
        int channels = bpp / 8;
        int offset = 18 + idLength;

        var rgba = new byte[width * height * 4];
        if (imageType == 2)
        {
            for (int y = 0; y < height; y++)
            {
                int srcRow = offset + y * width * channels;
                int dstY = topDown ? y : height - 1 - y;
                for (int x = 0; x < width; x++)
                {
                    int s = srcRow + x * channels, d = (dstY * width + x) * 4;
                    rgba[d] = tga[s + 2]; rgba[d + 1] = tga[s + 1]; rgba[d + 2] = tga[s]; rgba[d + 3] = channels == 4 ? tga[s + 3] : (byte)255;
                }
            }
        }
        else if (imageType == 10)
        {
            // RLE
            int p = offset, pixel = 0, total = width * height;
            while (pixel < total && p < tga.Length)
            {
                int packet = tga[p++];
                int count = (packet & 0x7F) + 1;
                if ((packet & 0x80) != 0)
                {
                    byte b = tga[p], g = tga[p + 1], r = tga[p + 2], a = channels == 4 ? tga[p + 3] : (byte)255;
                    p += channels;
                    for (int i = 0; i < count && pixel < total; i++, pixel++) Put(rgba, pixel, width, height, topDown, r, g, b, a);
                }
                else
                {
                    for (int i = 0; i < count && pixel < total; i++, pixel++)
                    {
                        Put(rgba, pixel, width, height, topDown, tga[p + 2], tga[p + 1], tga[p], channels == 4 ? tga[p + 3] : (byte)255);
                        p += channels;
                    }
                }
            }
        }
        else
        {
            throw new InvalidDataException($"TGA tipo {imageType} no soportado.");
        }

        return EncodePng(width, height, rgba);
    }

    private static void Put(byte[] rgba, int pixel, int width, int height, bool topDown, byte r, byte g, byte b, byte a)
    {
        int x = pixel % width, y = pixel / width;
        int dstY = topDown ? y : height - 1 - y;
        int d = (dstY * width + x) * 4;
        rgba[d] = r; rgba[d + 1] = g; rgba[d + 2] = b; rgba[d + 3] = a;
    }

    // ------------------------------------------------------------ PNG minimo (RGBA 8 bits, sin filtro)

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static byte[] EncodePng(int width, int height, byte[] rgba)
    {
        var raw = new byte[(width * 4 + 1) * height];
        for (int y = 0; y < height; y++)
        {
            raw[y * (width * 4 + 1)] = 0; // filtro None
            Buffer.BlockCopy(rgba, y * width * 4, raw, y * (width * 4 + 1) + 1, width * 4);
        }

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true)) z.Write(raw);

        using var output = new MemoryStream();
        output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8; ihdr[9] = 6; // 8 bits, RGBA
        Chunk(output, "IHDR", ihdr);
        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        uint crc = 0xFFFFFFFF;
        foreach (var b in typeBytes) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFF);
        s.Write(crcBytes);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
