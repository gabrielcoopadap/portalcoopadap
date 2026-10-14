using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Servicos;
using AgroCert.Application.Util;
using AgroCert.Domain.Entidades;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Testes.Aplicacao;

public class ConformidadeAmbientalServicoTestes
{
    private const byte Floresta = 3, Savana = 4, Pastagem = 15, Soja = 39;
    private const string Titulo2008 = "Supressão após 22/07/2008 só com autorização";
    private const string Titulo2014 = "Sem conversão de ecossistemas naturais desde 01/01/2014";
    private const string Titulo2020 = "Nenhuma supressão após 31/12/2020";

    private static readonly double[] Imovel = { -47.000, -19.000, -46.990, -18.990 };
    private static readonly double[] Mancha = { -46.998, -18.998, -46.995, -18.995 };
    private static readonly double[] Mancha2 = { -46.994, -18.994, -46.991, -18.991 };
    private static readonly double[] ManchaPequena = { -46.9925, -18.9925, -46.992, -18.992 };
    private static readonly double[] ManchaFora = { -47.008, -18.998, -47.004, -18.995 };

    private static bool Em(double[] r, double lon, double lat) => lon > r[0] && lon < r[2] && lat > r[1] && lat < r[3];

    private static Cenario Novo(params int[] anos)
    {
        var c = new Cenario(anos);
        c.Repo.Propriedades[1] = new Propriedade
        {
            Id = 1, Nome = "Fazenda Teste", CarCodigo = "MG-0000000-TESTE",
            GeoJson = Cenario.Retangulo(Imovel[0], Imovel[1], Imovel[2], Imovel[3]),
        };
        return c;
    }

    // Floresta na mancha até o ano informado (inclusive) e pastagem depois; o resto é pastagem em todos os anos.
    private static Func<int, double, double, byte> DesmatadaDepoisDe(int ultimoAnoComFloresta, double[]? mancha = null) =>
        (ano, lon, lat) => Em(mancha ?? Mancha, lon, lat) && ano <= ultimoAnoComFloresta ? Floresta : Pastagem;

    private static CriterioConformidade Criterio(RelatorioConformidadeDto r, string titulo) => Assert.Single(r.Criterios, c => c.Titulo == titulo);

    [Fact]
    public async Task FlorestaDerrubadaDepoisDe2020_IndicioNoRainforestENoAtitudes2020()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Cobertura.Classe = DesmatadaDepoisDe(2020);

        var r = await c.Servico().GerarAsync(1);

        var area = Assert.Single(r.Areas);
        Assert.Equal("2020", area.Marco);
        Assert.Equal("Depois de 2020", area.Periodo);
        Assert.Equal(new[] { 2008, 2014, 2020 }, area.AnosNatural!.ToArray());
        Assert.Equal(2020, area.AnoNatural);
        Assert.Equal(2024, area.AnoConvertida);
        Assert.Equal("Formação Florestal", area.ClasseAntes);
        Assert.Equal("Pastagem", area.ClasseDepois);
        Assert.Equal(1, area.Numero);
        Assert.InRange(area.AreaHa, 10.3m, 10.7m);
        Assert.Equal(-46.9965, area.Lon, 1e-6);
        Assert.Equal(-18.9965, area.Lat, 1e-6);
        Assert.Equal(area.AreaHa, r.ConversaoTotalHa);

