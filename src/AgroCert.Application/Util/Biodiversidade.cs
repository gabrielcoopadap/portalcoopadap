using AgroCert.Domain.Enums;

namespace AgroCert.Application.Util;

public sealed record GrupoBio(string Nome, string Plural, string Cor, string Icone);

public static class Biodiversidade
{
    public const string CorFauna = "#5e9a42";
    public const string CorFlora = "#2a78d6";
    public const string CorOutros = "#9aa0a6";

    public static readonly GrupoBio[] GruposFauna =
    {
        new("Ave", "Aves", "#2a78d6", "flutter_dash"),
        new("Mamífero", "Mamíferos", "#eb6834", "pets"),
        new("Réptil", "Répteis", "#1baf7a", "egg"),
        new("Anfíbio", "Anfíbios", "#eda100", "water_drop"),
        new("Inseto", "Insetos", "#e87ba4", "bug_report"),
        new("Peixe", "Peixes", "#4a3aa7", "set_meal"),
        new("Outro", "Outros", CorOutros, "cruelty_free"),
    };

    public static readonly GrupoBio[] GruposFlora =
    {
        new("Árvore", "Árvores", "#008300", "park"),
        new("Palmeira", "Palmeiras", "#1baf7a", "nature"),
        new("Arbusto", "Arbustos", "#eda100", "grass"),
        new("Herbácea", "Herbáceas e flores", "#e87ba4", "local_florist"),
        new("Outro", "Outros", CorOutros, "eco"),
    };

    public static GrupoBio[] Grupos(Reino reino) => reino == Reino.FAUNA ? GruposFauna : GruposFlora;

    public static GrupoBio Grupo(Reino reino, string? nome) =>
        Grupos(reino).FirstOrDefault(g => g.Nome == nome) ?? Grupos(reino)[^1];

    public static readonly string[] Areas = { "Reserva Legal", "APP", "Área de conservação", "Lavoura", "Estrada / acesso", "Outra" };

    public static readonly (string Codigo, string Rotulo)[] StatusConservacao =
    {
        ("LC", "Pouco preocupante"), ("NT", "Quase ameaçada"), ("VU", "Vulnerável"),
        ("EN", "Em perigo"), ("CR", "Criticamente em perigo"), ("NA", "Não avaliada"),
    };

    public static string RotuloStatus(string? codigo) =>
        StatusConservacao.FirstOrDefault(s => s.Codigo == codigo).Rotulo ?? "Não avaliada";

    public static bool Ameacada(string? codigo) => codigo is "VU" or "EN" or "CR";

    public static string RotuloReino(Reino r) => r == Reino.FAUNA ? "Fauna" : "Flora";

    public static string RotuloOrigem(OrigemRegistro o) => o switch
    {
        OrigemRegistro.CAMERA => "Câmera de monitoramento",
        OrigemRegistro.AVISTAMENTO => "Avistamento",
        OrigemRegistro.VESTIGIO => "Vestígio (pegada, fezes...)",
        OrigemRegistro.COLETA => "Registro de flora / coleta",
        _ => o.ToString(),
    };

    public static string ResumoOrigem(OrigemRegistro o, Reino r) => o switch
    {
        OrigemRegistro.CAMERA => "Registrada por câmera",
        OrigemRegistro.AVISTAMENTO => "Avistamento",
        OrigemRegistro.VESTIGIO => "Vestígio",
        _ => r == Reino.FLORA ? "Registro de flora" : "Coleta",
    };
}
