using System.Globalization;
using System.Text;
using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using static AgroCert.Infrastructure.Relatorios.PdfComum;

namespace AgroCert.Infrastructure.Relatorios;

public class RelatorioVistoriaPdf : IRelatorioVistoriaPdf
{
    private const double ImgLargura = 150;
    private readonly CarregadorImagens _imagens;

    public RelatorioVistoriaPdf(CarregadorImagens imagens) => _imagens = imagens;

    public async Task<byte[]> GerarAsync(VistoriaDto v, CancellationToken ct = default)
    {
        // carrega todas as fotos antes (a montagem do documento é síncrona)
        var fotos = new Dictionary<string, byte[]?>();
        foreach (var url in v.Apontamentos.SelectMany(a => new[] { a.ImagemEncontradaUrl, a.ImagemRequeridaUrl, a.ImagemEvidenciaEncerramentoUrl }))
            if (!string.IsNullOrWhiteSpace(url) && !fotos.ContainsKey(url))
                fotos[url] = await _imagens.CarregarAsync(url, ct);

        var doc = NovoDocumento("Relatório de Vistoria " + v.CodigoDocumento);
        var s = doc.LastSection;
        Rodape(s, v.CodigoDocumento);

        var pendentes = v.Apontamentos.Where(a => a.Aberto).ToList();

        Cabecalho(s, v);
        Metodologia(s);
        ResumoExecutivo(s, v);
        if (v.Apontamentos.Count > 0)
        {
            PlanoDeAcao(s, v.Apontamentos);
            Detalhamento(s, v.Apontamentos, fotos);
        }
        ParecerTecnico(s, v, pendentes);
        Assinaturas(s, v);
        Paragrafo(s,
            "Este relatório documenta os achados da vistoria de conformidade realizada na propriedade, com base " +
            "nos critérios internos de classificação de risco da cooperativa e, quando aplicável, nos requisitos " +
            "das normas de certificação referenciadas. Não substitui a auditoria oficial dos organismos certificadores.",
            8, italico: true, cor: Cinza).Format.SpaceBefore = Unit.FromPoint(14);

        return Renderizar(doc);
    }

    private static void Rodape(Section s, string codigo)
    {
        var f = s.Footers.Primary.AddParagraph();
        f.Format.Font.Size = 7.5;
        f.Format.Font.Color = Cinza;
        f.Format.TabStops.AddTabStop(Unit.FromPoint(LarguraUtil), TabAlignment.Right);
        f.AddText(codigo);
        f.AddTab();
        f.AddText("Página ");
        f.AddPageField();
        f.AddText(" de ");
        f.AddNumPagesField();
    }

    private static void Cabecalho(Section s, VistoriaDto v)
    {
        Paragrafo(s, "Relatório de Vistoria de Certificação", 18, true, VerdeEscuro, espacoDepois: 2);
        Paragrafo(s, $"{v.CodigoDocumento}  ·  {Rotulos.RotuloTipoVistoriaLongo(v.Tipo)}", 11, cor: CinzaEscuro, espacoDepois: 2);
        Paragrafo(s, $"Emitido em {DateTime.Now:dd/MM/yyyy} às {DateTime.Now:HH:mm}", 8.5, cor: Cinza, espacoDepois: 10);

        var t = Tabela(s, new double[] { 1, 1 }, false);
        void Linha(string rotulo, string? valor)
        {
            var r = t.AddRow();
            Paragrafo(r.Cells[0], rotulo, 10, true);
            Paragrafo(r.Cells[1], Ou(valor), 10);
        }
        Linha("Unidade/Propriedade", v.UnidadePropriedade);
        Linha("Setor/Área", v.SetorArea);
        Linha("Data da vistoria", Rotulos.Data(v.DataVistoria));
        Linha("Motivo", v.Motivo);
        Linha("Responsável técnico", v.TecnicoResponsavel);
        Linha("Cargo", v.CargoTecnico);
    }

    private static void Metodologia(Section s)
    {
        TituloSecao(s, "Metodologia e Critérios de Classificação de Risco", 12.5);
        Paragrafo(s,
            "Cada não conformidade identificada em campo é registrada com a situação encontrada, a " +
            "situação exigida e, sempre que aplicável, vinculada ao item específico da norma de certificação " +
            "(Rainforest Alliance, GlobalG.A.P., GRASP, SMETA, C.A.F.E. Practices ou ATITUDES) ou à legislação " +
            "trabalhista/ambiental correspondente. O grau de risco segue os critérios internos de vistoria da " +
            "cooperativa, descritos abaixo.", 9.5, espacoDepois: 8);

        var t = Tabela(s, new double[] { 1, 5 }, false);
        void Criterio(string rotulo, Color cor, string descricao)
        {
            var r = t.AddRow();
            Paragrafo(r.Cells[0], rotulo, 9, true, cor);
            Paragrafo(r.Cells[1], descricao, 9);
        }
        Criterio("CRÍTICO", Critico, "Risco iminente à segurança dos trabalhadores, ao meio ambiente ou à validade da certificação. Exige ação corretiva imediata.");
        Criterio("ALTO", Alto, "Desvio relevante que compromete requisito central da norma referenciada. Prazo de correção reduzido.");
        Criterio("MÉDIO", Medio, "Desvio que deve ser corrigido dentro do ciclo normal de acompanhamento, sem risco imediato.");
        Criterio("BAIXO", Baixo, "Oportunidade de melhoria ou desvio pontual de baixo impacto.");
    }

