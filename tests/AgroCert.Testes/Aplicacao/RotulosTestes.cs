using AgroCert.Application.Util;

namespace AgroCert.Testes.Aplicacao;

public class RotulosTestes
{
    [Theory]
    [InlineData(0.0, "Solo exposto / água")]
    [InlineData(0.19, "Solo exposto / água")]
    [InlineData(0.2, "Vegetação esparsa")]
    [InlineData(0.49, "Vegetação esparsa")]
    [InlineData(0.5, "Vegetação moderada")]
    [InlineData(0.79, "Vegetação moderada")]
    [InlineData(0.8, "Vegetação densa")]
    [InlineData(1.0, "Vegetação densa")]
    [InlineData(-0.3, "Solo exposto / água")]
    public void ClassificarNdvi_Faixas(double ndvi, string esperado)
    {
        Assert.Equal(esperado, Rotulos.ClassificarNdvi((decimal)ndvi).Rotulo);
    }

    [Fact]
    public void ClassificarNdvi_Nulo()
    {
        Assert.Equal(("-", "#9a9384"), Rotulos.ClassificarNdvi(null));
    }

    [Theory]
    [InlineData("Rainforest Alliance", "RA")]
    [InlineData("GlobalG.A.P.", "GG")]
    [InlineData("GRASP", "GR")]
    [InlineData("C.A.F.E. Practices", "CP")]
    [InlineData("Outra", "Outra")]
    public void Sigla_DaNorma(string norma, string esperado)
    {
        Assert.Equal(esperado, Rotulos.Sigla(norma));
    }

    [Fact]
    public void Sigla_Nula_EhVazia()
    {
        Assert.Equal("", Rotulos.Sigla(null));
    }

    [Theory]
    [InlineData("CRITICO", "Crítico")]
    [InlineData("ALTO", "Alto")]
    [InlineData("MEDIO", "Médio")]
    [InlineData("BAIXO", "Baixo")]
    [InlineData("OUTRO", "OUTRO")]
    public void RotuloGrauRisco(string grau, string esperado)
    {
        Assert.Equal(esperado, Rotulos.RotuloGrauRisco(grau));
        Assert.Equal("-", Rotulos.RotuloGrauRisco(null));
    }

    [Fact]
    public void RotuloTipoVistoria_ConhecidoEDesconhecido()
    {
        Assert.Equal("Mensal — Consultoria", Rotulos.RotuloTipoVistoria("MENSAL_CONSULTORIA"));
        Assert.Equal("XYZ", Rotulos.RotuloTipoVistoria("XYZ"));
        Assert.Equal("-", Rotulos.RotuloTipoVistoria(null));
    }

    [Fact]
    public void Datas_NoPadraoBrasileiro()
    {
        var d = new DateOnly(2025, 3, 5);
        Assert.Equal("05/03/2025", Rotulos.Data(d));
        Assert.Equal("—", Rotulos.Data(null));
        Assert.Equal("5 de mar.", Rotulos.DataCurta(d));
        Assert.Equal("", Rotulos.DataCurta(null));
        Assert.Equal("dez/09", Rotulos.MesAno(new DateOnly(2009, 12, 1)));
    }

    [Fact]
    public void Numero_NoPadraoBrasileiro()
    {
        Assert.Equal("1.234,57", Rotulos.Numero(1234.567m));
        Assert.Equal("10,0", Rotulos.Numero(10m, 1));
        Assert.Equal("-", Rotulos.Numero(null));
    }

    [Fact]
    public void EstiloStatusRequisito_DesconhecidoUsaNeutro()
    {
        Assert.Equal(new Estilo("#e4f3ea", "#1f8a4c"), Rotulos.EstiloStatusRequisito("Conforme"));
        Assert.Equal(new Estilo("#eeeeee", "#555555"), Rotulos.EstiloStatusRequisito("???"));
    }
}
