using System.ComponentModel.DataAnnotations;
using AgroCert.Domain.Entidades;
using AgroCert.Domain.Enums;

namespace AgroCert.Application.Dtos;

public class CriarVistoriaRequest
{
    [Required(ErrorMessage = "Informe a unidade/propriedade.")]
    public string UnidadePropriedade { get; set; } = "";
    public string? SetorArea { get; set; }
    [Required(ErrorMessage = "Informe a data da vistoria.")]
    public DateOnly? DataVistoria { get; set; }
    [Required(ErrorMessage = "Informe se é vistoria mensal (consultoria) ou diária (funcionário).")]
    public string Tipo { get; set; } = "";
    public string? TecnicoResponsavel { get; set; }
    public string? CargoTecnico { get; set; }
    public string? Motivo { get; set; }
    public string? AprovadoPor { get; set; }
    public string? CargoAprovador { get; set; }
    public string? UsuarioId { get; set; }
    public string? UsuarioNome { get; set; }
}

public class NovoApontamentoRequest
{
    public string? Anomalia { get; set; }
    public string? Localizacao { get; set; }
    public string? GrauRisco { get; set; }
    public DateOnly? Prazo { get; set; }
    public string? ResponsavelExecucao { get; set; }
    public string? SituacaoEncontrada { get; set; }
    public string? SituacaoRequerida { get; set; }
    public string? RiscoAssociado { get; set; }
    public long? RequisitoId { get; set; }
}

public record ApontamentoDto(
    long Id,
    long VistoriaId,
    string? VistoriaCodigoDocumento,
    string? VistoriaUnidade,
    DateOnly? VistoriaData,
    long? RequisitoId,
    string? RequisitoCodigo,
    string? RequisitoTitulo,
    string? RequisitoDescricao,
    string Anomalia,
    string? Localizacao,
    string GrauRisco,
    DateOnly? Prazo,
    int? DiasRestantes,
    string? ResponsavelExecucao,
    string? SituacaoEncontrada,
    string? SituacaoRequerida,
    string? RiscoAssociado,
    string? ImagemEncontradaUrl,
    string? ImagemRequeridaUrl,
    string Status,
    DateOnly? DataEncerramento,
    string? ImagemEvidenciaEncerramentoUrl,
    string? ObservacoesEncerramento)
{
    public static ApontamentoDto De(Apontamento a, DateOnly hoje)
    {
        var req = a.Requisito;
        int? dias = a.Prazo.HasValue ? a.Prazo.Value.DayNumber - hoje.DayNumber : null;

        string? referenciaFormatada = null;
        if (req is { Referencias.Count: > 0 })
        {
            var r = req.Referencias[0];
            referenciaFormatada = (r.Norma ?? "") + (string.IsNullOrWhiteSpace(r.Codigo) ? "" : " · " + r.Codigo);
        }

        return new ApontamentoDto(
            a.Id, a.VistoriaId,
            a.Vistoria?.CodigoDocumento, a.Vistoria?.UnidadePropriedade, a.Vistoria?.DataVistoria,
            req?.Id, referenciaFormatada, req?.Titulo, req?.Descricao,
            a.Anomalia, a.Localizacao, a.GrauRisco.ToString(), a.Prazo, dias,
            a.ResponsavelExecucao, a.SituacaoEncontrada, a.SituacaoRequerida, a.RiscoAssociado,
            a.ImagemEncontradaUrl, a.ImagemRequeridaUrl,
            a.Status.ToString(), a.DataEncerramento, a.ImagemEvidenciaEncerramentoUrl, a.ObservacoesEncerramento);
    }

    public bool Aberto => Status == nameof(StatusApontamento.ABERTO);
}

public record VistoriaDto(
    long Id,
    string CodigoDocumento,
    string UnidadePropriedade,
    string? SetorArea,
    DateOnly DataVistoria,
    string Tipo,
    string? TecnicoResponsavel,
    string? CargoTecnico,
    string? Motivo,
    string? AprovadoPor,
    string? CargoAprovador,
    string? UsuarioId,
    string? UsuarioNome,
    int TotalApontamentos,
    int Criticos,
    int Altos,
    int Medios,
    int Baixos,
    int EmAberto,
    int Encerrados,
    List<ApontamentoDto> Apontamentos)
{
    public static VistoriaDto De(Vistoria v, DateOnly hoje)
    {
        var aps = v.Apontamentos.OrderBy(a => a.CriadoEm).ThenBy(a => a.Id).ToList();
        int Conta(GrauRisco g) => aps.Count(a => a.GrauRisco == g);
        return new VistoriaDto(
            v.Id, v.CodigoDocumento, v.UnidadePropriedade, v.SetorArea, v.DataVistoria, v.Tipo.ToString(),
            v.TecnicoResponsavel, v.CargoTecnico, v.Motivo, v.AprovadoPor, v.CargoAprovador,
            v.UsuarioId, v.UsuarioNome,
            aps.Count,
            Conta(GrauRisco.CRITICO), Conta(GrauRisco.ALTO), Conta(GrauRisco.MEDIO), Conta(GrauRisco.BAIXO),
            aps.Count(a => a.Status == StatusApontamento.ABERTO),
            aps.Count(a => a.Status == StatusApontamento.ENCERRADO),
            aps.Select(a => ApontamentoDto.De(a, hoje)).ToList());
    }
}
