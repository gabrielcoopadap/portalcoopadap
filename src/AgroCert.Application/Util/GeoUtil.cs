using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Application.Util;

public static class GeoUtil
{
    public sealed record Poligono(List<(double X, double Y)[]> Aneis);

    public static List<Poligono> LerGeoJson(string texto)
    {
        JsonNode? raiz;
        try { raiz = JsonNode.Parse(texto); }
        catch (Exception e) { throw new RegraNegocioException("Não foi possível interpretar o GeoJSON: " + e.Message); }
        if (raiz is not JsonObject obj) throw new RegraNegocioException("O GeoJSON informado não é um objeto JSON.");

        var geometrias = new List<JsonObject>();
        switch ((string?)obj["type"])
        {
            case "FeatureCollection":
                if (obj["features"] is not JsonArray features || features.Count == 0)
                    throw new RegraNegocioException("O FeatureCollection colado não tem nenhum imóvel (features).");
                foreach (var f in features)
                    if (f?["geometry"] is JsonObject g) geometrias.Add(g);
                break;
            case "Feature":
                if (obj["geometry"] is JsonObject g1) geometrias.Add(g1);
                break;
            default:
                geometrias.Add(obj);
                break;
        }

        if (geometrias.Count == 0)
            throw new RegraNegocioException("Não foi encontrada nenhuma geometria válida no GeoJSON.");

        var poligonos = new List<Poligono>();
        foreach (var g in geometrias) AchatarEmPoligonos(g, poligonos);
        return poligonos;
    }

    private static void AchatarEmPoligonos(JsonObject geom, List<Poligono> destino)
    {
        switch ((string?)geom["type"])
        {
            case "Polygon":
                destino.Add(LerPoligono(geom["coordinates"] as JsonArray));
                break;
            case "MultiPolygon":
                if (geom["coordinates"] is JsonArray polys)
                    foreach (var p in polys) destino.Add(LerPoligono(p as JsonArray));
                break;
            case "GeometryCollection":
                if (geom["geometries"] is JsonArray gs)
                    foreach (var g in gs) if (g is JsonObject o) AchatarEmPoligonos(o, destino);
                break;
        }
    }

    private static Poligono LerPoligono(JsonArray? aneis)
    {
        if (aneis is null || aneis.Count == 0) throw new RegraNegocioException("Polígono sem coordenadas no GeoJSON.");
        var lista = new List<(double, double)[]>();
        foreach (var anel in aneis)
        {
            if (anel is not JsonArray pontos) continue;
            lista.Add(pontos.Select(p => ((double)p![0]!, (double)p[1]!)).ToArray());
        }
        return new Poligono(lista);
    }

    public static List<Poligono> LerWkt(string wkt)
    {
        var t = wkt.Trim();
        var i = t.IndexOf('(');
        if (i < 0) throw new FormatException("WKT sem coordenadas.");
        var tipo = t[..i].Trim().ToUpperInvariant();
        // remove "SRID=4326;" se vier em EWKT
        if (tipo.Contains(';')) tipo = tipo[(tipo.LastIndexOf(';') + 1)..].Trim();
        var corpo = t[i..];

        var pos = 0;
        var arvore = LerParenteses(corpo, ref pos);
        return tipo switch
        {
            "POLYGON" => new List<Poligono> { ParaPoligono(arvore) },
            "MULTIPOLYGON" => arvore.Filhos.Select(ParaPoligono).ToList(),
            _ => throw new FormatException("Tipo WKT não suportado: " + tipo),
        };
    }

    private sealed class NoWkt
    {
        public List<NoWkt> Filhos { get; } = new();
        public string? Texto { get; set; }
    }

    private static NoWkt LerParenteses(string s, ref int pos)
    {
        var no = new NoWkt();
        pos++;
        var buffer = new StringBuilder();
        while (pos < s.Length)
        {
            var c = s[pos];
            if (c == '(') { no.Filhos.Add(LerParenteses(s, ref pos)); continue; }
            if (c == ')') { pos++; break; }
            buffer.Append(c);
            pos++;
        }
        if (no.Filhos.Count == 0) no.Texto = buffer.ToString();
        return no;
    }

