using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ProcessarCorredores;

public static class Xlsx
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace NsR = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace NsRel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static Dictionary<string, List<string[]>> Ler(string caminho)
    {
        using var zip = ZipFile.OpenRead(caminho);
        XDocument Xml(string nome)
        {
            var e = zip.GetEntry(nome) ?? throw new InvalidDataException($"xlsx sem {nome}");
            using var s = e.Open();
            return XDocument.Load(s);
        }

        var textos = new List<string>();
        if (zip.GetEntry("xl/sharedStrings.xml") is not null)
            foreach (var si in Xml("xl/sharedStrings.xml").Root!.Elements(Ns + "si"))
                textos.Add(string.Concat(si.Descendants(Ns + "t").Where(t => t.Parent!.Name != Ns + "rPh").Select(t => t.Value)));

        var rels = Xml("xl/_rels/workbook.xml.rels").Root!.Elements(NsRel + "Relationship")
            .ToDictionary(r => (string)r.Attribute("Id")!, r => (string)r.Attribute("Target")!);

        var r = new Dictionary<string, List<string[]>>(StringComparer.OrdinalIgnoreCase);
        foreach (var sh in Xml("xl/workbook.xml").Root!.Element(Ns + "sheets")!.Elements(Ns + "sheet"))
        {
            var nome = (string)sh.Attribute("name")!;
            var alvo = rels[(string)sh.Attribute(NsR + "id")!].TrimStart('/');
            if (!alvo.StartsWith("xl/")) alvo = "xl/" + alvo;
            var linhas = new List<string[]>();
            foreach (var row in Xml(alvo).Descendants(Ns + "row"))
            {
                var cel = new SortedDictionary<int, string>();
                foreach (var c in row.Elements(Ns + "c"))
                {
                    var col = Coluna((string?)c.Attribute("r") ?? "");
                    var tipo = (string?)c.Attribute("t");
                    string valor = tipo switch
                    {
                        "s" => int.TryParse(c.Element(Ns + "v")?.Value, out var i) && i < textos.Count ? textos[i] : "",
                        "inlineStr" => string.Concat(c.Descendants(Ns + "t").Select(t => t.Value)),
                        _ => c.Element(Ns + "v")?.Value ?? "",
                    };
                    if (col >= 0) cel[col] = valor;
                }
                var arr = new string[cel.Count == 0 ? 0 : cel.Keys.Max() + 1];
                for (var i = 0; i < arr.Length; i++) arr[i] = cel.GetValueOrDefault(i, "");
                linhas.Add(arr);
            }
            r[nome] = linhas;
        }
        return r;
    }

    private static int Coluna(string refCel)
    {
        var n = 0; var achou = false;
        foreach (var ch in refCel)
        {
            if (ch is >= 'A' and <= 'Z') { n = n * 26 + (ch - 'A' + 1); achou = true; }
            else break;
        }
        return achou ? n - 1 : -1;
    }
}

public sealed record Cooperado(string Car, List<string> Nomes, string Municipio);

public static class Cooperados
{
    private static readonly Regex Car = new(@"\b[A-Z]{2}-\d{7}-[0-9A-F]{32}\b", RegexOptions.IgnoreCase);

    public static Dictionary<string, Cooperado> Ler(string caminho)
    {
        var planilhas = Xlsx.Ler(caminho);
        var r = new Dictionary<string, Cooperado>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, linhas) in planilhas.OrderByDescending(p => p.Key.Contains("Clientes", StringComparison.OrdinalIgnoreCase)))
        {
            for (var h = 0; h < Math.Min(linhas.Count, 10); h++)
            {
                var cab = linhas[h].Select(c => c.Trim().ToLowerInvariant()).ToList();
                var iCar = cab.FindIndex(c => c == "car usado");
                if (iCar < 0) iCar = cab.FindIndex(c => c.StartsWith("car"));
                var iNome = cab.FindIndex(c => c == "nome" || c.StartsWith("nome") || c == "produtor");
                if (iCar < 0 || iNome < 0) continue;
                var iMun = cab.FindIndex(c => c.StartsWith("município do car") || c.StartsWith("municipio do car"));
                var iSit = cab.FindIndex(c => c.StartsWith("situa"));
                for (var i = h + 1; i < linhas.Count; i++)
                {
                    var l = linhas[i];
                    string C(int k) => k >= 0 && k < l.Length ? l[k].Trim() : "";
                    var m = Car.Match(C(iCar));
                    if (!m.Success) continue;
                    if (iSit >= 0 && C(iSit).Length > 0 && !C(iSit).StartsWith("Encontrado", StringComparison.OrdinalIgnoreCase)) continue;
                    Adicionar(r, m.Value.ToUpperInvariant(), C(iNome), C(iMun));
                }
                if (r.Count > 0) return r;
            }
        }

        foreach (var (_, linhas) in planilhas)
            foreach (var l in linhas)
                foreach (var c in l)
                    foreach (Match m in Car.Matches(c))
                        Adicionar(r, m.Value.ToUpperInvariant(), "", "");
        return r;
    }

    private static void Adicionar(Dictionary<string, Cooperado> r, string car, string nome, string mun)
    {
        if (!r.TryGetValue(car, out var c)) r[car] = c = new Cooperado(car, new List<string>(), mun);
        nome = Regex.Replace(nome, @"\s+", " ").Trim();
        if (nome.Length > 0 && !c.Nomes.Contains(nome, StringComparer.OrdinalIgnoreCase)) c.Nomes.Add(nome);
    }
}
