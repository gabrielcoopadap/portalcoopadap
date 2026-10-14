using System.Text.RegularExpressions;
using AgroCert.Application.Interfaces;
using AgroCert.Infrastructure.Geo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroCert.Infrastructure.Integracoes;

public class CoberturaMapBiomasLocal : ICoberturaMapBiomas
{
    private readonly ILogger<CoberturaMapBiomasLocal> _log;
    private readonly object _trava = new();
    private Dictionary<int, string> _arquivos = new();
    private double[]? _extensao;
    private DateTime _lidoEm = DateTime.MinValue;

    public string Pasta { get; }

    public CoberturaMapBiomasLocal(IOptions<OpcoesAgroCert> opcoes, IHostEnvironment ambiente, ILogger<CoberturaMapBiomasLocal> log)
    {
        _log = log;
        var p = opcoes.Value.Ambiental.CoberturaMapBiomas.Pasta;
        Pasta = Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(ambiente.ContentRootPath, p));
    }

    public IReadOnlyList<int> Anos { get { Atualizar(); return _arquivos.Keys.OrderBy(a => a).ToList(); } }
    public double[]? Extensao { get { Atualizar(); return _extensao; } }

    private void Atualizar()
    {
        lock (_trava)
        {
            if (DateTime.UtcNow - _lidoEm < TimeSpan.FromMinutes(1)) return;
            _lidoEm = DateTime.UtcNow;
            var mapa = new Dictionary<int, string>();
            if (Directory.Exists(Pasta))
            {
                var tifs = Directory.GetFiles(Pasta, "*.tif").Concat(Directory.GetFiles(Pasta, "*.tiff"))
                    .OrderBy(f => Path.GetFileName(f).StartsWith("mapbiomas_regiao_", StringComparison.OrdinalIgnoreCase) ? 0 : 1);
                foreach (var f in tifs)
                {
                    var ano = Regex.Matches(Path.GetFileName(f), @"(19[89]\d|20\d\d)").Select(m => int.Parse(m.Value)).LastOrDefault();
                    if (ano > 0 && !mapa.ContainsKey(ano)) mapa[ano] = f;
                }
            }
            _arquivos = mapa;
            _extensao = null;
            var primeiro = mapa.Values.FirstOrDefault(v => Path.GetFileName(v).StartsWith("mapbiomas_regiao_", StringComparison.OrdinalIgnoreCase)) ?? mapa.Values.FirstOrDefault();
            if (primeiro is not null)
            {
                try
                {
                    using var t = new GeoTiffLeitor(primeiro);
                    _extensao = new[] { t.X0, t.Y0 - t.Altura * t.Dy, t.X0 + t.Largura * t.Dx, t.Y0 };
                }
                catch (Exception e) { _log.LogWarning("MapBiomas: não foi possível ler {Arquivo}: {Erro}", primeiro, e.Message); }
            }
        }
    }

    public RecorteCobertura? Recortar(int ano, double minLon, double minLat, double maxLon, double maxLat)
    {
        Atualizar();
        if (!_arquivos.TryGetValue(ano, out var arq)) return null;
        try
        {
            using var t = new GeoTiffLeitor(arq);
            var col0 = (int)Math.Floor((minLon - t.X0) / t.Dx + 1e-6);
            var col1 = (int)Math.Ceiling((maxLon - t.X0) / t.Dx - 1e-6);
            var lin0 = (int)Math.Floor((t.Y0 - maxLat) / t.Dy + 1e-6);
            var lin1 = (int)Math.Ceiling((t.Y0 - minLat) / t.Dy - 1e-6);
            if (col1 <= 0 || lin1 <= 0 || col0 >= t.Largura || lin0 >= t.Altura) return null;
            int w = col1 - col0, h = lin1 - lin0;
            if (w <= 0 || h <= 0 || (long)w * h > 40_000_000) return null;
            var dados = t.LerJanela(col0, lin0, w, h);
            return new RecorteCobertura(ano, dados, w, h, t.X0 + col0 * t.Dx, t.Y0 - lin0 * t.Dy, t.Dx, t.Dy);
        }
        catch (Exception e)
        {
            _log.LogWarning("MapBiomas: erro ao ler {Arquivo}: {Erro}", arq, e.Message);
            return null;
        }
    }
}