    private static void ResumoExecutivo(Section s, VistoriaDto v)
    {
        TituloSecao(s, "Resumo Executivo");
        var t = Tabela(s, new double[] { 1, 1, 1, 1, 1, 1 }, true);
        var r = t.AddRow();
        void Cel(int i, string rotulo, int valor, Color cor)
        {
            var c = r.Cells[i];
            c.Format.Alignment = ParagraphAlignment.Center;
            Paragrafo(c, valor.ToString(), 20, true, cor);
            Paragrafo(c, rotulo, 8.5, cor: Cinza);
        }
        Cel(0, "Total", v.TotalApontamentos, Colors.Black);
        Cel(1, "Críticos", v.Criticos, Critico);
        Cel(2, "Altos", v.Altos, Alto);
        Cel(3, "Médios", v.Medios, Medio);
        Cel(4, "Baixos", v.Baixos, Baixo);
        Cel(5, "Em aberto", v.EmAberto, Critico);

        BarraSeveridade(s, v.Criticos, v.Altos, v.Medios, v.Baixos);

        if (v.TotalApontamentos > 0)
        {
            var pct = v.Encerrados * 100.0 / v.TotalApontamentos;
            Paragrafo(s, $"{v.Encerrados} de {v.TotalApontamentos} não conformidade(s) já encerrada(s) ({pct.ToString("0", CultureInfo.InvariantCulture)}%). {v.EmAberto} permanece(m) em aberto.",
                9.5, cor: CinzaEscuro).Format.SpaceBefore = Unit.FromPoint(6);
        }
    }

    private static void BarraSeveridade(Section s, int criticos, int altos, int medios, int baixos)
    {
        var partes = new List<(double Peso, Color Cor)>();
        if (criticos > 0) partes.Add((criticos, Critico));
        if (altos > 0) partes.Add((altos, Alto));
        if (medios > 0) partes.Add((medios, Medio));
        if (baixos > 0) partes.Add((baixos, Baixo));
        if (partes.Count == 0) return;

        s.AddParagraph().Format.SpaceAfter = Unit.FromPoint(4);
        var t = Tabela(s, partes.Select(p => p.Peso).ToArray(), false);
        var r = t.AddRow();
        r.Height = Unit.FromPoint(10);
        r.HeightRule = RowHeightRule.Exactly;
        for (var i = 0; i < partes.Count; i++) r.Cells[i].Shading.Color = partes[i].Cor;
    }

    private static void PlanoDeAcao(Section s, List<ApontamentoDto> aps)
    {
        TituloSecao(s, "Plano de Ação Consolidado");
        var t = Tabela(s, new[] { 1.9, 1.6, 1, 1.3, 0.9, 1, 1.1 }, true);
        var h = t.AddRow();
        h.HeadingFormat = true;
        h.Shading.Color = CinzaClaro;
        var titulos = new[] { "Anomalia", "Base normativa", "Local", "Grau de Risco", "Prazo", "Status", "Responsável" };
        for (var i = 0; i < titulos.Length; i++) Paragrafo(h.Cells[i], titulos[i], 8.5, true);

        foreach (var a in aps)
        {
            var r = t.AddRow();
            Paragrafo(r.Cells[0], Ou(a.Anomalia), 9);
            Paragrafo(r.Cells[1], Ou(a.RequisitoCodigo), 9);
            Paragrafo(r.Cells[2], Ou(a.Localizacao), 9);
            Paragrafo(r.Cells[3], Rotulos.RotuloGrauRisco(a.GrauRisco), 9, cor: CorRisco(a.GrauRisco));
            Paragrafo(r.Cells[4], Rotulos.Data(a.Prazo).Replace("—", "-"), 9);
            Paragrafo(r.Cells[5], Rotulos.RotuloStatusApontamento(a.Status), 9);
            Paragrafo(r.Cells[6], Ou(a.ResponsavelExecucao), 9);
        }
    }

