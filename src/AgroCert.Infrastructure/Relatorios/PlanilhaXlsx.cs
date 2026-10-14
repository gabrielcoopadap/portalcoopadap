using System.IO.Compression;
using System.Security;
using System.Text;

namespace AgroCert.Infrastructure.Relatorios;

internal sealed class PlanilhaXlsx
{
    public sealed record EstiloCelula(
        bool Negrito = false, bool Italico = false, double Tamanho = 11, string? CorFonte = null,
        string? Preenchimento = null, bool Borda = false, string? CorBorda = null,
        string Horizontal = "general", string Vertical = "top", bool QuebraTexto = false);

    private sealed record Celula(object? Valor, int Estilo);

    private readonly string _nomeAba;
    private readonly SortedDictionary<int, SortedDictionary<int, Celula>> _linhas = new();
    private readonly Dictionary<int, double> _alturas = new();
    private readonly Dictionary<int, double> _larguras = new();
    private readonly List<string> _mesclas = new();
    private readonly List<EstiloCelula> _estilos = new() { new EstiloCelula() };
    private int _congelarLinhas;
    private string? _autoFiltro;

    public PlanilhaXlsx(string nomeAba) => _nomeAba = nomeAba;

    public int Estilo(EstiloCelula e)
    {
        var i = _estilos.IndexOf(e);
        if (i >= 0) return i;
        _estilos.Add(e);
        return _estilos.Count - 1;
    }

    public void Definir(int linha, int coluna, object? valor, int estilo = 0)
    {
        if (!_linhas.TryGetValue(linha, out var l)) _linhas[linha] = l = new SortedDictionary<int, Celula>();
        l[coluna] = new Celula(valor, estilo);
    }

    public void LarguraColuna(int coluna, double largura) => _larguras[coluna] = largura;
    public void AlturaLinha(int linha, double altura) => _alturas[linha] = altura;
    public void Mesclar(int linha1, int coluna1, int linha2, int coluna2) =>
        _mesclas.Add($"{Ref(linha1, coluna1)}:{Ref(linha2, coluna2)}");
    public void CongelarLinhas(int quantidade) => _congelarLinhas = quantidade;
    public void AutoFiltro(int linha, int coluna1, int coluna2) => _autoFiltro = $"{Ref(linha, coluna1)}:{Ref(linha, coluna2)}";

    public static string Ref(int linha, int coluna)
    {
        var sb = new StringBuilder();
        while (coluna > 0)
        {
            var r = (coluna - 1) % 26;
            sb.Insert(0, (char)('A' + r));
            coluna = (coluna - 1) / 26;
        }
        return sb.Append(linha).ToString();
    }

