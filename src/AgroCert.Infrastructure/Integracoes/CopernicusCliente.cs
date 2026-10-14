using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using AgroCert.Domain.Excecoes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroCert.Infrastructure.Integracoes;

public class CacheTokenCopernicus
{
    public readonly SemaphoreSlim Trava = new(1, 1);
    public string? Token { get; set; }
    public DateTime ExpiraEmUtc { get; set; }
}

public class CopernicusCliente : ICopernicusCliente
{
    private const string EvalscriptNdvi = """
        //VERSION=3
        function setup() {
          return {
            input: [{ bands: ["B04", "B08", "SCL", "dataMask"] }],
            output: [
              { id: "ndvi", bands: 1, sampleType: "FLOAT32" },
              { id: "dataMask", bands: 1 }
            ]
          };
        }
        function evaluatePixel(sample) {
          let ndvi = (sample.B08 - sample.B04) / (sample.B08 + sample.B04 + 1e-6);
          let valido = sample.dataMask;
          let nuvemOuSombra = [3, 8, 9, 10, 11].includes(sample.SCL);
          if (nuvemOuSombra) valido = 0;
          return { ndvi: [ndvi], dataMask: [valido] };
        }
        """;

    private const string EvalscriptCorReal = """
        //VERSION=3
        function setup() {
          return {
            input: [{ bands: ["B04", "B03", "B02", "dataMask"] }],
            output: { bands: 4 }
          };
        }
        function evaluatePixel(sample) {
          const ganho = 2.8; // realce de brilho padrão pra imagem não sair escura
          return [sample.B04 * ganho, sample.B03 * ganho, sample.B02 * ganho, sample.dataMask];
        }
        """;

    private const string EvalscriptMapaCalor = """
        //VERSION=3
        function setup() {
          return {
            input: [{ bands: ["B04", "B08", "SCL", "dataMask"] }],
            output: { bands: 4 }
          };
        }
        var rampa = [
          [-1.0, 0xb0392b], [0.2, 0xb0392b], [0.35, 0xd9a441],
          [0.5, 0xd9d441], [0.65, 0x7ab648], [0.8, 0x2f7a54], [1.0, 0x15533a]
        ];
        function misturar(c0, c1, f) {
          var r0 = (c0 >> 16) & 255, g0 = (c0 >> 8) & 255, b0 = c0 & 255;
          var r1 = (c1 >> 16) & 255, g1 = (c1 >> 8) & 255, b1 = c1 & 255;
          return [(r0 + (r1 - r0) * f) / 255, (g0 + (g1 - g0) * f) / 255, (b0 + (b1 - b0) * f) / 255];
        }
        function corRampa(v) {
          for (var i = 1; i < rampa.length; i++) {
            if (v <= rampa[i][0]) {
              return misturar(rampa[i - 1][1], rampa[i][1], (v - rampa[i - 1][0]) / (rampa[i][0] - rampa[i - 1][0] + 1e-9));
            }
          }
          return misturar(rampa[rampa.length - 1][1], rampa[rampa.length - 1][1], 0);
        }
        function evaluatePixel(sample) {
          var ndvi = (sample.B08 - sample.B04) / (sample.B08 + sample.B04 + 1e-6);
          var nuvemOuSombra = [3, 8, 9, 10, 11].includes(sample.SCL);
          var valido = sample.dataMask && !nuvemOuSombra;
          var cor = corRampa(ndvi);
          return [cor[0], cor[1], cor[2], valido ? 0.75 : 0];
        }
        """;

    private const string Crs4326 = "http://www.opengis.net/def/crs/EPSG/0/4326";

    private const double MetrosPorGrau = 111_320.0;
    private const int LadoPadraoPx = 512;
    private const int LadoMaxPx = 2000;               // acima disso fica caro (cota) sem ganho visual
    private const double MetrosPorPixelLimite = 1450; // limite real da API p/ S2L2A é 1500 m/pixel
    private const double MetrosPorPixelAlvoEstatistica = 10;
    private const int PixelsMaxEstatistica = 2500;

