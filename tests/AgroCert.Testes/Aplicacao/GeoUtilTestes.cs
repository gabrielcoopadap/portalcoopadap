using AgroCert.Application.Util;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Testes.Aplicacao;

public class GeoUtilTestes
{
    private const string Quadrado = """{"type":"Polygon","coordinates":[[[0,0],[10,0],[10,10],[0,10],[0,0]]]}""";
    private const string QuadradoComFuro = """{"type":"Polygon","coordinates":[[[0,0],[10,0],[10,10],[0,10],[0,0]],[[4,4],[6,4],[6,6],[4,6],[4,4]]]}""";

    private static string Retangulo(double x0, double y0, double x1, double y1) =>
        GeoUtil.EscreverMultiPolygon(new[] { new GeoUtil.Poligono(new() { new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1), (x0, y0) } }) });

    [Fact]
    public void LerGeoJson_Polygon_LeAnelExternoEFuros()
    {
        var p = Assert.Single(GeoUtil.LerGeoJson(QuadradoComFuro));
        Assert.Equal(2, p.Aneis.Count);
        Assert.Equal(5, p.Aneis[0].Length);
        Assert.Equal((10.0, 10.0), p.Aneis[0][2]);
    }

    [Fact]
    public void LerGeoJson_FeatureCollection_JuntaTodasAsFeatures()
    {
        const string fc = """
            {"type":"FeatureCollection","features":[
              {"type":"Feature","properties":{},"geometry":{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,0]]]}},
              {"type":"Feature","properties":{},"geometry":{"type":"MultiPolygon","coordinates":[[[[5,5],[6,5],[6,6],[5,5]]],[[[8,8],[9,8],[9,9],[8,8]]]]}},
              {"type":"Feature","properties":{},"geometry":{"type":"Point","coordinates":[3,3]}}
            ]}
            """;
        Assert.Equal(3, GeoUtil.LerGeoJson(fc).Count);
    }

    [Fact]
    public void LerGeoJson_FeatureEGeometryCollection()
    {
        const string feature = """{"type":"Feature","geometry":{"type":"GeometryCollection","geometries":[{"type":"Polygon","coordinates":[[[0,0],[1,0],[1,1],[0,0]]]},{"type":"LineString","coordinates":[[0,0],[1,1]]}]}}""";
        Assert.Single(GeoUtil.LerGeoJson(feature));
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"type":"FeatureCollection","features":[]}""")]
    [InlineData("""{"type":"Feature","geometry":null}""")]
    public void LerGeoJson_Invalido_LancaRegraNegocio(string texto)
    {
        Assert.Throws<RegraNegocioException>(() => GeoUtil.LerGeoJson(texto));
    }

    [Fact]
    public void LerWkt_PolygonComFuroEMultiPolygonComSrid()
    {
        var p = Assert.Single(GeoUtil.LerWkt("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0), (4 4, 6 4, 6 6, 4 6, 4 4))"));
        Assert.Equal(2, p.Aneis.Count);
        Assert.Equal((6.0, 4.0), p.Aneis[1][1]);

        var multi = GeoUtil.LerWkt("SRID=4326;MULTIPOLYGON(((-47.5 -19.25,-47.4 -19.25,-47.4 -19.2,-47.5 -19.25)),((1 1,2 1,2 2,1 1)))");
        Assert.Equal(2, multi.Count);
        Assert.Equal((-47.5, -19.25), multi[0].Aneis[0][0]);
    }

    [Theory]
    [InlineData("POINT (1 2)")]
    [InlineData("POLYGON")]
    public void LerWkt_NaoSuportado_LancaFormatException(string wkt)
    {
        Assert.Throws<FormatException>(() => GeoUtil.LerWkt(wkt));
    }

    [Fact]
    public void EscreverMultiPolygon_IdaEVoltaPreservaCoordenadas()
    {
        var original = GeoUtil.LerGeoJson(QuadradoComFuro);
        var json = GeoUtil.EscreverMultiPolygon(original);

        Assert.Contains("\"type\":\"MultiPolygon\"", json);
        var relido = Assert.Single(GeoUtil.LerGeoJson(json));
        Assert.Equal(original[0].Aneis[0], relido.Aneis[0]);
        Assert.Equal(original[0].Aneis[1], relido.Aneis[1]);
    }

    [Fact]
    public void NormalizarParaMultiPolygon_SemPoligono_LancaRegraNegocio()
    {
        Assert.Throws<RegraNegocioException>(() => GeoUtil.NormalizarParaMultiPolygon("""{"type":"Point","coordinates":[1,2]}"""));
    }

    [Fact]
    public void BoundingBox_DevolveMinimosEMaximos()
    {
        Assert.Equal(new[] { -47.5, -19.3, -47.1, -19.0 }, GeoUtil.BoundingBox(Retangulo(-47.5, -19.3, -47.1, -19.0)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("lixo")]
    public void BoundingBox_EntradaInvalida_DevolveNull(string geo)
    {
        Assert.Null(GeoUtil.BoundingBox(geo));
        Assert.Null(GeoUtil.BoundingBox((string?)null));
    }

    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(2, 2, true)]
    [InlineData(-1, 5, false)]
    [InlineData(11, 11, false)]
    public void Contem_PontoNoQuadrado(double x, double y, bool esperado)
    {
        Assert.Equal(esperado, GeoUtil.Contem(GeoUtil.LerGeoJson(Quadrado)[0], (x, y)));
    }

    [Fact]
    public void Contem_PontoNoFuro_EstaFora()
    {
        var p = GeoUtil.LerGeoJson(QuadradoComFuro)[0];
        Assert.False(GeoUtil.Contem(p, (5, 5)));
        Assert.True(GeoUtil.Contem(p, (3, 5)));
    }

    [Theory]
    [InlineData(5, 5, 15, 15, true)]
    [InlineData(2, 2, 3, 3, true)]
    [InlineData(-5, -5, 20, 20, true)]
    [InlineData(10, 0, 12, 5, true)]
    [InlineData(11, 11, 12, 12, false)]
    [InlineData(-5, 4, -1, 6, false)]
    public void Intersecta_Retangulos(double x0, double y0, double x1, double y1, bool esperado)
    {
        Assert.Equal(esperado, GeoUtil.Intersecta(Quadrado, Retangulo(x0, y0, x1, y1)));
    }

    [Fact]
    public void Intersecta_ForaDaCaixaMasNoMesmoBbox_NaoIntersecta()
    {
        const string triangulo = """{"type":"Polygon","coordinates":[[[0,0],[10,0],[0,10],[0,0]]]}""";
        Assert.False(GeoUtil.Intersecta(triangulo, Retangulo(8, 8, 9, 9)));
    }

    [Fact]
    public void Intersecta_EntradaVaziaOuInvalida_EhFalso()
    {
        Assert.False(GeoUtil.Intersecta(null, Quadrado));
        Assert.False(GeoUtil.Intersecta(Quadrado, "lixo"));
    }

    [Fact]
    public void AreaHectares_QuadradoDeUmKmPerto_DeLat19Sul_DaCercaDe100Ha()
    {
        const double lat = -19.0, lon = -47.0;
        var dLat = 1000 / 110_574.0;
        var dLon = 1000 / (111_320.0 * Math.Cos(lat * Math.PI / 180));

        var ha = GeoUtil.AreaHectares(Retangulo(lon, lat, lon + dLon, lat + dLat));

        Assert.NotNull(ha);
        Assert.InRange(ha!.Value, 99.5m, 100.5m);
    }

    [Fact]
    public void AreaHectares_DescontaFuro()
    {
        var cheio = GeoUtil.AreaHectares("""{"type":"Polygon","coordinates":[[[-47,-19],[-46.99,-19],[-46.99,-18.99],[-47,-18.99],[-47,-19]]]}""")!.Value;
        var comFuro = GeoUtil.AreaHectares("""{"type":"Polygon","coordinates":[[[-47,-19],[-46.99,-19],[-46.99,-18.99],[-47,-18.99],[-47,-19]],[[-46.9975,-18.9975],[-46.9925,-18.9975],[-46.9925,-18.9925],[-46.9975,-18.9925],[-46.9975,-18.9975]]]}""")!.Value;

        Assert.InRange(comFuro, cheio * 0.75m - 0.05m, cheio * 0.75m + 0.05m);
    }

    [Fact]
    public void AreaHectares_Invalido_DevolveNull()
    {
        Assert.Null(GeoUtil.AreaHectares(null));
        Assert.Null(GeoUtil.AreaHectares("lixo"));
    }

    [Fact]
    public void Centro_EhOMeioDoBbox()
    {
        var c = GeoUtil.Centro(Retangulo(-48, -20, -46, -18));
        Assert.NotNull(c);
        Assert.Equal(-19.0, c!.Value.Lat, 9);
        Assert.Equal(-47.0, c.Value.Lon, 9);
        Assert.Null(GeoUtil.Centro(null));
    }
}
