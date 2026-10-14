using AgroCert.Application.Dtos;
using AgroCert.Application.Servicos;
using AgroCert.Certificacao.Infra;
using AgroCert.Certificacao.Modelos;
using AgroCert.Domain.Entidades;
using CertificacaoEntidade = AgroCert.Domain.Entidades.Certificacao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgroCert.Certificacao.Controllers.Api;

[ApiController]
[Authorize(Policy = ModuloCertificacao.Politica)]
[TratarErros]
[JsonModulo]
[EnableCors(ModuloCertificacao.PoliticaCors)]
public abstract class ApiBaseController : ControllerBase { }

[Route("api/certificacoes")]
public class CertificacoesApiController : ApiBaseController
{
    private readonly CertificacaoServico _s;
    public CertificacoesApiController(CertificacaoServico s) => _s = s;

    [HttpGet] public Task<List<CertificacaoEntidade>> Listar(CancellationToken ct) => _s.ListarAsync(ct);
    [HttpGet("{id:long}")] public Task<CertificacaoEntidade> Obter(long id, CancellationToken ct) => _s.ObterAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<CertificacaoEntidade>> Criar(CertificacaoEntidade c, CancellationToken ct)
    {
        var nova = await _s.CriarAsync(c, ct);
        return CreatedAtAction(nameof(Obter), new { id = nova.Id }, nova);
    }

    [HttpPut("{id:long}")] public Task<CertificacaoEntidade> Atualizar(long id, CertificacaoEntidade c, CancellationToken ct) => _s.AtualizarAsync(id, c, ct);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct) { await _s.ExcluirAsync(id, ct); return NoContent(); }
}

[Route("api/requisitos")]
public class RequisitosApiController : ApiBaseController
{
    private readonly RequisitoServico _s;
    public RequisitosApiController(RequisitoServico s) => _s = s;

    [HttpGet] public Task<List<Requisito>> Listar(CancellationToken ct) => _s.ListarAsync(ct);

    [HttpPost]
    public async Task<ActionResult<Requisito>> Criar(Requisito r, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _s.CriarAsync(r, ct));

    [HttpPut("{id:long}")] public Task<Requisito> Atualizar(long id, Requisito r, CancellationToken ct) => _s.AtualizarAsync(id, r, ct);

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct) { await _s.ExcluirAsync(id, ct); return NoContent(); }

    [HttpGet("lista-mestra.xlsx")]
    public async Task<IActionResult> ListaMestra(string? certificacao, CancellationToken ct)
    {
        var (bytes, nome) = await _s.ExportarListaMestraAsync(certificacao, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nome);
    }
}

[Route("api/vistorias")]
public class VistoriasApiController : ApiBaseController
{
    private readonly VistoriaServico _s;
    public VistoriasApiController(VistoriaServico s) => _s = s;

    [HttpGet] public Task<List<VistoriaDto>> Listar(CancellationToken ct) => _s.ListarAsync(ct);
    [HttpGet("pendencias")] public Task<List<ApontamentoDto>> Pendencias(CancellationToken ct) => _s.ListarPendenciasAsync(ct);
    [HttpGet("{id:long}")] public Task<VistoriaDto> Obter(long id, CancellationToken ct) => _s.ObterAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<VistoriaDto>> Criar(CriarVistoriaRequest req, CancellationToken ct)
    {
        req.UsuarioId ??= UsuarioAtual.Id(User);
        req.UsuarioNome ??= UsuarioAtual.Nome(User);
        return StatusCode(StatusCodes.Status201Created, await _s.CriarAsync(req, ct));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct) { await _s.ExcluirAsync(id, ct); return NoContent(); }

    [HttpPost("{id:long}/apontamentos")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApontamentoDto>> AdicionarApontamento(long id, [FromForm] ApontamentoForm form, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await _s.AdicionarApontamentoAsync(id, form, form.ImagemEncontrada.ParaArquivo(), form.ImagemRequerida.ParaArquivo(), ct));

    [HttpPut("apontamentos/{id:long}/encerrar")]
    [Consumes("multipart/form-data")]
    public Task<ApontamentoDto> Encerrar(long id, [FromForm] EncerramentoForm form, CancellationToken ct) =>
        _s.EncerrarApontamentoAsync(id, form.Observacoes, form.Evidencia.ParaArquivo(), ct);

    [HttpPut("apontamentos/{id:long}/reabrir")]
    public async Task<IActionResult> Reabrir(long id, CancellationToken ct) { await _s.ReabrirApontamentoAsync(id, ct); return NoContent(); }

    [HttpDelete("apontamentos/{id:long}")]
    public async Task<IActionResult> ExcluirApontamento(long id, CancellationToken ct) { await _s.ExcluirApontamentoAsync(id, ct); return NoContent(); }

    [HttpGet("{id:long}/relatorio/pdf")]
    public async Task<IActionResult> RelatorioPdf(long id, CancellationToken ct)
    {
        var (pdf, nome) = await _s.GerarRelatorioPdfAsync(id, ct);
        Response.Headers.ContentDisposition = $"inline; filename={nome}";
        return File(pdf, "application/pdf");
    }
}

[Route("api/ambiental")]
public class AmbientalApiController : ApiBaseController
{
    private readonly MonitoramentoAmbientalServico _s;
    public AmbientalApiController(MonitoramentoAmbientalServico s) => _s = s;

    [HttpGet("propriedades")] public Task<List<PropriedadeDto>> Listar(CancellationToken ct) => _s.ListarPropriedadesAsync(ct);
    [HttpGet("propriedades/{id:long}")] public Task<PropriedadeDto> Obter(long id, CancellationToken ct) => _s.ObterPropriedadeAsync(id, ct);

