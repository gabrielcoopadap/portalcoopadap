using System.Text.Json;
using System.Text.RegularExpressions;
using AgroCert.Application.Dtos;
using AgroCert.Application.Servicos;
using AgroCert.Certificacao.Clima;
using AgroCert.Certificacao.Controllers.Telas;
using AgroCert.Certificacao.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AgroCert.Certificacao.Controllers.Api;

[Route("api/sessao")]
public class SessaoApiController : ApiBaseController
{
    private readonly IConfiguration _config;
    public SessaoApiController(IConfiguration config) => _config = config;

    [HttpGet]
    public object Obter()
    {
        var autenticado = User.Identity?.IsAuthenticated == true;
        return new
        {
            Autenticado = autenticado,
            Nome = autenticado ? UsuarioAtual.Nome(User) : _config["Autenticacao:UsuarioTeste"],
            Consultor = UsuarioAtual.EhConsultor(User),
        };
    }
}

[Route("api/painel")]
public class PainelApiController : ApiBaseController
{
    private readonly PainelServico _s;
    public PainelApiController(PainelServico s) => _s = s;

    [HttpGet] public Task<PainelDto> Obter(CancellationToken ct) => _s.MontarAsync(ct);
}

[Route("api/ambiental/propriedades/{id:long}/conformidade")]
public class ConformidadeApiController : ApiBaseController
{
    private readonly ConformidadeAmbientalServico _s;
    private readonly ImagemSateliteConformidade _satelite;

    public ConformidadeApiController(ConformidadeAmbientalServico s, ImagemSateliteConformidade satelite)
    {
        _s = s;
        _satelite = satelite;
    }

    [HttpGet] public Task<RelatorioConformidadeDto> Obter(long id, CancellationToken ct) => _s.GerarAsync(id, ct);

    [HttpGet("satelite")]
    [ResponseCache(Duration = 86400)]
    public async Task<IActionResult> Satelite(long id, string? tipo, int ano, string? caixa, CancellationToken ct)
    {
        var r = await _satelite.ObterAsync(id, tipo, ano, caixa, ct);
        return r.Arquivo is not null ? PhysicalFile(r.Arquivo, "image/png") : StatusCode(r.Status);
    }
}

[Route("api/fauna-flora/painel")]
public class FaunaFloraPainelApiController : ApiBaseController
{
    private readonly FaunaFloraServico _s;
    public FaunaFloraPainelApiController(FaunaFloraServico s) => _s = s;

    [HttpGet] public async Task<object> Obter(int meses, CancellationToken ct) => ProjecaoBio.Painel(await _s.MontarPainelAsync(meses == 12 ? 12 : 6, ct));
}

[Route("api/corredores")]
public partial class CorredoresApiController : ApiBaseController
{
    private readonly string _pasta;

    public CorredoresApiController(IConfiguration config, IWebHostEnvironment env)
    {
        var p = config["AgroCert:Corredores:Pasta"];
        if (string.IsNullOrWhiteSpace(p)) p = Path.Combine("..", "..", "dados-corredores", "resultado");
        _pasta = Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(env.ContentRootPath, p));
    }

    [HttpGet]
    public object Situacao()
    {
        var json = Path.Combine(_pasta, "corredores.json");
        if (!System.IO.File.Exists(json)) return new { TemDados = false, Pasta = _pasta };
        try
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllBytes(json));
            return new
            {
                TemDados = true, Pasta = _pasta, AtualizadoEm = System.IO.File.GetLastWriteTime(json),
                Regioes = doc.RootElement.GetProperty("regioes").GetArrayLength(),
            };
        }
        catch (Exception)
        {
            return new { TemDados = false, Pasta = _pasta, Erro = "O arquivo corredores.json está incompleto ou corrompido. Rode o ProcessarCorredores de novo." };
        }
    }

    [HttpGet("arquivo/{nome}")]
    [ResponseCache(Duration = 300)]
    public IActionResult Arquivo(string nome)
    {
        if (!NomeValido().IsMatch(nome)) return NotFound();
        var caminho = Path.Combine(_pasta, nome);
        if (!System.IO.File.Exists(caminho)) return NotFound();
        var tipo = Path.GetExtension(nome).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".geojson" => "application/geo+json",
            _ => "application/json",
        };
        return PhysicalFile(caminho, tipo);
    }

    [GeneratedRegex(@"^[a-z0-9_]{1,80}\.(png|geojson|json)$")]
    private static partial Regex NomeValido();
}

[ApiController]
[Route("api/clima")]
[Authorize(Policy = ModuloClima.Politica)]
[JsonModulo]
[EnableCors(ModuloCertificacao.PoliticaCors)]
public class ClimaApiController : ControllerBase
{
    private readonly ApiClimaCliente _api;
    public ClimaApiController(ApiClimaCliente api) => _api = api;

    [HttpGet("resumo")]
    public Task<IActionResult> Resumo(CancellationToken ct) => Responder(async () =>
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
    public Task<IActionResult> Previsao(int? ano, string? oni, int ciclo = 60, double pesoCiclo = 0.5, int nivel = 80, CancellationToken ct = default)
    {
        var p = new ParametrosPrevisao(
            ano is >= 1900 and <= 2200 ? ano.Value : DateTime.Today.Year,
            oni,
            Math.Clamp(ciclo, 1, 200),
            Math.Clamp(pesoCiclo, 0, 1),
            nivel is 80 or 90 or 95 ? nivel : 80);
        return Responder(() => _api.PreverAsync(p, ct));
    }

    [HttpGet("enso")]
    public Task<IActionResult> Enso(CancellationToken ct) =>
        Responder(async () => new EnsoTela(await _api.SituacaoEnsoAsync(ct), await _api.ImpactoEnsoAsync(ct)));

    [HttpGet("validacao")]
    public Task<IActionResult> Validacao(int antecedencia = 6, CancellationToken ct = default) =>
        Responder(() => _api.ValidacaoAsync(Math.Clamp(antecedencia, 1, 12), ct));

    [HttpGet("dados")]
    public Task<IActionResult> Dados(CancellationToken ct) => Responder(() => _api.QualidadeAsync(ct));

    [HttpPost("dados/corrigir-enso")]
    public Task<IActionResult> CorrigirEnso(bool aplicar, CancellationToken ct) => Responder(() => _api.CorrigirEnsoAsync(aplicar, ct));

    private async Task<IActionResult> Responder<T>(Func<Task<T>> carregar)
    {
        try
        {
            return Ok(await carregar());
        }
        catch (ApiClimaException e)
        {
            return StatusCode(e.Indisponivel ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status400BadRequest,
                new { mensagem = e.Message, endereco = _api.Endereco });
        }
    }
}
