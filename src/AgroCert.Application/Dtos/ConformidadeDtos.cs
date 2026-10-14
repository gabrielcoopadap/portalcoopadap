namespace AgroCert.Application.Dtos;

public enum StatusCriterio { Conforme, Verificar, IndicioNaoConformidade, SemDados }

public record CriterioConformidade(string Norma, string Titulo, string Exigencia, StatusCriterio Status, string Resultado, string? Observacao = null);

public record MarcoTemporal(string Chave, string Rotulo, int AnoCorte, int? AnoUsado, bool Aproximado);

public record LinhaAreaCar(string Area, decimal AreaHa, IReadOnlyDictionary<int, decimal> NativaHa);

public record LinhaClasse(int Codigo, string Nome, string Cor, bool Natural, IReadOnlyDictionary<int, decimal> Ha);

public record ImagemAno(int Ano, string? DataUri);

public record JanelaImagem(double[] Caixa, double ProporcaoLarguraAltura, List<ImagemAno> MapBiomas, string ContornoPrincipal, string? ContornoImovel, string? ContornoRl, string? ContornoApp);

public record AreaConversao(
    int Numero, decimal AreaHa, decimal NaReservaLegalHa, decimal NaAppHa,
    int? AnoNatural, int? AnoConvertida, string ClasseAntes, string ClasseDepois,
    string Marco, double Lon, double Lat, JanelaImagem Janela, bool ComSatelite,
    string Periodo = "", IReadOnlyList<int>? AnosNatural = null);

public record ItemLegenda(string Cor, string Texto);

public record UcProxima(string Nome, string? Categoria, string Grupo, string? Esfera, bool EhZonaAmortecimento,
    bool Dentro, decimal? AreaDentroHa, double DistanciaM, StatusCriterio Status, string Situacao);

public record RelatorioConformidadeDto
{
    public long PropriedadeId { get; init; }
    public string Nome { get; init; } = "";
    public string Car { get; init; } = "";
    public string? Municipio { get; init; }
    public string? Uf { get; init; }
    public decimal? AreaHa { get; init; }
    public decimal? AreaReservaLegalHa { get; init; }
    public decimal? AreaAppHa { get; init; }
    public decimal? AreaVegetacaoNativaHa { get; init; }
    public string? GeoJson { get; init; }
    public string? GeoJsonReservaLegal { get; init; }
    public string? GeoJsonApp { get; init; }

    public List<int> AnosMapBiomas { get; init; } = new();
    public List<int> AnosComparados { get; init; } = new();
    public int? AnoAtual { get; init; }
    public List<MarcoTemporal> Marcos { get; init; } = new();
    public List<CriterioConformidade> Criterios { get; init; } = new();

    public List<LinhaAreaCar> PorArea { get; init; } = new();
    public List<LinhaClasse> Classes { get; init; } = new();
    public JanelaImagem? Imovel { get; init; }
    public string? MapaMudanca { get; init; }
    public double[]? MapaMudancaLimites { get; init; }
    public List<ItemLegenda> LegendaMudanca { get; init; } = new();

    public List<AreaConversao> Areas { get; init; } = new();
    public decimal ConversaoTotalHa { get; init; }
    public int ManchasPequenas { get; init; }
    public decimal ManchasPequenasHa { get; init; }
    public decimal RegeneracaoHa { get; init; }

    public bool UcComDados { get; init; }
    public int RaioAmortecimentoM { get; init; }
    public List<UcProxima> Ucs { get; init; } = new();
    public string? UcsGeoJson { get; init; }

    public List<AlertaDto> Alertas { get; init; } = new();
    public List<LeituraNdviDto> Ndvi { get; init; } = new();
    public bool CopernicusConfigurado { get; init; }
    public List<int> AnosSatelite { get; init; } = new();

    public List<string> Avisos { get; init; } = new();
    public List<string> Fontes { get; init; } = new();
    public string PastaMapBiomas { get; init; } = "";
    public string PastaUcs { get; init; } = "";
    public DateTime GeradoEm { get; init; } = DateTime.Now;
}
