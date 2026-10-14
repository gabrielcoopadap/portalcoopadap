using System.Globalization;

namespace AgroCert.Certificacao.Clima;

public static class ClimaFormato
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly string[] Meses =
        { "janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro" };

    public static string NomeMes(int mes) => mes is >= 1 and <= 12 ? PtBr.TextInfo.ToTitleCase(Meses[mes - 1]) : mes.ToString();
    public static string MesCurto(int mes) => mes is >= 1 and <= 12 ? PtBr.TextInfo.ToTitleCase(Meses[mes - 1][..3]) : mes.ToString();
    public static string MesAno(int mes, int ano) => $"{MesCurto(mes)}/{ano % 100:00}";

    public static string N(double v, int casas = 1) => v.ToString("N" + casas, PtBr);

    public static string S(double v, int casas = 1)
    {
        var r = Math.Round(v, casas);
        if (r == 0) return N(0, casas);
        return (r > 0 ? "+" : "−") + N(Math.Abs(r), casas);
    }

    public static string Pct(double fracao) => Math.Round(fracao * 100).ToString("0", PtBr) + "%";

    public static string F(double v) => v.ToString("0.##", Inv);

    public static string NomeFase(string? fase) => fase switch
    {
        "NINO" => "El Niño",
        "NINA" => "La Niña",
        _ => "Neutro",
    };

    public static string ClasseFase(string? fase) => fase switch
    {
        "NINO" => "cl-fase--nino",
        "NINA" => "cl-fase--nina",
        _ => "cl-fase--neutro",
    };

    public static string RotuloOrigem(string? origem) => origem switch
    {
        "observado" => "ONI observado",
        "informado" => "ONI informado (previsão oficial)",
        "modelo" => "ONI projetado pelo modelo",
        "climatologia" => "Sem sinal de ENSO (além de 24 meses)",
        _ => origem ?? "",
    };

    public static (string Icone, string Rotulo) Risco(string tipo) => tipo switch
    {
        "SECA" => ("wb_sunny", "Chuva abaixo do normal"),
        "EXCESSO_CHUVA" => ("thunderstorm", "Chuva acima do normal"),
        "CALOR" => ("device_thermostat", "Calor acima do normal"),
        "FRIO" => ("ac_unit", "Frio / geada"),
        "UMIDADE_BAIXA" => ("water_drop", "Umidade baixa"),
        _ => ("info", tipo),
    };

    public static string NomeVariavel(string v) => v switch
    {
        "chuva" => "Chuva",
        "maxima" => "Temp. máxima",
        "minima" => "Temp. mínima",
        "temp_ambiente" => "Temp. média",
        "umidade_relativa" => "Umidade relativa",
        _ => v,
    };

    public static string Unidade(string v) => v switch
    {
        "chuva" => "mm",
        "umidade_relativa" => "%",
        _ => "°C",
    };

    public static string NomeProblema(string tipo) => tipo switch
    {
        "ENSO_INCONSISTENTE" => "ENSO gravado errado",
        "MES_FALTANDO" => "Mês faltando",
        "DUPLICADO" => "Registro duplicado",
        "FORA_DO_PADRAO" => "Valor fora do padrão",
        "VALOR_IMPOSSIVEL" => "Valor impossível",
        _ => tipo,
    };

    private static readonly string[] Azul = { "#dbe8f9", "#b7d3f6", "#86b6ef", "#5598e7" };
    private static readonly string[] Vermelho = { "#f9dcdb", "#f4bcbb", "#ec8f8e", "#e56a69" };
    public const string Neutro = "#f0efec";

    public static string CorDivergente(double valor, double escala, bool quente)
    {
        var a = Math.Abs(valor) / (escala <= 0 ? 1 : escala);
        if (a < 0.12) return Neutro;
        var i = a < 0.35 ? 0 : a < 0.6 ? 1 : a < 0.85 ? 2 : 3;
        return quente ? Vermelho[i] : Azul[i];
    }
}
