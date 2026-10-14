using AgroCert.Application.Util;
using AgroCert.Domain.Enums;

namespace AgroCert.Testes.Aplicacao;

public class BiodiversidadeTestes
{
    [Theory]
    [InlineData("VU", true)]
    [InlineData("EN", true)]
    [InlineData("CR", true)]
    [InlineData("NT", false)]
    [InlineData("LC", false)]
    [InlineData("NA", false)]
    [InlineData("vu", false)]
    public void Ameacada(string codigo, bool esperado)
    {
        Assert.Equal(esperado, Biodiversidade.Ameacada(codigo));
    }

    [Fact]
    public void Ameacada_Nulo_EhFalso()
    {
        Assert.False(Biodiversidade.Ameacada(null));
    }

    [Theory]
    [InlineData("EN", "Em perigo")]
    [InlineData("CR", "Criticamente em perigo")]
    [InlineData("LC", "Pouco preocupante")]
    [InlineData("XX", "Não avaliada")]
    public void RotuloStatus(string codigo, string esperado)
    {
        Assert.Equal(esperado, Biodiversidade.RotuloStatus(codigo));
    }

    [Fact]
    public void Grupo_ConhecidoEDesconhecido()
    {
        Assert.Equal("#eb6834", Biodiversidade.Grupo(Reino.FAUNA, "Mamífero").Cor);
        Assert.Equal("Árvores", Biodiversidade.Grupo(Reino.FLORA, "Árvore").Plural);
        Assert.Equal("Outro", Biodiversidade.Grupo(Reino.FAUNA, "Dinossauro").Nome);
        Assert.Equal("eco", Biodiversidade.Grupo(Reino.FLORA, null).Icone);
    }

    [Fact]
    public void Grupo_NomeDeFloraNaoValeParaFauna()
    {
        Assert.Equal("cruelty_free", Biodiversidade.Grupo(Reino.FAUNA, "Árvore").Icone);
    }

    [Theory]
    [InlineData(OrigemRegistro.CAMERA, Reino.FAUNA, "Registrada por câmera")]
    [InlineData(OrigemRegistro.COLETA, Reino.FLORA, "Registro de flora")]
    [InlineData(OrigemRegistro.COLETA, Reino.FAUNA, "Coleta")]
    public void ResumoOrigem(OrigemRegistro origem, Reino reino, string esperado)
    {
        Assert.Equal(esperado, Biodiversidade.ResumoOrigem(origem, reino));
    }
}
