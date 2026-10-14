using System.Globalization;
using System.Text;
using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Application.Servicos;

public partial class ConformidadeAmbientalServico
{
    private sealed class AnaliseMapas
    {
        public List<int> Anos = new();
        public int Ultimo;
        public List<LinhaAreaCar> PorArea = new();
        public List<LinhaClasse> Classes = new();
        public JanelaImagem? JanelaImovel;
        public string? MapaMudanca;
        public double[]? MapaLimites;
        public List<AreaConversao> Areas = new();
        public int ManchasPequenas;
        public decimal ManchasPequenasHa;
        public decimal RegeneracaoHa;
        public List<ItemLegenda> Legenda = new();
    }

    private AnaliseMapas? AnalisarMapas(List<int> anos, List<MarcoTemporal> marcos, double[] caixa,
        List<GeoUtil.Poligono> imovel, List<GeoUtil.Poligono> rl, List<GeoUtil.Poligono> app, double lat0, List<string> avisos)
    {
        var rec = new Dictionary<int, RecorteCobertura>();
        foreach (var a in anos)
        {
            var r = _cobertura.Recortar(a, caixa[0], caixa[1], caixa[2], caixa[3]);
            if (r is not null) rec[a] = r;
        }
        if (rec.Count < 2)
        {
            avisos.Add("O imóvel fica fora da área coberta pelos mapas do MapBiomas da pasta — recorte uma região maior com o RecortarMapBiomas.");
            return null;
        }
        var g = rec.Values.First();
        int w = g.Largura, h = g.Altura, n = w * h;
        double x0 = g.X0, y0 = g.Y0, dx = g.Dx, dy = g.Dy;
        if (rec.Values.Any(r => r.Largura != w || r.Altura != h || Math.Abs(r.X0 - x0) > 1e-9 || Math.Abs(r.Y0 - y0) > 1e-9))
        {
            avisos.Add("Os mapas do MapBiomas da pasta não têm todos a mesma grade — recorte todos os anos com os mesmos limites.");
            return null;
        }
        var ultimo = rec.Keys.Max();
        var cUlt = rec[ultimo].Classes;
        var areaLinha = new double[h];
        for (var l = 0; l < h; l++) areaLinha[l] = dx * 111_320 * Math.Cos((y0 - (l + 0.5) * dy) * Math.PI / 180) * dy * 110_574 / 10_000;
        double A(int i) => areaLinha[i / w];

        var mImovel = RasterUtil.Mascara(imovel, x0, y0, dx, dy, w, h);
        var mRl = RasterUtil.Mascara(rl, x0, y0, dx, dy, w, h);
        var mApp = RasterUtil.Mascara(app, x0, y0, dx, dy, w, h);

        var anosComp = marcos.Where(m => m.AnoUsado is not null && rec.ContainsKey(m.AnoUsado.Value)).Select(m => m.AnoUsado!.Value)
            .Append(ultimo).Distinct().OrderBy(a => a).ToList();
        var res = new AnaliseMapas { Anos = anosComp, Ultimo = ultimo };

        (string Nome, Func<int, bool> Dentro)[] partes =
        {
            ("Imóvel inteiro", i => mImovel[i]),
            ("Reserva Legal", i => mRl[i] && mImovel[i]),
            ("APP", i => mApp[i] && mImovel[i]),
            ("Demais áreas (fora de RL e APP)", i => mImovel[i] && !mRl[i] && !mApp[i]),
        };
        foreach (var (nome, dentro) in partes)
        {
            double area = 0;
            var nat = anosComp.ToDictionary(a => a, _ => 0.0);
            for (var i = 0; i < n; i++)
            {
                if (!dentro(i)) continue;
                var a = A(i);
                area += a;
                foreach (var ano in anosComp) if (LegendaMapBiomas.Natural(rec[ano].Classes[i])) nat[ano] += a;
            }
            if (area <= 0 && nome != "Imóvel inteiro") continue;
            res.PorArea.Add(new LinhaAreaCar(nome, R(area), nat.ToDictionary(k => k.Key, k => R(k.Value))));
        }

        var porClasse = new Dictionary<int, Dictionary<int, double>>();
        for (var i = 0; i < n; i++)
        {
            if (!mImovel[i]) continue;
            foreach (var ano in anosComp)
            {
                var c = rec[ano].Classes[i];
                if (!porClasse.TryGetValue(c, out var d)) porClasse[c] = d = anosComp.ToDictionary(a => a, _ => 0.0);
                d[ano] += A(i);
            }
        }
        res.Classes = porClasse.Where(kv => kv.Value.Values.Max() >= 0.5)
            .OrderByDescending(kv => kv.Value[ultimo]).ThenByDescending(kv => kv.Value.Values.Max())
            .Select(kv =>
            {
                var c = LegendaMapBiomas.Obter(kv.Key);
                return new LinhaClasse(kv.Key, LegendaMapBiomas.Nome(kv.Key), c?.Cor ?? "#cccccc", c?.Tipo == LegendaMapBiomas.Tipo.Natural,
                    kv.Value.ToDictionary(x => x.Key, x => R(x.Value)));
            }).ToList();

        int? AnoMarco(string chave)
        {
            var m = marcos.FirstOrDefault(x => x.Chave == chave);
            if (m?.AnoUsado is not int a || !rec.ContainsKey(a)) return null;
            return chave == "2008" || a >= m.AnoCorte ? a : null; // mapa anterior ao corte não separa antes/depois
        }
        var a2008 = AnoMarco("2008"); var a2014 = AnoMarco("2014"); var a2020 = AnoMarco("2020");
        var anosMarco = marcos.Where(m => m.AnoUsado is int a && rec.ContainsKey(a)).Select(m => m.AnoUsado!.Value).Distinct().ToList();
        var conv = new bool[n];
        double regeneracao = 0;
        for (var i = 0; i < n; i++)
        {
            if (!mImovel[i]) continue;
            if (LegendaMapBiomas.Antropico(cUlt[i]))
                conv[i] = anosMarco.Any(a => LegendaMapBiomas.Natural(rec[a].Classes[i]));
            else if (a2008 is not null && LegendaMapBiomas.Natural(cUlt[i]) && LegendaMapBiomas.Antropico(rec[a2008.Value].Classes[i]))
                regeneracao += A(i);
        }
        res.RegeneracaoHa = R(regeneracao);

        var (rot, nr) = RasterUtil.Rotular(conv, w, h);
        var pix = new List<int>[nr + 1];
        for (var i = 0; i < n; i++) if (rot[i] > 0) (pix[rot[i]] ??= new List<int>()).Add(i);
        var anosOrdem = rec.Keys.OrderBy(a => a).ToList();
        var areas = new List<(AreaConversao Area, List<int> Pixels)>();
        for (var k = 1; k <= nr; k++)
        {
            var px = pix[k];
            var ha = px.Sum(A);
            if (px.Count < MinPixels) { res.ManchasPequenas++; res.ManchasPequenasHa += (decimal)ha; continue; }
            bool NaturalEm(int ano) => px.Count(i => LegendaMapBiomas.Natural(rec[ano].Classes[i])) * 2 >= px.Count;
            var marco = a2020 is not null && NaturalEm(a2020.Value) ? "2020" : a2014 is not null && NaturalEm(a2014.Value) ? "2014" : "2008";
            int? anoNat = anosOrdem.Where(a => a < ultimo && NaturalEm(a)).Select(a => (int?)a).DefaultIfEmpty(null).Max();
            int? anoConv = anoNat is null ? null : anosOrdem.Where(a => a > anoNat && !NaturalEm(a)).Select(a => (int?)a).DefaultIfEmpty(null).Min();
            string Moda(int ano, bool soNatural) => LegendaMapBiomas.Nome(px.Select(i => rec[ano].Classes[i])
                .Where(c => !soNatural || LegendaMapBiomas.Natural(c)).GroupBy(c => c).OrderByDescending(gr => gr.Count()).Select(gr => (int)gr.Key).FirstOrDefault());
            int c0 = px.Min(i => i % w), c1 = px.Max(i => i % w), l0 = px.Min(i => i / w), l1 = px.Max(i => i / w);
            var cx = px.Average(i => i % w + 0.5); var cy = px.Average(i => i / w + 0.5);
            var periodo = marco switch
            {
                "2020" => $"Depois de {a2020}",
                "2014" => a2020 is null ? $"Depois de {a2014}" : $"Entre {a2014} e {a2020}",
                _ => (a2014 ?? a2020) is int prox ? $"Entre {a2008} e {prox}" : $"Depois de {a2008}",
            };
            var area = new AreaConversao(0, R(ha), R(px.Where(i => mRl[i]).Sum(A)), R(px.Where(i => mApp[i]).Sum(A)),
                anoNat, anoConv, anoNat is null ? "—" : Moda(anoNat.Value, true), Moda(ultimo, false), marco,
                x0 + cx * dx, y0 - cy * dy, null!, false, periodo, anosOrdem.Where(NaturalEm).ToList());
            areas.Add((area, px));
        }
        static int Gravidade(string m) => m == "2020" ? 0 : m == "2014" ? 1 : 2;
        areas = areas.OrderBy(x => Gravidade(x.Area.Marco)).ThenByDescending(x => x.Area.AreaHa).ToList();

        var lat = lat0;
        var mxx = 111_320 * Math.Cos(lat * Math.PI / 180);
        string? Contorno(List<GeoUtil.Poligono> pols) => pols.Count == 0 ? null : SvgPoligonos(pols);
        var svgImovel = SvgPoligonos(imovel);
        var svgRl = Contorno(rl);
        var svgApp = Contorno(app);
        res.JanelaImovel = Janela(new[] { x0, y0 - h * dy, x0 + w * dx, y0 }, anosComp, svgImovel, null, svgRl, svgApp, mxx, 520);

        var lista = new List<AreaConversao>();
        for (var k = 0; k < areas.Count && k < MaxAreas; k++)
        {
            var (a, px) = areas[k];
            var contorno = SvgPixels(px, w, x0, y0, dx, dy);
            var mancha = new[] { px.Min(i => i % w), px.Min(i => i / w), px.Max(i => i % w), px.Max(i => i / w) };
            var larguraM = (mancha[2] - mancha[0] + 1) * dx * mxx;
            var alturaM = (mancha[3] - mancha[1] + 1) * dy * 110_574;
            var lado = Math.Max(900, Math.Max(larguraM, alturaM) * 2.2);
            var cxL = x0 + (mancha[0] + mancha[2] + 1) / 2.0 * dx;
            var cyL = y0 - (mancha[1] + mancha[3] + 1) / 2.0 * dy;
            var jan = new[] { cxL - lado / 2 / mxx, cyL - lado / 2 / 110_574, cxL + lado / 2 / mxx, cyL + lado / 2 / 110_574 };
            var janela = k < MaxAreasComImagem
                ? Janela(jan, anosComp, contorno, svgImovel, svgRl, svgApp, mxx, 240)
                : new JanelaImagem(jan, 1, new(), contorno, svgImovel, svgRl, svgApp);
            lista.Add(a with { Numero = k + 1, Janela = janela, ComSatelite = k < MaxAreasComSatelite });
        }
        if (areas.Count > MaxAreas)
            avisos.Add($"Foram encontradas {areas.Count} áreas; o relatório detalha as {MaxAreas} maiores/mais recentes.");
        res.Areas = lista;

        var convMapa = new bool[n];
        for (var i = 0; i < n; i++)
            convMapa[i] = LegendaMapBiomas.Antropico(cUlt[i]) && anosMarco.Any(a => LegendaMapBiomas.Natural(rec[a].Classes[i]));
        var (rotMapa, nrMapa) = RasterUtil.Rotular(convMapa, w, h);
        var tamMapa = new int[nrMapa + 1];
        for (var i = 0; i < n; i++) if (rotMapa[i] > 0) tamMapa[rotMapa[i]]++;
        var idx = new byte[n];
        for (var i = 0; i < n; i++)
        {
            var u = cUlt[i];
            if (LegendaMapBiomas.Antropico(u))
            {
                if (rotMapa[i] == 0 || tamMapa[rotMapa[i]] < MinPixels) continue;
                if (a2020 is not null && LegendaMapBiomas.Natural(rec[a2020.Value].Classes[i])) idx[i] = 2;
                else if (a2014 is not null && LegendaMapBiomas.Natural(rec[a2014.Value].Classes[i])) idx[i] = 3;
                else if (a2008 is not null && LegendaMapBiomas.Natural(rec[a2008.Value].Classes[i])) idx[i] = 4;
            }
            else if (LegendaMapBiomas.Natural(u))
                idx[i] = a2008 is not null && LegendaMapBiomas.Antropico(rec[a2008.Value].Classes[i]) ? (byte)5 : (byte)1;
        }
        res.MapaMudanca = RasterUtil.DataUri(RasterUtil.Png(RasterUtil.Mercator(idx, w, h, y0, dy), w, h, PaletaMudanca));
        res.MapaLimites = new[] { x0, y0 - h * dy, x0 + w * dx, y0 };
        res.Legenda = new List<ItemLegenda>();
        if (a2020 is not null) res.Legenda.Add(new("#d7261e", $"Vegetação nativa convertida depois de {a2020}"));
        if (a2014 is not null) res.Legenda.Add(new("#f07d1a", a2020 is null ? $"Convertida depois de {a2014}" : $"Convertida entre {a2014} e {a2020}"));
        if (a2008 is not null) res.Legenda.Add(new("#f2c81d", (a2014 ?? a2020) is int px2 ? $"Convertida entre {a2008} e {px2}" : $"Convertida depois de {a2008}"));
        res.Legenda.Add(new("#2a78d6", "Voltou a ser vegetação nativa"));
        res.Legenda.Add(new("rgba(46,157,87,.45)", "Vegetação nativa mantida"));
        return res;
    }

