using System.Diagnostics;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using ProcessarCorredores;
using RecortarMapBiomas;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
var relogio = Stopwatch.StartNew();
var pasta = args.Length > 0 ? args[0] : "dados-corredores";
if (!Directory.Exists(pasta)) return Falha($"Pasta não encontrada: {Path.GetFullPath(pasta)}");
var saida = Path.Combine(pasta, "resultado");
Directory.CreateDirectory(saida);

const double BufferRegiaoM = 5000;
const double CorredorMinHa = 50;
const double LigacaoMaxM = 1000;
const int LigacoesPorCenario = 15;
int[] distancias = { 100, 200, 500 };
var vegetacoes = new[]
{
    new Vegetacao("matas", "Só matas", "Formação florestal (mata ciliar, mata seca, capões)", new[] { 3, 6 }),
    new Vegetacao("nativa", "Toda vegetação nativa", "Mata + cerrado + campo nativo + brejo", new[] { 3, 4, 5, 6, 11, 12, 49, 50 }),
};

var planilha = Directory.GetFiles(pasta, "*.xlsx").Where(f => !Path.GetFileName(f).StartsWith("~$")).OrderBy(f => f).FirstOrDefault();
if (planilha is null) return Falha("Coloque na pasta a planilha dos cooperados (.xlsx com os códigos do CAR).");
var cooperados = Cooperados.Ler(planilha);
Console.WriteLine($"Planilha: {Path.GetFileName(planilha)} — {cooperados.Count} códigos de CAR");

var imoveis = new Dictionary<string, Imovel>(StringComparer.OrdinalIgnoreCase);
var temas = new SortedSet<string>();
var reTema = new Regex(@"_(APP|AREA_CONSOLIDADA|RESERVA_LEGAL|VEGETACAO_NATIVA|AREA_IMOVEL|USO_RESTRITO|SERVIDAO_ADMINISTRATIVA)\.gpkg$", RegexOptions.IgnoreCase);
foreach (var arq in Directory.GetFiles(pasta, "*.gpkg").OrderBy(f => f))
{
    var m = reTema.Match(Path.GetFileName(arq));
    if (!m.Success) { Console.WriteLine($"  (ignorado: {Path.GetFileName(arq)})"); continue; }
    var tema = m.Groups[1].Value.ToUpperInvariant();
    temas.Add(tema);
    foreach (var f in Gpkg.Ler(arq))
    {
        var car = f.Texto("cod_imovel").Trim().ToUpperInvariant();
        if (car.Length == 0 || f.Poligonos.Count == 0) continue;
        if (!imoveis.TryGetValue(car, out var im)) imoveis[car] = im = new Imovel(car, f.Texto("municipio"));
        if (tema == "AREA_IMOVEL") im.Limite.AddRange(f.Poligonos);
        else im.Temas.AddRange(f.Poligonos);
        if (tema == "RESERVA_LEGAL") im.Rl.AddRange(f.Poligonos);
        if (tema == "APP" && f.Texto("nom_tema").Contains("Recompor", StringComparison.OrdinalIgnoreCase))
        {
            im.AppRecompor.AddRange(f.Poligonos);
            if (double.TryParse(f.Texto("num_area"), NumberStyles.Float, CultureInfo.InvariantCulture, out var ha)) im.AppRecomporHa += ha;
        }
    }
}
if (imoveis.Count == 0) return Falha("Nenhum GeoPackage do SICAR (.gpkg) encontrado na pasta.");
var temLimite = temas.Contains("AREA_IMOVEL");
Console.WriteLine($"CAR: {imoveis.Count:N0} imóveis ({string.Join(", ", temas)})");

var mapas = Directory.GetFiles(pasta, "mapbiomas_regiao_*.tif")
    .Select(f => (Ano: int.TryParse(Regex.Match(Path.GetFileName(f), @"(19|20)\d{2}").Value, out var a) ? a : 0, Arquivo: f))
    .Where(x => x.Ano > 0).OrderBy(x => x.Ano).ToList();
if (mapas.Count == 0) return Falha("Não achei mapbiomas_regiao_<ano>.tif. Rode antes o RecortarMapBiomas.");
using var grade = new GeoTiffLeitor(mapas[0].Arquivo);
foreach (var (_, arq) in mapas.Skip(1))
{
    using var t = new GeoTiffLeitor(arq);
    if (t.Largura != grade.Largura || t.Altura != grade.Altura || Math.Abs(t.X0 - grade.X0) > 1e-9 || Math.Abs(t.Y0 - grade.Y0) > 1e-9)
        return Falha($"{Path.GetFileName(arq)} não cobre a mesma área de {Path.GetFileName(mapas[0].Arquivo)}. Recorte os dois com os mesmos limites.");
}
var anos = mapas.Select(m => m.Ano).ToList();
Console.WriteLine($"MapBiomas: {string.Join(", ", anos)} ({grade.Largura:N0} x {grade.Altura:N0} pixels)");
double gX0 = grade.X0, gY0 = grade.Y0, gDx = grade.Dx, gDy = grade.Dy;
double gX1 = gX0 + grade.Largura * gDx, gY1 = gY0 - grade.Altura * gDy;

