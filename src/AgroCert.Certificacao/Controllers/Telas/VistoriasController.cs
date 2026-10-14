using AgroCert.Application.Dtos;
using AgroCert.Application.Servicos;
using AgroCert.Certificacao.Infra;
using AgroCert.Certificacao.Modelos;
using Microsoft.AspNetCore.Mvc;

namespace AgroCert.Certificacao.Controllers.Telas;

[Route("certificacao/vistorias")]
public class VistoriasController : TelaBaseController
{
    private readonly VistoriaServico _servico;
    private readonly RequisitoServico _requisitos;

    public VistoriasController(VistoriaServico servico, RequisitoServico requisitos)
    {
        _servico = servico;
        _requisitos = requisitos;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? tipo, CancellationToken ct)
    {
        var todas = await _servico.ListarAsync(ct);
        var aba = tipo is "MENSAL_CONSULTORIA" or "DIARIA_FUNCIONARIO" ? tipo : "TODAS";
        return View(new VistoriasViewModel
        {
            Todas = todas,
            Filtradas = aba == "TODAS" ? todas : todas.Where(v => v.Tipo == aba).ToList(),
            Pendencias = (await _servico.ListarPendenciasAsync(ct)).Count,
            Aba = aba,
        });
    }

    [HttpGet("pendencias")]
    public async Task<IActionResult> Pendencias(CancellationToken ct) => View(await _servico.ListarPendenciasAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalhe(long id, CancellationToken ct) => View(new VistoriaDetalheViewModel
    {
        Vistoria = await _servico.ObterAsync(id, ct),
        Requisitos = await _requisitos.ListarAsync(ct),
    });

    [HttpPost("")]
    public async Task<IActionResult> Criar(CriarVistoriaRequest req, CancellationToken ct)
    {
        req.UsuarioId = UsuarioAtual.Id(User);
        req.UsuarioNome = UsuarioAtual.Nome(User);
        var nova = await _servico.CriarAsync(req, ct);
        Sucesso($"Vistoria {nova.CodigoDocumento} aberta. Registre os apontamentos encontrados.");
        return RedirectToAction(nameof(Detalhe), new { id = nova.Id });
    }

    [HttpPost("{id:long}/excluir")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct)
    {
        await _servico.ExcluirAsync(id, ct);
        Sucesso("Vistoria excluída.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:long}/apontamentos")]
    public async Task<IActionResult> NovoApontamento(long id, ApontamentoForm form, CancellationToken ct)
    {
        await _servico.AdicionarApontamentoAsync(id, form, form.ImagemEncontrada.ParaArquivo(), form.ImagemRequerida.ParaArquivo(), ct);
        Sucesso("Apontamento registrado.");
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    [HttpPost("apontamentos/{id:long}/encerrar")]
    public async Task<IActionResult> Encerrar(long id, EncerramentoForm form, CancellationToken ct)
    {
        await _servico.EncerrarApontamentoAsync(id, form.Observacoes, form.Evidencia.ParaArquivo(), ct);
        Sucesso("Pendência encerrada.");
        return Redirect(Voltar("/certificacao/vistorias"));
    }

    [HttpPost("apontamentos/{id:long}/reabrir")]
    public async Task<IActionResult> Reabrir(long id, CancellationToken ct)
    {
        await _servico.ReabrirApontamentoAsync(id, ct);
        Sucesso("Apontamento reaberto.");
        return Redirect(Voltar("/certificacao/vistorias"));
    }

    [HttpPost("apontamentos/{id:long}/excluir")]
    public async Task<IActionResult> ExcluirApontamento(long id, CancellationToken ct)
    {
        await _servico.ExcluirApontamentoAsync(id, ct);
        Sucesso("Apontamento excluído.");
        return Redirect(Voltar("/certificacao/vistorias"));
    }

    [HttpGet("{id:long}/relatorio.pdf")]
    public async Task<IActionResult> RelatorioPdf(long id, CancellationToken ct)
    {
        var (pdf, nome) = await _servico.GerarRelatorioPdfAsync(id, ct);
        Response.Headers.ContentDisposition = $"inline; filename={nome}";
        return File(pdf, "application/pdf");
    }
}