    private readonly HttpClient _http;
    private readonly OpcoesCopernicus _opcoes;
    private readonly CacheTokenCopernicus _cache;
    private readonly ILogger<CopernicusCliente> _log;

    public CopernicusCliente(HttpClient http, IOptions<OpcoesAgroCert> opcoes, CacheTokenCopernicus cache, ILogger<CopernicusCliente> log)
    {
        _http = http;
        _opcoes = opcoes.Value.Ambiental.Copernicus;
        _cache = cache;
        _log = log;
    }

    public async Task<List<PontoNdvi>> ConsultarSerieNdviAsync(string geoJsonPoligono, DateOnly desde, DateOnly ate, string intervalo, CancellationToken ct = default)
    {
        var token = await AutenticarAsync(ct);

        // com crs EPSG:4326 a API lê resx/resy em graus, não em metros
        var (resx, resy) = CalcularResolucaoGraus(geoJsonPoligono);

        var corpo = new JsonObject
        {
            ["input"] = new JsonObject
            {
                ["bounds"] = new JsonObject
                {
                    ["geometry"] = ParseGeometria(geoJsonPoligono),
                    ["properties"] = new JsonObject { ["crs"] = Crs4326 },
                },
                ["data"] = new JsonArray(new JsonObject { ["type"] = "sentinel-2-l2a" }),
            },
            ["aggregation"] = new JsonObject
            {
                ["timeRange"] = IntervaloTempo(desde, ate),
                ["aggregationInterval"] = new JsonObject { ["of"] = intervalo },
                ["evalscript"] = EvalscriptNdvi,
                ["resx"] = resx,
                ["resy"] = resy,
            },
        };

        _log.LogInformation("Copernicus/Sentinel-2: consultando série de NDVI entre {Desde} e {Ate} (intervalo {Intervalo})", desde, ate, intervalo);

        JsonNode? resposta;
        try
        {
            using var req = Requisicao(_opcoes.StatisticsUrl, corpo, token, "application/json");
            using var resp = await _http.SendAsync(req, ct);
            var texto = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new RegraNegocioException($"Erro {(int)resp.StatusCode} ao consultar o NDVI no Copernicus Data Space Ecosystem: {JsonUtil.Resumir(texto)}");
            resposta = JsonNode.Parse(texto);
        }
        catch (RegraNegocioException) { throw; }
        catch (Exception e)
        {
            throw new RegraNegocioException("Não foi possível conectar ao Copernicus Data Space Ecosystem: " + e.Message);
        }

        if (resposta?["data"] is not JsonArray dados)
            throw new RegraNegocioException("O Copernicus Data Space Ecosystem não retornou dados de NDVI para esse período.");

        var pontos = new List<PontoNdvi>();
        foreach (var item in dados)
        {
            try
            {
                var p = InterpretarIntervalo(item);
                if (p != null) pontos.Add(p);
            }
            catch (Exception e)
            {
                _log.LogWarning("Copernicus/Sentinel-2: não foi possível interpretar um intervalo: {Erro} — nó bruto: {No}", e.Message, item?.ToJsonString());
            }
        }
        _log.LogInformation("Copernicus/Sentinel-2: {Total} ponto(s) de NDVI calculado(s).", pontos.Count);
        return pontos;
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_opcoes.ClientId) && !string.IsNullOrWhiteSpace(_opcoes.ClientSecret);

    private const string EvalscriptCorRetangulo = """
        //VERSION=3
        function setup() { return { input: [{ bands: ["B04", "B03", "B02", "dataMask"] }], output: { bands: 4 } }; }
        function evaluatePixel(s) { const g = 2.8; return [s.B04 * g, s.B03 * g, s.B02 * g, s.dataMask]; }
        """;

