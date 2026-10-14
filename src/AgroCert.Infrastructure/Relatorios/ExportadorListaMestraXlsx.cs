using AgroCert.Application.Interfaces;
using AgroCert.Domain.Entidades;

namespace AgroCert.Infrastructure.Relatorios;

public class ExportadorListaMestraXlsx : IExportadorListaMestra
{
    private const string VerdePortal = "#5E9A42";
    private const string VerdeEscuro = "#3E6E2A";
    private const string Borda = "#E0DACB";
    private const string Zebra = "#F4F8F1";

    private static readonly Dictionary<string, string> CorPrioridade = new()
    {
        ["Crítica"] = "#C0392B", ["Maior"] = "#BD7D15", ["Menor"] = "#5B6673", ["Recomendado"] = "#2A7F6F",
    };
    private static readonly Dictionary<string, string> CorStatus = new()
    {
        ["Conforme"] = "#1F8A4C", ["Em Andamento"] = "#1F6FB2", ["Pendente"] = "#B8860B",
        ["Não Conforme"] = "#C0392B", ["N/A"] = "#8A7F68",
    };

    private static readonly (string Titulo, double Largura)[] Colunas =
    {
        ("ID", 6), ("Requisito", 46), ("Tema", 18), ("Prioridade", 12), ("Status", 14),
        ("Referências (normas)", 34), ("O que apresentar", 62), ("Responsável", 18), ("Prazo", 12),
        ("O que é", 50), ("Passo a passo", 70), ("O que o auditor vai pedir", 50),
    };

    public byte[] Gerar(IReadOnlyList<Requisito> requisitos, string? certificacao)
    {
        var filtrada = !string.IsNullOrEmpty(certificacao);

        List<Referencia> RefsVisiveis(Requisito r) => filtrada
            ? r.Referencias.Where(x => x.Norma == certificacao).OrderBy(x => ChaveOrdem(x.Codigo)).ToList()
            : r.Referencias;
        if (filtrada)
            requisitos = requisitos.OrderBy(r => ChaveOrdem(RefsVisiveis(r).FirstOrDefault()?.Codigo)).ToList();

        var versoes = filtrada
            ? string.Join(", ", requisitos.SelectMany(RefsVisiveis).Select(x => x.Versao).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct())
            : "";
        var tituloNorma = !filtrada ? "Todas as certificações" : versoes.Length > 0 ? $"{certificacao} ({versoes})" : certificacao!;

        var p = new PlanilhaXlsx("Lista Mestra");
        var n = Colunas.Length;
        for (var i = 0; i < n; i++) p.LarguraColuna(i + 1, Colunas[i].Largura);

        p.Mesclar(1, 1, 1, n);
        p.Definir(1, 1, $"LISTA MESTRA DE CERTIFICAÇÕES — {tituloNorma}",
            p.Estilo(new(Negrito: true, Tamanho: 15, CorFonte: VerdeEscuro, Vertical: "center")));
        p.AlturaLinha(1, 26);

        p.Mesclar(2, 1, 2, n);
        p.Definir(2, 1, $"{requisitos.Count} requisitos · gerado em {DateTime.Now:dd/MM/yyyy}",
            p.Estilo(new(Italico: true, Tamanho: 10, CorFonte: "#6B6A5F")));
        p.AlturaLinha(2, 16);

        var cab = p.Estilo(new(Negrito: true, CorFonte: "#FFFFFF", Preenchimento: VerdePortal, Borda: true, CorBorda: Borda,
            Horizontal: "left", Vertical: "center", QuebraTexto: true));
        for (var i = 0; i < n; i++)
        {
            var titulo = i == 5 && filtrada ? $"Referências ({certificacao})" : Colunas[i].Titulo;
            p.Definir(3, i + 1, titulo, cab);
        }
        p.AlturaLinha(3, 22);

        var linha = 4;
        for (var idx = 0; idx < requisitos.Count; idx++, linha++)
        {
            var r = requisitos[idx];
            var zebra = idx % 2 == 1 ? Zebra : null;
            int Est(bool negrito = false, string? cor = null, string horizontal = "general") =>
                p.Estilo(new(Negrito: negrito, CorFonte: cor, Preenchimento: zebra, Borda: true, CorBorda: Borda,
                    Horizontal: horizontal, Vertical: "top", QuebraTexto: true));

            var refs = filtrada
                ? string.Join("\n", RefsVisiveis(r).Select(x => x.Codigo))
                : string.Join("\n", r.Referencias.Select(x => $"{(x.Norma ?? "").Split(' ')[0]} {x.Codigo}"));

            p.Definir(linha, 1, r.Id, Est(horizontal: "center"));
            p.Definir(linha, 2, r.Titulo ?? "", Est());
            p.Definir(linha, 3, r.Tema ?? "", Est());
            p.Definir(linha, 4, r.Prioridade ?? "", CorPrioridade.TryGetValue(r.Prioridade ?? "", out var cp) ? Est(true, cp) : Est());
            p.Definir(linha, 5, r.Status ?? "", CorStatus.TryGetValue(r.Status ?? "", out var cs) ? Est(true, cs) : Est());
            p.Definir(linha, 6, refs, Est());
            p.Definir(linha, 7, r.Observacao ?? "", Est());
            p.Definir(linha, 8, r.Responsavel ?? "", Est());
            p.Definir(linha, 9, r.Prazo?.ToString("dd/MM/yyyy") ?? "", Est());
            p.Definir(linha, 10, r.OQueE ?? "", Est());
            p.Definir(linha, 11, string.Join("\n", Requisito.Linhas(r.PassoAPasso).Select((x, i) => $"{i + 1}. {x}")), Est());
            p.Definir(linha, 12, string.Join("\n", Requisito.Linhas(r.Evidencias).Select(x => "• " + x)), Est());
        }

        p.CongelarLinhas(3);
        p.AutoFiltro(3, 1, n);
        return p.Gerar();
    }

    private static string ChaveOrdem(string? codigo) =>
        codigo is null ? "\uffff" : System.Text.RegularExpressions.Regex.Replace(codigo, @"\d+", m => m.Value.PadLeft(6, '0'));
}
