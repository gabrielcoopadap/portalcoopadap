using AgroCert.Domain.Entidades;

namespace AgroCert.Application.Dtos;

public record KpiBio(string Rotulo, string Valor, string Icone, decimal? Variacao, string Comparacao, string? Nota = null);

public record EspecieDestaque(Especie Especie, int Registros, decimal? Variacao, string? FotoUrl);

public record PontoMes(DateOnly Mes, int Fauna, int Flora);

public record FatiaGrupo(string Grupo, string Cor, int Quantidade, decimal Percentual);

public record PainelFaunaFloraDto(
    List<KpiBio> Indicadores,
    List<RegistroBiodiversidade> DestaquesHoje,
    List<RegistroBiodiversidade> Cameras,
    List<EspecieDestaque> DestaquesFauna,
    List<EspecieDestaque> DestaquesFlora,
    List<PontoMes> Evolucao,
    List<FatiaGrupo> DistribuicaoFauna,
    int TotalEspeciesFauna,
    List<RegistroBiodiversidade> RegistrosMapa,
    List<Propriedade> Propriedades,
    List<Especie> Especies);

public class NovoRegistroRequest
{
    public long? EspecieId { get; set; }
    public long? PropriedadeId { get; set; }
    public DateTime? DataHora { get; set; }
    public string? Origem { get; set; }
    public string? Camera { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Area { get; set; }
    public int? Quantidade { get; set; }
    public string? Observacao { get; set; }
    public string? RegistradoPor { get; set; }
}

public class EspecieRequest
{
    public string? NomePopular { get; set; }
    public string? NomeCientifico { get; set; }
    public string? Reino { get; set; }
    public string? Grupo { get; set; }
    public string? StatusConservacao { get; set; }
    public string? Descricao { get; set; }
}