    [HttpPost("propriedades")]
    public async Task<ActionResult<PropriedadeDto>> Cadastrar(CadastrarPropriedadeRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await _s.CadastrarPorCarAsync(req, ct));

    [HttpDelete("propriedades/{id:long}")]
    public async Task<IActionResult> Excluir(long id, CancellationToken ct) { await _s.ExcluirPropriedadeAsync(id, ct); return NoContent(); }

    [HttpPost("propriedades/{id:long}/sincronizar-alertas")]
    public Task<List<AlertaDto>> SincronizarAlertas(long id, DateOnly? desde, CancellationToken ct) => _s.SincronizarAlertasAsync(id, desde, ct);

    [HttpGet("propriedades/{id:long}/ndvi")]
    public Task<List<LeituraNdviDto>> ListarNdvi(long id, CancellationToken ct) => _s.ListarNdviAsync(id, ct);

    [HttpPost("propriedades/{id:long}/sincronizar-ndvi")]
    public Task<List<LeituraNdviDto>> SincronizarNdvi(long id, DateOnly? desde, CancellationToken ct) => _s.SincronizarNdviAsync(id, desde, ct);

    [HttpGet("propriedades/{id:long}/relatorio")]
    public Task<RelatorioAmbientalDto> Relatorio(long id, CancellationToken ct) => _s.GerarFichaAmbientalAsync(id, ct);

    [HttpGet("propriedades/{id:long}/relatorio.pdf")]
    public async Task<IActionResult> RelatorioPdf(long id, CancellationToken ct)
    {
        var (pdf, nome) = await _s.GerarRelatorioPdfAsync(id, ct);
        return File(pdf, "application/pdf", nome);
    }
}

[Route("api/fauna-flora")]
public class FaunaFloraApiController : ApiBaseController
{
    private readonly FaunaFloraServico _s;
    public FaunaFloraApiController(FaunaFloraServico s) => _s = s;

    private static object Json(Especie e) => ProjecaoBio.Especie(e);
    private static object Json(RegistroBiodiversidade r) => ProjecaoBio.Registro(r);

    [HttpGet("especies")]
    public async Task<IEnumerable<object>> ListarEspecies(CancellationToken ct) => (await _s.ListarEspeciesAsync(ct)).Select(Json);

    [HttpPost("especies")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CriarEspecie([FromForm] EspecieForm form, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, Json(await _s.CriarEspecieAsync(form, form.Foto.ParaArquivo(), ct)));

    [HttpPut("especies/{id:long}")]
    [Consumes("multipart/form-data")]
    public async Task<object> AtualizarEspecie(long id, [FromForm] EspecieForm form, CancellationToken ct) =>
        Json(await _s.AtualizarEspecieAsync(id, form, form.Foto.ParaArquivo(), ct));

    [HttpDelete("especies/{id:long}")]
    public async Task<IActionResult> ExcluirEspecie(long id, CancellationToken ct) { await _s.ExcluirEspecieAsync(id, ct); return NoContent(); }

    [HttpGet("registros")]
    public async Task<IEnumerable<object>> ListarRegistros(CancellationToken ct) => (await _s.ListarRegistrosAsync(ct)).Select(Json);

    [HttpPost("registros")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CriarRegistro([FromForm] RegistroForm form, CancellationToken ct)
    {
        form.RegistradoPor ??= UsuarioAtual.Nome(User);
        return StatusCode(StatusCodes.Status201Created, Json(await _s.CriarRegistroAsync(form.ParaRequest(), form.Foto.ParaArquivo(), ct)));
    }

    [HttpDelete("registros/{id:long}")]
    public async Task<IActionResult> ExcluirRegistro(long id, CancellationToken ct) { await _s.ExcluirRegistroAsync(id, ct); return NoContent(); }
}

public static class ProjecaoBio
{
    public static object Especie(Especie e) => new
    {
        e.Id, e.NomePopular, e.NomeCientifico, Reino = e.Reino.ToString(), e.Grupo, e.StatusConservacao, e.Descricao, e.FotoUrl,
    };

    public static object Registro(RegistroBiodiversidade r) => new
    {
        r.Id, r.EspecieId, Especie = r.Especie?.NomePopular, NomeCientifico = r.Especie?.NomeCientifico,
        Reino = r.Especie?.Reino.ToString(), Grupo = r.Especie?.Grupo, StatusConservacao = r.Especie?.StatusConservacao,
        EspecieFotoUrl = r.Especie?.FotoUrl,
        r.PropriedadeId, PropriedadeNome = r.Propriedade?.Nome, r.DataHora, Origem = r.Origem.ToString(), r.Camera,
        r.Latitude, r.Longitude, r.Area, r.Quantidade, r.Observacao, r.FotoUrl, r.RegistradoPor, r.Confirmado,
    };

    public static object Painel(PainelFaunaFloraDto p) => new
    {
        p.Indicadores,
        DestaquesHoje = p.DestaquesHoje.Select(Registro),
        Cameras = p.Cameras.Select(Registro),
        DestaquesFauna = p.DestaquesFauna.Select(Destaque),
        DestaquesFlora = p.DestaquesFlora.Select(Destaque),
        p.Evolucao,
        p.DistribuicaoFauna,
        p.TotalEspeciesFauna,
        RegistrosMapa = p.RegistrosMapa.Select(Registro),
        Propriedades = p.Propriedades.Select(x => new { x.Id, x.Nome, x.GeoJson, x.GeoJsonReservaLegal, x.GeoJsonApp }),
        Especies = p.Especies.Select(Especie),
    };

    private static object Destaque(EspecieDestaque d) => new { Especie = Especie(d.Especie), d.Registros, d.Variacao, d.FotoUrl };
}
