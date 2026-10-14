using AgroCert.Domain.Enums;

namespace AgroCert.Domain.Entidades;

public class Especie
{
    public long Id { get; set; }
    public string NomePopular { get; set; } = "";
    public string? NomeCientifico { get; set; }
    public Reino Reino { get; set; }
    public string Grupo { get; set; } = "";
    public string? StatusConservacao { get; set; }
    public string? Descricao { get; set; }
    public string? FotoUrl { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public List<RegistroBiodiversidade> Registros { get; set; } = new();
}
