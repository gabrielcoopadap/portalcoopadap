using System.Buffers.Binary;
using System.IO.Compression;

namespace RecortarMapBiomas;

public sealed class GeoTiffLeitor : IDisposable
{
    private readonly FileStream _arq;
    private readonly bool _little;
    private readonly bool _big;

    public int Largura { get; private set; }
    public int Altura { get; private set; }
    public int Compressao { get; private set; } = 1;
    public int Preditor { get; private set; } = 1;
    public int BlocoLargura { get; private set; }
    public int BlocoAltura { get; private set; }
    public bool EmTiles { get; private set; }
    public long[] Offsets { get; private set; } = Array.Empty<long>();
    public long[] Tamanhos { get; private set; } = Array.Empty<long>();
    public double[] Escala { get; private set; } = Array.Empty<double>();
    public double[] PontoAmarra { get; private set; } = Array.Empty<double>();
    public ushort[] GeoChaves { get; private set; } = Array.Empty<ushort>();
    public string? NoData { get; private set; }

    public double X0 => PontoAmarra[3] - PontoAmarra[0] * Escala[0];
    public double Y0 => PontoAmarra[4] + PontoAmarra[1] * Escala[1];
    public double Dx => Escala[0];
    public double Dy => Escala[1];

    public GeoTiffLeitor(string caminho)
    {
        _arq = new FileStream(caminho, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, FileOptions.RandomAccess);
        var cab = Ler(0, 16);
        _little = cab[0] == (byte)'I';
        var versao = U16(cab, 2);
        _big = versao == 43;
        if (versao != 42 && versao != 43) throw new InvalidDataException("Arquivo não é um TIFF.");
        var ifd = _big ? (long)U64(cab, 8) : U32(cab, 4);
        LerIfd(ifd);
        if (Escala.Length < 2 || PontoAmarra.Length < 6)
            throw new InvalidDataException("O TIFF não tem georreferência (ModelPixelScale/ModelTiepoint).");
    }

    private void LerIfd(long pos)
    {
        var tamCont = _big ? 8 : 2;
        var nb = Ler(pos, tamCont);
        long n = _big ? (long)U64(nb, 0) : U16(nb, 0);
        var tamEntrada = _big ? 20 : 12;
        var entradas = Ler(pos + tamCont, (int)(n * tamEntrada));
        int bits = 8, amostras = 1, rowsPerStrip = 0;
        for (var i = 0; i < n; i++)
        {
            var o = i * tamEntrada;
            int tag = U16(entradas, o), tipo = U16(entradas, o + 2);
            long cont = _big ? (long)U64(entradas, o + 4) : U32(entradas, o + 4);
            var valorPos = o + (_big ? 12 : 8);
            var tamTipo = tipo switch { 1 or 2 or 6 or 7 => 1, 3 or 8 => 2, 4 or 9 or 11 => 4, 5 or 10 or 12 or 16 or 17 or 18 => 8, _ => 1 };
            var bytes = cont * tamTipo;
            byte[] dados;
            var cabe = bytes <= (_big ? 8 : 4);
            if (cabe) { dados = new byte[bytes]; Array.Copy(entradas, valorPos, dados, 0, bytes); }
            else
            {
                var off = _big ? (long)U64(entradas, valorPos) : U32(entradas, valorPos);
                dados = Ler(off, (int)bytes);
            }
            long[] Inteiros() => Enumerable.Range(0, (int)cont).Select(k => tipo switch
            {
                3 => (long)U16(dados, k * 2),
                4 => U32(dados, k * 4),
                16 => (long)U64(dados, k * 8),
                _ => dados[k],
            }).ToArray();
            double[] Duplos() => Enumerable.Range(0, (int)cont).Select(k => BitConverter.Int64BitsToDouble(
                _little ? BinaryPrimitives.ReadInt64LittleEndian(dados.AsSpan(k * 8)) : BinaryPrimitives.ReadInt64BigEndian(dados.AsSpan(k * 8)))).ToArray();

            switch (tag)
            {
                case 256: Largura = (int)Inteiros()[0]; break;
                case 257: Altura = (int)Inteiros()[0]; break;
                case 258: bits = (int)Inteiros()[0]; break;
                case 259: Compressao = (int)Inteiros()[0]; break;
                case 277: amostras = (int)Inteiros()[0]; break;
                case 278: rowsPerStrip = (int)Inteiros()[0]; break;
                case 273: case 324: Offsets = Inteiros(); EmTiles |= tag == 324; break;
                case 279: case 325: Tamanhos = Inteiros(); break;
                case 317: Preditor = (int)Inteiros()[0]; break;
                case 322: BlocoLargura = (int)Inteiros()[0]; break;
                case 323: BlocoAltura = (int)Inteiros()[0]; break;
                case 33550: Escala = Duplos(); break;
                case 33922: PontoAmarra = Duplos(); break;
                case 34735: GeoChaves = Inteiros().Select(v => (ushort)v).ToArray(); break;
                case 42113: NoData = System.Text.Encoding.ASCII.GetString(dados).TrimEnd('\0', ' '); break;
            }
        }
        if (bits != 8 || amostras != 1)
            throw new NotSupportedException($"Esperado 1 banda de 8 bits (MapBiomas); o arquivo tem {amostras} banda(s) de {bits} bits.");
        if (!EmTiles)
        {
            BlocoLargura = Largura;
            BlocoAltura = rowsPerStrip <= 0 ? Altura : Math.Min(rowsPerStrip, Altura);
        }
        if (Compressao is not (1 or 5 or 8 or 32946 or 32773))
            throw new NotSupportedException($"Compressão TIFF {Compressao} não suportada (use LZW ou Deflate).");
    }

