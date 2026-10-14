using AgroCert.Domain.Enums;

namespace AgroCert.Domain.Entidades;

public class Apontamento
{
    public long Id { get; set; }

    public long VistoriaId { get; set; }
    public Vistoria Vistoria { get; set; } = null!;

    public long? RequisitoId { get; set; }
    public Requisito? Requisito { get; set; }

    public string Anomalia { get; set; } = "";
    public string? Localizacao { get; set; }
    public GrauRisco GrauRisco { get; set; }
    public DateOnly? Prazo { get; set; }
    public string? ResponsavelExecucao { get; set; }
    public string? SituacaoEncontrada { get; set; }
    public string? SituacaoRequerida { get; set; }
    public string? RiscoAssociado { get; set; }
    public string? ImagemEncontradaUrl { get; set; }
    public string? ImagemRequeridaUrl { get; set; }

    public StatusApontamento Status { get; set; } = StatusApontamento.ABERTO;
    public DateOnly? DataEncerramento { get; set; }
    public string? ImagemEvidenciaEncerramentoUrl { get; set; }
    public string? ObservacoesEncerramento { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