    private const string EvalscriptNdviRetangulo = """
        //VERSION=3
        function setup() { return { input: [{ bands: ["B04", "B08", "dataMask"] }], output: { bands: 4 } }; }
        var rampa = [[-1.0, 0xb0392b], [0.2, 0xb0392b], [0.35, 0xd9a441], [0.5, 0xd9d441], [0.65, 0x7ab648], [0.8, 0x2f7a54], [1.0, 0x15533a]];
        function mix(c0, c1, f) {
          return [(((c0 >> 16) & 255) + ((((c1 >> 16) & 255) - ((c0 >> 16) & 255)) * f)) / 255,
                  (((c0 >> 8) & 255) + ((((c1 >> 8) & 255) - ((c0 >> 8) & 255)) * f)) / 255,
                  ((c0 & 255) + (((c1 & 255) - (c0 & 255)) * f)) / 255];
        }
        function evaluatePixel(s) {
          var v = (s.B08 - s.B04) / (s.B08 + s.B04 + 1e-6);
          for (var i = 1; i < rampa.length; i++)
            if (v <= rampa[i][0]) { var c = mix(rampa[i - 1][1], rampa[i][1], (v - rampa[i - 1][0]) / (rampa[i][0] - rampa[i - 1][0] + 1e-9)); return [c[0], c[1], c[2], s.dataMask]; }
          var u = mix(0x15533a, 0x15533a, 0); return [u[0], u[1], u[2], s.dataMask];
        }
        """;

    public async Task<byte[]> GerarImagemRetanguloAsync(double[] caixa, DateOnly desde, DateOnly ate, string tipo, int largura, int altura, CancellationToken ct = default)
    {
        var token = await AutenticarAsync(ct);
        var colecao = desde.Year < 2019 ? "sentinel-2-l1c" : "sentinel-2-l2a";
        var corpo = new JsonObject
        {
            ["input"] = new JsonObject
            {
                ["bounds"] = new JsonObject
                {
                    ["bbox"] = new JsonArray(caixa[0], caixa[1], caixa[2], caixa[3]),
                    ["properties"] = new JsonObject { ["crs"] = Crs4326 },
                },
                ["data"] = new JsonArray(new JsonObject
                {
                    ["type"] = colecao,
                    ["dataFilter"] = new JsonObject
                    {
                        ["timeRange"] = IntervaloTempo(desde, ate),
                        ["mosaickingOrder"] = "leastCC",
                        ["maxCloudCoverage"] = 30,
                    },
                }),
            },
            ["output"] = new JsonObject
            {
                ["width"] = Math.Clamp(largura, 32, 2000),
                ["height"] = Math.Clamp(altura, 32, 2000),
                ["responses"] = new JsonArray(new JsonObject { ["identifier"] = "default", ["format"] = new JsonObject { ["type"] = "image/png" } }),
            },
            ["evalscript"] = tipo == "ndvi" ? EvalscriptNdviRetangulo : EvalscriptCorRetangulo,
        };
        _log.LogInformation("Copernicus/Sentinel-2: imagem {Tipo} {Colecao} de {Desde} a {Ate}", tipo, colecao, desde, ate);
        using var req = Requisicao(_opcoes.ProcessUrl, corpo, token, "image/png");
        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var texto = await resp.Content.ReadAsStringAsync(ct);
            throw new RegraNegocioException($"Erro {(int)resp.StatusCode} ao gerar a imagem do Sentinel-2: {JsonUtil.Resumir(texto)}");
        }
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    public Task<byte[]> GerarImagemCorRealAsync(string geoJsonPoligono, DateOnly desde, DateOnly ate, CancellationToken ct = default) =>
        ChamarProcessApiAsync(geoJsonPoligono, desde, ate, EvalscriptCorReal, "cor real", ct);

    public Task<byte[]> GerarImagemMapaCalorNdviAsync(string geoJsonPoligono, DateOnly desde, DateOnly ate, CancellationToken ct = default) =>
        ChamarProcessApiAsync(geoJsonPoligono, desde, ate, EvalscriptMapaCalor, "mapa de calor de NDVI", ct);

