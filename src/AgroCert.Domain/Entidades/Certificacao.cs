namespace AgroCert.Domain.Entidades;

public class Certificacao
{
    public long Id { get; set; }
    public string? Sigla { get; set; }
    public string Nome { get; set; } = "";
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
}

public class CategoriaConformidade
{
    public string? Nome { get; set; }
    public int? Conformes { get; set; }
    public int? Total { get; set; }
}
