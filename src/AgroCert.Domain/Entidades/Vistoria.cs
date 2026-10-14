using AgroCert.Domain.Enums;

namespace AgroCert.Domain.Entidades;

public class Vistoria
{
    public long Id { get; set; }
    public string CodigoDocumento { get; set; } = "";
    public string UnidadePropriedade { get; set; } = "";
    public string? SetorArea { get; set; }
    public DateOnly DataVistoria { get; set; }
    public TipoVistoria Tipo { get; set; }
    public string? TecnicoResponsavel { get; set; }
    public string? CargoTecnico { get; set; }
    public string? Motivo { get; set; }
    public string? AprovadoPor { get; set; }
    public string? CargoAprovador { get; set; }

    public string? UsuarioId { get; set; }
    public string? UsuarioNome { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    public List<Apontamento> Apontamentos { get; set; } = new();
}
