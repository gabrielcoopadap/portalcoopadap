using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Servicos;
using AgroCert.Application.Util;
using AgroCert.Domain.Entidades;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgroCert.Testes.Aplicacao;

internal sealed class RepositorioAmbientalFalso : IAmbientalRepositorio
{
    public Dictionary<long, Propriedade> Propriedades { get; } = new();
    public List<AlertaDesmatamento> Alertas { get; } = new();

    public Task<Propriedade?> ObterPropriedadeAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(Propriedades.GetValueOrDefault(id));

    public Task<List<AlertaDesmatamento>> ListarAlertasAsync(long propriedadeId, CancellationToken ct = default) =>
        Task.FromResult(Alertas.Where(a => a.PropriedadeId == propriedadeId).ToList());

    public Task<List<LeituraNdvi>> ListarNdviAsync(long propriedadeId, CancellationToken ct = default) =>
        Task.FromResult(new List<LeituraNdvi>());

    public Task<List<Propriedade>> ListarPropriedadesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Propriedade?> ObterPropriedadePorCarAsync(string carCodigo, CancellationToken ct = default) => throw new NotSupportedException();
    public Task AdicionarPropriedadeAsync(Propriedade propriedade, CancellationToken ct = default) => throw new NotSupportedException();
    public Task ExcluirPropriedadeAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> ExisteAlertaAsync(long propriedadeId, string? codigoAlerta, string fonte, CancellationToken ct = default) => throw new NotSupportedException();
    public Task AdicionarAlertaAsync(AlertaDesmatamento alerta, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<LeituraNdvi?> ObterNdviAsync(long propriedadeId, DateOnly dataImagem, CancellationToken ct = default) => throw new NotSupportedException();
    public Task AdicionarNdviAsync(LeituraNdvi leitura, CancellationToken ct = default) => throw new NotSupportedException();
    public Task SalvarAsync(CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class CoberturaFalsa : ICoberturaMapBiomas
{
    public const double X0 = -47.01, Y0 = -18.98, D = 0.00025;
    public const int Lado = 120;

    public CoberturaFalsa(params int[] anos) => Anos = anos;

    public IReadOnlyList<int> Anos { get; }
    public string Pasta => "/dados/mapbiomas";
    public double[]? Extensao => new[] { X0, Y0 - Lado * D, X0 + Lado * D, Y0 };
    public Func<int, double, double, byte> Classe { get; set; } = (_, _, _) => 15;

    public RecorteCobertura? Recortar(int ano, double minLon, double minLat, double maxLon, double maxLat)
    {
        if (!Anos.Contains(ano)) return null;
        var classes = new byte[Lado * Lado];
        for (var l = 0; l < Lado; l++)
            for (var c = 0; c < Lado; c++)
                classes[l * Lado + c] = Classe(ano, X0 + (c + 0.5) * D, Y0 - (l + 0.5) * D);
        return new RecorteCobertura(ano, classes, Lado, Lado, X0, Y0, D, D);
    }
}

internal sealed class UnidadesConservacaoFalsas : IUnidadesConservacao
{
    public bool TemDados { get; set; }
    public string Pasta => "/dados/ucs";
    public int RaioAmortecimentoM => 3000;
    public IReadOnlyList<string> Fontes => new[] { "cnuc.shp" };
    public IReadOnlyList<string> Avisos => Array.Empty<string>();
    public List<UnidadeConservacao> Lista { get; } = new();
    public IReadOnlyList<UnidadeConservacao> Proximas(double[] caixa, double raioM) => Lista;
}

internal sealed class CopernicusFalso : ICopernicusCliente
{
    public bool Configurado => false;
    public Task<List<PontoNdvi>> ConsultarSerieNdviAsync(string geoJsonPoligono, DateOnly desde, DateOnly ate, string intervalo, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<byte[]> GerarImagemCorRealAsync(string geoJsonPoligono, DateOnly desde, DateOnly ate, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<byte[]> GerarImagemMapaCalorNdviAsync(string geoJsonPoligono, DateOnly desde, DateOnly ate, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<byte[]> GerarImagemRetanguloAsync(double[] caixa, DateOnly desde, DateOnly ate, string tipo, int largura, int altura, CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class SicarFalso : ISicarCliente
{
    public Task<DadosImovelCar> ConsultarPorCodigoCarAsync(string carCodigo, CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class MapBiomasAlertaFalso : IMapBiomasCliente
{
    public Task<List<AlertaExterno>> ConsultarAlertasPorCarAsync(string carCodigo, DateOnly? desde, DateOnly? ate, CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class ArquivosFalsos : IArmazenamentoArquivos
{
    public Task<string?> SalvarAsync(ArquivoEnviado? arquivo, string subpasta, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<string?> SalvarBytesAsync(byte[]? conteudo, string subpasta, string extensao, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<byte[]?> LerAsync(string url, CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class PdfAmbientalFalso : IRelatorioAmbientalPdf
{
    public Task<byte[]> GerarAsync(RelatorioAmbientalDto relatorio, CancellationToken ct = default) => throw new NotSupportedException();
}

internal sealed class Cenario
{
    public RepositorioAmbientalFalso Repo { get; } = new();
    public CoberturaFalsa Cobertura { get; }
    public UnidadesConservacaoFalsas Ucs { get; } = new();

    public Cenario(params int[] anos) => Cobertura = new CoberturaFalsa(anos);

    public ConformidadeAmbientalServico Servico()
    {
        var copernicus = new CopernicusFalso();
        var monitoramento = new MonitoramentoAmbientalServico(Repo, new SicarFalso(), new MapBiomasAlertaFalso(), copernicus,
            new ArquivosFalsos(), new PdfAmbientalFalso(), NullLogger<MonitoramentoAmbientalServico>.Instance);
        return new ConformidadeAmbientalServico(Repo, Cobertura, Ucs, copernicus, monitoramento);
    }

    public static string Retangulo(double x0, double y0, double x1, double y1) =>
        GeoUtil.EscreverMultiPolygon(new[] { new GeoUtil.Poligono(new() { new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1), (x0, y0) } }) });
}
