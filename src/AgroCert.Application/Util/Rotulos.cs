using System.Globalization;

namespace AgroCert.Application.Util;

public readonly record struct Estilo(string Bg, string Cor);

public static class Rotulos
{
    public static readonly string[] Prioridades = { "Crítica", "Maior", "Menor", "Recomendado" };
    public static readonly string[] StatusRequisito = { "Conforme", "Em Andamento", "Pendente", "Não Conforme" };
    public static readonly string[] StatusCertificacao = { "Conforme", "Em Andamento", "Pendente" };
    public static readonly string[] Normas = { "Rainforest Alliance", "GlobalG.A.P.", "GRASP", "C.A.F.E. Practices", "SMETA", "ATITUDES" };
    public static readonly string[] GrausRisco = { "CRITICO", "ALTO", "MEDIO", "BAIXO" };

    public static readonly (string Valor, string Rotulo)[] TiposVistoria =
    {
        ("MENSAL_CONSULTORIA", "Mensal — Consultoria"),
        ("DIARIA_FUNCIONARIO", "Diária — Funcionário"),
    };

    public static readonly Dictionary<string, string> SiglaNorma = new()
    {
        ["Rainforest Alliance"] = "RA", ["GlobalG.A.P."] = "GG", ["GRASP"] = "GR",
        ["C.A.F.E. Practices"] = "CP", ["SMETA"] = "SM", ["ATITUDES"] = "AT",
    };

    public static readonly Dictionary<string, string> SiteNorma = new()
    {
        ["Rainforest Alliance"] = "https://knowledge.rainforest-alliance.org/docs/rainforest-alliance-sustainable-agriculture-standard-farm-requirements",
        ["GlobalG.A.P."] = "https://globalgap.org/what-we-offer/solutions/ifa-fruit-and-vegetables/",
        ["GRASP"] = "https://globalgap.org/what-we-offer/solutions/grasp/",
    };

    public static string Sigla(string? norma) =>
        norma != null && SiglaNorma.TryGetValue(norma, out var s) ? s : norma ?? "";

    private static readonly Estilo Neutro = new("#eeeeee", "#555555");

    public static Estilo EstiloPrioridade(string? p) => p switch
    {
        "Crítica" => new("#fdeceb", "#c0392b"),
        "Maior" => new("#fbeedd", "#bd7d15"),
        "Menor" => new("#eef1f4", "#5b6673"),
        "Recomendado" => new("#e6f4f1", "#2a7f6f"),
        _ => Neutro,
    };

    public static Estilo EstiloStatusRequisito(string? s) => s switch
    {
        "Conforme" => new("#e4f3ea", "#1f8a4c"),
        "Em Andamento" => new("#e5eef8", "#1f6fb2"),
        "Pendente" => new("#fdf6da", "#b8860b"),
        "Não Conforme" => new("#fdeceb", "#c0392b"),
        _ => Neutro,
    };

    public static Estilo EstiloStatusCertificacao(string? s) => s switch
    {
        "Conforme" => new("#e4f3ea", "#1f8a4c"),
        "Em Andamento" => new("#e5eef8", "#1f6fb2"),
        "Pendente" => new("#fbeedd", "#bd7d15"),
        _ => Neutro,
    };

    public static Estilo EstiloGrauRisco(string? g) => g switch
    {
        "CRITICO" => new("#fdeceb", "#c0392b"),
        "ALTO" => new("#fdf6da", "#b8860b"),
        "MEDIO" => new("#e9f1fb", "#1f6fb2"),
        "BAIXO" => new("#e4f3ea", "#1f8a4c"),
        _ => Neutro,
    };

    public static string RotuloGrauRisco(string? g) => g switch
    {
        "CRITICO" => "Crítico",
        "ALTO" => "Alto",
        "MEDIO" => "Médio",
        "BAIXO" => "Baixo",
        _ => g ?? "-",
    };

    public static Estilo EstiloStatusApontamento(string? s) =>
        s == "ENCERRADO" ? new("#e4f3ea", "#1f8a4c") : new("#fbeedd", "#bd7d15");

    public static string RotuloStatusApontamento(string? s) => s switch
    {
        "ABERTO" => "Em aberto",
        "ENCERRADO" => "Encerrado",
        _ => s ?? "-",
    };

    public static string RotuloTipoVistoria(string? tipo) =>
        TiposVistoria.FirstOrDefault(t => t.Valor == tipo).Rotulo ?? tipo ?? "-";

    public static string RotuloTipoVistoriaLongo(string? tipo) => tipo switch
    {
        "MENSAL_CONSULTORIA" => "Vistoria Mensal (Consultoria)",
        "DIARIA_FUNCIONARIO" => "Vistoria Diária (Funcionário)",
        _ => tipo ?? "-",
    };

    public static string RotuloStatusAlerta(string? s) => s switch
    {
        "NECESSITA_VERIFICACAO" => "Necessita verificação",
        "VERIFICADO_MANEJO" => "Verificado — manejo autorizado",
        "VERIFICADO_IRREGULAR" => "Verificado — irregular",
        "DESCARTADO" => "Descartado",
        _ => s ?? "-",
    };

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] MesesAbrev = { "jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez" };

    public static string Data(DateOnly? d) => d.HasValue ? d.Value.ToString("dd/MM/yyyy", PtBr) : "—";

    public static string DataCurta(DateOnly? d) => d.HasValue ? $"{d.Value.Day} de {MesesAbrev[d.Value.Month - 1]}." : "";

    public static string MesAno(DateOnly d) => $"{MesesAbrev[d.Month - 1]}/{d.Year % 100:00}";

    public static string Numero(decimal? v, int casas = 2) =>
        v.HasValue ? v.Value.ToString("N" + casas, PtBr) : "-";

    public static (string Rotulo, string Cor) ClassificarNdvi(decimal? ndvi)
    {
        if (ndvi is null) return ("-", "#9a9384");
        if (ndvi < 0.2m) return ("Solo exposto / água", "#b0392b");
        if (ndvi < 0.5m) return ("Vegetação esparsa", "#b8860b");
        if (ndvi < 0.8m) return ("Vegetação moderada", "#2f7a54");
        return ("Vegetação densa", "#15533a");
    }
}