    private static void Detalhamento(Section s, List<ApontamentoDto> aps, Dictionary<string, byte[]?> fotos)
    {
        TituloSecao(s, "Detalhamento das Não Conformidades");
        Paragrafo(s,
            "Registro completo de cada achado, com a base normativa que fundamenta a exigência, a situação " +
            "encontrada em campo, a situação requerida e as evidências fotográficas coletadas.",
            9, cor: CinzaEscuro, espacoDepois: 10);

        var n = 1;
        foreach (var a in aps) BlocoApontamento(s, n++, a, fotos);
    }

    private static void BlocoApontamento(Section s, int numero, ApontamentoDto a, Dictionary<string, byte[]?> fotos)
    {
        var t = Tabela(s, new double[] { 1, 1, 1, 1 }, false);
        t.LeftPadding = Unit.FromPoint(8);
        t.RightPadding = Unit.FromPoint(8);

        var r = t.AddRow();
        r.TopPadding = Unit.FromPoint(8);
        r.Cells[0].MergeRight = 2;
        Paragrafo(r.Cells[0], $"NC-{numero:00} · {Ou(a.Anomalia)}", 11.5, true);
        r.Cells[3].Format.Alignment = ParagraphAlignment.Right;
        r.Cells[3].VerticalAlignment = VerticalAlignment.Top;
        var selo = Paragrafo(r.Cells[3], " " + Rotulos.RotuloGrauRisco(a.GrauRisco).ToUpperInvariant() + " ", 9, true, Colors.White);
        selo.Format.Shading.Color = CorRisco(a.GrauRisco);

        r = t.AddRow();
        r.Cells[0].MergeRight = 3;
        if (!string.IsNullOrWhiteSpace(a.RequisitoCodigo))
        {
            var p = r.Cells[0].AddParagraph();
            p.Format.Font.Size = 9.5;
            p.AddFormattedText("Base normativa: ", TextFormat.Bold);
            p.AddText(a.RequisitoCodigo + (a.RequisitoTitulo != null ? " — " + a.RequisitoTitulo : ""));
            if (!string.IsNullOrWhiteSpace(a.RequisitoDescricao))
            {
                var d = Paragrafo(r.Cells[0], a.RequisitoDescricao, 9, italico: true, cor: CinzaEscuro);
                d.Format.LeftIndent = Unit.FromPoint(10);
                d.Format.SpaceBefore = Unit.FromPoint(2);
                d.Format.Borders.Left.Width = 1.5;
                d.Format.Borders.Left.Color = Borda;
                d.Format.Borders.DistanceFromLeft = Unit.FromPoint(6);
            }
        }
        else
        {
            Paragrafo(r.Cells[0], "Base normativa: não vinculada a um requisito específico do catálogo.", 9, italico: true, cor: Cinza);
        }

        r = t.AddRow();
        r.TopPadding = Unit.FromPoint(6);
        void Meta(int i, string rotulo, string? valor)
        {
            Paragrafo(r.Cells[i], rotulo, 7.5, cor: Cinza);
            Paragrafo(r.Cells[i], Ou(valor), 9.5);
        }
        Meta(0, "Localização", a.Localizacao);
        Meta(1, "Prazo", a.Prazo.HasValue ? Rotulos.Data(a.Prazo) : null);
        Meta(2, "Status", Rotulos.RotuloStatusApontamento(a.Status));
        Meta(3, "Responsável", a.ResponsavelExecucao);

        var textos = new[] { ("Situação encontrada", a.SituacaoEncontrada), ("Situação requerida", a.SituacaoRequerida), ("Risco associado", a.RiscoAssociado) }
            .Where(x => !string.IsNullOrWhiteSpace(x.Item2)).ToList();
        if (textos.Count > 0)
        {
            r = t.AddRow();
            r.Cells[0].MergeRight = 3;
            foreach (var (rotulo, texto) in textos)
            {
                var p = r.Cells[0].AddParagraph();
                p.Format.Font.Size = 9.5;
                p.Format.SpaceBefore = Unit.FromPoint(4);
                p.AddFormattedText(rotulo + ": ", TextFormat.Bold);
                p.AddText(texto!);
            }
        }

        if (!string.IsNullOrWhiteSpace(a.ImagemEncontradaUrl) || !string.IsNullOrWhiteSpace(a.ImagemRequeridaUrl))
        {
            r = t.AddRow();
            r.TopPadding = Unit.FromPoint(6);
            r.Cells[0].MergeRight = 1;
            r.Cells[2].MergeRight = 1;
            CelulaFoto(r.Cells[0], a.ImagemEncontradaUrl, "Situação encontrada", fotos);
            CelulaFoto(r.Cells[2], a.ImagemRequeridaUrl, "Situação requerida", fotos);
        }

        if (!a.Aberto)
        {
            r = t.AddRow();
            r.Cells[0].MergeRight = 3;
            r.Cells[0].Shading.Color = CinzaClaro;
            Paragrafo(r.Cells[0], "Encerramento", 9.5, true, Baixo);
            Paragrafo(r.Cells[0], "Data: " + Rotulos.Data(a.DataEncerramento), 9);
            if (!string.IsNullOrWhiteSpace(a.ObservacoesEncerramento)) Paragrafo(r.Cells[0], a.ObservacoesEncerramento, 9);
            if (!string.IsNullOrWhiteSpace(a.ImagemEvidenciaEncerramentoUrl))
                CelulaFoto(r.Cells[0], a.ImagemEvidenciaEncerramentoUrl, "Evidência de encerramento", fotos);
        }

        var ultima = t.AddRow();
        ultima.Height = Unit.FromPoint(6);
        t.SetEdge(0, 0, 4, t.Rows.Count, Edge.Box, BorderStyle.Single, 0.75, Borda);

        s.AddParagraph().Format.SpaceAfter = Unit.FromPoint(10);
    }

