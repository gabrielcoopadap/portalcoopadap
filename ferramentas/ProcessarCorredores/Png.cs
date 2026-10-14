using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace ProcessarCorredores;

public static class Png
{
    public static void Gravar(string caminho, byte[] indices, int w, int h, IReadOnlyList<(byte R, byte G, byte B, byte A)> cores)
    {
        using var fs = File.Create(caminho);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, w);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), h);
        ihdr[8] = 8; ihdr[9] = 3;
        Bloco(fs, "IHDR", ihdr);

        var plte = new byte[cores.Count * 3];
        var trns = new byte[cores.Count];
        for (var i = 0; i < cores.Count; i++)
        {
            plte[i * 3] = cores[i].R; plte[i * 3 + 1] = cores[i].G; plte[i * 3 + 2] = cores[i].B;
            trns[i] = cores[i].A;
        }
        Bloco(fs, "PLTE", plte);
        Bloco(fs, "tRNS", trns);

        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, true))
        {
            var linha = new byte[w + 1];
            for (var y = 0; y < h; y++)
            {
                linha[0] = 0;
                Buffer.BlockCopy(indices, y * w, linha, 1, w);
                z.Write(linha);
            }
        }
        Bloco(fs, "IDAT", ms.ToArray());
        Bloco(fs, "IEND", Array.Empty<byte>());
    }

    private static void Bloco(Stream s, string tipo, byte[] dados)
    {
        var cab = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(cab, dados.Length);
        Encoding.ASCII.GetBytes(tipo, cab.AsSpan(4));
        s.Write(cab);
        s.Write(dados);
        var crc = Crc(cab.AsSpan(4, 4), 0xFFFFFFFF);
        crc = Crc(dados, crc) ^ 0xFFFFFFFF;
        var c = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(c, crc);
        s.Write(c);
    }

    private static readonly uint[] Tabela = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc(ReadOnlySpan<byte> b, uint c)
    {
        foreach (var x in b) c = Tabela[(c ^ x) & 0xFF] ^ (c >> 8);
        return c;
    }
}
