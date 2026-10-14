using AgroCert.Application.Util;

namespace AgroCert.Application.Interfaces;

public sealed record RecorteCobertura(int Ano, byte[] Classes, int Largura, int Altura, double X0, double Y0, double Dx, double Dy);

public interface ICoberturaMapBiomas
{
    IReadOnlyList<int> Anos { get; }
    string Pasta { get; }
    double[]? Extensao { get; }
    RecorteCobertura? Recortar(int ano, double minLon, double minLat, double maxLon, double maxLat);
}

public sealed record UnidadeConservacao(
    string Nome, string? Categoria, string Grupo, string? Esfera, bool EhZonaAmortecimento,
    List<GeoUtil.Poligono> Poligonos, double[] Caixa, string Arquivo)
{
    public const string ProtecaoIntegral = "Proteção Integral";
    public const string UsoSustentavel = "Uso Sustentável";
    public const string Desconhecido = "Não informado";

    public bool TemZonaAmortecimento
    {
        get
        {
            var t = $"{Categoria} {Nome}".ToLowerInvariant();
            return !(t.Contains("proteção ambiental") || t.Contains("protecao ambiental") || t.StartsWith("apa ") || t.Contains(" apa ")
                     || t.Contains("rppn") || t.Contains("reserva particular"));
        }
    }
}

public interface IUnidadesConservacao
{
    bool TemDados { get; }
    string Pasta { get; }
    int RaioAmortecimentoM { get; }
    IReadOnlyList<string> Fontes { get; }
    IReadOnlyList<string> Avisos { get; }
    IReadOnlyList<UnidadeConservacao> Proximas(double[] caixa, double raioM);
}