    public byte[] Gerar()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            Escrever(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                <Default Extension="xml" ContentType="application/xml"/>
                <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
                </Types>
                """);
            Escrever(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
                </Relationships>
                """);
            Escrever(zip, "docProps/core.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                <dc:creator>AgroCert — Coopadap</dc:creator>
                <dcterms:created xsi:type="dcterms:W3CDTF">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created>
                </cp:coreProperties>
                """);
            var filtroDefinido = _autoFiltro is null ? "" :
                $"<definedNames><definedName name=\"_xlnm._FilterDatabase\" localSheetId=\"0\" hidden=\"1\">'{Esc(_nomeAba)}'!{Absoluto(_autoFiltro)}</definedName></definedNames>";
            Escrever(zip, "xl/workbook.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                <sheets><sheet name="{Esc(_nomeAba)}" sheetId="1" r:id="rId1"/></sheets>{filtroDefinido}
                </workbook>
                """);
            Escrever(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            Escrever(zip, "xl/styles.xml", GerarEstilos());
            Escrever(zip, "xl/worksheets/sheet1.xml", GerarAba());
        }
        return ms.ToArray();
    }

    private static string Absoluto(string intervalo) =>
        string.Join(":", intervalo.Split(':').Select(r =>
        {
            var i = r.IndexOfAny("0123456789".ToCharArray());
            return "$" + r[..i] + "$" + r[i..];
        }));

    private string GerarAba()
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">""");
        sb.Append("""<sheetPr><pageSetUpPr fitToPage="1"/></sheetPr>""");
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\">");
        if (_congelarLinhas > 0)
            sb.Append($"<pane ySplit=\"{_congelarLinhas}\" topLeftCell=\"A{_congelarLinhas + 1}\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\"/>");
        sb.Append("</sheetView></sheetViews>");
        sb.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");

        if (_larguras.Count > 0)
        {
            sb.Append("<cols>");
            foreach (var (col, w) in _larguras.OrderBy(k => k.Key))
                sb.Append($"<col min=\"{col}\" max=\"{col}\" width=\"{w.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
            sb.Append("</cols>");
        }

        sb.Append("<sheetData>");
        foreach (var (linha, celulas) in _linhas)
        {
            sb.Append($"<row r=\"{linha}\"");
            if (_alturas.TryGetValue(linha, out var h))
                sb.Append($" ht=\"{h.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" customHeight=\"1\"");
            sb.Append('>');
            foreach (var (col, cel) in celulas)
            {
                var r = Ref(linha, col);
                switch (cel.Valor)
                {
                    case null:
                        sb.Append($"<c r=\"{r}\" s=\"{cel.Estilo}\"/>");
                        break;
                    case int or long or double or decimal:
                        sb.Append($"<c r=\"{r}\" s=\"{cel.Estilo}\"><v>{Convert.ToString(cel.Valor, System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
                        break;
                    default:
                        sb.Append($"<c r=\"{r}\" s=\"{cel.Estilo}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(cel.Valor.ToString()!)}</t></is></c>");
                        break;
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");

        if (_autoFiltro != null) sb.Append($"<autoFilter ref=\"{_autoFiltro}\"/>");
        if (_mesclas.Count > 0)
        {
            sb.Append($"<mergeCells count=\"{_mesclas.Count}\">");
            foreach (var m in _mesclas) sb.Append($"<mergeCell ref=\"{m}\"/>");
            sb.Append("</mergeCells>");
        }
        sb.Append("""<pageMargins left="0.5" right="0.5" top="0.6" bottom="0.6" header="0.3" footer="0.3"/>""");
        sb.Append("""<pageSetup paperSize="9" orientation="landscape" fitToWidth="1" fitToHeight="0"/>""");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private string GerarEstilos()
    {
        var fontes = new List<string>();
        var preenchimentos = new List<string> { "<fill><patternFill patternType=\"none\"/></fill>", "<fill><patternFill patternType=\"gray125\"/></fill>" };
        var bordas = new List<string> { "<border><left/><right/><top/><bottom/><diagonal/></border>" };
        var xfs = new StringBuilder();

        int Indice(List<string> lista, string item)
        {
            var i = lista.IndexOf(item);
            if (i >= 0) return i;
            lista.Add(item);
            return lista.Count - 1;
        }

        foreach (var e in _estilos)
        {
            var fonte = new StringBuilder("<font>");
            if (e.Negrito) fonte.Append("<b/>");
            if (e.Italico) fonte.Append("<i/>");
            fonte.Append($"<sz val=\"{e.Tamanho.ToString(System.Globalization.CultureInfo.InvariantCulture)}\"/>");
            if (e.CorFonte != null) fonte.Append($"<color rgb=\"{Argb(e.CorFonte)}\"/>");
            fonte.Append("<name val=\"Calibri\"/><family val=\"2\"/></font>");
            var iFonte = Indice(fontes, fonte.ToString());

            var iPreench = e.Preenchimento is null ? 0 :
                Indice(preenchimentos, $"<fill><patternFill patternType=\"solid\"><fgColor rgb=\"{Argb(e.Preenchimento)}\"/><bgColor indexed=\"64\"/></patternFill></fill>");

            var iBorda = 0;
            if (e.Borda)
            {
                var cor = $"<color rgb=\"{Argb(e.CorBorda ?? "#E0DACB")}\"/>";
                iBorda = Indice(bordas, $"<border><left style=\"thin\">{cor}</left><right style=\"thin\">{cor}</right><top style=\"thin\">{cor}</top><bottom style=\"thin\">{cor}</bottom><diagonal/></border>");
            }

            xfs.Append($"<xf numFmtId=\"0\" fontId=\"{iFonte}\" fillId=\"{iPreench}\" borderId=\"{iBorda}\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\">");
            xfs.Append($"<alignment horizontal=\"{e.Horizontal}\" vertical=\"{e.Vertical}\"{(e.QuebraTexto ? " wrapText=\"1\"" : "")}/></xf>");
        }

        return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
               "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
               $"<fonts count=\"{fontes.Count}\">{string.Concat(fontes)}</fonts>" +
               $"<fills count=\"{preenchimentos.Count}\">{string.Concat(preenchimentos)}</fills>" +
               $"<borders count=\"{bordas.Count}\">{string.Concat(bordas)}</borders>" +
               "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
               $"<cellXfs count=\"{_estilos.Count}\">{xfs}</cellXfs>" +
               "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
               "</styleSheet>";
    }

    private static string Argb(string hex) => "FF" + hex.TrimStart('#').ToUpperInvariant();

    private static string Esc(string s)
    {
        // remove caracteres de controle inválidos em XML (exceto \t \n \r)
        var limpo = new string(s.Where(c => c >= 0x20 || c is '\t' or '\n' or '\r').ToArray());
        return SecurityElement.Escape(limpo)!;
    }

    private static void Escrever(ZipArchive zip, string caminho, string conteudo)
    {
        var entrada = zip.CreateEntry(caminho, CompressionLevel.Optimal);
        using var s = entrada.Open();
        var bytes = new UTF8Encoding(false).GetBytes(conteudo.Trim());
        s.Write(bytes);
    }
}
