// Lector de modelos .bmd de MU Online (formato de Webzen), segun BMD::Open2 de MuMain
// (src/source/Render/Models/ZzzBMD.cpp):
//   "BMD" + version. Version 0xC: int32 tamano + datos cifrados (MapFileDecrypt);
//   version 0xA: datos en claro. Despues: Name[32], NumMeshs, NumBones, NumActions (int16),
//   mallas (vertices con hueso, normales, UVs, triangulos de 64 bytes, textura[32]),
//   acciones (frames, LockPositions [+ posiciones]) y huesos (Dummy, Name[32], Parent,
//   por accion: Position[frames] + Rotation[frames] en radianes).
//
// La pose se calcula como en BMD::Animation(): matriz del hueso = matriz del padre *
// (rotacion Euler -> quaternion -> matriz, traslacion), frame 0 de la accion 0.

using System.Text;

namespace VyperMu.ItemEditor;

public sealed class BmdMesh
{
    public string Texture { get; set; } = string.Empty;
    public float[] Positions { get; set; } = Array.Empty<float>();
    public float[] Normals { get; set; } = Array.Empty<float>();
    public float[] Uvs { get; set; } = Array.Empty<float>();
    public int[] Indices { get; set; } = Array.Empty<int>();
    public int Triangles => Indices.Length / 3;
}

public sealed class BmdBone
{
    public bool Dummy;
    public string Name = string.Empty;
    public short Parent = -1;
    /// <summary>[accion][frame] posicion y rotacion.</summary>
    public float[][][] Position = Array.Empty<float[][]>();
    public float[][][] Rotation = Array.Empty<float[][]>();
}

public sealed class BmdModel
{
    private static readonly byte[] MapXorKey = { 0xD1, 0x73, 0x52, 0xF6, 0xD2, 0x9A, 0xCB, 0x27, 0x3E, 0xAF, 0x59, 0x31, 0x37, 0xB3, 0xE7, 0xA2 };

    public string Name { get; private set; } = string.Empty;
    public byte Version { get; private set; }
    public List<RawMesh> Meshes { get; } = new();
    public List<BmdBone> Bones { get; } = new();
    public List<int> ActionFrames { get; } = new();

    public sealed class RawMesh
    {
        public string Texture = string.Empty;
        public (short Node, float X, float Y, float Z)[] Vertices = Array.Empty<(short, float, float, float)>();
        public (short Node, float X, float Y, float Z)[] Normals = Array.Empty<(short, float, float, float)>();
        public (float U, float V)[] TexCoords = Array.Empty<(float, float)>();
        public (byte Polygon, short[] V, short[] N, short[] T)[] Triangles = Array.Empty<(byte, short[], short[], short[])>();
    }

