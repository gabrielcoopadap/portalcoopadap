using System.ComponentModel.DataAnnotations;
using AgroCert.Domain.Entidades;

namespace AgroCert.Application.Dtos;

public class CadastrarPropriedadeRequest
{
    [Required(ErrorMessage = "Informe o nome da propriedade.")]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe o código do CAR.")]
    public string CarCodigo { get; set; } = "";

    public string? GeoJson { get; set; }
}

public record PropriedadeDto(
    long Id, string Nome, string CarCodigo,
    decimal? AreaHa, decimal? AreaReservaLegalHa, decimal? AreaAppHa, decimal? AreaVegetacaoNativaHa,
    string? Municipio, string? Uf,
    string? GeoJson, string? GeoJsonReservaLegal, string? GeoJsonApp)
{
    public static PropriedadeDto De(Propriedade p) => new(
        p.Id, p.Nome, p.CarCodigo, p.AreaHa, p.AreaReservaLegalHa, p.AreaAppHa, p.AreaVegetacaoNativaHa,
        p.Municipio, p.Uf, p.GeoJson, p.GeoJsonReservaLegal, p.GeoJsonApp);
}

public record AlertaDto(
    long Id, string? CodigoAlerta, string Fonte, DateOnly? DataDeteccao, decimal? AreaHa,
    string? ClasseDesmatamento, bool? DentroReservaLegal, bool? DentroApp, string? GeoJson,
    string? ImagemAntesUrl, string? ImagemDepoisUrl, string Status)
{
    public static AlertaDto De(AlertaDesmatamento a) => new(
        a.Id, a.CodigoAlerta, a.Fonte, a.DataDeteccao, a.AreaHa, a.ClasseDesmatamento,
        a.DentroReservaLegal, a.DentroApp, a.GeoJson, a.ImagemAntesUrl, a.ImagemDepoisUrl, a.Status.ToString());
}

public record LeituraNdviDto(
    long Id, DateOnly DataImagem, decimal? NdviMedio, decimal? NdviMinimo, decimal? NdviMaximo,
    decimal? PercentualAmostrasValidas, int? Amostras, string? ImagemCorRealUrl, string? ImagemMapaCalorUrl,
    double? BboxMinLon, double? BboxMinLat, double? BboxMaxLon, double? BboxMaxLat)
{
    public static LeituraNdviDto De(LeituraNdvi l, double[]? bbox) => new(
        l.Id, l.DataImagem, l.NdviMedio, l.NdviMinimo, l.NdviMaximo, l.PercentualAmostrasValidas, l.Amostras,
        l.ImagemCorRealUrl, l.ImagemMapaCalorUrl,
        bbox?[0], bbox?[1], bbox?[2], bbox?[3]);
}

public record RelatorioAmbientalDto(
    long PropriedadeId, string PropriedadeNome, string CarCodigo,
    decimal? AreaHa, decimal? AreaReservaLegalHa, decimal? AreaAppHa, decimal? AreaVegetacaoNativaHa,
    string? GeoJson, string? GeoJsonReservaLegal, string? GeoJsonApp,
    int TotalAlertas, decimal AreaTotalAlertasHa, DateOnly? UltimoAlerta, string Situacao,
    List<AlertaDto> Alertas, List<string> FontesConsultadas, string Aviso)
{
    public const string AvisoPadrao =
        "Este documento é um relatório de monitoramento ambiental baseado nas fontes " +
        "de dados públicas consultadas (CAR/SICAR, MapBiomas Alerta e, quando aplicável, " +
        "Sentinel-2). Não constitui certificação ambiental nem conclusão jurídica. " +
        "Alertas indicam necessidade de verificação em campo antes de qualquer decisão.";
}

public class DadosImovelCar
{
    public decimal? AreaHa { get; set; }
    public decimal? AreaReservaLegalHa { get; set; }
    public decimal? AreaAppHa { get; set; }
    public decimal? AreaVegetacaoNativaHa { get; set; }
    public string? Municipio { get; set; }
    public string? Uf { get; set; }
    public string? GeoJson { get; set; }
    public string? GeoJsonReservaLegal { get; set; }
    public string? GeoJsonApp { get; set; }
}

public class AlertaExterno
{
    public string? CodigoAlerta { get; set; }
    public string Fonte { get; set; } = "MAPBIOMAS_ALERTA";
    public DateOnly? DataDeteccao { get; set; }
    public decimal? AreaHa { get; set; }
    public string? Classe { get; set; }
    public string? ImagemAntesUrl { get; set; }
    public string? ImagemDepoisUrl { get; set; }
    public string? GeoJson { get; set; }
}

public class PontoNdvi
{
    public DateOnly DataInicio { get; set; }
    public decimal? NdviMedio { get; set; }
    public decimal? NdviMinimo { get; set; }
    public decimal? NdviMaximo { get; set; }
    public decimal? PercentualAmostrasValidas { get; set; }
    public int? Amostras { get; set; }
}