    public byte[] LerJanela(int col0, int lin0, int larg, int alt, byte fora = 0, Action<double>? progresso = null)
    {
        var saida = new byte[(long)larg * alt];
        if (fora != 0) Array.Fill(saida, fora);
        var blocosX = (Largura + BlocoLargura - 1) / BlocoLargura;
        int bx0 = Math.Max(0, col0 / BlocoLargura), bx1 = Math.Min(blocosX - 1, (col0 + larg - 1) / BlocoLargura);
        int by0 = Math.Max(0, lin0 / BlocoAltura), by1 = Math.Min((Altura - 1) / BlocoAltura, (lin0 + alt - 1) / BlocoAltura);
        var total = Math.Max(1, (bx1 - bx0 + 1) * (by1 - by0 + 1));
        var feitos = 0;
        var bloco = new byte[BlocoLargura * BlocoAltura];
        for (var by = by0; by <= by1; by++)
        {
            for (var bx = bx0; bx <= bx1; bx++)
            {
                var idx = EmTiles ? by * blocosX + bx : by;
                if (idx >= Offsets.Length) continue;
                var linhasBloco = EmTiles ? BlocoAltura : Math.Min(BlocoAltura, Altura - by * BlocoAltura);
                var esperado = BlocoLargura * linhasBloco;
                Array.Clear(bloco);
                if (Tamanhos[idx] > 0)
                {
                    var bruto = Ler(Offsets[idx], (int)Tamanhos[idx]);
                    Descomprimir(bruto, bloco, esperado);
                    if (Preditor == 2)
                        for (var l = 0; l < linhasBloco; l++)
                            for (var c = 1; c < BlocoLargura; c++)
                                bloco[l * BlocoLargura + c] += bloco[l * BlocoLargura + c - 1];
                }
                int gx0 = bx * BlocoLargura, gy0 = by * BlocoAltura;
                int ix0 = Math.Max(gx0, col0), ix1 = Math.Min(Math.Min(gx0 + BlocoLargura, Largura), col0 + larg);
                int iy0 = Math.Max(gy0, lin0), iy1 = Math.Min(Math.Min(gy0 + linhasBloco, Altura), lin0 + alt);
                for (var y = iy0; y < iy1; y++)
                    Buffer.BlockCopy(bloco, (y - gy0) * BlocoLargura + (ix0 - gx0), saida, (int)((long)(y - lin0) * larg + (ix0 - col0)), ix1 - ix0);
                progresso?.Invoke(++feitos / (double)total);
            }
        }
        return saida;
    }