    public static readonly IReadOnlyList<(byte R, byte G, byte B, byte A)> PaletaMudanca = new List<(byte, byte, byte, byte)>
    {
        (0, 0, 0, 0),
        (0x2e, 0x9d, 0x57, 95),
        (0xd7, 0x26, 0x1e, 245),
        (0xf0, 0x7d, 0x1a, 245),
        (0xf2, 0xc8, 0x1d, 245),
        (0x2a, 0x78, 0xd6, 230),
    };

    private JanelaImagem Janela(double[] jan, List<int> anos, string principal, string? imovel, string? rl, string? app, double mPorGrauLon, int ladoAlvoPx)
    {
        var imgs = new List<ImagemAno>();
        double[] caixaReal = jan;
        foreach (var ano in anos)
        {
            var r = _cobertura.Recortar(ano, jan[0], jan[1], jan[2], jan[3]);
            if (r is null) { imgs.Add(new ImagemAno(ano, null)); continue; }
            caixaReal = new[] { r.X0, r.Y0 - r.Altura * r.Dy, r.X0 + r.Largura * r.Dx, r.Y0 };
            var escala = Math.Clamp((int)Math.Round((double)ladoAlvoPx / Math.Max(r.Largura, r.Altura)), 1, 12);
            imgs.Add(new ImagemAno(ano, RasterUtil.DataUri(RasterUtil.Png(r.Classes, r.Largura, r.Altura, LegendaMapBiomas.Paleta, escala))));
        }
        var proporcao = (caixaReal[2] - caixaReal[0]) * mPorGrauLon / ((caixaReal[3] - caixaReal[1]) * 110_574);
        return new JanelaImagem(caixaReal, Math.Round(proporcao, 4), imgs, principal, imovel, rl, app);
    }

