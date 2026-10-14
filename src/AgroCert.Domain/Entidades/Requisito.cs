namespace AgroCert.Domain.Entidades;

public class Requisito
{
    public long Id { get; set; }
    public string? Codigo { get; set; }
    public string? Titulo { get; set; }
    public string? Descricao { get; set; }
    public string? Tema { get; set; }
    public string? Prioridade { get; set; }
    public string? Status { get; set; }
    public string? Responsavel { get; set; }
    public string? Periodicidade { get; set; }
    public DateOnly? Prazo { get; set; }
    public string? Observacao { get; set; }

    public string? OQueE { get; set; }
    public string? PassoAPasso { get; set; }
    public string? Evidencias { get; set; }
    public string? Autoverificacao { get; set; }
    public string? ErrosComuns { get; set; }
    public string? OndeBuscarAjuda { get; set; }

    public bool TemGuia => !string.IsNullOrWhiteSpace(OQueE) || !string.IsNullOrWhiteSpace(PassoAPasso) || !string.IsNullOrWhiteSpace(Evidencias);

    public static IReadOnlyList<string> Linhas(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? Array.Empty<string>() : texto.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public List<Referencia> Referencias { get; set; } = new();
}

public class Referencia
{
    public string? Norma { get; set; }
    public string? Versao { get; set; }
    public string? Codigo { get; set; }
}