var coopComMapa = cooperados.Values.Where(c => imoveis.ContainsKey(c.Car)).ToList();
var coopSemMapa = cooperados.Values.Where(c => !imoveis.ContainsKey(c.Car)).ToList();
Console.WriteLine($"Cooperados com mapa do CAR: {coopComMapa.Count} de {cooperados.Count}");
if (coopComMapa.Count == 0) return Falha("Nenhum CAR da planilha aparece nos GeoPackages. Confira os municípios baixados do SICAR.");

var caixas = new List<(double X0, double Y0, double X1, double Y1, List<string> Cars)>();
foreach (var c in coopComMapa)
{
    var (a, b, cc, d) = imoveis[c.Car].Caixa;
    var bx = BufferRegiaoM / (111320 * Math.Cos((b + d) / 2 * Math.PI / 180));
    var by = BufferRegiaoM / 110574;
    caixas.Add((a - bx, b - by, cc + bx, d + by, new List<string> { c.Car }));
}
for (var mudou = true; mudou;)
{
    mudou = false;
    for (var i = 0; i < caixas.Count && !mudou; i++)
        for (var j = i + 1; j < caixas.Count && !mudou; j++)
        {
            var p = caixas[i]; var q = caixas[j];
            if (p.X0 > q.X1 || q.X0 > p.X1 || p.Y0 > q.Y1 || q.Y0 > p.Y1) continue;
            p.Cars.AddRange(q.Cars);
            caixas[i] = (Math.Min(p.X0, q.X0), Math.Min(p.Y0, q.Y0), Math.Max(p.X1, q.X1), Math.Max(p.Y1, q.Y1), p.Cars);
            caixas.RemoveAt(j);
            mudou = true;
        }
}
caixas = caixas.OrderByDescending(c => c.Cars.Count).ToList();

var avisos = new List<string>();
if (!temLimite)
    avisos.Add("O limite do imóvel (AREA_IMOVEL) não foi baixado do SICAR: a área de cada fazenda foi estimada juntando APP, Reserva Legal, vegetação nativa e área consolidada. Baixe o tema \"Área do Imóvel\" e coloque na pasta para ter o limite exato.");
foreach (var m in coopSemMapa.GroupBy(c => string.IsNullOrWhiteSpace(c.Municipio) ? "município não informado" : c.Municipio).OrderByDescending(g => g.Count()))
    avisos.Add($"{m.Count()} CAR de cooperado(s) em {m.Key} ficaram de fora: falta baixar os GeoPackages desse município no SICAR.");

foreach (var f in Directory.GetFiles(saida, "*.png").Concat(Directory.GetFiles(saida, "*.geojson"))) File.Delete(f);

