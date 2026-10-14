using AgroCert.Certificacao.Clima;

namespace AgroCert.Testes.Clima;

public class ClimaFormatoTestes
{
    [Theory]
    [InlineData(1, "Janeiro", "Jan")]
    [InlineData(3, "Março", "Mar")]
    [InlineData(12, "Dezembro", "Dez")]
    [InlineData(13, "13", "13")]
    public void NomeMesEMesCurto(int mes, string nome, string curto)
    {
        Assert.Equal(nome, ClimaFormato.NomeMes(mes));
        Assert.Equal(curto, ClimaFormato.MesCurto(mes));
    }

    [Fact]
    public void MesAno_UsaDoisDigitosDoAno()
    {
        Assert.Equal("Dez/09", ClimaFormato.MesAno(12, 2009));
    }

    [Theory]
    [InlineData(1.26, "+1,3")]
    [InlineData(-0.84, "−0,8")]
    [InlineData(0.04, "0,0")]
    [InlineData(-0.04, "0,0")]
    [InlineData(1234.5, "+1.234,5")]
    public void S_NumeroComSinal(double valor, string esperado)
    {
        Assert.Equal(esperado, ClimaFormato.S(valor));
    }

    [Fact]
    public void N_PctEF()
    {
        Assert.Equal("1.234,6", ClimaFormato.N(1234.56));
        Assert.Equal("46%", ClimaFormato.Pct(0.456));
        Assert.Equal("1.24", ClimaFormato.F(1.236));
        Assert.Equal("2", ClimaFormato.F(2.0));
    }

    [Theory]
    [InlineData(0.1, 1.0, true, ClimaFormato.Neutro)]
    [InlineData(0.2, 1.0, true, "#f9dcdb")]
    [InlineData(-0.5, 1.0, false, "#b7d3f6")]
    [InlineData(-0.7, 1.0, false, "#86b6ef")]
    [InlineData(3.0, 1.0, true, "#e56a69")]
    [InlineData(0.5, 0.0, true, "#f4bcbb")]
    public void CorDivergente_PorIntensidade(double valor, double escala, bool quente, string esperado)
    {
        Assert.Equal(esperado, ClimaFormato.CorDivergente(valor, escala, quente));
    }

    [Theory]
    [InlineData("NINO", "El Niño", "cl-fase--nino")]
    [InlineData("NINA", "La Niña", "cl-fase--nina")]
    [InlineData("NEUTRO", "Neutro", "cl-fase--neutro")]
    public void FaseEnso(string fase, string nome, string classe)
    {
        Assert.Equal(nome, ClimaFormato.NomeFase(fase));
        Assert.Equal(classe, ClimaFormato.ClasseFase(fase));
    }
}

public class ResumoClimaTestes
{
    private static FaixaDto Faixa(double normal = 100, double abaixo = 0.33, double acima = 0.33, double? observado = null) =>
        new(normal * 0.5, normal, normal * 1.5, normal, 0, null, 0, null, abaixo, 1 - abaixo - acima, acima, observado);

    private static PrevisaoMesDto Mes(int mes, int ano, FaixaDto? chuva = null, FaixaDto? maxima = null, string origem = "modelo", params RiscoDto[] riscos) =>
        new(mes, ano, new EnsoMesDto(0, 0.2, "NEUTRO", "", "", origem, 0.2, 0.2),
            chuva ?? Faixa(), maxima ?? Faixa(30), Faixa(18), Faixa(24), Faixa(70), riscos.ToList(), null);

    private static SituacaoEnsoDto Situacao(string fase, string episodio = "NEUTRO", string intensidade = "", double tendencia = 0, params OniProjetadoDto[] proximos) =>
        new(new AnoMesDto(2025, 9), 0, fase, intensidade, episodio, tendencia, "", new(), proximos.ToList(), null);

    private static MesResumo Unico(PrevisaoMesDto m) =>
        Assert.Single(ResumoClima.Montar(Situacao("NEUTRO"), null, new[] { m }, m.Mes, m.Ano).Meses);

    [Fact]
    public void ChanceDeChuvaAbaixo_DizQueChoveMenos()
    {
        var r = Unico(Mes(1, 2026, chuva: Faixa(200, abaixo: 0.5, acima: 0.2)));
        Assert.Equal(NivelChuva.Menos, r.Chuva);
        Assert.Equal("wb_sunny", r.Icone);
        Assert.Equal("Deve chover menos que o normal.", r.Frase);
    }

    [Fact]
    public void AbaixoNoLimiteMasAcimaMaior_DizQueChoveMais()
    {
        var r = Unico(Mes(1, 2026, chuva: Faixa(200, abaixo: 0.45, acima: 0.5)));
        Assert.Equal(NivelChuva.Mais, r.Chuva);
        Assert.Equal("thunderstorm", r.Icone);
    }

    [Theory]
    [InlineData(10.0, "wb_sunny", "Época seca, como de costume.")]
    [InlineData(80.0, "wb_cloudy", "Chuva dentro do normal.")]
    [InlineData(150.0, "water_drop", "Chuva dentro do normal.")]
    public void ChuvaNormal_IconePelaEpoca(double normal, string icone, string frase)
    {
        var r = Unico(Mes(6, 2026, chuva: Faixa(normal)));
        Assert.Equal(NivelChuva.Normal, r.Chuva);
        Assert.Equal(icone, r.Icone);
        Assert.Equal(frase, r.Frase);
    }