        Assert.Equal(StatusCriterio.IndicioNaoConformidade, Criterio(r, Titulo2014).Status);
        Assert.Equal(StatusCriterio.Verificar, Criterio(r, Titulo2008).Status);
        Assert.Equal(StatusCriterio.IndicioNaoConformidade, Criterio(r, Titulo2020).Status);
        Assert.Contains("1 área(s)", Criterio(r, Titulo2020).Resultado);
        Assert.Equal(new[] { 2008, 2014, 2020, 2024 }, r.AnosComparados.ToArray());
        Assert.Equal(2024, r.AnoAtual);
    }

    [Fact]
    public async Task FlorestaDerrubadaEntre2014E2020_SoAtingeRainforestE2008()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Cobertura.Classe = DesmatadaDepoisDe(2014);

        var r = await c.Servico().GerarAsync(1);

        var area = Assert.Single(r.Areas);
        Assert.Equal("2014", area.Marco);
        Assert.Equal("Entre 2014 e 2020", area.Periodo);
        Assert.Equal(2020, area.AnoConvertida);
        Assert.Equal(StatusCriterio.IndicioNaoConformidade, Criterio(r, Titulo2014).Status);
        Assert.Equal(StatusCriterio.Verificar, Criterio(r, Titulo2008).Status);
        Assert.Equal(StatusCriterio.Conforme, Criterio(r, Titulo2020).Status);
    }

    [Fact]
    public async Task FlorestaDerrubadaEntre2008E2014_SoExigeAutorizacaoDe2008()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Cobertura.Classe = DesmatadaDepoisDe(2008);

        var r = await c.Servico().GerarAsync(1);

        var area = Assert.Single(r.Areas);
        Assert.Equal("2008", area.Marco);
        Assert.Equal("Entre 2008 e 2014", area.Periodo);
        Assert.Equal(StatusCriterio.Verificar, Criterio(r, Titulo2008).Status);
        Assert.Equal(StatusCriterio.Conforme, Criterio(r, Titulo2014).Status);
        Assert.Equal(StatusCriterio.Conforme, Criterio(r, Titulo2020).Status);
    }

    [Fact]
    public async Task SavanaConvertidaEmSoja_TambemContaComoConversao()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Cobertura.Classe = (ano, lon, lat) => Em(Mancha, lon, lat) ? (ano <= 2020 ? Savana : Soja) : Pastagem;

        var r = await c.Servico().GerarAsync(1);

        var area = Assert.Single(r.Areas);
        Assert.Equal("Formação Savânica", area.ClasseAntes);
        Assert.Equal("Soja", area.ClasseDepois);
        Assert.Equal(StatusCriterio.IndicioNaoConformidade, Criterio(r, Titulo2020).Status);
    }

    [Fact]
    public async Task SemMudanca_TodosOsMarcosConformes()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Cobertura.Classe = (_, lon, lat) => Em(Mancha, lon, lat) ? Floresta : Pastagem;

        var r = await c.Servico().GerarAsync(1);

        Assert.Empty(r.Areas);
        Assert.Equal(0m, r.ConversaoTotalHa);
        Assert.Equal(0, r.ManchasPequenas);
        Assert.Equal(StatusCriterio.Conforme, Criterio(r, Titulo2008).Status);
        Assert.Equal(StatusCriterio.Conforme, Criterio(r, Titulo2014).Status);
        Assert.Equal(StatusCriterio.Conforme, Criterio(r, Titulo2020).Status);

        var imovel = Assert.Single(r.PorArea, p => p.Area == "Imóvel inteiro");
        Assert.InRange(imovel.AreaHa, 115m, 118m);
        Assert.All(imovel.NativaHa.Values, ha => Assert.InRange(ha, 10.3m, 10.7m));
        Assert.Contains(r.Classes, l => l.Codigo == Floresta && l.Natural);
        Assert.Contains(r.Classes, l => l.Codigo == Pastagem && !l.Natural);
    }

    [Fact]
    public async Task ManchaMenorQueMinPixels_EhIgnoradaNoParecerEAvisada()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Cobertura.Classe = DesmatadaDepoisDe(2020, ManchaPequena);

        var r = await c.Servico().GerarAsync(1);

        Assert.Empty(r.Areas);
        Assert.Equal(1, r.ManchasPequenas);
        Assert.True(r.ManchasPequenasHa > 0);
        Assert.Equal(StatusCriterio.Conforme, Criterio(r, Titulo2020).Status);
        Assert.Contains(r.Avisos, a => a.Contains("menos de 0,4 ha"));
    }

    [Fact]
    public async Task DesmatamentoForaDoImovel_NaoEntraNoParecer()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Cobertura.Classe = DesmatadaDepoisDe(2020, ManchaFora);

        var r = await c.Servico().GerarAsync(1);

        Assert.Empty(r.Areas);
        Assert.Equal(0, r.ManchasPequenas);
        Assert.Equal(StatusCriterio.Conforme, Criterio(r, Titulo2020).Status);
    }

    [Fact]
    public async Task DuasManchas_OrdenadasDaMaisRecenteParaAMaisAntiga()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Cobertura.Classe = (ano, lon, lat) =>
            (Em(Mancha, lon, lat) && ano <= 2014) || (Em(Mancha2, lon, lat) && ano <= 2020) ? Floresta : Pastagem;

        var r = await c.Servico().GerarAsync(1);

        Assert.Collection(r.Areas,
            a => { Assert.Equal(1, a.Numero); Assert.Equal("2020", a.Marco); },
            a => { Assert.Equal(2, a.Numero); Assert.Equal("2014", a.Marco); });
        Assert.Equal(r.Areas.Sum(a => a.AreaHa), r.ConversaoTotalHa);
        Assert.Contains("2 área(s)", Criterio(r, Titulo2014).Resultado);
        Assert.Contains("1 área(s)", Criterio(r, Titulo2020).Resultado);
    }

    [Fact]
    public async Task PastagemQueVoltouASerFloresta_ContaComoRegeneracao()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Cobertura.Classe = (ano, lon, lat) => Em(Mancha, lon, lat) && ano >= 2020 ? Floresta : Pastagem;

        var r = await c.Servico().GerarAsync(1);

        Assert.Empty(r.Areas);
        Assert.InRange(r.RegeneracaoHa, 10.3m, 10.7m);
        Assert.Equal(StatusCriterio.Conforme, Criterio(r, Titulo2008).Status);
    }

    [Fact]
    public async Task FaltaOMapaDe2014_UsaODe2008EPedeVerificacao()
    {
        var c = Novo(2008, 2020, 2024);
        c.Cobertura.Classe = DesmatadaDepoisDe(2008);

        var r = await c.Servico().GerarAsync(1);

        var marco = Assert.Single(r.Marcos, m => m.Chave == "2014");
        Assert.Equal(2008, marco.AnoUsado);
        Assert.True(marco.Aproximado);
        Assert.Contains(r.Avisos, a => a.Contains("Falta o mapa de 2014"));
        Assert.Equal("2008", Assert.Single(r.Areas).Marco);
        Assert.Equal(StatusCriterio.Verificar, Criterio(r, Titulo2014).Status);
        Assert.Contains("Sem o mapa de 2014", Criterio(r, Titulo2014).Resultado);
        Assert.Equal(StatusCriterio.Conforme, Criterio(r, Titulo2020).Status);
    }

    [Fact]
    public async Task UltimoMapaEhDe2020_Marco2020FicaSemDados()
    {
        var c = Novo(2008, 2014, 2020);
        c.Cobertura.Classe = DesmatadaDepoisDe(2014);

        var r = await c.Servico().GerarAsync(1);

        Assert.Null(Assert.Single(r.Marcos, m => m.Chave == "2020").AnoUsado);
        Assert.Equal(StatusCriterio.SemDados, Criterio(r, Titulo2020).Status);
        Assert.Equal(StatusCriterio.IndicioNaoConformidade, Criterio(r, Titulo2014).Status);
    }

    [Theory]
    [InlineData(new int[0], "Nenhum mapa do MapBiomas")]
    [InlineData(new[] { 2024 }, "Só há o mapa de 2024")]
    public async Task MenosDeDoisMapas_MarcosSemDados(int[] anos, string aviso)
    {
        var r = await Novo(anos).Servico().GerarAsync(1);

        Assert.Equal(StatusCriterio.SemDados, Criterio(r, Titulo2008).Status);
        Assert.Equal(StatusCriterio.SemDados, Criterio(r, Titulo2014).Status);
        Assert.Equal(StatusCriterio.SemDados, Criterio(r, Titulo2020).Status);
        Assert.Contains(r.Avisos, a => a.Contains(aviso));
        Assert.Empty(r.Areas);
    }

    [Fact]
    public async Task PropriedadeSemLimiteDoCar_MarcosSemDados()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Repo.Propriedades[1].GeoJson = null;

        var r = await c.Servico().GerarAsync(1);

        Assert.Equal(3, r.Criterios.Count);
        Assert.All(r.Criterios, k => Assert.Equal(StatusCriterio.SemDados, k.Status));
        Assert.Contains(r.Avisos, a => a.Contains("limite do CAR"));
    }

    [Fact]
    public async Task PropriedadeInexistente_LancaNaoEncontrado()
    {
        await Assert.ThrowsAsync<NaoEncontradoException>(() => Novo(2008, 2024).Servico().GerarAsync(99));
    }

    [Fact]
    public async Task SemCamadaDeUcs_CriteriosDeUcSemDados()
    {
        var r = await Novo(2008, 2014, 2020, 2024).Servico().GerarAsync(1);

        Assert.Equal(StatusCriterio.SemDados, Criterio(r, "Fora de unidade de conservação").Status);
        Assert.Equal(StatusCriterio.SemDados, Criterio(r, "Fora de zona de amortecimento").Status);
    }

    [Fact]
    public async Task ImovelDentroDeUcDeProtecaoIntegral_IndicioDeNaoConformidade()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        var poligonos = GeoUtil.LerGeoJson(Cenario.Retangulo(-47.005, -19.005, -46.985, -18.985));
        c.Ucs.TemDados = true;
        c.Ucs.Lista.Add(new UnidadeConservacao("Parque Estadual Teste", "Parque", UnidadeConservacao.ProtecaoIntegral, "Estadual", false,
            poligonos, GeoUtil.BoundingBox(poligonos)!, "ucs.shp"));

        var r = await c.Servico().GerarAsync(1);

        Assert.Equal(StatusCriterio.IndicioNaoConformidade, Criterio(r, "Fora de unidade de conservação").Status);
        var uc = Assert.Single(r.Ucs);
        Assert.True(uc.Dentro);
        Assert.Equal(0, uc.DistanciaM);
        Assert.InRange(uc.AreaDentroHa!.Value, 110m, 122m);
        Assert.NotNull(r.UcsGeoJson);
    }

    [Fact]
    public async Task AlertaDentroDoImovel_PedeVerificacao()
    {
        var c = Novo(2008, 2014, 2020, 2024);
        c.Repo.Alertas.Add(new AlertaDesmatamento
        {
            Id = 10, PropriedadeId = 1, AreaHa = 2.5m, DataDeteccao = new DateOnly(2023, 8, 1),
            GeoJson = Cenario.Retangulo(-46.997, -18.997, -46.996, -18.996),
        });
        c.Repo.Alertas.Add(new AlertaDesmatamento
        {
            Id = 11, PropriedadeId = 1, AreaHa = 7m, DataDeteccao = new DateOnly(2023, 9, 1),
            GeoJson = Cenario.Retangulo(-46.90, -18.90, -46.89, -18.89),
        });

        var r = await c.Servico().GerarAsync(1);

        var k = Criterio(r, "Alertas de desmatamento validados (desde 2019)");
        Assert.Equal(StatusCriterio.Verificar, k.Status);
        Assert.Contains("1 alerta(s)", k.Resultado);
        Assert.Single(r.Alertas);
    }
}