var regioesJson = new List<object>();
for (var ri = 0; ri < caixas.Count; ri++)
{
    var cx = caixas[ri];
    var rid = $"r{ri + 1}";
    var col0 = Math.Max(0, (int)Math.Floor((cx.X0 - gX0) / gDx));
    var lin0 = Math.Max(0, (int)Math.Floor((gY0 - cx.Y1) / gDy));
    var col1 = Math.Min(grade.Largura, (int)Math.Ceiling((cx.X1 - gX0) / gDx));
    var lin1 = Math.Min(grade.Altura, (int)Math.Ceiling((gY0 - cx.Y0) / gDy));
    var w = col1 - col0; var h = lin1 - lin0;
    if (w < 10 || h < 10) { avisos.Add($"Fazendas fora da área dos mapas do MapBiomas: {string.Join(", ", cx.Cars)}"); continue; }
    double x0 = gX0 + col0 * gDx, y0 = gY0 - lin0 * gDy;
    double x1 = x0 + w * gDx, y1 = y0 - h * gDy;
    var n = w * h;
    var latC = (y0 + y1) / 2;
    var px = gDx * 111320 * Math.Cos(latC * Math.PI / 180);
    var py = gDy * 110574;
    var areaLinha = new double[h];
    for (var l = 0; l < h; l++) areaLinha[l] = gDx * 111320 * Math.Cos((y0 - (l + 0.5) * gDy) * Math.PI / 180) * py / 10000;
    double AreaPx(int i) => areaLinha[i / w];

    var coops = cx.Cars.Select(c => imoveis[c]).ToList();
    var coopSet = new HashSet<string>(cx.Cars, StringComparer.OrdinalIgnoreCase);
    var outros = imoveis.Values.Where(im => !coopSet.Contains(im.Car) && im.Intersecta(x0, y1, x1, y0)).ToList();
    var todas = coops.Concat(outros).ToList();
    var nc = coops.Count;
    var faz = new int[n]; Array.Fill(faz, -1);
    var rl = new int[n]; Array.Fill(rl, -1);
    for (var k = todas.Count - 1; k >= 0; k--) // cooperados por último = prevalecem
        foreach (var p in todas[k].Contorno)
            Raster.Rasterizar(p, x0, y0, gDx, gDy, w, h, i => faz[i] = k);
    for (var k = 0; k < nc; k++)
        foreach (var p in coops[k].Rl)
            Raster.Rasterizar(p, x0, y0, gDx, gDy, w, h, i => rl[i] = k);

    var appRec = new int[n]; Array.Fill(appRec, -1);
    var rlTodas = new bool[n];
    for (var k = todas.Count - 1; k >= 0; k--)
    {
        foreach (var p in todas[k].AppRecompor) // faixas de APP são estreitas: marca o pixel se qualquer sub-pixel (3x3) cair dentro
            Raster.Rasterizar(p, x0, y0, gDx / 3, gDy / 3, w * 3, h * 3, i => appRec[(i / (w * 3) / 3) * w + (i % (w * 3)) / 3] = k);
        foreach (var p in todas[k].Rl) Raster.Rasterizar(p, x0, y0, gDx, gDy, w, h, i => rlTodas[i] = true);
    }

    var areaFaz = new double[nc]; var areaRl = new double[nc];
    var somaX = new double[nc]; var somaY = new double[nc]; var cont = new int[nc];
    var caixaFaz = new int[nc][];
    for (var k = 0; k < nc; k++) caixaFaz[k] = new[] { int.MaxValue, int.MaxValue, -1, -1 };
    for (var i = 0; i < n; i++)
    {
        var a = AreaPx(i);
        if (faz[i] is var f and >= 0 && f < nc)
        {
            areaFaz[f] += a; somaX[f] += i % w; somaY[f] += i / w; cont[f]++;
            var cb = caixaFaz[f]; int cc = i % w, ll = i / w;
            if (cc < cb[0]) cb[0] = cc; if (ll < cb[1]) cb[1] = ll; if (cc > cb[2]) cb[2] = cc; if (ll > cb[3]) cb[3] = ll;
        }
        if (rl[i] >= 0) areaRl[rl[i]] += a;
    }
    var ponto = new (double Lon, double Lat)[nc];
    {
        var melhor = new double[nc]; Array.Fill(melhor, double.MaxValue);
        var cxp = new double[nc]; var cyp = new double[nc];
        for (var k = 0; k < nc; k++) if (cont[k] > 0) { cxp[k] = somaX[k] / cont[k]; cyp[k] = somaY[k] / cont[k]; }
        for (var i = 0; i < n; i++)
        {
            var f = faz[i];
            if (f < 0 || f >= nc) continue;
            double ddx = i % w - cxp[f], ddy = i / w - cyp[f];
            var d = ddx * ddx + ddy * ddy;
            if (d < melhor[f]) { melhor[f] = d; ponto[f] = (x0 + (i % w + 0.5) * gDx, y0 - (i / w + 0.5) * gDy); }
        }
        for (var k = 0; k < nc; k++)
            if (cont[k] == 0) { var c = coops[k].Caixa; ponto[k] = ((c.MinX + c.MaxX) / 2, (c.MinY + c.MaxY) / 2); }
    }

    var municipios = coops.GroupBy(c => c.Municipio).OrderByDescending(g => g.Count()).Select(g => g.Key).ToList();
    var nomeRegiao = municipios.Count switch
    {
        1 => municipios[0],
        2 => $"{municipios[0]} e {municipios[1]}",
        _ => $"{string.Join(", ", municipios.Take(municipios.Count - 1))} e {municipios[^1]}",
    };
    Console.WriteLine();
    Console.WriteLine($"Região {ri + 1}: {nomeRegiao} — {nc} fazenda(s) de cooperados, {outros.Count:N0} outros imóveis, {w:N0} x {h:N0} pixels");

    var classes = new Dictionary<int, byte[]>();
    foreach (var (ano, arq) in mapas)
    {
        using var t = new GeoTiffLeitor(arq);
        classes[ano] = t.LerJanela(col0, lin0, w, h);
    }

    var lei = new byte[n]; // bit 1 = APP a recompor sem vegetação, bit 2 = Reserva Legal sem vegetação
    var appRecHa = new double[nc]; var rlSemVegHa = new double[nc];
    double leiAppHa = 0, leiRlHa = 0;
    {
        var nativaFinal = new bool[256];
        foreach (var k in vegetacoes[^1].Classes) nativaFinal[k] = true;
        var cf = classes[anos[^1]];
        for (var i = 0; i < n; i++)
        {
            if (nativaFinal[cf[i]]) continue;
            var a = AreaPx(i);
            if (appRec[i] >= 0) { lei[i] |= 1; leiAppHa += a; if (appRec[i] < nc) appRecHa[appRec[i]] += a; }
            if (rlTodas[i]) { lei[i] |= 2; if ((lei[i] & 1) == 0) leiRlHa += a; }
            if (rl[i] >= 0) rlSemVegHa[rl[i]] += a;
        }
        var idxLei = new byte[n];
        for (var i = 0; i < n; i++)
        {
            if (lei[i] == 0) continue;
            var listra = ((i % w) + (i / w)) % 4 < 2;
            idxLei[i] = (byte)(((lei[i] & 1) != 0 ? 1 : 3) + (listra ? 0 : 1));
        }
        Png.Gravar(Path.Combine(saida, $"{rid}_lei.png"), Mercator(idxLei, w, h, y0, gDy), w, h, Paleta.Lei);
    }
    Console.WriteLine($"  A recompor pela lei (sem vegetação em {anos[^1]}): APP {leiAppHa:N0} ha, Reserva Legal {leiRlHa:N0} ha");

    var cenarios = new Dictionary<string, object>();
    var mudancas = new Dictionary<string, object>();
    foreach (var veg in vegetacoes)
    {
        var nativas = new Dictionary<int, bool[]>();
        foreach (var ano in anos)
        {
            var c = classes[ano];
            var nat = new bool[n];
            var set = new bool[256];
            foreach (var k in veg.Classes) set[k] = true;
            for (var i = 0; i < n; i++) nat[i] = set[c[i]];
            nativas[ano] = nat;
            var (dist2, _) = Raster.Distancia(nat, w, h, px, py);

            foreach (var D in distancias)
            {
                var chave = $"{ano}|{veg.Id}|{D}";
                var raio2 = (float)(D / 2.0 * (D / 2.0));
                var dil = new bool[n];
                for (var i = 0; i < n; i++) dil[i] = dist2[i] <= raio2;
                var (rot, ng) = Raster.Rotular(dil, w, h);

                var areaG = new double[ng + 1];
                for (var i = 0; i < n; i++) if (nat[i]) areaG[rot[i]] += AreaPx(i);
                var corr = new bool[ng + 1];
                for (var g = 1; g <= ng; g++) corr[g] = areaG[g] >= CorredorMinHa;
                var ordem = Enumerable.Range(1, ng).Where(g => corr[g]).OrderByDescending(g => areaG[g]).ToList();
                var numero = new int[ng + 1];
                for (var k = 0; k < ordem.Count; k++) numero[ordem[k]] = k + 1;

                var idx = new byte[n];
                var caixaG = new Dictionary<int, int[]>();
                var fazG = new Dictionary<int, HashSet<int>>();
                var natFaz = new double[nc]; var corrFaz = new double[nc]; var maiorFaz = new double[nc]; var maiorNum = new int[nc];
                var rlNat = new double[nc]; var rlCorr = new double[nc];
                double natTot = 0, corrTot = 0;
                for (var i = 0; i < n; i++)
                {
                    var g = rot[i];
                    if (g == 0) continue;
                    var nr = numero[g];
                    if (nat[i])
                    {
                        var a = AreaPx(i);
                        natTot += a;
                        idx[i] = nr == 0 ? (byte)1 : nr <= Paleta.Cores ? (byte)(4 + 2 * (nr - 1)) : (byte)2;
                        var f = faz[i];
                        if (f >= 0 && f < nc) { natFaz[f] += a; }
                        if (rl[i] >= 0) rlNat[rl[i]] += a;
                        if (nr > 0)
                        {
                            corrTot += a;
                            if (!caixaG.TryGetValue(nr, out var cb)) caixaG[nr] = cb = new[] { int.MaxValue, int.MaxValue, -1, -1 };
                            int cc = i % w, ll = i / w;
                            if (cc < cb[0]) cb[0] = cc; if (ll < cb[1]) cb[1] = ll; if (cc > cb[2]) cb[2] = cc; if (ll > cb[3]) cb[3] = ll;
                            if (f >= 0 && f < nc)
                            {
                                corrFaz[f] += a;
                                if (areaG[g] > maiorFaz[f]) { maiorFaz[f] = areaG[g]; maiorNum[f] = nr; }
                                if (!fazG.TryGetValue(nr, out var fs)) fazG[nr] = fs = new HashSet<int>();
                                fs.Add(f);
                            }
                            if (rl[i] >= 0) rlCorr[rl[i]] += a;
                        }
                    }
                    else if (nr > 0) idx[i] = nr <= Paleta.Cores ? (byte)(5 + 2 * (nr - 1)) : (byte)3;
                }

                var isolados = Enumerable.Range(1, ng).Where(g => !corr[g] && areaG[g] > 0).ToList();
                var imagem = $"{rid}_{ano}_{veg.Id}_{D}.png";
                Png.Gravar(Path.Combine(saida, imagem), Mercator(idx, w, h, y0, gDy), w, h, Paleta.Corredores);

                var (ligacoes, ligFaz) = Ligacoes(nat, rot, numero, areaG, w, h, px, py, LigacaoMaxM, LigacoesPorCenario, faz, nc, lei, caixaFaz,
                    (c, l) => (x0 + (c + 0.5) * gDx, y0 - (l + 0.5) * gDy));

                cenarios[chave] = new
                {
                    imagem,
                    nativaHa = R(natTot),
                    emCorredorHa = R(corrTot),
                    isolados = new { quantidade = isolados.Count, ha = R(isolados.Sum(g => areaG[g])) },
                    classes = new
                    {
                        grandes = ordem.Count(g => areaG[g] >= 5000),
                        medios = ordem.Count(g => areaG[g] >= 500 && areaG[g] < 5000),
                        pequenos = ordem.Count(g => areaG[g] < 500),
                    },
                    corredores = ordem.Select((g, k) => new
                    {
                        numero = k + 1,
                        ha = R(areaG[g]),
                        cor = k < Paleta.Cores ? Paleta.Hex(k) : Paleta.HexOutros,
                        caixa = caixaG.TryGetValue(k + 1, out var cb)
                            ? new[] { R5(x0 + cb[0] * gDx), R5(y0 - (cb[3] + 1) * gDy), R5(x0 + (cb[2] + 1) * gDx), R5(y0 - cb[1] * gDy) }
                            : null,
                        fazendas = fazG.TryGetValue(k + 1, out var fs) ? fs.OrderBy(x => x).ToArray() : Array.Empty<int>(),
                    }).ToList(),
                    ligacoes,
                    fazendas = Enumerable.Range(0, nc).Select(f => new
                    {
                        id = f,
                        nativaHa = R(natFaz[f]),
                        emCorredorHa = R(corrFaz[f]),
                        corredor = maiorNum[f],
                        corredorHa = R(maiorFaz[f]),
                        rlNativaHa = R(rlNat[f]),
                        rlEmCorredorHa = R(rlCorr[f]),
                        ligacoes = ligFaz[f],
                        situacaoRl = areaRl[f] <= 0 ? "sem-rl"
                            : rlNat[f] < 0.2 * areaRl[f] ? "sem-vegetacao"
                            : rlCorr[f] >= 0.5 * rlNat[f] ? "conectada" : "isolada",
                    }).ToList(),
                };
                Console.WriteLine($"  {ano} {veg.Id,-6} {D,3} m: {ordem.Count,4} corredores (maior {(ordem.Count > 0 ? areaG[ordem[0]] : 0):N0} ha), {isolados.Count,5} fragmentos isolados, {ligacoes.Count} ligações");
            }
        }

        if (anos.Count >= 2)
        {
            var a0 = nativas[anos[0]]; var a1 = nativas[anos[^1]];
            var idx = new byte[n];
            double perda = 0, ganho = 0, estavel = 0;
            var perdaF = new double[nc]; var ganhoF = new double[nc]; var perdaRl = new double[nc];
            for (var i = 0; i < n; i++)
            {
                var a = AreaPx(i);
                var f = faz[i];
                if (a0[i] && a1[i]) { idx[i] = 1; estavel += a; }
                else if (a0[i]) { idx[i] = 2; perda += a; if (f >= 0 && f < nc) perdaF[f] += a; if (rl[i] >= 0) perdaRl[rl[i]] += a; }
                else if (a1[i]) { idx[i] = 3; ganho += a; if (f >= 0 && f < nc) ganhoF[f] += a; }
            }
            var imagem = $"{rid}_mudanca_{veg.Id}.png";
            Png.Gravar(Path.Combine(saida, imagem), Mercator(idx, w, h, y0, gDy), w, h, Paleta.Mudanca);
            mudancas[veg.Id] = new
            {
                imagem,
                perdaHa = R(perda),
                ganhoHa = R(ganho),
                estavelHa = R(estavel),
                fazendas = Enumerable.Range(0, nc).Select(f => new { id = f, perdaHa = R(perdaF[f]), ganhoHa = R(ganhoF[f]), rlPerdaHa = R(perdaRl[f]) }).ToList(),
            };
        }
    }

    var feicoes = new List<object>();
    for (var k = 0; k < nc; k++)
    {
        foreach (var p in coops[k].Rl) feicoes.Add(Feature(p, "rl", k));
        foreach (var p in coops[k].Limite) feicoes.Add(Feature(p, "imovel", k));
    }
    var contornos = $"{rid}_fazendas.geojson";
    File.WriteAllText(Path.Combine(saida, contornos), JsonSerializer.Serialize(new { type = "FeatureCollection", features = feicoes }, Json.Opcoes));

    regioesJson.Add(new
    {
        id = rid,
        nome = nomeRegiao,
        limites = new[] { R5(x0), R5(y1), R5(x1), R5(y0) },
        contornos,
        lei = new { imagem = $"{rid}_lei.png", appHa = R(leiAppHa), rlHa = R(leiRlHa), ano = anos[^1] },
        outrosImoveis = outros.Count,
        fazendas = Enumerable.Range(0, nc).Select(k =>
        {
            var c = cooperados[coops[k].Car];
            return new
            {
                id = k,
                car = coops[k].Car,
                nome = c.Nomes.Count > 0 ? string.Join(" / ", c.Nomes) : coops[k].Car,
                municipio = coops[k].Municipio,
                lon = R5(ponto[k].Lon),
                lat = R5(ponto[k].Lat),
                areaHa = R(areaFaz[k]),
                rlHa = R(areaRl[k]),
                appRecomporHa = R(coops[k].AppRecomporHa),
                appRecomporSemVegetacaoHa = R(appRecHa[k]),
                rlSemVegetacaoHa = R(rlSemVegHa[k]),
                caixa = caixaFaz[k][2] < 0 ? null : new[] { R5(x0 + caixaFaz[k][0] * gDx), R5(y0 - (caixaFaz[k][3] + 1) * gDy), R5(x0 + (caixaFaz[k][2] + 1) * gDx), R5(y0 - caixaFaz[k][1] * gDy) },
            };
        }).ToList(),
        cenarios,
        mudancas,
    });
}