    public static BmdModel Load(string path)
    {
        var file = File.ReadAllBytes(path);
        if (file.Length < 4 || file[0] != (byte)'B' || file[1] != (byte)'M' || file[2] != (byte)'D')
        {
            throw new InvalidDataException("No es un archivo BMD.");
        }

        var model = new BmdModel { Version = file[3] };
        byte[] data;
        int p;
        switch (model.Version)
        {
            case 0xC:
            {
                int encSize = BitConverter.ToInt32(file, 4);
                data = new byte[encSize];
                ushort key = 0x5E;
                for (int i = 0; i < encSize; i++)
                {
                    byte src = file[8 + i];
                    data[i] = (byte)((src ^ MapXorKey[i % 16]) - (byte)key);
                    key = (ushort)((src + 0x3D) & 0xFF);
                }
                p = 0;
                break;
            }
            case 0xA:
                data = file;
                p = 4;
                break;
            default:
                throw new InvalidDataException($"Version de BMD no soportada: 0x{model.Version:X}.");
        }

        model.Name = ReadString(data, p, 32); p += 32;
        int numMeshes = BitConverter.ToInt16(data, p); p += 2;
        int numBones = BitConverter.ToInt16(data, p); p += 2;
        int numActions = BitConverter.ToInt16(data, p); p += 2;

        for (int m = 0; m < numMeshes; m++)
        {
            var mesh = new RawMesh();
            int nv = BitConverter.ToInt16(data, p); p += 2;
            int nn = BitConverter.ToInt16(data, p); p += 2;
            int nt = BitConverter.ToInt16(data, p); p += 2;
            int ntri = BitConverter.ToInt16(data, p); p += 2;
            p += 2; // Texture (indice, no se usa)

            mesh.Vertices = new (short, float, float, float)[nv];
            for (int i = 0; i < nv; i++)
            {
                mesh.Vertices[i] = (BitConverter.ToInt16(data, p), BitConverter.ToSingle(data, p + 4), BitConverter.ToSingle(data, p + 8), BitConverter.ToSingle(data, p + 12));
                p += 16;
            }

            mesh.Normals = new (short, float, float, float)[nn];
            for (int i = 0; i < nn; i++)
            {
                mesh.Normals[i] = (BitConverter.ToInt16(data, p), BitConverter.ToSingle(data, p + 4), BitConverter.ToSingle(data, p + 8), BitConverter.ToSingle(data, p + 12));
                p += 20;
            }

            mesh.TexCoords = new (float, float)[nt];
            for (int i = 0; i < nt; i++)
            {
                mesh.TexCoords[i] = (BitConverter.ToSingle(data, p), BitConverter.ToSingle(data, p + 4));
                p += 8;
            }

            mesh.Triangles = new (byte, short[], short[], short[])[ntri];
            for (int i = 0; i < ntri; i++)
            {
                byte polygon = data[p];
                var v = new short[4]; var n = new short[4]; var t = new short[4];
                for (int k = 0; k < 4; k++)
                {
                    v[k] = BitConverter.ToInt16(data, p + 2 + k * 2);
                    n[k] = BitConverter.ToInt16(data, p + 10 + k * 2);
                    t[k] = BitConverter.ToInt16(data, p + 18 + k * 2);
                }
                mesh.Triangles[i] = (polygon, v, n, t);
                p += 64; // sizeof(Triangle_t2)
            }

            mesh.Texture = ReadString(data, p, 32); p += 32;
            model.Meshes.Add(mesh);
        }

        for (int a = 0; a < numActions; a++)
        {
            int keys = BitConverter.ToInt16(data, p); p += 2;
            bool lockPositions = data[p] != 0; p += 1;
            if (lockPositions && keys > 0) p += 12 * keys;
            model.ActionFrames.Add(keys);
        }

        for (int b = 0; b < numBones; b++)
        {
            var bone = new BmdBone { Dummy = data[p] != 0 };
            p += 1;
            if (!bone.Dummy)
            {
                bone.Name = ReadString(data, p, 32); p += 32;
                bone.Parent = BitConverter.ToInt16(data, p); p += 2;
                bone.Position = new float[numActions][][];
                bone.Rotation = new float[numActions][][];
                for (int a = 0; a < numActions; a++)
                {
                    int keys = model.ActionFrames[a];
                    bone.Position[a] = new float[keys][];
                    bone.Rotation[a] = new float[keys][];
                    for (int k = 0; k < keys; k++)
                    {
                        bone.Position[a][k] = new[] { BitConverter.ToSingle(data, p), BitConverter.ToSingle(data, p + 4), BitConverter.ToSingle(data, p + 8) };
                        p += 12;
                    }
                    for (int k = 0; k < keys; k++)
                    {
                        bone.Rotation[a][k] = new[] { BitConverter.ToSingle(data, p), BitConverter.ToSingle(data, p + 4), BitConverter.ToSingle(data, p + 8) };
                        p += 12;
                    }
                }
            }
            model.Bones.Add(bone);
        }

        return model;
    }

    /// <summary>Matrices 3x4 de cada hueso para la accion y frame dados (como BMD::Animation sin interpolar).</summary>
    public float[][] BoneMatrices(int action = 0, int frame = 0)
    {
        var result = new float[Bones.Count][];
        for (int i = 0; i < Bones.Count; i++)
        {
            var b = Bones[i];
            var local = Identity();
            if (!b.Dummy && b.Position.Length > action && b.Position[action].Length > frame)
            {
                local = QuaternionMatrix(AngleQuaternion(b.Rotation[action][frame]));
                local[3] = b.Position[action][frame][0];
                local[7] = b.Position[action][frame][1];
                local[11] = b.Position[action][frame][2];
            }

            result[i] = b.Dummy || b.Parent < 0 || b.Parent >= i ? local : Concat(result[b.Parent], local);
        }

        return result;
    }

