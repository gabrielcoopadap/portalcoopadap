using System.Text.RegularExpressions;
using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Domain.Entidades;
using AgroCert.Domain.Enums;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Application.Servicos;

public class VistoriaServico
{
    private readonly IVistoriaRepositorio _repo;
    private readonly IRequisitoRepositorio _requisitos;
    private readonly IArmazenamentoArquivos _arquivos;
    private readonly IRelatorioVistoriaPdf _pdf;

    public VistoriaServico(IVistoriaRepositorio repo, IRequisitoRepositorio requisitos,
                           IArmazenamentoArquivos arquivos, IRelatorioVistoriaPdf pdf)
    {
        _repo = repo;
        _requisitos = requisitos;
        _arquivos = arquivos;
        _pdf = pdf;
    }

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    public async Task<List<VistoriaDto>> ListarAsync(CancellationToken ct = default) =>
        (await _repo.ListarAsync(ct))
            .OrderByDescending(v => v.DataVistoria).ThenByDescending(v => v.Id)
            .Select(v => VistoriaDto.De(v, Hoje)).ToList();

    public async Task<VistoriaDto> ObterAsync(long id, CancellationToken ct = default) =>
        VistoriaDto.De(await ObterEntidadeAsync(id, ct), Hoje);

    private async Task<Vistoria> ObterEntidadeAsync(long id, CancellationToken ct) =>
        await _repo.ObterAsync(id, ct) ?? throw new NaoEncontradoException("Vistoria não encontrada: " + id);

