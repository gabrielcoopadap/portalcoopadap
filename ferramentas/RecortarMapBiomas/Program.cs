using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using RecortarMapBiomas;

var inv = CultureInfo.InvariantCulture;
var pasta = args.Length > 0 ? args[0] : "dados-corredores";
double lonMin = -47.6, latMin = -20.2, lonMax = -45.5, latMax = -17.3;
if (args.Length >= 5)
{
    lonMin = double.Parse(args[1], inv); latMin = double.Parse(args[2], inv);
    lonMax = double.Parse(args[3], inv); latMax = double.Parse(args[4], inv);
}

if (!Directory.Exists(pasta))
{
    Console.WriteLine($"Pasta não encontrada: {Path.GetFullPath(pasta)}");
    return 1;
}
var arquivos = Directory.GetFiles(pasta, "*.tif")
    .Where(f => !Path.GetFileName(f).StartsWith("mapbiomas_regiao_", StringComparison.OrdinalIgnoreCase))
    .OrderBy(f => f)
    .ToList();
if (arquivos.Count == 0)
{
    Console.WriteLine($"Nenhum arquivo .tif em {Path.GetFullPath(pasta)}.");
    return 1;
}

Console.WriteLine($"Região: longitude {lonMin} a {lonMax}, latitude {latMin} a {latMax}");
foreach (var arquivo in arquivos)
{
    var nome = Path.GetFileName(arquivo);
    var ano = Regex.Matches(nome, @"(19|20)\d{2}").Select(m => m.Value).LastOrDefault() ?? Path.GetFileNameWithoutExtension(nome);
    var destino = Path.Combine(pasta, $"mapbiomas_regiao_{ano}.tif");
    Console.WriteLine();
    Console.WriteLine($"{nome}");
    try
    {
        var relogio = Stopwatch.StartNew();
        using var tif = new GeoTiffLeitor(arquivo);
        Console.WriteLine($"  {tif.Largura:N0} x {tif.Altura:N0} pixels, {(tif.EmTiles ? $"tiles {tif.BlocoLargura}x{tif.BlocoAltura}" : "faixas")}, compressão {tif.Compressao}");
        if (!EhWgs84(tif.GeoChaves))
            Console.WriteLine("  AVISO: o arquivo não parece estar em EPSG:4326 (lat/long). Confira se é o mapa do MapBiomas.");
        var col0 = (int)Math.Floor((lonMin - tif.X0) / tif.Dx);
        var lin0 = (int)Math.Floor((tif.Y0 - latMax) / tif.Dy);
        var larg = (int)Math.Ceiling((lonMax - lonMin) / tif.Dx);
        var alt = (int)Math.Ceiling((latMax - latMin) / tif.Dy);
        var ultimo = -1;
        var dados = tif.LerJanela(col0, lin0, larg, alt, 0, p =>
        {
            var pct = (int)(p * 100);
            if (pct / 10 != ultimo) { ultimo = pct / 10; Console.Write($"\r  lendo... {pct}%   "); }
        });
        Console.WriteLine($"\r  lido: {larg:N0} x {alt:N0} pixels            ");
        GeoTiffEscritor.Gravar(destino, dados, larg, alt, tif.X0 + col0 * tif.Dx, tif.Y0 - lin0 * tif.Dy, tif.Dx, tif.Dy, tif.GeoChaves, tif.NoData);
        var mb = new FileInfo(destino).Length / 1024.0 / 1024.0;
        Console.WriteLine($"  gravado: {Path.GetFileName(destino)} ({mb:N1} MB) em {relogio.Elapsed.TotalSeconds:N0} s");
    }
    catch (Exception e)
    {
        Console.WriteLine($"  ERRO: {e.Message}");
    }
}
Console.WriteLine();
Console.WriteLine("Pronto.");
return 0;

static bool EhWgs84(ushort[] chaves)
{
    for (var i = 4; i + 3 < chaves.Length; i += 4)
        if (chaves[i] == 2048) return chaves[i + 3] == 4326;
    return true; // sem a chave: assume lat/long
}