var resultado = new
{
    geradoEm = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
    anos,
    distancias,
    vegetacoes = vegetacoes.Select(v => new { id = v.Id, nome = v.Nome, descricao = v.Descricao }),
    parametros = new { corredorMinHa = CorredorMinHa, ligacaoMaxM = LigacaoMaxM, bufferRegiaoM = BufferRegiaoM, fonte = "MapBiomas Coleção (cobertura e uso, 30 m) + SICAR" },
    paleta = new { isolado = Paleta.HexIsolado, outros = Paleta.HexOutros, perda = "#e34948", ganho = "#2a78d6", estavel = "#3f9a4a" },
    cooperados = new { naPlanilha = cooperados.Count, comMapa = coopComMapa.Count },
    semMapa = coopSemMapa.Select(c => new { car = c.Car, nome = string.Join(" / ", c.Nomes), municipio = c.Municipio }),
    avisos,
    regioes = regioesJson,
};
File.WriteAllText(Path.Combine(saida, "corredores.json"), JsonSerializer.Serialize(resultado, Json.Opcoes));
Console.WriteLine();
Console.WriteLine($"Pronto em {relogio.Elapsed.TotalSeconds:N0} s. Resultado em {Path.GetFullPath(saida)}");
foreach (var a in avisos) Console.WriteLine($"  Aviso: {a}");
return 0;

