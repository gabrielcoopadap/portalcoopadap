namespace AgroCert.Domain.Entidades;

public class LeituraNdvi
{
    public long Id { get; set; }
    public long PropriedadeId { get; set; }
    public Propriedade Propriedade { get; set; } = null!;

    public DateOnly DataImagem { get; set; }
    public decimal? NdviMedio { get; set; }
    public decimal? NdviMinimo { get; set; }
    public decimal? NdviMaximo { get; set; }
    public decimal? PercentualAmostrasValidas { get; set; }
    public int? Amostras { get; set; }
    public string? ImagemCorRealUrl { get; set; }
    public string? ImagemMapaCalorUrl { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
