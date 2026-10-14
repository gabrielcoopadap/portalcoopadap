using System.Text.Json.Nodes;
using AgroCert.Application.Dtos;
using AgroCert.Application.Interfaces;
using AgroCert.Application.Util;
using AgroCert.Domain.Excecoes;

namespace AgroCert.Infrastructure.Integracoes;

public class SicarCliente : ISicarCliente
{
    private readonly HttpClient _http;

    public SicarCliente(HttpClient http) => _http = http;

    public async Task<DadosImovelCar> ConsultarPorCodigoCarAsync(string carCodigo, CancellationToken ct = default)
    {
        JsonNode? corpo;
        try
        {
            using var resp = await _http.GetAsync("imovel/" + Uri.EscapeDataString(carCodigo), ct);
            var texto = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"HTTP {(int)resp.StatusCode}: {JsonUtil.Resumir(texto)}");
            corpo = JsonNode.Parse(texto);
        }
        catch (Exception e) when (e is not RegraNegocioException)
        {
            throw new RegraNegocioException(
                "Não foi possível consultar o CAR no SICAR (a API oficial normalmente exige cadastro/token via " +
                "Conecta gov.br). Cadastre a propriedade colando a geometria manualmente, ou ajuste " +
                "AgroCert:Ambiental:Sicar no appsettings.json. Detalhe: " + e.Message);
        }

        if (corpo is not JsonObject obj)
            throw new RegraNegocioException("O SICAR não retornou dados para o CAR " + carCodigo + ".");
        return Interpretar(obj);
    }

    private static DadosImovelCar Interpretar(JsonObject body) => new()
    {
        AreaHa = JsonUtil.Decimal(body["area_ha"]),
        AreaReservaLegalHa = JsonUtil.Decimal(body["area_reserva_legal_ha"]),
        AreaAppHa = JsonUtil.Decimal(body["area_app_ha"]),
        AreaVegetacaoNativaHa = JsonUtil.Decimal(body["area_vegetacao_nativa_ha"]),
        Municipio = JsonUtil.Texto(body["municipio"]),
        Uf = JsonUtil.Texto(body["uf"]),
        GeoJson = LerGeometria(body, "geometria"),
        GeoJsonReservaLegal = LerGeometria(body, "reserva_legal"),
        GeoJsonApp = LerGeometria(body, "app"),
    };

    private static string? LerGeometria(JsonObject body, string campo)
    {
        var n = body[campo];
        if (n is null) return null;
        try
        {
            var texto = n is JsonValue v && v.TryGetValue<string>(out var s) ? s : n.ToJsonString();
            return GeoUtil.NormalizarParaMultiPolygon(texto);
        }
        catch
        {
            throw new RegraNegocioException($"Não foi possível interpretar a geometria '{campo}' retornada pelo SICAR.");
        }
    }
}
