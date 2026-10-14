using AgroCert.Domain.Entidades;
using CertificacaoEntidade = AgroCert.Domain.Entidades.Certificacao;
using Microsoft.AspNetCore.Http;

namespace AgroCert.Certificacao.Modelos;

public class CertificacaoForm
{
    public string Nome { get; set; } = "";
    public string? Sigla { get; set; }
    public string? Escopo { get; set; }
    public string? Status { get; set; }
    public string? Cor { get; set; }
    public int? Conformidade { get; set; }
    public int? TotalRequisitos { get; set; }
    public int? Conformes { get; set; }
    public int? Pendentes { get; set; }
    public int? NaoConformes { get; set; }
    public string? Organismo { get; set; }
    public DateOnly? ValidoAte { get; set; }
    public DateOnly? UltimaAuditoria { get; set; }
    public DateOnly? ProximaAuditoria { get; set; }
    public List<CategoriaConformidade> Categorias { get; set; } = new();

    public CertificacaoEntidade ParaEntidade() => new()
    {
        Nome = Nome ?? "", Sigla = Sigla, Escopo = Escopo, Status = Status, Cor = Cor,
        Conformidade = Conformidade, TotalRequisitos = TotalRequisitos, Conformes = Conformes,
        Pendentes = Pendentes, NaoConformes = NaoConformes, Organismo = Organismo,
        ValidoAte = ValidoAte, UltimaAuditoria = UltimaAuditoria, ProximaAuditoria = ProximaAuditoria,
        Categorias = Categorias ?? new(),
    };
}

public class RequisitoForm
{
    public string? Titulo { get; set; }
    public string? Descricao { get; set; }
    public string? Tema { get; set; }
    public string? Prioridade { get; set; }
    public string? Status { get; set; }
    public string? Responsavel { get; set; }
    public string? Periodicidade { get; set; }
    public DateOnly? Prazo { get; set; }
    public string? Observacao { get; set; }
    public List<Referencia> Referencias { get; set; } = new();

    public Requisito ParaEntidade() => new()
    {
        Titulo = Titulo, Descricao = Descricao, Tema = Tema, Prioridade = Prioridade, Status = Status,
        Responsavel = Responsavel, Periodicidade = Periodicidade, Prazo = Prazo, Observacao = Observacao,
        Referencias = Referencias ?? new(),
    };
}

public class ApontamentoForm : Application.Dtos.NovoApontamentoRequest
{
    public IFormFile? ImagemEncontrada { get; set; }
    public IFormFile? ImagemRequerida { get; set; }
}

public class EncerramentoForm
{
    public string? Observacoes { get; set; }
    public IFormFile? Evidencia { get; set; }
}

internal static class ArquivoExt
{
    public static Application.Interfaces.ArquivoEnviado? ParaArquivo(this IFormFile? f) =>
        f is null || f.Length == 0 ? null : new(f.FileName, f.OpenReadStream(), f.Length);
}

public class EspecieForm : Application.Dtos.EspecieRequest
{
    public IFormFile? Foto { get; set; }
}

public class RegistroForm
{
    public long? EspecieId { get; set; }
    public long? PropriedadeId { get; set; }
    public DateTime? DataHora { get; set; }
    public string? Origem { get; set; }
    public string? Camera { get; set; }
    public string? Latitude { get; set; }
    public string? Longitude { get; set; }
    public string? Area { get; set; }
    public int? Quantidade { get; set; }
    public string? Observacao { get; set; }
    public string? RegistradoPor { get; set; }
    public IFormFile? Foto { get; set; }

    public Application.Dtos.NovoRegistroRequest ParaRequest() => new()
    {
        EspecieId = EspecieId, PropriedadeId = PropriedadeId, DataHora = DataHora, Origem = Origem, Camera = Camera,
        Latitude = Coordenada(Latitude, "Latitude"), Longitude = Coordenada(Longitude, "Longitude"),
        Area = Area, Quantidade = Quantidade, Observacao = Observacao, RegistradoPor = RegistradoPor,
    };

    private static double? Coordenada(string? texto, string campo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (double.TryParse(texto.Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v)) return v;
        throw new Domain.Excecoes.RegraNegocioException($"{campo} inválida: {texto}. Use o formato -19.205000.");
    }
}