    private void Descomprimir(byte[] origem, byte[] destino, int esperado)
    {
        switch (Compressao)
        {
            case 1:
                Buffer.BlockCopy(origem, 0, destino, 0, Math.Min(origem.Length, esperado));
                break;
            case 8:
            case 32946:
            {
                using var z = new ZLibStream(new MemoryStream(origem), CompressionMode.Decompress);
                var lido = 0;
                while (lido < esperado)
                {
                    var n = z.Read(destino, lido, esperado - lido);
                    if (n == 0) break;
                    lido += n;
                }
                break;
            }
            case 5:
                Lzw.Decodificar(origem, destino, esperado);
                break;
            case 32773:
            {
                int i = 0, o = 0;
                while (i < origem.Length && o < esperado)
                {
                    var n = (sbyte)origem[i++];
                    if (n >= 0) { var q = Math.Min(n + 1, esperado - o); Buffer.BlockCopy(origem, i, destino, o, q); i += n + 1; o += q; }
                    else if (n != -128) { var q = Math.Min(1 - n, esperado - o); Array.Fill(destino, origem[i++], o, q); o += q; }
                }
                break;
            }
        }
    }

    private byte[] Ler(long pos, int n)
    {
        var b = new byte[n];
        _arq.Seek(pos, SeekOrigin.Begin);
        _arq.ReadExactly(b, 0, n);
        return b;
    }

    private ushort U16(byte[] b, int o) => _little ? BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(o)) : BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(o));
    private uint U32(byte[] b, int o) => _little ? BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(o)) : BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(o));
    private ulong U64(byte[] b, int o) => _little ? BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(o)) : BinaryPrimitives.ReadUInt64BigEndian(b.AsSpan(o));

    public void Dispose() => _arq.Dispose();
}

internal static class Lzw
{
    public static void Decodificar(byte[] entrada, byte[] saida, int esperado)
    {
        var tabela = new byte[4096][];
        for (var i = 0; i < 256; i++) tabela[i] = new[] { (byte)i };
        int prox = 258, largura = 9, o = 0;
        long bitPos = 0, totalBits = (long)entrada.Length * 8;
        byte[]? anterior = null;
        int LerCodigo()
        {
            if (bitPos + largura > totalBits) return 257;
            var v = 0;
            for (var k = 0; k < largura; k++)
            {
                var bit = (entrada[(bitPos + k) >> 3] >> (7 - (int)((bitPos + k) & 7))) & 1;
                v = (v << 1) | bit;
            }
            bitPos += largura;
            return v;
        }
        while (o < esperado)
        {
            var cod = LerCodigo();
            if (cod == 257) break;
            if (cod == 256)
            {
                prox = 258; largura = 9; anterior = null;
                cod = LerCodigo();
                if (cod == 257) break;
                var s0 = tabela[cod];
                o += Copiar(s0, saida, o, esperado);
                anterior = s0;
                continue;
            }
            byte[] s;
            if (cod < prox && tabela[cod] is { } t) s = t;
            else if (anterior is not null) { s = new byte[anterior.Length + 1]; anterior.CopyTo(s, 0); s[^1] = anterior[0]; }
            else break;
            o += Copiar(s, saida, o, esperado);
            if (anterior is not null && prox < 4096)
            {
                var novo = new byte[anterior.Length + 1];
                anterior.CopyTo(novo, 0);
                novo[^1] = s[0];
                tabela[prox++] = novo;
            }
            anterior = s;
            if (prox >= (1 << largura) - 1 && largura < 12) largura++;
        }
    }

    private static int Copiar(byte[] s, byte[] saida, int o, int esperado)
    {
        var n = Math.Min(s.Length, esperado - o);
        Buffer.BlockCopy(s, 0, saida, o, n);
        return n;
    }
}

