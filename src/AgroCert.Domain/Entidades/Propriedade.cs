namespace AgroCert.Domain.Entidades;

public class Propriedade
{
    public long Id { get; set; }
    public string Nome { get; set; } = "";
    public string CarCodigo { get; set; } = "";
    public decimal? AreaHa { get; set; }
    public decimal? AreaReservaLegalHa { get; set; }
    public decimal? AreaAppHa { get; set; }
    public decimal? AreaVegetacaoNativaHa { get; set; }
    public string? Municipio { get; set; }
    public string? Uf { get; set; }

    public string? GeoJson { get; set; }
    public string? GeoJsonReservaLegal { get; set; }
    public string? GeoJsonApp { get; set; }

    public string FonteGeometria { get; set; } = "SICAR";
    public DateTime AtualizadoEm { get; set; } = DateTime.Now;
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public List<AlertaDesmatamento> Alertas { get; set; } = new();
    public List<LeituraNdvi> LeiturasNdvi { get; set; } = new();
}
