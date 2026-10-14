using AgroCert.Application.Servicos;
using AgroCert.Certificacao.Modelos;
using Microsoft.AspNetCore.Mvc;

namespace AgroCert.Certificacao.Controllers.Telas;

[Route("certificacao/requisitos")]
public class RequisitosController : TelaBaseController
{
    private readonly RequisitoServico _servico;
    public RequisitosController(RequisitoServico servico) => _servico = servico;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? busca, string? tema, string? norma, string? status, string? prioridade, CancellationToken ct)
    {
        var todos = await _servico.ListarAsync(ct);
        return View(new RequisitosViewModel
        {
            Todos = todos,
            Filtrados = RequisitoServico.Filtrar(todos, busca, tema, status, norma, prioridade).ToList(),
            Temas = RequisitoServico.TemasPresentes(todos),
            Normas = RequisitoServico.NormasPresentes(todos),
            Busca = busca, Tema = tema, Norma = norma, Status = status, Prioridade = prioridade,
        });
    }

    [HttpPost("")]
    public async Task<IActionResult> Criar(RequisitoForm form, CancellationToken ct)
    {
        await _servico.CriarAsync(form.ParaEntidade(), ct);
        Sucesso("Requisito cadastrado.");
        return Redirect(Voltar("/certificacao/requisitos"));
    }

    [HttpPost("{id:long}")]
    public async Task<IActionResult> Editar(long id, RequisitoForm form, CancellationToken ct)
    {
        await _servico.AtualizarAsync(id, form.ParaEntidade(), ct);
        Sucesso("Requisito atualizado.");
        return Redirect(Voltar("/certificacao/requisitos"));
    }

    [HttpPost("{id:long}/excluir")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct)
    {
        await _servico.ExcluirAsync(id, ct);
        Sucesso("Requisito excluído.");
        return Redirect(Voltar("/certificacao/requisitos"));
    }

    [HttpGet("lista-mestra")]
    public async Task<IActionResult> ListaMestra(string? certificacao, CancellationToken ct)
    {
        var (conteudo, nome) = await _servico.ExportarListaMestraAsync(certificacao, ct);
        return File(conteudo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nome);
    }
}
