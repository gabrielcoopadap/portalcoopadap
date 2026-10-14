using System.Globalization;
using System.Text;
using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Application.Servicos;

public partial class ConformidadeAmbientalServico
{
    private (List<UcProxima>, string?) AnalisarUcs(List<GeoUtil.Poligono> imovel, double[] bb, double lat0, double mx, double my, AnaliseMapas? analise)
    {
        var lista = new List<UcProxima>();
        var feicoes = new List<string>();
        var raioZa = _ucs.RaioAmortecimentoM;
        foreach (var uc in _ucs.Proximas(bb, RaioBuscaUcM))
        {
            var dentro = GeoUtil.Intersecta(imovel, uc.Poligonos);
            var dist = dentro ? 0 : DistanciaM(imovel, uc.Poligonos, bb, lat0, mx, my);
            if (dist > RaioBuscaUcM) continue;
            decimal? haDentro = dentro ? AreaDentro(imovel, uc.Poligonos, bb, mx, my) : null;

            StatusCriterio st; string sit;
            var nome = uc.Nome + (uc.Categoria is null || uc.Nome.Contains(uc.Categoria, StringComparison.OrdinalIgnoreCase) ? "" : $" ({uc.Categoria})");
            if (uc.EhZonaAmortecimento)
            {
                st = dentro ? StatusCriterio.Verificar : StatusCriterio.Conforme;
                sit = dentro ? $"Imóvel dentro da zona de amortecimento de {uc.Nome}: produzir só com anuência/autorização do órgão gestor." : $"Fora da zona de amortecimento de {uc.Nome} ({Km(dist)} do limite).";
            }
            else if (dentro)
            {
                st = uc.Grupo == UnidadeConservacao.ProtecaoIntegral ? StatusCriterio.IndicioNaoConformidade : StatusCriterio.Verificar;
                sit = uc.Grupo == UnidadeConservacao.ProtecaoIntegral
                    ? $"Imóvel dentro de {nome}, de Proteção Integral."
                    : $"Imóvel dentro de {nome}, de {uc.Grupo}: confira o que o plano de manejo permite.";
            }
            else if (uc.TemZonaAmortecimento && dist <= raioZa)
            {
                st = StatusCriterio.Verificar;
                sit = $"A {Km(dist)} de {nome}: dentro da faixa de {raioZa / 1000.0:0.#} km — confira se a UC tem zona de amortecimento definida no plano de manejo e, se o imóvel estiver nela, guarde a autorização do órgão gestor.";
            }
            else
            {
                st = StatusCriterio.Conforme;
                sit = $"A {Km(dist)} de {nome}" + (uc.TemZonaAmortecimento ? "." : " (não tem zona de amortecimento).");
            }
            lista.Add(new UcProxima(uc.Nome, uc.Categoria, uc.Grupo, uc.Esfera, uc.EhZonaAmortecimento, dentro, haDentro, Math.Round(dist), st, sit));
            feicoes.Add($"{{\"type\":\"Feature\",\"properties\":{{\"nome\":{System.Text.Json.JsonSerializer.Serialize(uc.Nome)},\"za\":{(uc.EhZonaAmortecimento ? "true" : "false")}}},\"geometry\":{GeoJsonSimplificado(uc.Poligonos, 4000)}}}");
        }
        lista = lista.OrderBy(u => u.DistanciaM).ThenBy(u => u.Nome).ToList();
        return (lista, feicoes.Count == 0 ? null : "{\"type\":\"FeatureCollection\",\"features\":[" + string.Join(",", feicoes) + "]}");
    }

    private static string Km(double m) => m < 1000 ? $"{m:0} m" : (m / 1000).ToString("0.0", CultureInfo.GetCultureInfo("pt-BR")) + " km";

    private static double DistanciaM(List<GeoUtil.Poligono> a, List<GeoUtil.Poligono> b, double[] bbA, double lat0, double mx, double my)
    {
        double lon0 = (bbA[0] + bbA[2]) / 2;
        (double, double) P((double X, double Y) q) => ((q.X - lon0) * mx, (q.Y - lat0) * my);
        var sa = a.SelectMany(p => p.Aneis).Select(r => r.Select(P).ToArray()).ToList();
        var sb = b.SelectMany(p => p.Aneis).Select(r => r.Select(P).ToArray()).ToList();
        var melhor = double.MaxValue;
        void Medir(List<(double, double)[]> pontos, List<(double, double)[]> segs)
        {
            foreach (var anel in segs)
                for (var i = 0; i + 1 < anel.Length; i++)
                {
                    var (x1, y1) = anel[i]; var (x2, y2) = anel[i + 1];
                    foreach (var r in pontos)
                        foreach (var (px, py) in r)
                        {
                            double vx = x2 - x1, vy = y2 - y1, len = vx * vx + vy * vy;
                            var t = len == 0 ? 0 : Math.Clamp(((px - x1) * vx + (py - y1) * vy) / len, 0, 1);
                            double ex = x1 + t * vx - px, ey = y1 + t * vy - py;
                            var d = ex * ex + ey * ey;
                            if (d < melhor) melhor = d;
                        }
                }
        }
        Medir(sa, sb);
        Medir(sb, sa);
        return Math.Sqrt(melhor);
    }

    private static decimal AreaDentro(List<GeoUtil.Poligono> imovel, List<GeoUtil.Poligono> uc, double[] bb, double mx, double my)
    {
        var dx = 10 / mx; var dy = 10 / my;
        var w = Math.Clamp((int)Math.Ceiling((bb[2] - bb[0]) / dx), 1, 2000);
        var h = Math.Clamp((int)Math.Ceiling((bb[3] - bb[1]) / dy), 1, 2000);
        dx = (bb[2] - bb[0]) / w; dy = (bb[3] - bb[1]) / h;
        var a = RasterUtil.Mascara(imovel, bb[0], bb[3], dx, dy, w, h);
        var b = RasterUtil.Mascara(uc, bb[0], bb[3], dx, dy, w, h);
        var n = 0;
        for (var i = 0; i < a.Length; i++) if (a[i] && b[i]) n++;
        return Math.Round((decimal)(n * dx * mx * dy * my / 10_000), 1);
    }

    private static string GeoJsonSimplificado(List<GeoUtil.Poligono> pols, int maxPontos)
    {
        var total = pols.Sum(p => p.Aneis.Sum(r => r.Length));
        var passo = Math.Max(1, total / maxPontos);
        var sb = new StringBuilder("{\"type\":\"MultiPolygon\",\"coordinates\":[");
        var primeiroP = true;
        foreach (var p in pols)
        {
            if (!primeiroP) sb.Append(',');
            primeiroP = false;
            sb.Append('[');
            var primeiroA = true;
            foreach (var anel in p.Aneis)
            {
                if (anel.Length < 4) continue;
                if (!primeiroA) sb.Append(',');
                primeiroA = false;
                sb.Append('[');
                var pts = anel.Where((_, i) => i % passo == 0 || i == anel.Length - 1).ToList();
                if (pts.Count < 4) pts = anel.ToList();
                sb.Append(string.Join(",", pts.Select(q => $"[{q.X.ToString("0.#####", Inv)},{q.Y.ToString("0.#####", Inv)}]")));
                sb.Append(']');
            }
            sb.Append(']');
        }
        return sb.Append("]}").ToString();
    }
}
