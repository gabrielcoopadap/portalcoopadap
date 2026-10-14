using AgroCert.Application.Interfaces;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace AgroCert.Infrastructure.Relatorios;

internal sealed class ResolvedorFontes : IFontResolver
{
    public const string Familia = "Liberation Sans";
    private static readonly object Trava = new();
    private static bool _registrado;

    private readonly string _pasta = Path.Combine(AppContext.BaseDirectory, "Fontes");

    public static void Registrar()
    {
        lock (Trava)
        {
            if (_registrado) return;
            if (GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new ResolvedorFontes();
            _registrado = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        var face = (bold, italic) switch
        {
            (true, true) => "LiberationSans-BoldItalic",
            (true, false) => "LiberationSans-Bold",
            (false, true) => "LiberationSans-Italic",
            _ => "LiberationSans-Regular",
        };
        return new FontResolverInfo(face);
    }

    public byte[]? GetFont(string faceName)
    {
        var caminho = Path.Combine(_pasta, faceName + ".ttf");
        return File.Exists(caminho) ? File.ReadAllBytes(caminho) : null;
    }
}

internal static class PdfComum
{
    public static readonly Color Verde = new(94, 154, 66);
    public static readonly Color VerdeEscuro = new(62, 110, 42);
    public static readonly Color Critico = new(192, 57, 43);
    public static readonly Color Alto = new(184, 134, 11);
    public static readonly Color Medio = new(31, 111, 178);
    public static readonly Color Baixo = new(31, 138, 76);
    public static readonly Color CinzaClaro = new(245, 245, 245);
    public static readonly Color Borda = new(224, 224, 224);
    public static readonly Color Cinza = new(128, 128, 128);
    public static readonly Color CinzaEscuro = new(80, 80, 80);

    public const double LarguraUtil = 523;

    public static Document NovoDocumento(string titulo)
    {
        ResolvedorFontes.Registrar();
        var doc = new Document();
        doc.Info.Title = titulo;
        doc.Info.Author = "AgroCert — Coopadap";

        var normal = doc.Styles["Normal"]!;
        normal.Font.Name = ResolvedorFontes.Familia;
        normal.Font.Size = 10;

        var sec = doc.AddSection();
        sec.PageSetup = doc.DefaultPageSetup.Clone();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromPoint(36);
        sec.PageSetup.LeftMargin = Unit.FromPoint(36);
        sec.PageSetup.RightMargin = Unit.FromPoint(36);
        sec.PageSetup.BottomMargin = Unit.FromPoint(50);
        sec.PageSetup.FooterDistance = Unit.FromPoint(20);
        return doc;
    }

    public static byte[] Renderizar(Document doc)
    {
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public static Paragraph Paragrafo(Section s, string texto, double tamanho = 10, bool negrito = false,
        Color? cor = null, bool italico = false, double espacoDepois = 4)
    {
        var p = s.AddParagraph(texto);
        Formatar(p, tamanho, negrito, cor, italico, espacoDepois);
        return p;
    }

    public static Paragraph Paragrafo(Cell c, string texto, double tamanho = 10, bool negrito = false,
        Color? cor = null, bool italico = false, double espacoDepois = 0)
    {
        var p = c.AddParagraph(texto);
        Formatar(p, tamanho, negrito, cor, italico, espacoDepois);
        return p;
    }

    private static void Formatar(Paragraph p, double tamanho, bool negrito, Color? cor, bool italico, double espacoDepois)
    {
        p.Format.Font.Size = tamanho;
        p.Format.Font.Bold = negrito;
        p.Format.Font.Italic = italico;
        if (cor.HasValue) p.Format.Font.Color = cor.Value;
        p.Format.SpaceAfter = Unit.FromPoint(espacoDepois);
    }

    public static Paragraph TituloSecao(Section s, string texto, double tamanho = 13)
    {
        var p = Paragrafo(s, texto, tamanho, true, VerdeEscuro, espacoDepois: 6);
        p.Format.SpaceBefore = Unit.FromPoint(12);
        p.Format.KeepWithNext = true;
        return p;
    }

    public static Table Tabela(Section s, double[] pesos, bool bordas, double largura = LarguraUtil)
    {
        var t = s.AddTable();
        var soma = pesos.Sum();
        foreach (var peso in pesos) t.AddColumn(Unit.FromPoint(largura * peso / soma));
        if (bordas)
        {
            t.Borders.Width = 0.5;
            t.Borders.Color = Borda;
        }
        t.TopPadding = Unit.FromPoint(3);
        t.BottomPadding = Unit.FromPoint(3);
        t.LeftPadding = Unit.FromPoint(4);
        t.RightPadding = Unit.FromPoint(4);
        return t;
    }

    public static string Ou(string? v, string padrao = "-") => string.IsNullOrWhiteSpace(v) ? padrao : v;

    public static void Imagem(Cell cel, byte[]? bytes, double largura, string legenda)
    {
        cel.Format.Alignment = ParagraphAlignment.Center;
        if (bytes is null || bytes.Length == 0)
        {
            Paragrafo(cel, "(imagem indisponível)", 8.5, italico: true, cor: Cinza);
            return;
        }
        if (!EhPngOuJpeg(bytes))
        {
            Paragrafo(cel, "(formato de imagem não suportado no PDF)", 8.5, italico: true, cor: Cinza);
            return;
        }
        var img = cel.AddImage("base64:" + Convert.ToBase64String(bytes));
        img.LockAspectRatio = true;
        img.Width = Unit.FromPoint(largura);
        var legendaP = Paragrafo(cel, legenda, 8, cor: Cinza);
        legendaP.Format.SpaceBefore = Unit.FromPoint(2);
    }

    public static bool EhPngOuJpeg(byte[] b) =>
        (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) ||
        (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF);
}

public class CarregadorImagens
{
    private readonly IArmazenamentoArquivos _arquivos;
    private readonly IHttpClientFactory _http;

    public CarregadorImagens(IArmazenamentoArquivos arquivos, IHttpClientFactory http)
    {
        _arquivos = arquivos;
        _http = http;
    }

    public async Task<byte[]?> CarregarAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var cliente = _http.CreateClient("imagens-relatorio");
                return await cliente.GetByteArrayAsync(url, ct);
            }
            return await _arquivos.LerAsync(url, ct);
        }
        catch
        {
            return null; // uma URL fora do ar não derruba o PDF inteiro
        }
    }
}
