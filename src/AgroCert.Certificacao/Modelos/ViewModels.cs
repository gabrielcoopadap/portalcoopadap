using AgroCert.Application.Dtos;
using AgroCert.Domain.Entidades;

namespace AgroCert.Certificacao.Modelos;

public class RequisitosViewModel
{
    public List<Requisito> Todos { get; set; } = new();
    public List<Requisito> Filtrados { get; set; } = new();
    public List<string> Temas { get; set; } = new();
    public List<string> Normas { get; set; } = new();
    public string? Busca { get; set; }
    public string? Tema { get; set; }
    public string? Norma { get; set; }
    public string? Status { get; set; }
    public string? Prioridade { get; set; }
    public int Conformes => Todos.Count(r => r.Status == "Conforme");
}

public class VistoriasViewModel
{
    public List<VistoriaDto> Todas { get; set; } = new();
    public List<VistoriaDto> Filtradas { get; set; } = new();
    public int Pendencias { get; set; }
    public string Aba { get; set; } = "TODAS";
}

public class VistoriaDetalheViewModel
{
    public VistoriaDto Vistoria { get; set; } = null!;
    public List<Requisito> Requisitos { get; set; } = new();
}

public class AmbientalDetalheViewModel
{
    public RelatorioAmbientalDto Relatorio { get; set; } = null!;
    public List<LeituraNdviDto> Leituras { get; set; } = new();
    public LeituraNdviDto? LeituraSelecionada { get; set; }
    public LeituraNdviDto? LeituraAnterior { get; set; }
}

public class ModalRegistroViewModel
{
    public List<Especie> Especies { get; set; } = new();
    public List<Propriedade> Propriedades { get; set; } = new();
    public string Voltar { get; set; } = "/certificacao/fauna-flora";
}

public class EspeciesViewModel
{
    public List<Especie> Especies { get; set; } = new();
    public Dictionary<long, (int Total, DateTime? Ultimo)> Registros { get; set; } = new();
    public string? Reino { get; set; }
    public string? Grupo { get; set; }
    public string? Busca { get; set; }
    public string? Status { get; set; }
    public ModalRegistroViewModel Modal { get; set; } = new();
}

public class RegistrosViewModel
{
    public List<RegistroBiodiversidade> Registros { get; set; } = new();
    public int Total { get; set; }
    public string? Reino { get; set; }
    public string? Origem { get; set; }
    public long? EspecieId { get; set; }
    public int Dias { get; set; }
    public ModalRegistroViewModel Modal { get; set; } = new();
}