    private static Poligono ParaPoligono(NoWkt no) =>
        new(no.Filhos.Select(anel => anel.Texto!
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(par =>
            {
                var xy = par.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return (double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture));
            }).ToArray()).ToList());

    public static string EscreverMultiPolygon(IEnumerable<Poligono> poligonos)
    {
        var coords = new JsonArray();
        foreach (var p in poligonos)
        {
            var aneis = new JsonArray();
            foreach (var anel in p.Aneis)
            {
                var pts = new JsonArray();
                foreach (var (x, y) in anel) pts.Add(new JsonArray(x, y));
                aneis.Add(pts);
            }
            coords.Add(aneis);
        }
        return new JsonObject { ["type"] = "MultiPolygon", ["coordinates"] = coords }.ToJsonString();
    }

    public static string NormalizarParaMultiPolygon(string geoJson)
    {
        var poligonos = LerGeoJson(geoJson);
        if (poligonos.Count == 0)
            throw new RegraNegocioException("O GeoJSON precisa conter ao menos um Polygon ou MultiPolygon.");
        return EscreverMultiPolygon(poligonos);
    }

    public static double[]? BoundingBox(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return null;
        try { return BoundingBox(LerGeoJson(geoJson)); }
        catch { return null; }
    }

    public static double[]? BoundingBox(IEnumerable<Poligono> poligonos)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        var algum = false;
        foreach (var p in poligonos)
            foreach (var anel in p.Aneis)
                foreach (var (x, y) in anel)
                {
                    algum = true;
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
        return algum ? new[] { minX, minY, maxX, maxY } : null;
    }

    public static bool Intersecta(string? geoJsonA, string? geoJsonB)
    {
        if (string.IsNullOrWhiteSpace(geoJsonA) || string.IsNullOrWhiteSpace(geoJsonB)) return false;
        try { return Intersecta(LerGeoJson(geoJsonA), LerGeoJson(geoJsonB)); }
        catch { return false; }
    }

    public static bool Intersecta(List<Poligono> a, List<Poligono> b)
    {
        var ba = BoundingBox(a); var bb = BoundingBox(b);
        if (ba is null || bb is null) return false;
        if (ba[2] < bb[0] || bb[2] < ba[0] || ba[3] < bb[1] || bb[3] < ba[1]) return false;

        foreach (var pa in a)
            foreach (var pb in b)
                if (PoligonosIntersectam(pa, pb)) return true;
        return false;
    }

    private static bool PoligonosIntersectam(Poligono a, Poligono b)
    {
        foreach (var anelA in a.Aneis)
            foreach (var anelB in b.Aneis)
                for (var i = 0; i < anelA.Length - 1; i++)
                    for (var j = 0; j < anelB.Length - 1; j++)
                        if (SegmentosCruzam(anelA[i], anelA[i + 1], anelB[j], anelB[j + 1])) return true;

        if (a.Aneis.Count > 0 && a.Aneis[0].Length > 0 && Contem(b, a.Aneis[0][0])) return true;
        if (b.Aneis.Count > 0 && b.Aneis[0].Length > 0 && Contem(a, b.Aneis[0][0])) return true;
        return false;
    }

    public static bool Contem(Poligono p, (double X, double Y) pt)
    {
        if (p.Aneis.Count == 0 || !DentroDoAnel(p.Aneis[0], pt)) return false;
        for (var i = 1; i < p.Aneis.Count; i++)
            if (DentroDoAnel(p.Aneis[i], pt)) return false;
        return true;
    }

    private static bool DentroDoAnel((double X, double Y)[] anel, (double X, double Y) pt)
    {
        var dentro = false;
        for (int i = 0, j = anel.Length - 1; i < anel.Length; j = i++)
        {
            var (xi, yi) = anel[i];
            var (xj, yj) = anel[j];
            if ((yi > pt.Y) != (yj > pt.Y) && pt.X < (xj - xi) * (pt.Y - yi) / (yj - yi) + xi)
                dentro = !dentro;
        }
        return dentro;
    }

    private static bool SegmentosCruzam((double X, double Y) p1, (double X, double Y) p2, (double X, double Y) p3, (double X, double Y) p4)
    {
        static double Orient((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        static bool NoSegmento((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) =>
            Math.Min(a.X, b.X) <= c.X && c.X <= Math.Max(a.X, b.X) && Math.Min(a.Y, b.Y) <= c.Y && c.Y <= Math.Max(a.Y, b.Y);

        double d1 = Orient(p3, p4, p1), d2 = Orient(p3, p4, p2), d3 = Orient(p1, p2, p3), d4 = Orient(p1, p2, p4);
        if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return true;
        if (d1 == 0 && NoSegmento(p3, p4, p1)) return true;
        if (d2 == 0 && NoSegmento(p3, p4, p2)) return true;
        if (d3 == 0 && NoSegmento(p1, p2, p3)) return true;
        if (d4 == 0 && NoSegmento(p1, p2, p4)) return true;
        return false;
    }

    public static decimal? AreaHectares(string? geoJson)
    {
        if (string.IsNullOrWhiteSpace(geoJson)) return null;
        try
        {
            var poligonos = LerGeoJson(geoJson);
            var bbox = BoundingBox(poligonos);
            if (bbox is null) return null;
            var lat0 = (bbox[1] + bbox[3]) / 2 * Math.PI / 180;
            const double mPorGrauLat = 110_574.0;
            var mPorGrauLon = 111_320.0 * Math.Cos(lat0);
            double total = 0;
            foreach (var p in poligonos)
                for (var i = 0; i < p.Aneis.Count; i++)
                {
                    var anel = p.Aneis[i];
                    double soma = 0;
                    for (int k = 0, j = anel.Length - 1; k < anel.Length; j = k++)
                        soma += (anel[j].X * mPorGrauLon) * (anel[k].Y * mPorGrauLat) - (anel[k].X * mPorGrauLon) * (anel[j].Y * mPorGrauLat);
                    var area = Math.Abs(soma) / 2;
                    total += i == 0 ? area : -area;
                }
            return Math.Round((decimal)(total / 10_000), 2);
        }
        catch { return null; }
    }

    public static (double Lat, double Lon)? Centro(string? geoJson)
    {
        var bbox = BoundingBox(geoJson);
        return bbox is null ? null : ((bbox[1] + bbox[3]) / 2, (bbox[0] + bbox[2]) / 2);
    }
}
