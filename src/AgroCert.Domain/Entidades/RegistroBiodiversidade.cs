using AgroCert.Domain.Enums;

namespace AgroCert.Domain.Entidades;

public class RegistroBiodiversidade
{
    public long Id { get; set; }
    public long EspecieId { get; set; }
    public Especie Especie { get; set; } = null!;
    public long? PropriedadeId { get; set; }
    public Propriedade? Propriedade { get; set; }

    public DateTime DataHora { get; set; }
    public OrigemRegistro Origem { get; set; }
    public string? Camera { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Area { get; set; }
    public int Quantidade { get; set; } = 1;
    public string? Observacao { get; set; }
    public string? FotoUrl { get; set; }
    public string? RegistradoPor { get; set; }
    public bool Confirmado { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
