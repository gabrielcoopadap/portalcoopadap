using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using static AgroCert.Infrastructure.Relatorios.PdfComum;

namespace AgroCert.Infrastructure.Relatorios;

public class RelatorioAmbientalPdf : IRelatorioAmbientalPdf
{
    private const double ImgLargura = 130;
    private readonly CarregadorImagens _imagens;

    public RelatorioAmbientalPdf(CarregadorImagens imagens) => _imagens = imagens;

    public async Task<byte[]> GerarAsync(RelatorioAmbientalDto r, CancellationToken ct = default)
    {
        var comImagem = r.Alertas.Where(a => a.ImagemAntesUrl != null || a.ImagemDepoisUrl != null).ToList();
        var fotos = new Dictionary<string, byte[]?>();
        foreach (var url in comImagem.SelectMany(a => new[] { a.ImagemAntesUrl, a.ImagemDepoisUrl }))
            if (!string.IsNullOrWhiteSpace(url) && !fotos.ContainsKey(url))
                fotos[url] = await _imagens.CarregarAsync(url, ct);

        var doc = NovoDocumento("Monitoramento Ambiental — " + r.PropriedadeNome);
        var s = doc.LastSection;

        var f = s.Footers.Primary.AddParagraph();
        f.Format.Font.Size = 7.5;
        f.Format.Font.Color = Cinza;
        f.Format.TabStops.AddTabStop(Unit.FromPoint(LarguraUtil), TabAlignment.Right);
        f.AddText("CAR " + r.CarCodigo);
        f.AddTab();
        f.AddText("Página ");
        f.AddPageField();

        Paragrafo(s, "Monitoramento Ambiental", 16, true, VerdeEscuro, espacoDepois: 2);
        Paragrafo(s, r.PropriedadeNome, 13, espacoDepois: 2);
        Paragrafo(s, "CAR: " + r.CarCodigo, 10, cor: CinzaEscuro, espacoDepois: 2);
        Paragrafo(s, $"Emitido em {DateTime.Now:dd/MM/yyyy} às {DateTime.Now:HH:mm}", 8.5, cor: Cinza, espacoDepois: 12);

        var dados = Tabela(s, new double[] { 1, 1 }, true);
        void Linha(string rotulo, string valor)
        {
            var row = dados.AddRow();
            Paragrafo(row.Cells[0], rotulo, 10, true);
            Paragrafo(row.Cells[1], valor, 10);
        }
        Linha("Área do imóvel", Ha(r.AreaHa));
        Linha("Reserva Legal", Ha(r.AreaReservaLegalHa));
        Linha("APP", Ha(r.AreaAppHa));
        Linha("Vegetação nativa", Ha(r.AreaVegetacaoNativaHa));
        Linha("Alertas de desmatamento", r.TotalAlertas.ToString());
        Linha("Área total dos alertas", Ha(r.AreaTotalAlertasHa));
        Linha("Último alerta", Rotulos.Data(r.UltimoAlerta).Replace("—", "-"));
        Linha("Situação", r.Situacao);
        Linha("Fontes consultadas", string.Join(", ", r.FontesConsultadas));

        if (r.Alertas.Count > 0)
        {
            TituloSecao(s, "Alertas encontrados");
            var t = Tabela(s, new double[] { 2, 2, 1, 2 }, true);
            var h = t.AddRow();
            h.HeadingFormat = true;
            h.Shading.Color = CinzaClaro;
            foreach (var (titulo, i) in new[] { "Código", "Data", "Área (ha)", "Status" }.Select((x, i) => (x, i)))
                Paragrafo(h.Cells[i], titulo, 9.5, true);
            foreach (var a in r.Alertas)
            {
                var row = t.AddRow();
                Paragrafo(row.Cells[0], Ou(a.CodigoAlerta), 9.5);
                Paragrafo(row.Cells[1], Rotulos.Data(a.DataDeteccao).Replace("—", "-"), 9.5);
                Paragrafo(row.Cells[2], Rotulos.Numero(a.AreaHa), 9.5);
                Paragrafo(row.Cells[3], Rotulos.RotuloStatusAlerta(a.Status), 9.5);
            }
        }

        LinhaDoTempo(s, comImagem, fotos);

        Paragrafo(s, r.Aviso, 9, italico: true, cor: CinzaEscuro).Format.SpaceBefore = Unit.FromPoint(14);
        return Renderizar(doc);
    }

    private static void LinhaDoTempo(Section s, List<AlertaDto> comImagem, Dictionary<string, byte[]?> fotos)
    {
        if (comImagem.Count == 0) return;

        TituloSecao(s, "Linha do Tempo de Imagens");
        Paragrafo(s, "Imagens de antes/depois de cada alerta, organizadas por ano de detecção.", 9, cor: CinzaEscuro);

        foreach (var grupo in comImagem
                     .GroupBy(a => a.DataDeteccao?.Year.ToString() ?? "Sem data")
                     .OrderBy(g => g.Key))
        {
            Paragrafo(s, grupo.Key, 11, true).Format.SpaceBefore = Unit.FromPoint(8);
            foreach (var a in grupo.OrderBy(a => a.DataDeteccao ?? DateOnly.MinValue))
            {
                var t = Tabela(s, new double[] { 1, 1, 2 }, false);
                var row = t.AddRow();
                row.BottomPadding = Unit.FromPoint(6);
                FotoOuAviso(row.Cells[0], a.ImagemAntesUrl, "Antes", fotos);
                FotoOuAviso(row.Cells[1], a.ImagemDepoisUrl, "Depois", fotos);
                row.Cells[2].VerticalAlignment = VerticalAlignment.Center;
                Paragrafo(row.Cells[2], Ou(a.CodigoAlerta), 10, true);
                if (a.DataDeteccao.HasValue) Paragrafo(row.Cells[2], Rotulos.Data(a.DataDeteccao), 10);
                if (a.AreaHa.HasValue) Paragrafo(row.Cells[2], Ha(a.AreaHa), 10);
            }
        }
    }

    private static void FotoOuAviso(Cell cel, string? url, string legenda, Dictionary<string, byte[]?> fotos)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            Paragrafo(cel, "(sem imagem)", 9, italico: true, cor: Cinza);
            return;
        }
        Imagem(cel, fotos.GetValueOrDefault(url), ImgLargura, legenda);
    }

    private static string Ha(decimal? v) => v.HasValue ? Rotulos.Numero(v) + " ha" : "-";
}
