using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace AgroCert.Application.Util;

public static class RasterUtil
{
    public static void Rasterizar(GeoUtil.Poligono pol, double x0, double y0, double dx, double dy, int w, int h, Action<int> marcar)
    {
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var a in pol.Aneis)
            foreach (var (_, y) in a) { if (y < minY) minY = y; if (y > maxY) maxY = y; }
        var l0 = Math.Max(0, (int)Math.Floor((y0 - maxY) / dy - 0.5));
        var l1 = Math.Min(h - 1, (int)Math.Ceiling((y0 - minY) / dy - 0.5));
        var xs = new List<double>();
        for (var l = l0; l <= l1; l++)
        {
            var y = y0 - (l + 0.5) * dy;
            xs.Clear();
            foreach (var a in pol.Aneis)
                for (int i = 0, j = a.Length - 1; i < a.Length; j = i++)
                {
                    var (xi, yi) = a[i];
                    var (xj, yj) = a[j];
                    if ((yi > y) != (yj > y)) xs.Add(xi + (y - yi) / (yj - yi) * (xj - xi));
                }
            if (xs.Count < 2) continue;
            xs.Sort();
            for (var k = 0; k + 1 < xs.Count; k += 2)
            {
                var c0 = Math.Max(0, (int)Math.Ceiling((xs[k] - x0) / dx - 0.5));
                var c1 = Math.Min(w - 1, (int)Math.Ceiling((xs[k + 1] - x0) / dx - 0.5) - 1);
                for (var c = c0; c <= c1; c++) marcar(l * w + c);
            }
        }
    }

    public static bool[] Mascara(IEnumerable<GeoUtil.Poligono> pols, double x0, double y0, double dx, double dy, int w, int h)
    {
        var m = new bool[w * h];
        foreach (var p in pols) Rasterizar(p, x0, y0, dx, dy, w, h, i => m[i] = true);
        return m;
    }

    public static (int[] Rotulos, int Quantidade) Rotular(bool[] m, int w, int h)
    {
        var rot = new int[w * h];
        var pai = new List<int> { 0 };
        int Raiz(int a) { while (pai[a] != a) { pai[a] = pai[pai[a]]; a = pai[a]; } return a; }
        void Unir(int a, int b) { a = Raiz(a); b = Raiz(b); if (a != b) { if (a < b) pai[b] = a; else pai[a] = b; } }
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                if (!m[i]) continue;
                var r = 0;
                void Ver(int j) { var l = rot[j]; if (l == 0) return; if (r == 0) r = l; else if (l != r) Unir(r, l); }
                if (x > 0) Ver(i - 1);
                if (y > 0) { if (x > 0) Ver(i - w - 1); Ver(i - w); if (x < w - 1) Ver(i - w + 1); }
                if (r == 0) { r = pai.Count; pai.Add(r); }
                rot[i] = r;
            }
        var novo = new int[pai.Count];
        var n = 0;
        for (var i = 1; i < pai.Count; i++) if (Raiz(i) == i) novo[i] = ++n;
        for (var i = 1; i < pai.Count; i++) novo[i] = novo[Raiz(i)];
        for (var i = 0; i < rot.Length; i++) if (rot[i] != 0) rot[i] = novo[rot[i]];
        return (rot, n);
    }

    public static byte[] Mercator(byte[] src, int w, int h, double latTopo, double dy)
    {
        static double M(double lat) => Math.Log(Math.Tan(Math.PI / 4 + lat * Math.PI / 360));
        double yT = M(latTopo), yB = M(latTopo - h * dy);
        var dst = new byte[src.Length];
        for (var k = 0; k < h; k++)
        {
            var ym = yT - (k + 0.5) * (yT - yB) / h;
            var lat = (2 * Math.Atan(Math.Exp(ym)) - Math.PI / 2) * 180 / Math.PI;
            var r = Math.Clamp((int)((latTopo - lat) / dy), 0, h - 1);
            Buffer.BlockCopy(src, r * w, dst, k * w, w);
        }
        return dst;
    }

    public static byte[] Png(byte[] indices, int w, int h, IReadOnlyList<(byte R, byte G, byte B, byte A)> cores, int escala = 1)
    {
        escala = Math.Max(1, escala);
        int W = w * escala, H = h * escala;
        using var ms = new MemoryStream();
        ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, W);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), H);
        ihdr[8] = 8; ihdr[9] = 3;
        Bloco(ms, "IHDR", ihdr);
        var plte = new byte[cores.Count * 3];
        var trns = new byte[cores.Count];
        for (var i = 0; i < cores.Count; i++)
        {
            plte[i * 3] = cores[i].R; plte[i * 3 + 1] = cores[i].G; plte[i * 3 + 2] = cores[i].B; trns[i] = cores[i].A;
        }
        Bloco(ms, "PLTE", plte);
        Bloco(ms, "tRNS", trns);
        using (var dados = new MemoryStream())
        {
            using (var z = new ZLibStream(dados, CompressionLevel.Optimal, true))
            {
                var linha = new byte[W + 1];
                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        var v = indices[y * w + x];
                        for (var e = 0; e < escala; e++) linha[1 + x * escala + e] = v;
                    }
                    for (var e = 0; e < escala; e++) z.Write(linha);
                }
            }
            Bloco(ms, "IDAT", dados.ToArray());
        }
        Bloco(ms, "IEND", Array.Empty<byte>());
        return ms.ToArray();
    }

    public static string DataUri(byte[] png) => "data:image/png;base64," + Convert.ToBase64String(png);

    private static void Bloco(Stream s, string tipo, byte[] dados)
    {
        var cab = new byte[8];
        BinaryPrimitives.WriteInt32BigEndian(cab, dados.Length);
        Encoding.ASCII.GetBytes(tipo, cab.AsSpan(4));
        s.Write(cab);
        s.Write(dados);
        var crc = Crc(dados, Crc(cab.AsSpan(4, 4), 0xFFFFFFFF)) ^ 0xFFFFFFFF;
        var c = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(c, crc);
        s.Write(c);
    }

    private static readonly uint[] TabelaCrc = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc(ReadOnlySpan<byte> b, uint c)
    {
        foreach (var x in b) c = TabelaCrc[(c ^ x) & 0xFF] ^ (c >> 8);
        return c;
    }
}