    private static void CelulaFoto(Cell cel, string? url, string legenda, Dictionary<string, byte[]?> fotos)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            cel.Format.Alignment = ParagraphAlignment.Center;
            Paragrafo(cel, "(sem imagem)", 8.5, italico: true, cor: Cinza);
            return;
        }
        Imagem(cel, fotos.GetValueOrDefault(url), ImgLargura, legenda);
    }

    private static void ParecerTecnico(Section s, VistoriaDto v, List<ApontamentoDto> pendentes)
    {
        TituloSecao(s, "Parecer Técnico");
        var texto = new StringBuilder();
        if (v.TotalApontamentos == 0)
        {
            texto.Append("Nenhuma não conformidade foi identificada durante esta vistoria. ")
                 .Append("A unidade/propriedade encontra-se em conformidade com os critérios verificados nesta visita.");
        }
        else
        {
            texto.Append($"Foram identificadas {v.TotalApontamentos} não conformidade(s) nesta vistoria, das quais {v.Encerrados} já foram encerradas e {v.EmAberto} permanece(m) em aberto");
            var graves = v.Criticos + v.Altos;
            if (graves > 0)
                texto.Append($", sendo {graves}")
                     .Append(v.Criticos > 0 ? " classificada(s) como crítica ou alta prioridade" : " de alta prioridade")
                     .Append(", que requer(em) atenção imediata da equipe responsável");
            texto.Append('.');
            texto.Append(pendentes.Count > 0
                ? " Recomenda-se o acompanhamento dos prazos definidos no plano de ação e a verificação em campo do encerramento de cada item antes da próxima vistoria."
                : " Todos os apontamentos desta vistoria já foram encerrados; recomenda-se manter o monitoramento contínuo até a próxima verificação programada.");
        }
        Paragrafo(s, texto.ToString(), 10, espacoDepois: 6);

        if (pendentes.Count > 0)
        {
            Paragrafo(s, "Itens em aberto para acompanhamento:", 9.5, true).Format.SpaceBefore = Unit.FromPoint(4);
            foreach (var a in pendentes)
            {
                var p = s.AddParagraph();
                p.Format.Font.Size = 9.5;
                p.Format.Font.Color = CorRisco(a.GrauRisco);
                p.AddFormattedText("• " + a.Anomalia + " ", TextFormat.Bold);
                p.AddText($"({Rotulos.RotuloGrauRisco(a.GrauRisco)}) — prazo: {(a.Prazo.HasValue ? Rotulos.Data(a.Prazo) : "-")} — responsável: {Ou(a.ResponsavelExecucao)}");
            }
        }
    }

    private static void Assinaturas(Section s, VistoriaDto v)
    {
        var t = Tabela(s, new double[] { 1, 1 }, false);
        var r = t.AddRow();
        r.TopPadding = Unit.FromPoint(30);
        void Assinatura(Cell c, string rotulo, string? nome, string? cargo)
        {
            Paragrafo(c, "_______________________________", 11);
            Paragrafo(c, Ou(nome) + (string.IsNullOrWhiteSpace(cargo) ? "" : " — " + cargo), 10.5, true);
            Paragrafo(c, rotulo, 9, cor: Cinza);
        }
        Assinatura(r.Cells[0], "Realizado por", v.TecnicoResponsavel, v.CargoTecnico);
        Assinatura(r.Cells[1], "Aprovado por", v.AprovadoPor, v.CargoAprovador);
    }

    private static Color CorRisco(string? grau) => grau switch
    {
        "CRITICO" => Critico,
        "ALTO" => Alto,
        "MEDIO" => Medio,
        "BAIXO" => Baixo,
        _ => Colors.Black,
    };
}