public static class GeoTiffEscritor
{
    public static void Gravar(string caminho, byte[] dados, int largura, int altura, double x0, double y0, double dx, double dy, ushort[] geoChaves, string? noData)
    {
        const int linhasPorFaixa = 64;
        var faixas = (altura + linhasPorFaixa - 1) / linhasPorFaixa;
        var comprimidas = new byte[faixas][];
        for (var f = 0; f < faixas; f++)
        {
            var l0 = f * linhasPorFaixa;
            var n = Math.Min(linhasPorFaixa, altura - l0);
            // preditor horizontal: comprime muito melhor mapas de classes
            var faixa = new byte[largura * n];
            for (var l = 0; l < n; l++)
            {
                var b = (l0 + l) * largura;
                faixa[l * largura] = dados[b];
                for (var c = 1; c < largura; c++) faixa[l * largura + c] = (byte)(dados[b + c] - dados[b + c - 1]);
            }
            using var ms = new MemoryStream();
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(faixa);
            comprimidas[f] = ms.ToArray();
        }
        geoChaves = new ushort[] { 1, 1, 0, 3, 1024, 0, 1, 2, 1025, 0, 1, 1, 2048, 0, 1, 4326 };

        using var s = new FileStream(caminho, FileMode.Create, FileAccess.Write);
        using var w = new BinaryWriter(s);
        w.Write((byte)'I'); w.Write((byte)'I'); w.Write((ushort)42); w.Write(0u);

        var offsets = new uint[faixas];
        for (var f = 0; f < faixas; f++) { offsets[f] = (uint)s.Position; w.Write(comprimidas[f]); if ((s.Position & 1) == 1) w.Write((byte)0); }

        uint Bloco(Action<BinaryWriter> escrever) { var p = (uint)s.Position; escrever(w); if ((s.Position & 1) == 1) w.Write((byte)0); return p; }
        var pOff = Bloco(b => { foreach (var o in offsets) b.Write(o); });
        var pTam = Bloco(b => { foreach (var c in comprimidas) b.Write((uint)c.Length); });
        var pEsc = Bloco(b => { b.Write(dx); b.Write(dy); b.Write(0d); });
        var pAmarra = Bloco(b => { b.Write(0d); b.Write(0d); b.Write(0d); b.Write(x0); b.Write(y0); b.Write(0d); });
        var pGeo = Bloco(b => { foreach (var v in geoChaves) b.Write(v); });
        var nd = noData is null ? null : System.Text.Encoding.ASCII.GetBytes(noData + "\0");
        var pNd = nd is null ? 0u : Bloco(b => b.Write(nd));

        var entradas = new List<(ushort Tag, ushort Tipo, uint Cont, uint Valor)>
        {
            (256, 4, 1, (uint)largura), (257, 4, 1, (uint)altura), (258, 3, 1, 8), (259, 3, 1, 8), (262, 3, 1, 1),
            (273, 4, (uint)faixas, faixas == 1 ? offsets[0] : pOff), (277, 3, 1, 1), (278, 4, 1, linhasPorFaixa),
            (279, 4, (uint)faixas, faixas == 1 ? (uint)comprimidas[0].Length : pTam), (284, 3, 1, 1), (317, 3, 1, 2),
            (339, 3, 1, 1), (33550, 12, 3, pEsc), (33922, 12, 6, pAmarra), (34735, 3, (uint)geoChaves.Length, pGeo),
        };
        if (nd is not null) entradas.Add((42113, 2, (uint)nd.Length, nd.Length <= 4 ? BitConverter.ToUInt32(nd.Concat(new byte[4]).Take(4).ToArray()) : pNd));
        entradas.Sort((a, b) => a.Tag.CompareTo(b.Tag));

        var pIfd = (uint)s.Position;
        w.Write((ushort)entradas.Count);
        foreach (var (tag, tipo, cont, valor) in entradas)
        {
            w.Write(tag); w.Write(tipo); w.Write(cont);
            if (tipo == 3 && cont == 1) { w.Write((ushort)valor); w.Write((ushort)0); } else w.Write(valor);
        }
        w.Write(0u);
        s.Seek(4, SeekOrigin.Begin);
        w.Write(pIfd);
    }
}
