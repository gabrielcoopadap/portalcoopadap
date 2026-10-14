using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using AgroCert.Infrastructure.Geo;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroCert.Infrastructure.Integracoes;

public class UnidadesConservacaoLocal : IUnidadesConservacao
{
    private readonly OpcoesUnidadesConservacao _opcoes;
    private readonly ICoberturaMapBiomas _cobertura;
    private readonly ILogger<UnidadesConservacaoLocal> _log;
    private readonly object _trava = new();
    private string _assinatura = "";
    private List<UnidadeConservacao> _ucs = new();
    private List<string> _fontes = new();
    private List<string> _avisos = new();

    public string Pasta { get; }
    public int RaioAmortecimentoM => _opcoes.RaioAmortecimentoM;

    public UnidadesConservacaoLocal(IOptions<OpcoesAgroCert> opcoes, IHostEnvironment ambiente, ICoberturaMapBiomas cobertura, ILogger<UnidadesConservacaoLocal> log)
    {
        _opcoes = opcoes.Value.Ambiental.UnidadesConservacao;
        _cobertura = cobertura;
        _log = log;
        Pasta = Path.GetFullPath(Path.IsPathRooted(_opcoes.Pasta) ? _opcoes.Pasta : Path.Combine(ambiente.ContentRootPath, _opcoes.Pasta));
    }

    public bool TemDados { get { Carregar(); return _ucs.Count > 0; } }
    public IReadOnlyList<string> Fontes { get { Carregar(); return _fontes; } }
    public IReadOnlyList<string> Avisos { get { Carregar(); return _avisos; } }

    public IReadOnlyList<UnidadeConservacao> Proximas(double[] caixa, double raioM)
    {
        Carregar();
        var lat = (caixa[1] + caixa[3]) / 2;
        var gx = raioM / (111_320 * Math.Cos(lat * Math.PI / 180));
        var gy = raioM / 110_574;
        return _ucs.Where(u => !(u.Caixa[0] > caixa[2] + gx || u.Caixa[2] < caixa[0] - gx || u.Caixa[1] > caixa[3] + gy || u.Caixa[3] < caixa[1] - gy)).ToList();
    }

    private void Carregar()
    {
        lock (_trava)
        {
            if (!Directory.Exists(Pasta)) { _ucs = new(); _assinatura = ""; return; }
            var arquivos = Directory.GetFiles(Pasta, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".shp", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".geojson", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f).ToList();
            var assinatura = string.Join("|", arquivos.Select(f => f + ":" + File.GetLastWriteTimeUtc(f).Ticks));
            if (assinatura == _assinatura) return;
            _assinatura = assinatura;

            var filtro = _opcoes.Regiao is { Length: 4 } r ? r
                : _cobertura.Extensao is { } ext ? new[] { ext[0] - 0.5, ext[1] - 0.5, ext[2] + 0.5, ext[3] + 0.5 } : null;
            var ucs = new List<UnidadeConservacao>();
            var fontes = new List<string>();
            var avisos = new List<string>();
            foreach (var arq in arquivos)
            {
                try
                {
                    var camadas = arq.EndsWith("json", StringComparison.OrdinalIgnoreCase)
                        ? new[] { (Path.GetFileName(arq), Shapefile.LerGeoJson(arq, filtro), (string?)null) }
                        : Shapefile.Ler(arq, filtro).ToArray();
                    foreach (var (nome, feicoes, aviso) in camadas)
                    {
                        if (aviso is not null) avisos.Add(aviso);
                        var za = nome.Contains("amortec", StringComparison.OrdinalIgnoreCase);
                        foreach (var f in feicoes) ucs.Add(Converter(f, za, nome));
                        fontes.Add($"{nome} ({feicoes.Count} feições na região)");
                    }
                }
                catch (Exception e)
                {
                    avisos.Add($"Não foi possível ler {Path.GetFileName(arq)}: {e.Message}");
                    _log.LogWarning("UCs: erro ao ler {Arquivo}: {Erro}", arq, e.Message);
                }
            }
            _ucs = ucs;
            _fontes = fontes;
            _avisos = avisos;
            _log.LogInformation("UCs: {Total} feições carregadas de {Pasta}", ucs.Count, Pasta);
        }
    }

    private static string? Campo(Dictionary<string, string> a, params string[] chaves)
    {
        foreach (var c in chaves)
        {
            var k = a.Keys.FirstOrDefault(x => x.Equals(c, StringComparison.OrdinalIgnoreCase));
            if (k is not null && !string.IsNullOrWhiteSpace(a[k])) return a[k].Trim();
        }
        foreach (var c in chaves)
        {
            var k = a.Keys.FirstOrDefault(x => x.Contains(c, StringComparison.OrdinalIgnoreCase));
            if (k is not null && !string.IsNullOrWhiteSpace(a[k])) return a[k].Trim();
        }
        return null;
    }

    private static UnidadeConservacao Converter(FeicaoGeo f, bool zaPeloArquivo, string arquivo)
    {
        var a = f.Atributos;
        var nome = Campo(a, "nome_uc", "nome_uc1", "nomeuc", "nm_uc", "nome", "name", "nm_") ?? "UC sem nome";
        var categoria = Campo(a, "categoria", "categori", "cat_uc", "categ", "tipo");
        var grupo = Campo(a, "grupo", "grupo_uc");
        var esfera = Campo(a, "esfera", "esfera_adm", "jurisdicao", "jurisd", "esfera5");
        var texto = $"{grupo} {categoria} {nome}".ToLowerInvariant();
        var za = zaPeloArquivo || texto.Contains("amortecimento");
        string g;
        if (grupo is not null && grupo.Contains("integral", StringComparison.OrdinalIgnoreCase) || grupo is "PI") g = UnidadeConservacao.ProtecaoIntegral;
        else if (grupo is not null && grupo.Contains("sustent", StringComparison.OrdinalIgnoreCase) || grupo is "US") g = UnidadeConservacao.UsoSustentavel;
        else if (new[] { "parque", "estação ecológica", "estacao ecologica", "reserva biológica", "reserva biologica", "monumento natural", "refúgio de vida", "refugio de vida", "esec", "rebio" }.Any(texto.Contains)) g = UnidadeConservacao.ProtecaoIntegral;
        else if (new[] { "proteção ambiental", "protecao ambiental", "apa", "relevante interesse", "floresta nacional", "floresta estadual", "extrativista", "desenvolvimento sustentável", "reserva de fauna", "rppn", "reserva particular" }.Any(texto.Contains)) g = UnidadeConservacao.UsoSustentavel;
        else g = UnidadeConservacao.Desconhecido;
        return new UnidadeConservacao(nome, categoria, g, esfera, za, f.Poligonos, f.Caixa, arquivo);
    }
}