    private async Task<byte[]> ChamarProcessApiAsync(string geoJson, DateOnly desde, DateOnly ate, string evalscript, string rotulo, CancellationToken ct)
    {
        var token = await AutenticarAsync(ct);
        var (largura, altura) = CalcularDimensoesImagem(geoJson);

        var corpo = new JsonObject
        {
            ["input"] = new JsonObject
            {
                ["bounds"] = new JsonObject
                {
                    ["geometry"] = ParseGeometria(geoJson),
                    ["properties"] = new JsonObject { ["crs"] = Crs4326 },
                },
                ["data"] = new JsonArray(new JsonObject
                {
                    ["type"] = "sentinel-2-l2a",
                    ["dataFilter"] = new JsonObject
                    {
                        ["timeRange"] = IntervaloTempo(desde, ate),
                        // cena com menos nuvem no período, em vez da mais recente
                        ["mosaickingOrder"] = "leastCC",
                    },
                }),
            },
            ["output"] = new JsonObject
            {
                ["width"] = largura,
                ["height"] = altura,
                ["responses"] = new JsonArray(new JsonObject
                {
                    ["identifier"] = "default",
                    ["format"] = new JsonObject { ["type"] = "image/png" },
                }),
            },
            ["evalscript"] = evalscript,
        };

        _log.LogInformation("Copernicus/Sentinel-2: gerando imagem ({Rotulo}) entre {Desde} e {Ate} ({L}x{A}px)", rotulo, desde, ate, largura, altura);

        try
        {
            using var req = Requisicao(_opcoes.ProcessUrl, corpo, token, "image/png");
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var texto = await resp.Content.ReadAsStringAsync(ct);
                throw new RegraNegocioException($"Erro {(int)resp.StatusCode} ao gerar a imagem ({rotulo}) no Copernicus Data Space Ecosystem: {JsonUtil.Resumir(texto)}");
            }
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
        catch (RegraNegocioException) { throw; }
        catch (Exception e)
        {
            throw new RegraNegocioException($"Não foi possível gerar a imagem ({rotulo}) no Copernicus Data Space Ecosystem: {e.Message}");
        }
    }

