using AgroCert.Domain.Entidades;

namespace AgroCert.Application.Interfaces;

public interface ICertificacaoRepositorio
{
    Task<List<Certificacao>> ListarAsync(CancellationToken ct = default);
    Task<Certificacao?> ObterAsync(long id, CancellationToken ct = default);
    Task AdicionarAsync(Certificacao certificacao, CancellationToken ct = default);
    Task ExcluirAsync(long id, CancellationToken ct = default);
    Task<int> ContarAsync(CancellationToken ct = default);
    Task SalvarAsync(CancellationToken ct = default);
}

public interface IRequisitoRepositorio
{
    Task<List<Requisito>> ListarAsync(CancellationToken ct = default);
    Task<Requisito?> ObterAsync(long id, CancellationToken ct = default);
    Task AdicionarAsync(Requisito requisito, CancellationToken ct = default);
    Task AdicionarVariosAsync(IEnumerable<Requisito> requisitos, CancellationToken ct = default);
    Task ExcluirAsync(long id, CancellationToken ct = default);
    Task<int> ContarAsync(CancellationToken ct = default);
    Task SalvarAsync(CancellationToken ct = default);
}

public interface IVistoriaRepositorio
{
    Task<List<Vistoria>> ListarAsync(CancellationToken ct = default);
    Task<Vistoria?> ObterAsync(long id, CancellationToken ct = default);
    Task<List<string>> ListarCodigosDocumentoAsync(CancellationToken ct = default);
    Task AdicionarAsync(Vistoria vistoria, CancellationToken ct = default);
    Task ExcluirAsync(long id, CancellationToken ct = default);

    Task<Apontamento?> ObterApontamentoAsync(long id, CancellationToken ct = default);
    Task AdicionarApontamentoAsync(Apontamento apontamento, CancellationToken ct = default);
    Task ExcluirApontamentoAsync(long id, CancellationToken ct = default);
    Task<List<Apontamento>> ListarApontamentosPorStatusAsync(Domain.Enums.StatusApontamento status, CancellationToken ct = default);

    Task SalvarAsync(CancellationToken ct = default);
}

public interface IAmbientalRepositorio
{
    Task<List<Propriedade>> ListarPropriedadesAsync(CancellationToken ct = default);
    Task<Propriedade?> ObterPropriedadeAsync(long id, CancellationToken ct = default);
    Task<Propriedade?> ObterPropriedadePorCarAsync(string carCodigo, CancellationToken ct = default);
    Task AdicionarPropriedadeAsync(Propriedade propriedade, CancellationToken ct = default);
    Task ExcluirPropriedadeAsync(long id, CancellationToken ct = default);

    Task<bool> ExisteAlertaAsync(long propriedadeId, string? codigoAlerta, string fonte, CancellationToken ct = default);
    Task AdicionarAlertaAsync(AlertaDesmatamento alerta, CancellationToken ct = default);
    Task<List<AlertaDesmatamento>> ListarAlertasAsync(long propriedadeId, CancellationToken ct = default);

    Task<List<LeituraNdvi>> ListarNdviAsync(long propriedadeId, CancellationToken ct = default);
    Task<LeituraNdvi?> ObterNdviAsync(long propriedadeId, DateOnly dataImagem, CancellationToken ct = default);
    Task AdicionarNdviAsync(LeituraNdvi leitura, CancellationToken ct = default);

    Task SalvarAsync(CancellationToken ct = default);
}

public interface IFaunaFloraRepositorio
{
    Task<List<Especie>> ListarEspeciesAsync(CancellationToken ct = default);
    Task<Especie?> ObterEspecieAsync(long id, CancellationToken ct = default);
    Task AdicionarEspecieAsync(Especie especie, CancellationToken ct = default);
    Task ExcluirEspecieAsync(long id, CancellationToken ct = default);

    Task<List<RegistroBiodiversidade>> ListarRegistrosAsync(CancellationToken ct = default);
    Task<RegistroBiodiversidade?> ObterRegistroAsync(long id, CancellationToken ct = default);
    Task AdicionarRegistroAsync(RegistroBiodiversidade registro, CancellationToken ct = default);
    Task ExcluirRegistroAsync(long id, CancellationToken ct = default);

    Task SalvarAsync(CancellationToken ct = default);
}