    public async Task<VistoriaDto> CriarAsync(CriarVistoriaRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.UnidadePropriedade)) throw new RegraNegocioException("Informe a unidade/propriedade.");
        if (req.DataVistoria is null) throw new RegraNegocioException("Informe a data da vistoria.");
        if (!Enum.TryParse<TipoVistoria>(req.Tipo, out var tipo) || !Enum.IsDefined(tipo))
            throw new RegraNegocioException("Tipo de vistoria inválido: " + req.Tipo);

        var vistoria = new Vistoria
        {
            CodigoDocumento = await GerarCodigoDocumentoAsync(ct),
            UnidadePropriedade = req.UnidadePropriedade.Trim(),
            SetorArea = req.SetorArea,
            DataVistoria = req.DataVistoria.Value,
            Tipo = tipo,
            TecnicoResponsavel = req.TecnicoResponsavel,
            CargoTecnico = req.CargoTecnico,
            Motivo = req.Motivo,
            AprovadoPor = req.AprovadoPor,
            CargoAprovador = req.CargoAprovador,
            UsuarioId = req.UsuarioId,
            UsuarioNome = req.UsuarioNome,
        };
        await _repo.AdicionarAsync(vistoria, ct);
        await _repo.SalvarAsync(ct);
        return VistoriaDto.De(vistoria, Hoje);
    }

    public async Task ExcluirAsync(long id, CancellationToken ct = default)
    {
        await _repo.ExcluirAsync(id, ct);
        await _repo.SalvarAsync(ct);
    }

    private async Task<string> GerarCodigoDocumentoAsync(CancellationToken ct)
    {
        var codigos = await _repo.ListarCodigosDocumentoAsync(ct);
        var maior = codigos
            .Select(c => Regex.Match(c, @"^VIST-(\d+)$"))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value))
            .DefaultIfEmpty(0)
            .Max();
        return $"VIST-{maior + 1:000}";
    }

    public async Task<ApontamentoDto> AdicionarApontamentoAsync(long vistoriaId, NovoApontamentoRequest req,
        ArquivoEnviado? imagemEncontrada, ArquivoEnviado? imagemRequerida, CancellationToken ct = default)
    {
        var vistoria = await ObterEntidadeAsync(vistoriaId, ct);

        if (string.IsNullOrWhiteSpace(req.Anomalia))
            throw new RegraNegocioException("Informe o título/anomalia do apontamento.");

        var apontamento = new Apontamento
        {
            Vistoria = vistoria,
            VistoriaId = vistoria.Id,
            Anomalia = req.Anomalia.Trim(),
            Localizacao = req.Localizacao,
            GrauRisco = ParseGrauRisco(req.GrauRisco),
            Prazo = req.Prazo,
            ResponsavelExecucao = req.ResponsavelExecucao,
            SituacaoEncontrada = req.SituacaoEncontrada,
            SituacaoRequerida = req.SituacaoRequerida,
            RiscoAssociado = req.RiscoAssociado,
            Status = StatusApontamento.ABERTO,
        };

        if (req.RequisitoId is long rid)
        {
            var requisito = await _requisitos.ObterAsync(rid, ct);
            if (requisito != null) { apontamento.Requisito = requisito; apontamento.RequisitoId = requisito.Id; }
        }

        apontamento.ImagemEncontradaUrl = await _arquivos.SalvarAsync(imagemEncontrada, "vistorias", ct);
        apontamento.ImagemRequeridaUrl = await _arquivos.SalvarAsync(imagemRequerida, "vistorias", ct);

        await _repo.AdicionarApontamentoAsync(apontamento, ct);
        await _repo.SalvarAsync(ct);
        return ApontamentoDto.De(apontamento, Hoje);
    }

    public async Task<ApontamentoDto> EncerrarApontamentoAsync(long apontamentoId, string? observacoes,
        ArquivoEnviado? evidencia, CancellationToken ct = default)
    {
        var a = await ObterApontamentoAsync(apontamentoId, ct);
        a.Status = StatusApontamento.ENCERRADO;
        a.DataEncerramento = Hoje;
        a.ObservacoesEncerramento = observacoes;
        if (evidencia is { Tamanho: > 0 })
            a.ImagemEvidenciaEncerramentoUrl = await _arquivos.SalvarAsync(evidencia, "vistorias", ct);
        await _repo.SalvarAsync(ct);
        return ApontamentoDto.De(a, Hoje);
    }

    public async Task ReabrirApontamentoAsync(long apontamentoId, CancellationToken ct = default)
    {
        var a = await ObterApontamentoAsync(apontamentoId, ct);
        a.Status = StatusApontamento.ABERTO;
        a.DataEncerramento = null;
        await _repo.SalvarAsync(ct);
    }

    public async Task<long?> ExcluirApontamentoAsync(long id, CancellationToken ct = default)
    {
        var a = await _repo.ObterApontamentoAsync(id, ct);
        if (a is null) return null;
        var vistoriaId = a.VistoriaId;
        await _repo.ExcluirApontamentoAsync(id, ct);
        await _repo.SalvarAsync(ct);
        return vistoriaId;
    }

    public async Task<ApontamentoDto> ObterApontamentoDtoAsync(long id, CancellationToken ct = default) =>
        ApontamentoDto.De(await ObterApontamentoAsync(id, ct), Hoje);

    private async Task<Apontamento> ObterApontamentoAsync(long id, CancellationToken ct) =>
        await _repo.ObterApontamentoAsync(id, ct) ?? throw new NaoEncontradoException("Apontamento não encontrado: " + id);

    public async Task<List<ApontamentoDto>> ListarPendenciasAsync(CancellationToken ct = default) =>
        (await _repo.ListarApontamentosPorStatusAsync(StatusApontamento.ABERTO, ct))
            .Select(a => ApontamentoDto.De(a, Hoje)).ToList();

    public async Task<(byte[] Pdf, string NomeArquivo)> GerarRelatorioPdfAsync(long vistoriaId, CancellationToken ct = default)
    {
        var dto = await ObterAsync(vistoriaId, ct);
        return (await _pdf.GerarAsync(dto, ct), dto.CodigoDocumento + ".pdf");
    }

    private static GrauRisco ParseGrauRisco(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) throw new RegraNegocioException("Informe o grau de risco do apontamento.");
        if (Enum.TryParse<GrauRisco>(texto.Trim().ToUpperInvariant(), out var g) && Enum.IsDefined(g)) return g;
        throw new RegraNegocioException("Grau de risco inválido: " + texto);
    }
}
