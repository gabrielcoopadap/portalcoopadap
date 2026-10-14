using AgroCert.Application.Servicos;
using AgroCert.Domain.Entidades;
using EntidadeCertificacao = AgroCert.Domain.Entidades.Certificacao;

namespace AgroCert.Testes.Aplicacao;

public class ConformidadeTestes
{
    private static Requisito Req(string? status, params string[] normas) => new()
    {
        Status = status,
        Referencias = normas.Select(n => new Referencia { Norma = n }).ToList(),
    };

    [Fact]
    public void PorCertificacao_ContaConformesPendentesENaoConformes()
    {
        var requisitos = new[]
        {
            Req("Conforme", "Rainforest Alliance"),
            Req("Conforme", "Rainforest Alliance"),
            Req("Não Conforme", "Rainforest Alliance"),
            Req("Em Andamento", "Rainforest Alliance"),
            Req(null, "Rainforest Alliance"),
        };

        var s = Conformidade.PorCertificacao(requisitos)["Rainforest Alliance"];

        Assert.Equal(5, s.Total);
        Assert.Equal(2, s.Conformes);
        Assert.Equal(1, s.NaoConformes);
        Assert.Equal(2, s.Pendentes);
        Assert.Equal(40, s.Percentual);
    }

    [Fact]
    public void Grasp_ContaComoGlobalGap_SemDuplicarRequisito()
    {
        var requisitos = new[]
        {
            Req("Conforme", "GRASP", "GlobalG.A.P."),
            Req("Pendente", "GRASP"),
        };

        var stats = Conformidade.PorCertificacao(requisitos);

        Assert.False(stats.ContainsKey("GRASP"));
        Assert.Equal(2, stats["GlobalG.A.P."].Total);
        Assert.Equal(1, stats["GlobalG.A.P."].Conformes);
    }

    [Theory]
    [InlineData("C.A.F.E. Practices", "CAFE Practices")]
    [InlineData("SMETA", "SMETA")]
    [InlineData("ATITUDES", "ATITUDES")]
    [InlineData("Norma Nova", "Norma Nova")]
    public void CertificacoesDoRequisito_MapeiaNormaParaCertificacao(string norma, string esperado)
    {
        Assert.Equal(new[] { esperado }, Conformidade.CertificacoesDoRequisito(Req("Conforme", norma)).ToArray());
    }

    [Fact]
    public void CertificacoesDoRequisito_IgnoraReferenciaSemNorma()
    {
        var r = new Requisito { Referencias = { new Referencia { Norma = null, Codigo = "1.1" } } };
        Assert.Empty(Conformidade.CertificacoesDoRequisito(r));
    }

    [Theory]
    [InlineData(1, 8, 13)]
    [InlineData(1, 3, 33)]
    [InlineData(2, 3, 67)]
    [InlineData(1, 200, 1)]
    [InlineData(1, 40, 3)]
    [InlineData(0, 4, 0)]
    [InlineData(4, 4, 100)]
    public void Percentual_ArredondaMeioParaCima(int conformes, int total, int esperado)
    {
        Assert.Equal(esperado, new Conformidade.Estatistica(total, conformes, total - conformes, 0).Percentual);
    }

    [Fact]
    public void Percentual_SemRequisitos_EhZero()
    {
        Assert.Equal(0, new Conformidade.Estatistica(0, 0, 0, 0).Percentual);
    }

    [Fact]
    public void Aplicar_SobrescreveNumerosEDevolveCopia()
    {
        var original = new EntidadeCertificacao
        {
            Id = 7, Nome = "GlobalG.A.P.", Conformidade = 10, TotalRequisitos = 99, Conformes = 1, Pendentes = 1, NaoConformes = 1,
            Categorias = { new CategoriaConformidade { Nome = "Água", Conformes = 1, Total = 2 } },
        };
        var requisitos = new[] { Req("Conforme", "GRASP"), Req("Não Conforme", "GlobalG.A.P.") };

        var resultado = Assert.Single(Conformidade.Aplicar(new[] { original }, requisitos));

        Assert.NotSame(original, resultado);
        Assert.Equal(50, resultado.Conformidade);
        Assert.Equal(2, resultado.TotalRequisitos);
        Assert.Equal(1, resultado.Conformes);
        Assert.Equal(0, resultado.Pendentes);
        Assert.Equal(1, resultado.NaoConformes);
        Assert.Equal(10, original.Conformidade);
        Assert.Equal(99, original.TotalRequisitos);
        Assert.NotSame(original.Categorias[0], resultado.Categorias[0]);
        Assert.Equal("Água", resultado.Categorias[0].Nome);
    }

    [Fact]
    public void Aplicar_CertificacaoSemRequisitos_MantemValoresCadastrados()
    {
        var smeta = new EntidadeCertificacao { Nome = "SMETA", Conformidade = 72, TotalRequisitos = 25, Conformes = 18, Pendentes = 7, NaoConformes = 0 };

        var resultado = Assert.Single(Conformidade.Aplicar(new[] { smeta }, new[] { Req("Conforme", "Rainforest Alliance") }));

        Assert.Equal(72, resultado.Conformidade);
        Assert.Equal(25, resultado.TotalRequisitos);
        Assert.Equal(18, resultado.Conformes);
    }

    [Fact]
    public void RequisitosDaCertificacao_IncluiGraspEmGlobalGap()
    {
        var gg = Req("Conforme", "GlobalG.A.P.");
        var grasp = Req("Pendente", "GRASP");
        var ra = Req("Conforme", "Rainforest Alliance");

        var lista = Conformidade.RequisitosDaCertificacao(new[] { gg, grasp, ra }, "GlobalG.A.P.").ToList();

        Assert.Equal(2, lista.Count);
        Assert.Contains(gg, lista);
        Assert.Contains(grasp, lista);
        Assert.DoesNotContain(ra, lista);
    }
}
