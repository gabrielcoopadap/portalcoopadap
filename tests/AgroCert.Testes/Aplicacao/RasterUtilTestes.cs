using AgroCert.Application.Util;

namespace AgroCert.Testes.Aplicacao;

public class RasterUtilTestes
{
    private static GeoUtil.Poligono Ret(double x0, double y0, double x1, double y1) =>
        new(new() { new[] { (x0, y0), (x1, y0), (x1, y1), (x0, y1), (x0, y0) } });

    private static bool[] Grade(params string[] linhas) =>
        linhas.SelectMany(l => l.Select(c => c == '#')).ToArray();

    [Fact]
    public void Mascara_MarcaPixelsComCentroDentroDoPoligono()
    {
        var m = RasterUtil.Mascara(new[] { Ret(2, 2, 5, 6) }, 0, 10, 1, 1, 10, 10);

        Assert.Equal(12, m.Count(x => x));
        // linha 4 tem centro em y = 5,5; colunas 2..4 têm centro em x = 2,5..4,5
        Assert.True(m[4 * 10 + 2]);
        Assert.True(m[4 * 10 + 4]);
        Assert.False(m[4 * 10 + 5]);
        Assert.False(m[3 * 10 + 2]);
    }

    [Fact]
    public void Mascara_RespeitaFuro()
    {
        var comFuro = new GeoUtil.Poligono(new()
        {
            new[] { (0.0, 0.0), (10.0, 0.0), (10.0, 10.0), (0.0, 10.0), (0.0, 0.0) },
            new[] { (4.0, 4.0), (6.0, 4.0), (6.0, 6.0), (4.0, 6.0), (4.0, 4.0) },
        });

        var m = RasterUtil.Mascara(new[] { comFuro }, 0, 10, 1, 1, 10, 10);

        Assert.Equal(96, m.Count(x => x));
        Assert.False(m[5 * 10 + 5]);
    }

    [Fact]
    public void Mascara_PoligonoMaiorQueAGrade_FicaRecortado()
    {
        var m = RasterUtil.Mascara(new[] { Ret(-100, -100, 100, 100) }, 0, 4, 1, 1, 4, 4);
        Assert.All(m, x => Assert.True(x));
    }

    [Fact]
    public void Rotular_ContaManchasComVizinhancaDe8()
    {
        var m = Grade(
            "##...",
            "##..#",
            "....#",
            ".#...",
            "#...#");

        var (rot, n) = RasterUtil.Rotular(m, 5, 5);

        Assert.Equal(4, n);
        Assert.Equal(rot[0], rot[6]);
        Assert.Equal(rot[16], rot[20]);
        Assert.NotEqual(rot[0], rot[9]);
        Assert.Equal(0, rot[2]);
    }

    [Fact]
    public void Rotular_FormatoEmU_UneOsDoisBracos()
    {
        var m = Grade(
            "#.#",
            "#.#",
            "###");

        var (rot, n) = RasterUtil.Rotular(m, 3, 3);

        Assert.Equal(1, n);
        Assert.All(rot.Where((_, i) => m[i]), r => Assert.Equal(1, r));
    }

    [Fact]
    public void Rotular_GradeVazia_NaoTemManchas()
    {
        var (rot, n) = RasterUtil.Rotular(new bool[9], 3, 3);
        Assert.Equal(0, n);
        Assert.All(rot, r => Assert.Equal(0, r));
    }

    [Fact]
    public void Png_TemAssinaturaEDimensoesAmpliadas()
    {
        var png = RasterUtil.Png(new byte[] { 0, 1, 1, 0 }, 2, 2, new[] { ((byte)0, (byte)0, (byte)0, (byte)0), ((byte)255, (byte)0, (byte)0, (byte)255) }, escala: 3);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8).ToArray());
        Assert.Equal(6, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16)));
        Assert.Equal(6, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20)));
        Assert.StartsWith("data:image/png;base64,", RasterUtil.DataUri(png));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(12)]
    [InlineData(5)]
    public void Legenda_ClassesNaturais(int codigo)
    {
        Assert.True(LegendaMapBiomas.Natural((byte)codigo));
        Assert.False(LegendaMapBiomas.Antropico((byte)codigo));
    }

    [Theory]
    [InlineData(15)]
    [InlineData(9)]
    [InlineData(39)]
    [InlineData(46)]
    [InlineData(21)]
    [InlineData(24)]
    public void Legenda_ClassesAntropicas(int codigo)
    {
        Assert.True(LegendaMapBiomas.Antropico((byte)codigo));
        Assert.False(LegendaMapBiomas.Natural((byte)codigo));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(33)]
    [InlineData(27)]
    [InlineData(200)]
    public void Legenda_NeutrasOuDesconhecidas_NaoSaoNaturaisNemAntropicas(int codigo)
    {
        Assert.False(LegendaMapBiomas.Natural((byte)codigo));
        Assert.False(LegendaMapBiomas.Antropico((byte)codigo));
    }

    [Theory]
    [InlineData(3, "Formação Florestal")]
    [InlineData(15, "Pastagem")]
    [InlineData(46, "Café")]
    [InlineData(0, "Sem dado")]
    [InlineData(99, "Classe 99")]
    [InlineData(-1, "Classe -1")]
    public void Legenda_Nome(int codigo, string esperado)
    {
        Assert.Equal(esperado, LegendaMapBiomas.Nome(codigo));
    }

    [Fact]
    public void Legenda_PaletaIndexadaPeloCodigo()
    {
        Assert.Equal(256, LegendaMapBiomas.Paleta.Count);
        Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)0), LegendaMapBiomas.Paleta[0]);
        Assert.Equal(((byte)0x1f, (byte)0x8d, (byte)0x49, (byte)255), LegendaMapBiomas.Paleta[3]);
    }
}
