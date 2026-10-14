using AgroCert.Domain.Entidades;

namespace AgroCert.Application.Dtos;

public record IndicadorPainel(string Rotulo, int Valor, string Sub, string Cor, string Icone);

public record PainelDto(
    List<IndicadorPainel> Indicadores,
    List<Certificacao> Certificacoes,
    List<Requisito> AcoesPrioritarias,
    List<ApontamentoDto> PendenciasVistoria);
