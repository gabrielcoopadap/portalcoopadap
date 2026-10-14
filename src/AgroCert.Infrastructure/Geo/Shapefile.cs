using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using AgroCert.Application.Util;

namespace AgroCert.Infrastructure.Geo;

public sealed record FeicaoGeo(Dictionary<string, string> Atributos, List<GeoUtil.Poligono> Poligonos, double[] Caixa);

public static class Shapefile
{
    public static IEnumerable<(string Nome, List<FeicaoGeo> Feicoes, string? Aviso)> Ler(string caminho, double[]? filtro)
    {
        if (caminho.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(caminho);
            var entradas = zip.Entries.ToDictionary(e => e.FullName.ToLowerInvariant(), e => e);
            foreach (var shp in zip.Entries.Where(e => e.FullName.EndsWith(".shp", StringComparison.OrdinalIgnoreCase)).ToList())
            {
                var baseNome = shp.FullName[..^4].ToLowerInvariant();
                byte[]? Bytes(string ext) => entradas.TryGetValue(baseNome + ext, out var e) ? LerTudo(e) : null;
                yield return Camada(Path.GetFileName(caminho) + "/" + Path.GetFileName(shp.FullName), LerTudo(shp), Bytes(".dbf"), Bytes(".prj"), Bytes(".cpg"), filtro);
            }
        }
        else
        {
            var b = caminho[..^4];
            byte[]? Bytes(string ext) => File.Exists(b + ext) ? File.ReadAllBytes(b + ext) : File.Exists(b + ext.ToUpperInvariant()) ? File.ReadAllBytes(b + ext.ToUpperInvariant()) : null;
            yield return Camada(Path.GetFileName(caminho), File.ReadAllBytes(caminho), Bytes(".dbf"), Bytes(".prj"), Bytes(".cpg"), filtro);
        }
    }