    public static string SvgPoligonos(IEnumerable<GeoUtil.Poligono> pols)
    {
        var sb = new StringBuilder();
        foreach (var p in pols)
            foreach (var anel in p.Aneis)
            {
                for (var i = 0; i < anel.Length; i++)
                    sb.Append(i == 0 ? 'M' : 'L').Append(anel[i].X.ToString("0.######", Inv)).Append(',').Append((-anel[i].Y).ToString("0.######", Inv));
                sb.Append('Z');
            }
        return sb.ToString();
    }

    private static string SvgPixels(List<int> px, int w, double x0, double y0, double dx, double dy)
    {
        var set = new HashSet<int>(px);
        var sb = new StringBuilder();
        string X(int c) => (x0 + c * dx).ToString("0.######", Inv);
        string Y(int l) => (-(y0 - l * dy)).ToString("0.######", Inv);
        foreach (var i in px)
        {
            int c = i % w, l = i / w;
            if (l == 0 || !set.Contains(i - w)) sb.Append('M').Append(X(c)).Append(',').Append(Y(l)).Append('H').Append(X(c + 1));
            if (!set.Contains(i + w)) sb.Append('M').Append(X(c)).Append(',').Append(Y(l + 1)).Append('H').Append(X(c + 1));
            if (c == 0 || !set.Contains(i - 1)) sb.Append('M').Append(X(c)).Append(',').Append(Y(l)).Append('V').Append(Y(l + 1));
            if (c == w - 1 || !set.Contains(i + 1)) sb.Append('M').Append(X(c + 1)).Append(',').Append(Y(l)).Append('V').Append(Y(l + 1));
        }
        return sb.ToString();
    }

    private static decimal R(double v) => Math.Round((decimal)v, 1);
}
