using AgroCert.Application.Dtos;
using AgroCert.Application.Servicos;
using AgroCert.Certificacao.Infra;
using AgroCert.Certificacao.Modelos;
using Microsoft.AspNetCore.Mvc;

namespace AgroCert.Certificacao.Controllers.Telas;

[Route("certificacao/ambiental")]
public class AmbientalController : TelaBaseController
{
    private readonly MonitoramentoAmbientalServico _servico;
    private readonly ConformidadeAmbientalServico _conformidade;
    private readonly ImagemSateliteConformidade _satelite;

    public AmbientalController(MonitoramentoAmbientalServico servico, ConformidadeAmbientalServico conformidade,
        ImagemSateliteConformidade satelite)
    {
        _servico = servico;
        _conformidade = conformidade;
        _satelite = satelite;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _servico.ListarPropriedadesAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detalhe(long id, string? mes, CancellationToken ct)
    {
        var relatorio = await _servico.GerarFichaAmbientalAsync(id, ct);
        var leituras = await _servico.ListarNdviAsync(id, ct);
        var selecionada = leituras.FirstOrDefault(l => l.DataImagem.ToString("yyyy-MM") == mes) ?? leituras.LastOrDefault();
        var indice = selecionada is null ? -1 : leituras.IndexOf(selecionada);
        return View(new AmbientalDetalheViewModel
        {
            Relatorio = relatorio,
            Leituras = leituras,
            LeituraSelecionada = selecionada,
            LeituraAnterior = indice > 0 ? leituras[indice - 1] : null,
        });
    }

    [HttpPost("")]
    public async Task<IActionResult> Cadastrar(CadastrarPropriedadeRequest req, bool geometriaManual, CancellationToken ct)
    {
        if (!geometriaManual) req.GeoJson = null;
        var p = await _servico.CadastrarPorCarAsync(req, ct);
        Sucesso($"Propriedade \"{p.Nome}\" cadastrada.");
        return RedirectToAction(nameof(Detalhe), new { id = p.Id });
    }

    [HttpPost("{id:long}/excluir")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct)
    {
        await _servico.ExcluirPropriedadeAsync(id, ct);
        Sucesso("Propriedade excluída.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:long}/sincronizar-alertas")]
    public async Task<IActionResult> SincronizarAlertas(long id, CancellationToken ct)
    {
        var alertas = await _servico.SincronizarAlertasAsync(id, null, ct);
        Sucesso($"MapBiomas Alerta consultado: {alertas.Count} alerta(s) dentro do perímetro do CAR.");
        return RedirectToAction(nameof(Detalhe), new { id });
    }

    [HttpPost("{id:long}/sincronizar-ndvi")]
    public async Task<IActionResult> SincronizarNdvi(long id, CancellationToken ct)
    {
        var leituras = await _servico.SincronizarNdviAsync(id, null, ct);
        Sucesso($"NDVI atualizado: {leituras.Count} mês(es) com leitura.");
        return Redirect(Url.Action(nameof(Detalhe), new { id }) + "#ndvi");
    }

    [HttpGet("{id:long}/relatorio.pdf")]
    public async Task<IActionResult> RelatorioPdf(long id, CancellationToken ct)
    {
        var (pdf, nome) = await _servico.GerarRelatorioPdfAsync(id, ct);
        return File(pdf, "application/pdf", nome);
    }

    [HttpGet("{id:long}/conformidade")]
    public async Task<IActionResult> Conformidade(long id, CancellationToken ct) => View(await _conformidade.GerarAsync(id, ct));

    [HttpGet("{id:long}/conformidade/satelite")]
    [ResponseCache(Duration = 86400)]
    public async Task<IActionResult> Satelite(long id, string? tipo, int ano, string? caixa, CancellationToken ct)
    {
        var r = await _satelite.ObterAsync(id, tipo, ano, caixa, ct);
        return r.Arquivo is not null ? PhysicalFile(r.Arquivo, "image/png") : StatusCode(r.Status);
    }
}
