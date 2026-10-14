using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using AgroCert.Domain.Entidades;
using AgroCert.Domain.Excecoes;
using Microsoft.Extensions.Logging;

namespace AgroCert.Application.Servicos;

public class MonitoramentoAmbientalServico
{
    public static readonly DateOnly DesdePadraoAlertas = new(2019, 1, 1);

    private readonly IAmbientalRepositorio _repo;
    private readonly ISicarCliente _sicar;
    private readonly IMapBiomasCliente _mapBiomas;
    private readonly ICopernicusCliente _copernicus;
    private readonly IArmazenamentoArquivos _arquivos;
    private readonly IRelatorioAmbientalPdf _pdf;
    private readonly ILogger<MonitoramentoAmbientalServico> _log;

    public MonitoramentoAmbientalServico(IAmbientalRepositorio repo, ISicarCliente sicar, IMapBiomasCliente mapBiomas,
        ICopernicusCliente copernicus, IArmazenamentoArquivos arquivos, IRelatorioAmbientalPdf pdf,
        ILogger<MonitoramentoAmbientalServico> log)
    {
        _repo = repo;
        _sicar = sicar;
        _mapBiomas = mapBiomas;
        _copernicus = copernicus;
        _arquivos = arquivos;
        _pdf = pdf;
        _log = log;
    }

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);

    public async Task<List<PropriedadeDto>> ListarPropriedadesAsync(CancellationToken ct = default) =>
        (await _repo.ListarPropriedadesAsync(ct)).OrderBy(p => p.Nome).Select(PropriedadeDto.De).ToList();

    public async Task<PropriedadeDto> ObterPropriedadeAsync(long id, CancellationToken ct = default) =>
        PropriedadeDto.De(await ObterEntidadeAsync(id, ct));

    private async Task<Propriedade> ObterEntidadeAsync(long id, CancellationToken ct) =>
        await _repo.ObterPropriedadeAsync(id, ct) ?? throw new NaoEncontradoException("Propriedade não encontrada: " + id);

    public async Task<PropriedadeDto> CadastrarPorCarAsync(CadastrarPropriedadeRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Nome)) throw new RegraNegocioException("Informe o nome da propriedade.");
        if (string.IsNullOrWhiteSpace(req.CarCodigo)) throw new RegraNegocioException("Informe o código do CAR.");
        var car = req.CarCodigo.Trim();

        var propriedade = await _repo.ObterPropriedadePorCarAsync(car, ct);
        var nova = propriedade is null;
        propriedade ??= new Propriedade();
        propriedade.Nome = req.Nome.Trim();
        propriedade.CarCodigo = car;
        propriedade.AtualizadoEm = DateTime.Now;

        if (!string.IsNullOrWhiteSpace(req.GeoJson))
        {
            propriedade.GeoJson = GeoUtil.NormalizarParaMultiPolygon(req.GeoJson);
            propriedade.FonteGeometria = "MANUAL";
            // sem o SICAR não vem a área oficial: calcula uma aproximada a partir do polígono colado
            propriedade.AreaHa = GeoUtil.AreaHectares(propriedade.GeoJson);
        }
        else
        {
            var dados = await _sicar.ConsultarPorCodigoCarAsync(car, ct);
            propriedade.AreaHa = dados.AreaHa;
            propriedade.AreaReservaLegalHa = dados.AreaReservaLegalHa;
            propriedade.AreaAppHa = dados.AreaAppHa;
            propriedade.AreaVegetacaoNativaHa = dados.AreaVegetacaoNativaHa;
            propriedade.Municipio = dados.Municipio;
            propriedade.Uf = dados.Uf;
            propriedade.GeoJson = dados.GeoJson;
            propriedade.GeoJsonReservaLegal = dados.GeoJsonReservaLegal;
            propriedade.GeoJsonApp = dados.GeoJsonApp;
            propriedade.FonteGeometria = "SICAR";
        }

        if (nova) await _repo.AdicionarPropriedadeAsync(propriedade, ct);
        await _repo.SalvarAsync(ct);
        return PropriedadeDto.De(propriedade);
    }

    public async Task ExcluirPropriedadeAsync(long id, CancellationToken ct = default)
    {
        await _repo.ExcluirPropriedadeAsync(id, ct);
        await _repo.SalvarAsync(ct);
    }

    public async Task<List<AlertaDto>> SincronizarAlertasAsync(long propriedadeId, DateOnly? desde, CancellationToken ct = default)
    {
        var propriedade = await ObterEntidadeAsync(propriedadeId, ct);
        var externos = await _mapBiomas.ConsultarAlertasPorCarAsync(propriedade.CarCodigo, desde ?? DesdePadraoAlertas, Hoje, ct);

        foreach (var e in externos)
        {
            if (await _repo.ExisteAlertaAsync(propriedadeId, e.CodigoAlerta, e.Fonte, ct)) continue;
            await _repo.AdicionarAlertaAsync(new AlertaDesmatamento
            {
                Propriedade = propriedade,
                PropriedadeId = propriedade.Id,
                CodigoAlerta = e.CodigoAlerta,
                Fonte = e.Fonte,
                DataDeteccao = e.DataDeteccao,
                AreaHa = e.AreaHa,
                ClasseDesmatamento = e.Classe,
                ImagemAntesUrl = e.ImagemAntesUrl,
                ImagemDepoisUrl = e.ImagemDepoisUrl,
                GeoJson = e.GeoJson,
                DentroReservaLegal = GeoUtil.Intersecta(e.GeoJson, propriedade.GeoJsonReservaLegal),
                DentroApp = GeoUtil.Intersecta(e.GeoJson, propriedade.GeoJsonApp),
            }, ct);
        }
        await _repo.SalvarAsync(ct);

        return (await AlertasDentroDoCarAsync(propriedade, ct)).Select(AlertaDto.De).ToList();
    }

    private async Task<List<AlertaDesmatamento>> AlertasDentroDoCarAsync(Propriedade p, CancellationToken ct)
    {
        var alertas = await _repo.ListarAlertasAsync(p.Id, ct);
        if (string.IsNullOrWhiteSpace(p.GeoJson)) return new();
        var perimetro = GeoUtil.LerGeoJson(p.GeoJson);
        return alertas.Where(a =>
        {
            if (string.IsNullOrWhiteSpace(a.GeoJson)) return false;
            try { return GeoUtil.Intersecta(GeoUtil.LerGeoJson(a.GeoJson), perimetro); }
            catch { return false; }
        }).ToList();
    }

    public async Task<List<LeituraNdviDto>> ListarNdviAsync(long propriedadeId, CancellationToken ct = default)
    {
        var propriedade = await ObterEntidadeAsync(propriedadeId, ct);
        var bbox = GeoUtil.BoundingBox(propriedade.GeoJson);
        return (await _repo.ListarNdviAsync(propriedadeId, ct)).Select(l => LeituraNdviDto.De(l, bbox)).ToList();
    }

    public async Task<List<LeituraNdviDto>> SincronizarNdviAsync(long propriedadeId, DateOnly? desde, CancellationToken ct = default)
    {
        var propriedade = await ObterEntidadeAsync(propriedadeId, ct);
        if (string.IsNullOrWhiteSpace(propriedade.GeoJson))
            throw new RegraNegocioException(
                "Esta propriedade ainda não tem a geometria do CAR cadastrada — não é possível calcular o NDVI sem o polígono.");

        var inicio = desde ?? Hoje.AddMonths(-12);
        var pontos = await _copernicus.ConsultarSerieNdviAsync(propriedade.GeoJson, inicio, Hoje, "P1M", ct);

        foreach (var ponto in pontos)
        {
            var leitura = await _repo.ObterNdviAsync(propriedadeId, ponto.DataInicio, ct);
            if (leitura is null)
            {
                leitura = new LeituraNdvi { Propriedade = propriedade, PropriedadeId = propriedade.Id, DataImagem = ponto.DataInicio };
                await _repo.AdicionarNdviAsync(leitura, ct);
            }
            leitura.NdviMedio = ponto.NdviMedio;
            leitura.NdviMinimo = ponto.NdviMinimo;
            leitura.NdviMaximo = ponto.NdviMaximo;
            leitura.PercentualAmostrasValidas = ponto.PercentualAmostrasValidas;
            leitura.Amostras = ponto.Amostras;

            if (leitura.ImagemCorRealUrl is null || leitura.ImagemMapaCalorUrl is null)
            {
                var fim = ponto.DataInicio.AddMonths(1).AddDays(-1);
                await GerarESalvarImagensAsync(leitura, propriedade.GeoJson, ponto.DataInicio, fim, ct);
            }
        }
        await _repo.SalvarAsync(ct);
        return await ListarNdviAsync(propriedadeId, ct);
    }

    private async Task GerarESalvarImagensAsync(LeituraNdvi leitura, string geoJson, DateOnly desde, DateOnly ate, CancellationToken ct)
    {
        if (leitura.ImagemCorRealUrl is null)
        {
            try
            {
                var img = await _copernicus.GerarImagemCorRealAsync(geoJson, desde, ate, ct);
                leitura.ImagemCorRealUrl = await _arquivos.SalvarBytesAsync(img, "ndvi-imagens", "png", ct);
            }
            catch (Exception e)
            {
                _log.LogWarning("Copernicus/Sentinel-2: não foi possível gerar a imagem em cor real de {Desde} a {Ate}: {Erro}", desde, ate, e.Message);
            }
        }
        if (leitura.ImagemMapaCalorUrl is null)
        {
            try
            {
                var img = await _copernicus.GerarImagemMapaCalorNdviAsync(geoJson, desde, ate, ct);
                leitura.ImagemMapaCalorUrl = await _arquivos.SalvarBytesAsync(img, "ndvi-imagens", "png", ct);
            }
            catch (Exception e)
            {
                _log.LogWarning("Copernicus/Sentinel-2: não foi possível gerar o mapa de calor de NDVI de {Desde} a {Ate}: {Erro}", desde, ate, e.Message);
            }
        }
    }

    public async Task<RelatorioAmbientalDto> GerarFichaAmbientalAsync(long propriedadeId, CancellationToken ct = default)
    {
        var p = await ObterEntidadeAsync(propriedadeId, ct);
        var alertas = await AlertasDentroDoCarAsync(p, ct);

        var areaTotal = alertas.Sum(a => a.AreaHa ?? 0m);
        var ultimo = alertas.Where(a => a.DataDeteccao.HasValue).Select(a => a.DataDeteccao).Max();
        var situacao = alertas.Count == 0
            ? "Nenhum alerta encontrado dentro do perímetro do CAR"
            : "Existem alertas que requerem verificação";

        return new RelatorioAmbientalDto(
            p.Id, p.Nome, p.CarCodigo, p.AreaHa, p.AreaReservaLegalHa, p.AreaAppHa, p.AreaVegetacaoNativaHa,
            p.GeoJson, p.GeoJsonReservaLegal, p.GeoJsonApp,
            alertas.Count, areaTotal, ultimo, situacao,
            alertas.Select(AlertaDto.De).ToList(),
            new List<string> { "SICAR/CAR", "MapBiomas Alerta" },
            RelatorioAmbientalDto.AvisoPadrao);
    }

    public async Task<(byte[] Pdf, string NomeArquivo)> GerarRelatorioPdfAsync(long propriedadeId, CancellationToken ct = default)
    {
        var ficha = await GerarFichaAmbientalAsync(propriedadeId, ct);
        return (await _pdf.GerarAsync(ficha, ct), $"monitoramento-ambiental-{ficha.CarCodigo}.pdf");
    }
}