public static class LegendaMapBiomas
{
    public enum Tipo { Natural, Antropico, Neutro }

    public sealed record Classe(int Codigo, string Nome, string Cor, Tipo Tipo);

    private static readonly Classe[] Lista =
    {
        new(3, "Formação Florestal", "#1f8d49", Tipo.Natural),
        new(4, "Formação Savânica", "#7dc975", Tipo.Natural),
        new(5, "Mangue", "#04381d", Tipo.Natural),
        new(6, "Floresta Alagável", "#026975", Tipo.Natural),
        new(49, "Restinga Arbórea", "#02d659", Tipo.Natural),
        new(11, "Campo Alagado e Área Pantanosa", "#519799", Tipo.Natural),
        new(12, "Formação Campestre", "#d6bc74", Tipo.Natural),
        new(32, "Apicum", "#fc8114", Tipo.Natural),
        new(29, "Afloramento Rochoso", "#ffaa5f", Tipo.Natural),
        new(50, "Restinga Herbácea", "#ad5100", Tipo.Natural),
        new(13, "Outra Formação não Florestal", "#d89f5c", Tipo.Natural),
        new(9, "Silvicultura", "#7a5900", Tipo.Antropico),
        new(15, "Pastagem", "#edde8e", Tipo.Antropico),
        new(14, "Agropecuária", "#ffefc3", Tipo.Antropico),
        new(18, "Agricultura", "#e974ed", Tipo.Antropico),
        new(19, "Lavoura Temporária", "#c27ba0", Tipo.Antropico),
        new(39, "Soja", "#f5b3c8", Tipo.Antropico),
        new(20, "Cana", "#db7093", Tipo.Antropico),
        new(40, "Arroz", "#c71585", Tipo.Antropico),
        new(62, "Algodão", "#ff69b4", Tipo.Antropico),
        new(41, "Outras Lavouras Temporárias", "#f54ca9", Tipo.Antropico),
        new(36, "Lavoura Perene", "#d082de", Tipo.Antropico),
        new(46, "Café", "#d68fe2", Tipo.Antropico),
        new(47, "Citrus", "#9932cc", Tipo.Antropico),
        new(35, "Dendê", "#9065d0", Tipo.Antropico),
        new(48, "Outras Lavouras Perenes", "#e6ccff", Tipo.Antropico),
        new(21, "Mosaico de Usos", "#ffefc3", Tipo.Antropico),
        new(24, "Área Urbanizada", "#d4271e", Tipo.Antropico),
        new(30, "Mineração", "#9c0027", Tipo.Antropico),
        new(75, "Usina Fotovoltaica", "#c12100", Tipo.Antropico),
        new(31, "Aquicultura", "#091077", Tipo.Antropico),
        new(23, "Praia, Duna e Areal", "#ffa07a", Tipo.Neutro),
        new(25, "Outras Áreas não Vegetadas", "#db4d4f", Tipo.Neutro),
        new(22, "Área não Vegetada", "#d4271e", Tipo.Neutro),
        new(26, "Corpo d'Água", "#2532e4", Tipo.Neutro),
        new(33, "Rio, Lago e Oceano", "#2532e4", Tipo.Neutro),
        new(27, "Não Observado", "#ffffff", Tipo.Neutro),
    };

    private static readonly Classe?[] PorCodigo = Criar();
    private static Classe?[] Criar()
    {
        var a = new Classe?[256];
        foreach (var c in Lista) a[c.Codigo] = c;
        return a;
    }

    public static Classe? Obter(int codigo) => codigo is >= 0 and < 256 ? PorCodigo[codigo] : null;
    public static string Nome(int codigo) => Obter(codigo)?.Nome ?? (codigo == 0 ? "Sem dado" : $"Classe {codigo}");
    public static bool Natural(byte c) => PorCodigo[c]?.Tipo == Tipo.Natural;
    public static bool Antropico(byte c) => PorCodigo[c]?.Tipo == Tipo.Antropico;

    public static readonly IReadOnlyList<(byte R, byte G, byte B, byte A)> Paleta = Enumerable.Range(0, 256).Select(i =>
    {
        var c = PorCodigo[i];
        if (c is null) return i == 0 ? ((byte)0, (byte)0, (byte)0, (byte)0) : ((byte)200, (byte)200, (byte)200, (byte)255);
        return (Convert.ToByte(c.Cor.Substring(1, 2), 16), Convert.ToByte(c.Cor.Substring(3, 2), 16), Convert.ToByte(c.Cor.Substring(5, 2), 16), (byte)255);
    }).ToList();
}
