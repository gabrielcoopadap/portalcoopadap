using AgroCert.Application.Servicos;
using AgroCert.Application.Util;
using AgroCert.Certificacao.Infra;
using AgroCert.Certificacao.Modelos;
using AgroCert.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace AgroCert.Certificacao.Controllers.Telas;

[Route("certificacao/fauna-flora")]
public class FaunaFloraController : TelaBaseController
{
    private readonly FaunaFloraServico _servico;
    private readonly MonitoramentoAmbientalServico _ambiental;

    public FaunaFloraController(FaunaFloraServico servico, MonitoramentoAmbientalServico ambiental)
    {
        _servico = servico;
        _ambiental = ambiental;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int meses, CancellationToken ct)
    {
        ViewData["Meses"] = meses == 12 ? 12 : 6;
        return View(await _servico.MontarPainelAsync(meses, ct));
    }

    [HttpGet("especies")]
    public async Task<IActionResult> Especies(string? reino, string? grupo, string? busca, string? status, CancellationToken ct)
    {
        var especies = await _servico.ListarEspeciesAsync(ct);
        var registros = await _servico.ListarRegistrosAsync(ct);
        var filtradas = especies.Where(e =>
            (string.IsNullOrEmpty(reino) || e.Reino.ToString() == reino) &&
            (string.IsNullOrEmpty(grupo) || e.Grupo == grupo) &&
            (string.IsNullOrEmpty(status) || (status == "AMEACADA" ? Biodiversidade.Ameacada(e.StatusConservacao) : e.StatusConservacao == status)) &&
            (string.IsNullOrWhiteSpace(busca) || $"{e.NomePopular} {e.NomeCientifico}".Contains(busca.Trim(), StringComparison.CurrentCultureIgnoreCase)))
            .ToList();

        return View(new EspeciesViewModel
        {
            Especies = filtradas,
            Registros = registros.GroupBy(r => r.EspecieId).ToDictionary(g => g.Key, g => (g.Count(), (DateTime?)g.Max(r => r.DataHora))),
            Reino = reino, Grupo = grupo, Busca = busca, Status = status,
            Modal = await ModalAsync(especies, ct),
        });
    }

    [HttpPost("especies")]
    public async Task<IActionResult> CriarEspecie(EspecieForm form, CancellationToken ct)
    {
        var e = await _servico.CriarEspecieAsync(form, form.Foto.ParaArquivo(), ct);
        Sucesso($"Espécie \"{e.NomePopular}\" cadastrada.");
        return Redirect(Voltar("/certificacao/fauna-flora/especies"));
    }

    [HttpPost("especies/{id:long}")]
    public async Task<IActionResult> EditarEspecie(long id, EspecieForm form, CancellationToken ct)
    {
        await _servico.AtualizarEspecieAsync(id, form, form.Foto.ParaArquivo(), ct);
        Sucesso("Espécie atualizada.");
        return Redirect(Voltar("/certificacao/fauna-flora/especies"));
    }

    [HttpPost("especies/{id:long}/excluir")]
    public async Task<IActionResult> ExcluirEspecie(long id, CancellationToken ct)
    {
        await _servico.ExcluirEspecieAsync(id, ct);
        Sucesso("Espécie excluída.");
        return Redirect(Voltar("/certificacao/fauna-flora/especies"));
    }

    [HttpGet("registros")]
    public async Task<IActionResult> Registros(string? reino, string? origem, long? especieId, int dias, CancellationToken ct)
    {
        var todos = await _servico.ListarRegistrosAsync(ct);
        dias = dias is 30 or 90 or 365 ? dias : 0;
        var limite = dias > 0 ? DateTime.Now.AddDays(-dias) : DateTime.MinValue;
        var filtrados = todos.Where(r =>
            (string.IsNullOrEmpty(reino) || r.Especie.Reino.ToString() == reino) &&
            (string.IsNullOrEmpty(origem) || r.Origem.ToString() == origem) &&
            (especieId is null || r.EspecieId == especieId) &&
            r.DataHora >= limite).ToList();

        return View(new RegistrosViewModel
        {
            Registros = filtrados, Total = todos.Count, Reino = reino, Origem = origem, EspecieId = especieId, Dias = dias,
            Modal = await ModalAsync(null, ct),
        });
    }

    [HttpPost("registros")]
    public async Task<IActionResult> CriarRegistro(RegistroForm form, CancellationToken ct)
    {
        form.RegistradoPor ??= UsuarioAtual.Nome(User);
        var r = await _servico.CriarRegistroAsync(form.ParaRequest(), form.Foto.ParaArquivo(), ct);
        Sucesso($"Ocorrência de {r.Especie.NomePopular} registrada.");
        return Redirect(Voltar("/certificacao/fauna-flora"));
    }

    [HttpPost("registros/{id:long}/excluir")]
    public async Task<IActionResult> ExcluirRegistro(long id, CancellationToken ct)
    {
        await _servico.ExcluirRegistroAsync(id, ct);
        Sucesso("Registro excluído.");
        return Redirect(Voltar("/certificacao/fauna-flora/registros"));
    }

    private async Task<ModalRegistroViewModel> ModalAsync(List<Domain.Entidades.Especie>? especies, CancellationToken ct) => new()
    {
        Especies = especies ?? await _servico.ListarEspeciesAsync(ct),
        Propriedades = (await _ambiental.ListarPropriedadesAsync(ct))
            .Select(p => new Domain.Entidades.Propriedade { Id = p.Id, Nome = p.Nome }).ToList(),
        Voltar = Request.Path + Request.QueryString,
    };
}