static int Falha(string msg)
{
    Console.WriteLine(msg);
    return 1;
}

static double R(double v) => Math.Round(v, 1);
static double R5(double v) => Math.Round(v, 5);

static object Feature(Poligono p, string tipo, int fazenda) => new
{
    type = "Feature",
    properties = new { tipo, fazenda },
    geometry = new
    {
        type = "Polygon",
        coordinates = p.Aneis.Select(a => Enumerable.Range(0, a.Length / 2).Select(i => new[] { R5(a[i * 2]), R5(a[i * 2 + 1]) }).ToArray()).ToArray(),
    },
};

static byte[] Mercator(byte[] src, int w, int h, double latTopo, double dy)
{
    static double M(double lat) => Math.Log(Math.Tan(Math.PI / 4 + lat * Math.PI / 360));
    double yT = M(latTopo), yB = M(latTopo - h * dy);
    var dst = new byte[src.Length];
    for (var k = 0; k < h; k++)
    {
        var ym = yT - (k + 0.5) * (yT - yB) / h;
        var lat = (2 * Math.Atan(Math.Exp(ym)) - Math.PI / 2) * 180 / Math.PI;
        var r = Math.Clamp((int)((latTopo - lat) / dy), 0, h - 1);
        Buffer.BlockCopy(src, r * w, dst, k * w, w);
    }
    return dst;
}

