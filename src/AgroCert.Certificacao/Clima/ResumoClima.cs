namespace AgroCert.Certificacao.Clima;

public record ResumoClimaTela(
    SituacaoEnsoDto Situacao,
    ImpactoEnsoDto? Impacto,
    List<MesResumo> Meses,
    string Titulo,
    string Explicacao,
    string? EfeitoNaRegiao,
    string IconeSituacao,
    string ClasseSituacao);

public enum NivelChuva { Menos, Normal, Mais }
public enum NivelCalor { MaisFrio, Normal, MaisQuente }

public record MesResumo(
    int Mes,
    int Ano,
    NivelChuva Chuva,
    double ChuvaMm,
    double ChuvaMin,
    double ChuvaMax,
    double ChuvaNormalMm,
    NivelCalor Calor,
    double Maxima,
    double MaximaNormal,
    double Minima,
    string Icone,
    string Frase,
    List<(bool Alto, string Texto)> Alertas,
    bool JaMedido);

public static class ResumoClima
{
    private const double Limite = 0.45;

    public static ResumoClimaTela Montar(SituacaoEnsoDto s, ImpactoEnsoDto? imp, IEnumerable<PrevisaoMesDto> previsao, int mesInicial, int anoInicial, int quantidade = 6)
    {
        var inicio = anoInicial * 12 + mesInicial - 1;
        var meses = previsao
            .Where(m => m.Ano * 12 + m.Mes - 1 >= inicio)
            .OrderBy(m => m.Ano * 12 + m.Mes)
            .Take(quantidade)
            .Select(ParaResumo)
            .ToList();

        var (titulo, explicacao, icone, classe) = Situacao(s);
        return new ResumoClimaTela(s, imp, meses, titulo, explicacao, EfeitoNaRegiao(s, imp), icone, classe);
    }

    private static (string Titulo, string Texto, string Icone, string Classe) Situacao(SituacaoEnsoDto s)
    {
        var intensidade = s.Intensidade.ToLowerInvariant() switch
        {
            "fraco" => "fraco",
            "moderado" => "moderado",
            "forte" => "forte",
            "muito forte" => "muito forte",
            _ => "",
        };
        var pico = s.Proximos12Meses.OrderByDescending(p => Math.Abs(p.Oni)).FirstOrDefault();
        var mesPico = pico is null ? "" : $"{ClimaFormato.NomeMes(pico.Mes).ToLowerInvariant()} de {pico.Ano}";

        if (s.Fase == "NINO")
        {
            var titulo = s.Episodio == "NINO" ? $"Estamos em um ano de El Niño {intensidade}".Trim() : "O El Niño está chegando";
            var texto = s.Tendencia3Meses >= 0.3 && pico is { Oni: >= 0.5 }
                ? $"As águas do Oceano Pacífico estão esquentando. A tendência é o El Niño ficar mais forte até {mesPico}."
                : "As águas do Oceano Pacífico estão mais quentes que o normal.";
            return (titulo, texto, "wb_sunny", "cl-sit--nino");
        }
        if (s.Fase == "NINA")
        {
            var titulo = s.Episodio == "NINA" ? $"Estamos em um ano de La Niña {intensidade}".Trim() : "A La Niña está chegando";
            var texto = s.Tendencia3Meses <= -0.3 && pico is { Oni: <= -0.5 }
                ? $"As águas do Oceano Pacífico estão esfriando. A tendência é a La Niña ficar mais forte até {mesPico}."
                : "As águas do Oceano Pacífico estão mais frias que o normal.";
            return (titulo, texto, "water_drop", "cl-sit--nina");
        }
        if (pico is { Oni: >= 0.5 })
            return ("Clima normal agora, mas o El Niño deve chegar", $"O Oceano Pacífico está esquentando. O El Niño pode aparecer nos próximos meses (mais forte perto de {mesPico}).", "wb_cloudy", "cl-sit--neutro");
        if (pico is { Oni: <= -0.5 })
            return ("Clima normal agora, mas a La Niña deve chegar", $"O Oceano Pacífico está esfriando. A La Niña pode aparecer nos próximos meses (mais forte perto de {mesPico}).", "wb_cloudy", "cl-sit--neutro");
        return ("Sem El Niño e sem La Niña", "O Oceano Pacífico está normal. O clima deve seguir o padrão de cada mês.", "thumb_up", "cl-sit--neutro");
    }

