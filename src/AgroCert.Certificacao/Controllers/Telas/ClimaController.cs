using AgroCert.Certificacao.Clima;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgroCert.Certificacao.Controllers.Telas;

[Area("Clima")]
[Route("clima")]
[Authorize(Policy = ModuloClima.Politica)]
[AutoValidateAntiforgeryToken]
public class ClimaController : Controller
{
    private readonly ApiClimaCliente _api;
    public ClimaController(ApiClimaCliente api) => _api = api;

    [HttpGet("")]
    [HttpGet("resumo")]
    public Task<IActionResult> Resumo(CancellationToken ct) =>
        Tela("resumo", "Resumo", async () =>
        {
            var hoje = DateTime.Today;
            var situacao = await _api.SituacaoEnsoAsync(ct);
            ImpactoEnsoDto? impacto = null;
            try { impacto = await _api.ImpactoEnsoAsync(ct); } catch (ApiClimaException e) when (!e.Indisponivel) {  }
            var p = new ParametrosPrevisao(hoje.Year, null, 60, 0.5, 80);
            var meses = new List<PrevisaoMesDto>((await _api.PreverAsync(p, ct)).Meses);
            if (hoje.Month > 7) meses.AddRange((await _api.PreverAsync(p with { Ano = hoje.Year + 1 }, ct)).Meses);
            return ResumoClima.Montar(situacao, impacto, meses, hoje.Month, hoje.Year);
        });

    [HttpGet("previsao")]
    public async Task<IActionResult> Previsao(int? ano, string? oni, int ciclo = 60, double pesoCiclo = 0.5, int nivel = 80, CancellationToken ct = default)
    {
        var p = new ParametrosPrevisao(
            ano is >= 1900 and <= 2200 ? ano.Value : DateTime.Today.Year,
            oni,
            Math.Clamp(ciclo, 1, 200),
            Math.Clamp(pesoCiclo, 0, 1),
            nivel is 80 or 90 or 95 ? nivel : 80);
        return await Tela("previsao", "Previsao", () => _api.PreverAsync(p, ct), p);
    }

    [HttpGet("enso")]
    public Task<IActionResult> Enso(CancellationToken ct) =>
        Tela("enso", "Enso", async () => new EnsoTela(await _api.SituacaoEnsoAsync(ct), await _api.ImpactoEnsoAsync(ct)));

    [HttpGet("validacao")]
    public Task<IActionResult> Validacao(int antecedencia = 6, CancellationToken ct = default) =>
        Tela("validacao", "Validacao", () => _api.ValidacaoAsync(Math.Clamp(antecedencia, 1, 12), ct), antecedencia);

    [HttpGet("dados")]
    public Task<IActionResult> Dados(CancellationToken ct) =>
        Tela("dados", "Dados", async () => new DadosTela(await _api.QualidadeAsync(ct), null));

    [HttpPost("dados/corrigir-enso")]
    public Task<IActionResult> CorrigirEnso(bool aplicar, CancellationToken ct) =>
        Tela("dados", "Dados", async () =>
        {
            var correcao = await _api.CorrigirEnsoAsync(aplicar, ct);
            return new DadosTela(await _api.QualidadeAsync(ct), correcao);
        });

    private async Task<IActionResult> Tela<T>(string aba, string view, Func<Task<T>> carregar, object? parametros = null)
    {
        ViewData["AbaClima"] = aba;
        ViewData["ApiClima"] = _api.Endereco;
        ViewData["Parametros"] = parametros;
        try
        {
            return View(view, await carregar());
        }
        catch (ApiClimaException e) when (e.Indisponivel)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ViewData["Erro"] = e.Message;
            return View("Indisponivel");
        }
        catch (ApiClimaException e)
        {
            ViewData["Erro"] = e.Message;
            return View(view, default(T));
        }
    }
}

public record EnsoTela(SituacaoEnsoDto Situacao, ImpactoEnsoDto Impacto);

public record DadosTela(QualidadeDto Qualidade, CorrecaoEnsoDto? Correcao);
