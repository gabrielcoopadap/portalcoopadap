using AgroCert.Domain.Enums;

namespace AgroCert.Domain.Entidades;

public class AlertaDesmatamento
{
    public long Id { get; set; }
    public long PropriedadeId { get; set; }
    public Propriedade Propriedade { get; set; } = null!;

    public string? CodigoAlerta { get; set; }
    public string Fonte { get; set; } = "MAPBIOMAS_ALERTA";
    public DateOnly? DataDeteccao { get; set; }
    public decimal? AreaHa { get; set; }
    public string? ClasseDesmatamento { get; set; }
    public bool? DentroReservaLegal { get; set; }
    public bool? DentroApp { get; set; }

    public string? GeoJson { get; set; }

    public string? ImagemAntesUrl { get; set; }
    public string? ImagemDepoisUrl { get; set; }
    public StatusAlerta Status { get; set; } = StatusAlerta.NECESSITA_VERIFICACAO;
    public string? Observacoes { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