    private static byte[] LerTudo(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static (string, List<FeicaoGeo>, string?) Camada(string nome, byte[] shp, byte[]? dbf, byte[]? prj, byte[]? cpg, double[]? filtro)
    {
        string? aviso = null;
        var prjTxt = prj is null ? "" : Encoding.ASCII.GetString(prj);
        if (prjTxt.Contains("PROJCS", StringComparison.OrdinalIgnoreCase))
            return (nome, new List<FeicaoGeo>(), $"{nome}: está em coordenadas projetadas (UTM/Albers). Exporte em SIRGAS 2000 ou WGS84 geográfico (graus).");

        var atributos = dbf is null ? null : LerDbf(dbf, cpg);
        if (atributos is null) aviso = $"{nome}: sem o arquivo .dbf — os nomes das UCs não aparecerão.";

        var feicoes = new List<FeicaoGeo>();
        var p = 100;
        var registro = 0;
        while (p + 8 <= shp.Length)
        {
            var tamanho = BinaryPrimitives.ReadInt32BigEndian(shp.AsSpan(p + 4)) * 2;
            var c = p + 8;
            p = c + tamanho;
            var indice = registro++;
            if (c + 4 > shp.Length) break;
            var tipo = BinaryPrimitives.ReadInt32LittleEndian(shp.AsSpan(c));
            if (tipo is not (5 or 15 or 25)) continue;
            var caixa = new[] { D(shp, c + 4), D(shp, c + 12), D(shp, c + 20), D(shp, c + 28) };
            if (filtro is not null && (caixa[0] > filtro[2] || caixa[2] < filtro[0] || caixa[1] > filtro[3] || caixa[3] < filtro[1])) continue;
            var nPartes = BinaryPrimitives.ReadInt32LittleEndian(shp.AsSpan(c + 36));
            var nPontos = BinaryPrimitives.ReadInt32LittleEndian(shp.AsSpan(c + 40));
            var partes = new int[nPartes + 1];
            for (var i = 0; i < nPartes; i++) partes[i] = BinaryPrimitives.ReadInt32LittleEndian(shp.AsSpan(c + 44 + i * 4));
            partes[nPartes] = nPontos;
            var pp = c + 44 + nPartes * 4;
            var aneis = new List<(double X, double Y)[]>();
            for (var k = 0; k < nPartes; k++)
            {
                var anel = new (double, double)[partes[k + 1] - partes[k]];
                for (var i = 0; i < anel.Length; i++)
                {
                    var o = pp + (partes[k] + i) * 16;
                    anel[i] = (D(shp, o), D(shp, o + 8));
                }
                if (anel.Length >= 3) aneis.Add(anel);
            }
            var attrs = atributos is not null && indice < atributos.Count ? atributos[indice] : new Dictionary<string, string>();
            feicoes.Add(new FeicaoGeo(attrs, Montar(aneis), caixa));
        }
        return (nome, feicoes, aviso);
    }

    private static List<GeoUtil.Poligono> Montar(List<(double X, double Y)[]> aneis)
    {
        var pols = new List<GeoUtil.Poligono>();
        var buracos = new List<(double X, double Y)[]>();
        foreach (var a in aneis)
        {
            double s = 0;
            for (int i = 0, j = a.Length - 1; i < a.Length; j = i++) s += (a[i].X - a[j].X) * (a[i].Y + a[j].Y);
            if (s >= 0) pols.Add(new GeoUtil.Poligono(new List<(double X, double Y)[]> { a }));
            else buracos.Add(a);
        }
        if (pols.Count == 0) return buracos.Select(b => new GeoUtil.Poligono(new List<(double X, double Y)[]> { b })).ToList();
        foreach (var b in buracos)
        {
            var dono = pols.FirstOrDefault(p => GeoUtil.Contem(p, b[0])) ?? pols[^1];
            dono.Aneis.Add(b);
        }
        return pols;
    }

    private static double D(byte[] b, int o) => BinaryPrimitives.ReadDoubleLittleEndian(b.AsSpan(o));

    private static List<Dictionary<string, string>> LerDbf(byte[] b, byte[]? cpg)
    {
        var n = BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(4));
        int tamCab = BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(8));
        int tamReg = BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(10));
        var campos = new List<(string Nome, int Tamanho)>();
        for (var o = 32; o + 32 <= tamCab && b[o] != 0x0D; o += 32)
            campos.Add((Encoding.ASCII.GetString(b, o, 11).TrimEnd('\0', ' '), b[o + 16]));

        var cp = cpg is null ? "" : Encoding.ASCII.GetString(cpg).Trim().ToUpperInvariant();
        Encoding? fixa = cp.Contains("UTF") ? new UTF8Encoding(false, true) : cp.Length > 0 ? Encoding.Latin1 : null;
        var utf8Estrito = new UTF8Encoding(false, true);

        var lista = new List<Dictionary<string, string>>(n);
        for (var r = 0; r < n; r++)
        {
            var o = tamCab + r * tamReg;
            if (o + tamReg > b.Length) break;
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var p = o + 1;
            foreach (var (nome, tam) in campos)
            {
                string v;
                if (fixa is not null)
                {
                    try { v = fixa.GetString(b, p, tam); } catch { v = Encoding.Latin1.GetString(b, p, tam); }
                }
                else
                {
                    try { v = utf8Estrito.GetString(b, p, tam); } catch { v = Encoding.Latin1.GetString(b, p, tam); }
                }
                d[nome] = v.Trim('\0', ' ');
                p += tam;
            }
            lista.Add(d);
        }
        return lista;
    }

    public static List<FeicaoGeo> LerGeoJson(string caminho, double[]? filtro)
    {
        var raiz = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(caminho));
        var lista = new List<FeicaoGeo>();
        var feats = raiz?["features"] as System.Text.Json.Nodes.JsonArray;
        if (feats is null) return lista;
        foreach (var f in feats)
        {
            var geo = f?["geometry"];
            if (geo is null) continue;
            List<GeoUtil.Poligono> pols;
            try { pols = GeoUtil.LerGeoJson(geo.ToJsonString()); } catch { continue; }
            var caixa = GeoUtil.BoundingBox(pols);
            if (caixa is null) continue;
            if (filtro is not null && (caixa[0] > filtro[2] || caixa[2] < filtro[0] || caixa[1] > filtro[3] || caixa[3] < filtro[1])) continue;
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (f?["properties"] is System.Text.Json.Nodes.JsonObject props)
                foreach (var (k, v) in props) attrs[k] = v?.ToString() ?? "";
            lista.Add(new FeicaoGeo(attrs, pols, caixa));
        }
        return lista;
    }
}
