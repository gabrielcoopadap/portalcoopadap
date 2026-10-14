using AgroCert.Application.Dtos;

namespace AgroCert.Application.Interfaces;

public sealed record ArquivoEnviado(string NomeArquivo, Stream Conteudo, long Tamanho);

public interface IArmazenamentoArquivos
{
    Task<string?> SalvarAsync(ArquivoEnviado? arquivo, string subpasta, CancellationToken ct = default);
    Task<string?> SalvarBytesAsync(byte[]? conteudo, string subpasta, string extensao, CancellationToken ct = default);
    Task<byte[]?> LerAsync(string url, CancellationToken ct = default);
}

public interface ISicarCliente
{
    Task<DadosImovelCar> ConsultarPorCodigoCarAsync(string carCodigo, CancellationToken ct = default);
}

public interface IMapBiomasCliente
{
    Task<List<AlertaExterno>> ConsultarAlertasPorCarAsync(string carCodigo, DateOnly? desde, DateOnly? ate, CancellationToken ct = default);
}

public interface ICopernicusCliente
{
    Task<List<PontoNdvi>> ConsultarSerieNdviAsync(string geoJsonPoligono, DateOnly desde, DateOnly ate, string intervalo, CancellationToken ct = default);
    Task<byte[]> GerarImagemCorRealAsync(string geoJsonPoligono, DateOnly desde, DateOnly ate, CancellationToken ct = default);
    Task<byte[]> GerarImagemMapaCalorNdviAsync(string geoJsonPoligono, DateOnly desde, DateOnly ate, CancellationToken ct = default);

    bool Configurado { get; }

    Task<byte[]> GerarImagemRetanguloAsync(double[] caixa, DateOnly desde, DateOnly ate, string tipo, int largura, int altura, CancellationToken ct = default);
}

public interface IRelatorioVistoriaPdf
{
    Task<byte[]> GerarAsync(VistoriaDto vistoria, CancellationToken ct = default);
}

public interface IRelatorioAmbientalPdf
{
    Task<byte[]> GerarAsync(RelatorioAmbientalDto relatorio, CancellationToken ct = default);
}

public interface IExportadorListaMestra
{
    byte[] Gerar(IReadOnlyList<Domain.Entidades.Requisito> requisitos, string? certificacao);
}