static (List<object> Regiao, List<object>[] PorFazenda) Ligacoes(bool[] nat, int[] rot, int[] numero, double[] areaG, int w, int h, double px, double py,
    double maxM, int limite, int[] faz, int nc, byte[] lei, int[][] caixaFaz, Func<int, int, (double Lon, double Lat)> geo)
{
    var n = w * h;
    var sitio = new bool[n];
    for (var i = 0; i < n; i++) sitio[i] = nat[i] && numero[rot[i]] > 0;
    var (d2, sit) = Raster.Distancia(sitio, w, h, px, py);
    var max2 = (float)(maxM * maxM);
    var melhores = new Dictionary<long, (double Gap, int A, int B)>();
    var meioPx = (px + py) / 2;
    void Par(int i, int j)
    {
        int si = sit[i], sj = sit[j];
        if (si < 0 || sj < 0) return;
        int gi = rot[si], gj = rot[sj];
        if (gi == gj) return;
        double ddx = (si % w - sj % w) * px, ddy = (si / w - sj / w) * py;
        var gap = Math.Max(0, Math.Sqrt(ddx * ddx + ddy * ddy) - meioPx);
        if (gap > maxM) return;
        var (a, b, sa, sb) = gi < gj ? (gi, gj, si, sj) : (gj, gi, sj, si);
        var chave = ((long)a << 32) | (uint)b;
        if (!melhores.TryGetValue(chave, out var atual) || gap < atual.Gap) melhores[chave] = (gap, sa, sb);
    }
    for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
        {
            var i = y * w + x;
            if (d2[i] > max2) continue;
            if (x < w - 1) Par(i, i + 1);
            if (y < h - 1) Par(i, i + w);
        }

    var cands = new List<Candidato>(melhores.Count);
    foreach (var (chave, v) in melhores)
    {
        var c = new Candidato { A = (int)(chave >> 32), B = (int)(chave & 0xFFFFFFFF), Gap = v.Gap, SA = v.A, SB = v.B };
        int ca = c.SA % w, la = c.SA / w, cb = c.SB % w, lb = c.SB / w;
        var passos = Math.Max(1, Math.Max(Math.Abs(cb - ca), Math.Abs(lb - la)) * 2);
        int falha = 0, legal = 0, tipo = 0;
        for (var s = 0; s <= passos; s++)
        {
            var cc = (int)Math.Round(ca + (cb - ca) * (double)s / passos);
            var ll = (int)Math.Round(la + (lb - la) * (double)s / passos);
            var i = ll * w + cc;
            var f = faz[i];
            if (f >= 0) { if (f < nc) c.Coops.Add(f); else c.Outros.Add(f); }
            if (nat[i]) continue;
            falha++;
            var t = 0;
            for (var dy = -1; dy <= 1; dy++)
                for (var dx = -1; dx <= 1; dx++)
                {
                    int x2 = cc + dx, y2 = ll + dy;
                    if (x2 >= 0 && y2 >= 0 && x2 < w && y2 < h) t |= lei[y2 * w + x2];
                }
            if (t != 0) { legal++; tipo |= t; }
        }
        c.LeiFracao = falha == 0 ? 0 : (double)legal / falha;
        c.LeiTipo = tipo;
        c.Prioridade = Math.Min(areaG[c.A], areaG[c.B]) / Math.Max(c.Gap, 30) * (1 + c.LeiFracao);
        cands.Add(c);
    }
    cands.Sort((x, y) => y.Prioridade.CompareTo(x.Prioridade));

    object Obj(Candidato c, bool? naFazenda = null)
    {
        var (lo1, la1) = geo(c.SA % w, c.SA / w);
        var (lo2, la2) = geo(c.SB % w, c.SB / w);
        int na = numero[c.A], nb = numero[c.B];
        return new
        {
            de = Math.Min(na, nb),
            para = Math.Max(na, nb),
            deHa = Math.Round(areaG[na < nb ? c.A : c.B], 1),
            paraHa = Math.Round(areaG[na < nb ? c.B : c.A], 1),
            distanciaM = (int)Math.Round(c.Gap / 10) * 10,
            juntosHa = Math.Round(areaG[c.A] + areaG[c.B], 1),
            linha = new[] { new[] { Math.Round(lo1, 5), Math.Round(la1, 5) }, new[] { Math.Round(lo2, 5), Math.Round(la2, 5) } },
            fazendas = c.Coops.ToArray(),
            outrosImoveis = c.Outros.Count,
            leiPct = (int)Math.Round(c.LeiFracao * 100),
            leiTipo = c.LeiTipo switch { 1 => "app", 2 => "rl", 3 => "app-rl", _ => null },
            naFazenda,
        };
    }

    var pai = new Dictionary<int, int>();
    int Raiz(int g) { while (pai.TryGetValue(g, out var p) && p != g) g = p; return g; }
    var regiao = new List<object>();
    foreach (var c in cands)
    {
        if (regiao.Count >= limite) break;
        int ra = Raiz(c.A), rb = Raiz(c.B);
        if (ra == rb) continue;
        pai[ra] = rb;
        regiao.Add(Obj(c));
    }

    var porFazenda = new List<object>[nc];
    for (var f = 0; f < nc; f++) porFazenda[f] = new List<object>();
    const double PertoM = 1500;
    for (var f = 0; f < nc; f++)
    {
        var cb = caixaFaz[f];
        if (cb[2] < 0) continue;
        var na = cands.Where(c => c.Coops.Contains(f)).Take(3).ToList();
        var lista = na.Select(c => Obj(c, true)).ToList();
        if (lista.Count < 3)
            foreach (var c in cands)
            {
                if (lista.Count >= 3) break;
                if (na.Contains(c)) continue;
                double mc = (c.SA % w + c.SB % w) / 2.0, ml = (c.SA / w + c.SB / w) / 2.0;
                var dx = Math.Max(0, Math.Max(cb[0] - mc, mc - cb[2])) * px;
                var dy = Math.Max(0, Math.Max(cb[1] - ml, ml - cb[3])) * py;
                if (Math.Sqrt(dx * dx + dy * dy) <= PertoM) lista.Add(Obj(c, false));
            }
        porFazenda[f] = lista;
    }
    return (regiao, porFazenda);
}