    [Fact]
    public void MaximaAcimaDoNormal_AcrescentaMaisQuente()
    {
        var r = Unico(Mes(10, 2026, maxima: Faixa(31, abaixo: 0.1, acima: 0.6)));
        Assert.Equal(NivelCalor.MaisQuente, r.Calor);
        Assert.EndsWith(" Mais quente que o normal.", r.Frase);
    }

    [Fact]
    public void Alertas_AltoPrimeiroEDesconhecidoDescartado()
    {
        var r = Unico(Mes(1, 2026, riscos: new[]
        {
            new RiscoDto("CALOR", "MEDIO", 0.4, ""),
            new RiscoDto("XYZ", "ALTO", 0.9, ""),
            new RiscoDto("SECA", "ALTO", 0.7, ""),
        }));

        Assert.Collection(r.Alertas,
            a => { Assert.True(a.Alto); Assert.StartsWith("Risco de pouca chuva", a.Texto); },
            a => { Assert.False(a.Alto); Assert.StartsWith("Calor acima do normal", a.Texto); });
    }

    [Fact]
    public void JaMedido_SoComOrigemObservadaEValorObservado()
    {
        Assert.True(Unico(Mes(1, 2026, chuva: Faixa(observado: 90), origem: "observado")).JaMedido);
        Assert.False(Unico(Mes(1, 2026, chuva: Faixa(), origem: "observado")).JaMedido);
        Assert.False(Unico(Mes(1, 2026, chuva: Faixa(observado: 90), origem: "modelo")).JaMedido);
    }

    [Fact]
    public void Montar_ComecaNoMesPedidoOrdenaELimita()
    {
        var previsao = new[] { Mes(1, 2026), Mes(10, 2025), Mes(12, 2025), Mes(11, 2025) };

        var r = ResumoClima.Montar(Situacao("NEUTRO"), null, previsao, 11, 2025, quantidade: 2);

        Assert.Equal(new[] { (11, 2025), (12, 2025) }, r.Meses.Select(m => (m.Mes, m.Ano)).ToArray());
    }

    [Theory]
    [InlineData("NINO", "NINO", "Moderado", "Estamos em um ano de El Niño moderado", "cl-sit--nino")]
    [InlineData("NINO", "NEUTRO", "Fraco", "O El Niño está chegando", "cl-sit--nino")]
    [InlineData("NINA", "NINA", "", "Estamos em um ano de La Niña", "cl-sit--nina")]
    [InlineData("NEUTRO", "NEUTRO", "", "Sem El Niño e sem La Niña", "cl-sit--neutro")]
    public void Situacao_TituloEClasse(string fase, string episodio, string intensidade, string titulo, string classe)
    {
        var r = ResumoClima.Montar(Situacao(fase, episodio, intensidade), null, Array.Empty<PrevisaoMesDto>(), 1, 2026);
        Assert.Equal(titulo, r.Titulo);
        Assert.Equal(classe, r.ClasseSituacao);
    }

    [Fact]
    public void Neutro_ComElNinoProjetado_AvisaQueEstaChegando()
    {
        var s = Situacao("NEUTRO", proximos: new[]
        {
            new OniProjetadoDto(2025, 11, 0.3, 0.2, "NEUTRO", ""),
            new OniProjetadoDto(2025, 12, 0.8, 0.3, "NINO", ""),
        });

        var r = ResumoClima.Montar(s, null, Array.Empty<PrevisaoMesDto>(), 1, 2026);

        Assert.Equal("Clima normal agora, mas o El Niño deve chegar", r.Titulo);
        Assert.Contains("dezembro de 2025", r.Explicacao);
        Assert.Null(r.EfeitoNaRegiao);
    }

    private static ImpactoLinhaDto Linha(int mes, string variavel, double normal, double graus, double? pct, string evidencia) =>
        new(mes, variavel, variavel, normal, 0, graus, graus, 0, pct, null, 2.5, 0, evidencia, "fraca", 8, 8, 30, 0.3);

    [Fact]
    public void EfeitoNaRegiao_ListaSoEfeitosComEvidencia()
    {
        var imp = new ImpactoEnsoDto("1991–2024", "", new()
        {
            Linha(1, "chuva", 200, 0, -15, "forte"),
            Linha(2, "chuva", 200, 0, -20, "fraca"),
            Linha(3, "chuva", 10, 0, 40, "forte"),
            Linha(9, "maxima", 30, 0.5, null, "moderada"),
        });

        var r = ResumoClima.Montar(Situacao("NINO", "NINO", "Forte"), imp, Array.Empty<PrevisaoMesDto>(), 1, 2026);

        Assert.Equal("Pelos dados da nossa estação desde 1991, em anos de El Niño costuma chover menos em janeiro e fazer mais calor em setembro.", r.EfeitoNaRegiao);
    }

    [Fact]
    public void EfeitoNaRegiao_SemEfeitoForte()
    {
        var imp = new ImpactoEnsoDto("1991–2024", "", new() { Linha(1, "chuva", 200, 0, -15, "fraca") });

        var r = ResumoClima.Montar(Situacao("NINO", "NINO"), imp, Array.Empty<PrevisaoMesDto>(), 1, 2026);

        Assert.Equal("Pelos dados da nossa estação, o El Niño não costuma mudar muito o clima aqui.", r.EfeitoNaRegiao);
    }
}
