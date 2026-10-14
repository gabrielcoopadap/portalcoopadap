using AgroCert.Application.Servicos;
using AgroCert.Certificacao.Modelos;
using Microsoft.AspNetCore.Mvc;

namespace AgroCert.Certificacao.Controllers.Telas;

[Route("certificacao/certificacoes")]
public class CertificacoesController : TelaBaseController
{
    private readonly CertificacaoServico _servico;
    public CertificacoesController(CertificacaoServico servico) => _servico = servico;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _servico.ListarAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalhe(long id, CancellationToken ct) => View(await _servico.ObterAsync(id, ct));

    [HttpPost("")]
    public async Task<IActionResult> Criar(CertificacaoForm form, CancellationToken ct)
    {
        var nova = await _servico.CriarAsync(form.ParaEntidade(), ct);
        Sucesso($"Certificação \"{nova.Nome}\" cadastrada.");
        return RedirectToAction(nameof(Detalhe), new { id = nova.Id });
    }

    [HttpPost("{id:long}")]
    public async Task<IActionResult> Editar(long id, CertificacaoForm form, CancellationToken ct)
    {
        await _servico.AtualizarAsync(id, form.ParaEntidade(), ct);
        Sucesso("Certificação atualizada.");
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    [HttpPost("{id:long}/excluir")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct)
    {
        await _servico.ExcluirAsync(id, ct);
        Sucesso("Certificação excluída.");
        return RedirectToAction(nameof(Index));
    }
}