sealed class Candidato
{
    public int A, B, SA, SB, LeiTipo;
    public double Gap, LeiFracao, Prioridade;
    public SortedSet<int> Coops = new();
    public HashSet<int> Outros = new();
}

sealed record Vegetacao(string Id, string Nome, string Descricao, int[] Classes);

sealed class Imovel
{
    public Imovel(string car, string municipio) { Car = car; Municipio = municipio; }
    public string Car { get; }
    public string Municipio { get; }
    public List<Poligono> Temas { get; } = new();
    public List<Poligono> Rl { get; } = new();
    public List<Poligono> AppRecompor { get; } = new();
    public double AppRecomporHa { get; set; }
    public List<Poligono> Limite { get; } = new();
    public List<Poligono> Contorno => Limite.Count > 0 ? Limite : Temas;
    private (double, double, double, double)? _caixa;
    public (double MinX, double MinY, double MaxX, double MaxY) Caixa => _caixa ??= Raster.Limites(Contorno.Concat(Rl));
    public bool Intersecta(double minX, double minY, double maxX, double maxY)
    {
        var c = Caixa;
        return !(c.MinX > maxX || c.MaxX < minX || c.MinY > maxY || c.MaxY < minY);
    }
}

static class Json
{
    public static readonly JsonSerializerOptions Opcoes = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
}