    /// <summary>Aplana las mallas a listas planas, transformando vertices por sus huesos (o los del esqueleto dado).</summary>
    public List<BmdMesh> Bake(float[][]? skeleton = null)
    {
        var bones = skeleton ?? BoneMatrices();
        var output = new List<BmdMesh>();
        foreach (var mesh in Meshes)
        {
            var pos = new List<float>();
            var nor = new List<float>();
            var uv = new List<float>();
            var idx = new List<int>();
            var cache = new Dictionary<(short, short, short), int>();

            int Corner(short vi, short ni, short ti)
            {
                var key = (vi, ni, ti);
                if (cache.TryGetValue(key, out var existing)) return existing;
                var v = mesh.Vertices[vi];
                (short Node, float X, float Y, float Z) n = ni >= 0 && ni < mesh.Normals.Length ? mesh.Normals[ni] : (v.Node, 0f, 0f, 1f);
                (float U, float V) t = ti >= 0 && ti < mesh.TexCoords.Length ? mesh.TexCoords[ti] : (0f, 0f);
                var m = v.Node >= 0 && v.Node < bones.Length ? bones[v.Node] : Identity();
                var mn = n.Node >= 0 && n.Node < bones.Length ? bones[n.Node] : m;
                pos.Add(m[0] * v.X + m[1] * v.Y + m[2] * v.Z + m[3]);
                pos.Add(m[4] * v.X + m[5] * v.Y + m[6] * v.Z + m[7]);
                pos.Add(m[8] * v.X + m[9] * v.Y + m[10] * v.Z + m[11]);
                nor.Add(mn[0] * n.X + mn[1] * n.Y + mn[2] * n.Z);
                nor.Add(mn[4] * n.X + mn[5] * n.Y + mn[6] * n.Z);
                nor.Add(mn[8] * n.X + mn[9] * n.Y + mn[10] * n.Z);
                uv.Add(t.U);
                uv.Add(t.V);
                int index = pos.Count / 3 - 1;
                cache[key] = index;
                return index;
            }

            foreach (var (polygon, v, n, t) in mesh.Triangles)
            {
                if (v[0] >= mesh.Vertices.Length || v[1] >= mesh.Vertices.Length || v[2] >= mesh.Vertices.Length) continue;
                idx.Add(Corner(v[0], n[0], t[0]));
                idx.Add(Corner(v[1], n[1], t[1]));
                idx.Add(Corner(v[2], n[2], t[2]));
                if (polygon == 4 && v[3] < mesh.Vertices.Length)
                {
                    idx.Add(Corner(v[0], n[0], t[0]));
                    idx.Add(Corner(v[2], n[2], t[2]));
                    idx.Add(Corner(v[3], n[3], t[3]));
                }
            }

            output.Add(new BmdMesh { Texture = mesh.Texture, Positions = pos.ToArray(), Normals = nor.ToArray(), Uvs = uv.ToArray(), Indices = idx.ToArray() });
        }

        return output;
    }

    // ------------------------------------------------------------ matematica (ZzzMathLib.cpp)

    private static float[] Identity() => new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0 };

    private static float[] AngleQuaternion(float[] angles)
    {
        float sy = MathF.Sin(angles[2] * 0.5f), cy = MathF.Cos(angles[2] * 0.5f);
        float sp = MathF.Sin(angles[1] * 0.5f), cp = MathF.Cos(angles[1] * 0.5f);
        float sr = MathF.Sin(angles[0] * 0.5f), cr = MathF.Cos(angles[0] * 0.5f);
        return new[]
        {
            sr * cp * cy - cr * sp * sy,
            cr * sp * cy + sr * cp * sy,
            cr * cp * sy - sr * sp * cy,
            cr * cp * cy + sr * sp * sy,
        };
    }

    private static float[] QuaternionMatrix(float[] q)
    {
        float x = q[0], y = q[1], z = q[2], w = q[3];
        return new float[]
        {
            1 - 2 * y * y - 2 * z * z, 2 * x * y - 2 * w * z, 2 * x * z + 2 * w * y, 0,
            2 * x * y + 2 * w * z, 1 - 2 * x * x - 2 * z * z, 2 * y * z - 2 * w * x, 0,
            2 * x * z - 2 * w * y, 2 * y * z + 2 * w * x, 1 - 2 * x * x - 2 * y * y, 0,
        };
    }

    /// <summary>R_ConcatTransforms: out = a * b (matrices 3x4 por filas).</summary>
    private static float[] Concat(float[] a, float[] b)
    {
        var o = new float[12];
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 4; c++)
            {
                o[r * 4 + c] = a[r * 4] * b[c] + a[r * 4 + 1] * b[4 + c] + a[r * 4 + 2] * b[8 + c] + (c == 3 ? a[r * 4 + 3] : 0);
            }
        }
        return o;
    }

    private static string ReadString(byte[] data, int offset, int length)
    {
        int end = Array.IndexOf(data, (byte)0, offset, length);
        if (end < 0) end = offset + length;
        return Encoding.Latin1.GetString(data, offset, end - offset);
    }
}
