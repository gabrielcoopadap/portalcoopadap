namespace AgroCert.Infrastructure.Integracoes;

public class OpcoesAgroCert
{
    public const string Secao = "AgroCert";

    public OpcoesUploads Uploads { get; set; } = new();
    public OpcoesAmbiental Ambiental { get; set; } = new();
    public OpcoesRelatorios Relatorios { get; set; } = new();
}

public class OpcoesUploads
{
    public string Diretorio { get; set; } = "uploads";
    public int TamanhoMaximoMb { get; set; } = 15;
}

public class OpcoesRelatorios
{
    public string Fonte { get; set; } = "Liberation Sans";
}

public class OpcoesAmbiental
{
    public OpcoesMapBiomas MapBiomas { get; set; } = new();
    public OpcoesSicar Sicar { get; set; } = new();
    public OpcoesCopernicus Copernicus { get; set; } = new();
    public OpcoesCoberturaMapBiomas CoberturaMapBiomas { get; set; } = new();
    public OpcoesUnidadesConservacao UnidadesConservacao { get; set; } = new();
}

public class OpcoesCoberturaMapBiomas
{
    public string Pasta { get; set; } = "../../dados-corredores";
}

public class OpcoesUnidadesConservacao
{
    public string Pasta { get; set; } = "../../dados-ambiental/ucs";
    public int RaioAmortecimentoM { get; set; } = 3000;
    public double[]? Regiao { get; set; }
}

public class OpcoesMapBiomas
{
    public string BaseUrl { get; set; } = "https://plataforma.alerta.mapbiomas.org/api/v2/graphql";
    public string? Email { get; set; }
    public string? Senha { get; set; }
}

public class OpcoesSicar
{
    public string BaseUrl { get; set; } = "https://api.car.gov.br";
    public string? ApiKey { get; set; }
}

public class OpcoesCopernicus
{
    public string OAuthTokenUrl { get; set; } = "https://identity.dataspace.copernicus.eu/auth/realms/CDSE/protocol/openid-connect/token";
    public string StatisticsUrl { get; set; } = "https://sh.dataspace.copernicus.eu/api/v1/statistics";
    public string ProcessUrl { get; set; } = "https://sh.dataspace.copernicus.eu/api/v1/process";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