static class Paleta
{
    public const int Cores = 126;
    public const string HexIsolado = "#eb6834";
    public const string HexOutros = "#8fbf6a";
    private static readonly (byte R, byte G, byte B)[] Base = Gerar();

    private static (byte, byte, byte)[] Gerar()
    {
        var r = new (byte, byte, byte)[Cores];
        for (var k = 0; k < Cores; k++)
        {
            var hue = 120 + (k * 137.508) % 220;
            if (hue >= 330) hue = 50 + (hue - 330) * 1.5;
            var sat = k == 0 ? 0.70 : 0.55 + 0.25 * ((k * 7) % 3) / 2.0;
            var lum = k == 0 ? 0.36 : new[] { 0.42, 0.55, 0.34, 0.62 }[k % 4];
            r[k] = Hsl(hue, sat, lum);
        }
        return r;
    }

    private static (byte, byte, byte) Hsl(double hDeg, double s, double l)
    {
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var hp = (hDeg % 360) / 60;
        var x = c * (1 - Math.Abs(hp % 2 - 1));
        var (r, g, b) = hp switch { < 1 => (c, x, 0.0), < 2 => (x, c, 0.0), < 3 => (0.0, c, x), < 4 => (0.0, x, c), < 5 => (x, 0.0, c), _ => (c, 0.0, x) };
        var m = l - c / 2;
        byte B(double v) => (byte)Math.Clamp(Math.Round((v + m) * 255), 0, 255);
        return (B(r), B(g), B(b));
    }

    public static string Hex(int k) => $"#{Base[k].R:x2}{Base[k].G:x2}{Base[k].B:x2}";

    public static readonly IReadOnlyList<(byte R, byte G, byte B, byte A)> Corredores = CriarCorredores();
    private static List<(byte, byte, byte, byte)> CriarCorredores()
    {
        var l = new List<(byte, byte, byte, byte)>
        {
            (0, 0, 0, 0),
            (0xeb, 0x68, 0x34, 235),
            (0x8f, 0xbf, 0x6a, 230),
            (0x8f, 0xbf, 0x6a, 90),
        };
        foreach (var (r, g, b) in Base) { l.Add((r, g, b, 235)); l.Add((r, g, b, 95)); }
        return l;
    }

    public static readonly IReadOnlyList<(byte R, byte G, byte B, byte A)> Lei = new List<(byte, byte, byte, byte)>
    {
        (0, 0, 0, 0),
        (0x00, 0xd5, 0xff, 240), (0x00, 0xd5, 0xff, 70),
        (0xff, 0x3d, 0xc8, 240), (0xff, 0x3d, 0xc8, 70),
    };

    public static readonly IReadOnlyList<(byte R, byte G, byte B, byte A)> Mudanca = new List<(byte, byte, byte, byte)>
    {
        (0, 0, 0, 0),
        (0x3f, 0x9a, 0x4a, 110),
        (0xe3, 0x49, 0x48, 245),
        (0x2a, 0x78, 0xd6, 245),
    };
}
