using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using AgroCert.Domain.Excecoes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgroCert.Infrastructure.Integracoes;

public class MapBiomasCliente : IMapBiomasCliente
{
    private const string QueryAlertas = """
        query AlertasPorCar($carCodes: [ID!], $startDate: BaseDate, $endDate: BaseDate, $limit: Int) {
          alerts(carCodes: $carCodes, startDate: $startDate, endDate: $endDate, limit: $limit) {
            collection {
              alertCode
              detectedAt
              areaHa
              deforestationClasses
              sources
              geometryWkt
              publishedImages {
                url
              }
            }
          }
        }
        """;

    private const string MutationLogin = """
        mutation SignIn($email: String!, $password: String!) {
          signIn(email: $email, password: $password) {
            token
          }
        }
        """;

    private readonly HttpClient _http;
    private readonly OpcoesMapBiomas _opcoes;
    private readonly ILogger<MapBiomasCliente> _log;

    public MapBiomasCliente(HttpClient http, IOptions<OpcoesAgroCert> opcoes, ILogger<MapBiomasCliente> log)
    {
        _http = http;
        _opcoes = opcoes.Value.Ambiental.MapBiomas;
        _log = log;
    }

    public async Task<List<AlertaExterno>> ConsultarAlertasPorCarAsync(string carCodigo, DateOnly? desde, DateOnly? ate, CancellationToken ct = default)
    {
        var token = await AutenticarAsync(ct);

        var variaveis = new Dictionary<string, object?>
        {
            ["carCodes"] = new[] { carCodigo },
            ["startDate"] = desde?.ToString("yyyy-MM-dd"),
            ["endDate"] = ate?.ToString("yyyy-MM-dd"),
            ["limit"] = 200,
        };

        _log.LogInformation("MapBiomas Alerta: consultando CAR {Car} entre {Desde} e {Ate}", carCodigo, desde, ate);

        var resposta = await ExecutarGraphQlAsync(QueryAlertas, variaveis, token, ct);
        LancarSeHouverErros(resposta, "consultar os alertas");

        var lista = ExtrairLista(resposta["data"]?["alerts"]);
        var resultado = new List<AlertaExterno>();
        foreach (var no in lista)
        {
            try { resultado.Add(Interpretar(no!)); }
            catch (Exception e)
            {
                _log.LogWarning("MapBiomas Alerta: não foi possível interpretar um registro: {Erro} — nó bruto: {No}", e.Message, no?.ToJsonString());
            }
        }

        _log.LogInformation("MapBiomas Alerta: {Total} alerta(s) encontrado(s) para o CAR {Car}.", resultado.Count, carCodigo);
        _log.LogDebug("MapBiomas Alerta — resposta bruta: {Resposta}", resposta.ToJsonString());
        return resultado;
    }

    private async Task<string> AutenticarAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opcoes.Email) || string.IsNullOrWhiteSpace(_opcoes.Senha))
            throw new RegraNegocioException(
                "A consulta ao MapBiomas Alerta exige login. Crie uma conta gratuita em " +
                "https://plataforma.alerta.mapbiomas.org/sign-up, confirme o e-mail recebido e preencha " +
                "AgroCert:Ambiental:MapBiomas:Email e :Senha (User Secrets ou variáveis de ambiente).");

        var resposta = await ExecutarGraphQlAsync(MutationLogin,
            new Dictionary<string, object?> { ["email"] = _opcoes.Email, ["password"] = _opcoes.Senha }, null, ct);
        LancarSeHouverErros(resposta, "fazer login");

        var token = JsonUtil.Texto(resposta["data"]?["signIn"]?["token"]);
        if (string.IsNullOrWhiteSpace(token))
            throw new RegraNegocioException(
                "O login no MapBiomas Alerta não retornou um token válido. Confira o e-mail/senha configurados " +
                "e se a conta já teve o e-mail confirmado.");
        return token;
    }

    private async Task<JsonObject> ExecutarGraphQlAsync(string query, Dictionary<string, object?> variaveis, string? token, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "")
            {
                Content = JsonContent.Create(new { query, variables = variaveis }),
            };
            if (token != null) req.Headers.Authorization = new("Bearer", token);

            using var resp = await _http.SendAsync(req, ct);
            var texto = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new RegraNegocioException($"Erro {(int)resp.StatusCode} ao chamar o MapBiomas Alerta: {JsonUtil.Resumir(texto)}");

            return JsonNode.Parse(texto) as JsonObject
                   ?? throw new RegraNegocioException("O MapBiomas Alerta não retornou nenhum dado.");
        }
        catch (RegraNegocioException) { throw; }
        catch (Exception e)
        {
            throw new RegraNegocioException("Não foi possível conectar ao MapBiomas Alerta: " + e.Message);
        }
    }

    private static void LancarSeHouverErros(JsonObject resposta, string acao)
    {
        if (resposta["errors"] is JsonArray erros && erros.Count > 0)
        {
            var msgs = string.Join("; ", erros.Select(e => JsonUtil.Texto(e?["message"]) ?? e?.ToJsonString()));
            throw new RegraNegocioException($"O MapBiomas Alerta retornou erro ao {acao}: {msgs}");
        }
    }

    private static JsonArray ExtrairLista(JsonNode? no)
    {
        if (no is JsonArray a) return a;
        if (no is JsonObject o)
            foreach (var campo in new[] { "collection", "items", "nodes", "data" })
                if (o[campo] is JsonArray lista) return lista;
        return new JsonArray();
    }

    private static AlertaExterno Interpretar(JsonNode no)
    {
        var alerta = new AlertaExterno
        {
            CodigoAlerta = JsonUtil.Texto(no["alertCode"]) ?? JsonUtil.Texto(no["id"]),
            DataDeteccao = LerData(JsonUtil.Texto(no["detectedAt"])),
            AreaHa = JsonUtil.Decimal(no["areaHa"]) ?? 0m,
            Classe = Juntar(no["deforestationClasses"]),
            Fonte = Juntar(no["sources"]) is { Length: > 0 } f ? f : "MAPBIOMAS_ALERTA",
        };

        var urls = (no["publishedImages"] as JsonArray ?? new JsonArray())
            .Select(i => JsonUtil.Texto(i?["url"]))
            .Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
        alerta.ImagemAntesUrl = urls.ElementAtOrDefault(0);
        alerta.ImagemDepoisUrl = urls.ElementAtOrDefault(1);

        var wkt = JsonUtil.Texto(no["geometryWkt"]);
        if (!string.IsNullOrWhiteSpace(wkt))
        {
            try { alerta.GeoJson = GeoUtil.EscreverMultiPolygon(GeoUtil.LerWkt(wkt)); }
            catch {  }
        }
        return alerta;
    }

    private static DateOnly? LerData(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return DateOnly.TryParse(texto.Length >= 10 ? texto[..10] : texto, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    }

    private static string? Juntar(JsonNode? n) =>
        n is JsonArray a && a.Count > 0 ? string.Join(", ", a.Select(JsonUtil.Texto)) : null;
}