    private static string? EfeitoNaRegiao(SituacaoEnsoDto s, ImpactoEnsoDto? imp)
    {
        if (imp is null) return null;
        var fase = s.Fase != "NEUTRO" ? s.Fase
            : s.Proximos12Meses.OrderByDescending(p => Math.Abs(p.Oni)).FirstOrDefault() is { } p && Math.Abs(p.Oni) >= 0.5 ? (p.Oni > 0 ? "NINO" : "NINA")
            : null;
        if (fase is null) return null;
        var nino = fase == "NINO";
        var nome = nino ? "El Niño" : "La Niña";

        bool Forte(ImpactoLinhaDto l) => (nino ? l.EvidenciaElNino : l.EvidenciaLaNina) != "fraca";
        double Pct(ImpactoLinhaDto l) => (nino ? l.ElNinoModeradoPct : l.LaNinaModeradaPct) ?? 0;
        double Graus(ImpactoLinhaDto l) => nino ? l.ElNinoModerado : l.LaNinaModerada;

        var chuva = imp.Linhas.Where(l => l.Variavel == "chuva" && l.Normal >= 20 && Forte(l)).ToList();
        var menosChuva = chuva.Where(l => Pct(l) <= -8).Select(l => l.Mes).ToList();
        var maisChuva = chuva.Where(l => Pct(l) >= 8).Select(l => l.Mes).ToList();
        var temp = imp.Linhas.Where(l => l.Variavel == "maxima" && Forte(l)).ToList();
        var maisCalor = temp.Where(l => Graus(l) >= 0.3).Select(l => l.Mes).ToList();
        var maisFrio = temp.Where(l => Graus(l) <= -0.3).Select(l => l.Mes).ToList();

        var partes = new List<string>();
        if (menosChuva.Count > 0) partes.Add($"chover menos em {ListaMeses(menosChuva)}");
        if (maisChuva.Count > 0) partes.Add($"chover mais em {ListaMeses(maisChuva)}");
        if (maisCalor.Count > 0) partes.Add($"fazer mais calor em {ListaMeses(maisCalor)}");
        if (maisFrio.Count > 0) partes.Add($"fazer mais frio em {ListaMeses(maisFrio)}");
        if (partes.Count == 0)
            return $"Pelos dados da nossa estação, o {nome} não costuma mudar muito o clima aqui.";
        return $"Pelos dados da nossa estação desde {imp.Periodo.Split('–')[0]}, em anos de {nome} costuma {Juntar(partes)}.";
    }

    private static string ListaMeses(List<int> meses)
    {
        var nomes = meses.OrderBy(m => m).Select(m => ClimaFormato.NomeMes(m).ToLowerInvariant()).ToList();
        return Juntar(nomes);
    }

    private static string Juntar(List<string> itens) =>
        itens.Count switch
        {
            0 => "",
            1 => itens[0],
            _ => string.Join(", ", itens.Take(itens.Count - 1)) + " e " + itens[^1],
        };

    private static MesResumo ParaResumo(PrevisaoMesDto m)
    {
        var c = m.Chuva;
        var chuva = c.ProbAbaixoNormal >= Limite && c.ProbAbaixoNormal > c.ProbAcimaNormal ? NivelChuva.Menos
            : c.ProbAcimaNormal >= Limite ? NivelChuva.Mais
            : NivelChuva.Normal;
        var calor = m.Maxima.ProbAcimaNormal >= Limite ? NivelCalor.MaisQuente
            : m.Maxima.ProbAbaixoNormal >= Limite ? NivelCalor.MaisFrio
            : NivelCalor.Normal;
        var epocaSeca = c.Normal < 30;

        var icone = chuva switch
        {
            NivelChuva.Menos => "wb_sunny",
            NivelChuva.Mais => "thunderstorm",
            _ => epocaSeca ? "wb_sunny" : c.Normal >= 120 ? "water_drop" : "wb_cloudy",
        };

        var frase = (chuva, epocaSeca) switch
        {
            (NivelChuva.Menos, _) => "Deve chover menos que o normal.",
            (NivelChuva.Mais, _) => "Deve chover mais que o normal.",
            (_, true) => "Época seca, como de costume.",
            _ => "Chuva dentro do normal.",
        };
        frase += calor switch
        {
            NivelCalor.MaisQuente => " Mais quente que o normal.",
            NivelCalor.MaisFrio => " Mais frio que o normal.",
            _ => "",
        };

        var alertas = m.Riscos
            .OrderByDescending(r => r.Nivel == "ALTO")
            .Select(r => (r.Nivel == "ALTO", AlertaSimples(r.Tipo)))
            .Where(a => a.Item2 is not null)
            .Select(a => (a.Item1, a.Item2!))
            .ToList();

        return new MesResumo(m.Mes, m.Ano, chuva, c.Valor, c.Min, c.Max, c.Normal, calor,
            m.Maxima.Valor, m.Maxima.Normal, m.Minima.Valor, icone, frase, alertas, m.Enso.Origem == "observado" && c.Observado.HasValue);
    }

    private static string? AlertaSimples(string tipo) => tipo switch
    {
        "SECA" => "Risco de pouca chuva e veranico. Planeje o plantio e a irrigação.",
        "EXCESSO_CHUVA" => "Risco de chuva demais. Atenção a doenças, erosão e colheita.",
        "CALOR" => "Calor acima do normal. Atenção à água para as plantas e os animais.",
        "FRIO" => "Noites mais frias. Atenção a geadas.",
        "UMIDADE_BAIXA" => "Ar mais seco que o normal.",
        _ => null,
    };
}