    private static HttpRequestMessage Requisicao(string url, JsonObject corpo, string token, string accept)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        return req;
    }

    private static JsonObject IntervaloTempo(DateOnly desde, DateOnly ate) => new()
    {
        ["from"] = desde.ToString("yyyy-MM-dd") + "T00:00:00Z",
        ["to"] = ate.ToString("yyyy-MM-dd") + "T23:59:59Z",
    };

    private static JsonNode ParseGeometria(string geoJson)
    {
        try { return JsonNode.Parse(geoJson) ?? throw new FormatException("vazio"); }
        catch (Exception e) { throw new RegraNegocioException("Geometria da propriedade inválida para consulta de NDVI: " + e.Message); }
    }

    private static (double LarguraM, double AlturaM, double CentroLatRad)? Medidas(string geoJson)
    {
        var bbox = GeoUtil.BoundingBox(geoJson);
        if (bbox is null) return null;
        var centroLatRad = (bbox[1] + bbox[3]) / 2.0 * Math.PI / 180.0;
        var largura = Math.Max((bbox[2] - bbox[0]) * MetrosPorGrau * Math.Cos(centroLatRad), 1.0);
        var altura = Math.Max((bbox[3] - bbox[1]) * MetrosPorGrau, 1.0);
        return (largura, altura, centroLatRad);
    }

    private static (double, double) CalcularResolucaoGraus(string geoJson)
    {
        var m = Medidas(geoJson);
        if (m is null)
        {
            var fallback = MetrosPorPixelAlvoEstatistica / MetrosPorGrau;
            return (fallback, fallback);
        }
        var (larguraM, alturaM, centroLatRad) = m.Value;
        var metrosPorPixel = Math.Max(MetrosPorPixelAlvoEstatistica, Math.Max(larguraM, alturaM) / PixelsMaxEstatistica);
        metrosPorPixel = Math.Min(metrosPorPixel, MetrosPorPixelLimite);
        return (metrosPorPixel / (MetrosPorGrau * Math.Cos(centroLatRad)), metrosPorPixel / MetrosPorGrau);
    }

    private static (int, int) CalcularDimensoesImagem(string geoJson)
    {
        var m = Medidas(geoJson);
        if (m is null) return (512, 512);
        var (larguraM, alturaM, _) = m.Value;
        var ladoMax = (int)Math.Max(LadoPadraoPx, Math.Ceiling(Math.Max(larguraM, alturaM) / MetrosPorPixelLimite));
        ladoMax = Math.Min(ladoMax, LadoMaxPx);

        int largura, altura;
        if (larguraM >= alturaM) { largura = ladoMax; altura = (int)Math.Round(ladoMax * (alturaM / larguraM)); }
        else { altura = ladoMax; largura = (int)Math.Round(ladoMax * (larguraM / alturaM)); }
        return (Math.Max(largura, 32), Math.Max(altura, 32));
    }

    private static PontoNdvi? InterpretarIntervalo(JsonNode? item)
    {
        var stats = item?["outputs"]?["ndvi"]?["bands"]?["B0"]?["stats"];
        var amostras = (int)(JsonUtil.Numero(stats?["sampleCount"]) ?? 0);
        if (stats is null || amostras == 0) return null;

        var semDado = (int)(JsonUtil.Numero(stats["noDataCount"]) ?? 0);
        var de = JsonUtil.Texto(item!["interval"]?["from"]) ?? throw new FormatException("intervalo sem data");

        return new PontoNdvi
        {
            DataInicio = DateOnly.ParseExact(de[..10], "yyyy-MM-dd"),
            NdviMedio = Arredondar(JsonUtil.Numero(stats["mean"])),
            NdviMinimo = Arredondar(JsonUtil.Numero(stats["min"])),
            NdviMaximo = Arredondar(JsonUtil.Numero(stats["max"])),
            Amostras = amostras,
            PercentualAmostrasValidas = amostras + semDado > 0 ? Arredondar(100.0 * amostras / (amostras + semDado)) : null,
        };
    }

    private static decimal? Arredondar(double? v)
    {
        if (v is null || double.IsNaN(v.Value) || double.IsInfinity(v.Value)) return null;
        return Math.Round((decimal)v.Value, 3, MidpointRounding.AwayFromZero);
    }

    private async Task<string> AutenticarAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.ClientId) || string.IsNullOrWhiteSpace(_opcoes.ClientSecret))
            throw new RegraNegocioException(
                "A consulta de NDVI exige credenciais do Copernicus Data Space Ecosystem. Crie uma conta gratuita em " +
                "https://dataspace.copernicus.eu, gere um OAuth client em \"Account Settings -> OAuth clients\" e preencha " +
                "AgroCert:Ambiental:Copernicus:ClientId e :ClientSecret (User Secrets ou variáveis de ambiente).");

        if (_cache.Token != null && DateTime.UtcNow < _cache.ExpiraEmUtc) return _cache.Token;

        await _cache.Trava.WaitAsync(ct);
        try
        {
            if (_cache.Token != null && DateTime.UtcNow < _cache.ExpiraEmUtc) return _cache.Token;

            JsonNode? resposta;
            try
            {
                using var form = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _opcoes.ClientId!,
                    ["client_secret"] = _opcoes.ClientSecret!,
                });
                using var resp = await _http.PostAsync(_opcoes.OAuthTokenUrl, form, ct);
                var texto = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                    throw new RegraNegocioException($"Erro {(int)resp.StatusCode} ao autenticar no Copernicus Data Space Ecosystem — confira o Client ID/Secret: {JsonUtil.Resumir(texto)}");
                resposta = JsonNode.Parse(texto);
            }
            catch (RegraNegocioException) { throw; }
            catch (Exception e)
            {
                throw new RegraNegocioException("Não foi possível autenticar no Copernicus Data Space Ecosystem: " + e.Message);
            }

            var token = JsonUtil.Texto(resposta?["access_token"]);
            if (string.IsNullOrWhiteSpace(token))
                throw new RegraNegocioException("O Copernicus Data Space Ecosystem não retornou um token válido.");

            var expira = (int)(JsonUtil.Numero(resposta!["expires_in"]) ?? 300);
            _cache.Token = token;
            _cache.ExpiraEmUtc = DateTime.UtcNow.AddSeconds(Math.Max(0, expira - 30)); // renova 30s antes
            return token;
        }
        finally
        {
            _cache.Trava.Release();
        }
    }
}
