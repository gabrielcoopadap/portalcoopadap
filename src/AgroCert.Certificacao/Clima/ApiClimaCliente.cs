using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgroCert.Certificacao.Clima;

public class ApiClimaException : Exception
{
    public bool Indisponivel { get; }
    public ApiClimaException(string mensagem, bool indisponivel, Exception? interna = null) : base(mensagem, interna) =>
        Indisponivel = indisponivel;
}

public class ApiClimaCliente
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _http;
    public ApiClimaCliente(HttpClient http) => _http = http;

    public string Endereco => _http.BaseAddress?.ToString().TrimEnd('/') ?? "";

    public Task<PrevisaoAnoDto> PreverAsync(ParametrosPrevisao p, CancellationToken ct)
    {
        var inv = CultureInfo.InvariantCulture;
        var q = new List<string>
        {
            $"ciclo={p.Ciclo}",
            $"pesoCiclo={p.PesoCiclo.ToString(inv)}",
            $"nivel={p.Nivel}",
        };
        if (!string.IsNullOrWhiteSpace(p.Oni)) q.Add("oni=" + Uri.EscapeDataString(p.Oni.Trim().Replace(" ", "")));
        return GetAsync<PrevisaoAnoDto>($"ciclos/prever/{p.Ano}?{string.Join("&", q)}", ct);
    }

    public Task<SituacaoEnsoDto> SituacaoEnsoAsync(CancellationToken ct) => GetAsync<SituacaoEnsoDto>("enso/situacao", ct);

    public Task<ImpactoEnsoDto> ImpactoEnsoAsync(CancellationToken ct) => GetAsync<ImpactoEnsoDto>("enso/impacto", ct);

    public Task<ValidacaoDto> ValidacaoAsync(int antecedencia, CancellationToken ct) =>
        GetAsync<ValidacaoDto>($"ciclos/validacao?antecedencia={antecedencia}", ct);

    public Task<QualidadeDto> QualidadeAsync(CancellationToken ct) => GetAsync<QualidadeDto>("dados-climaticos/qualidade", ct);

    public Task<CorrecaoEnsoDto> CorrigirEnsoAsync(bool aplicar, CancellationToken ct) =>
        EnviarAsync<CorrecaoEnsoDto>(HttpMethod.Post, $"dados-climaticos/corrigir-enso{(aplicar ? "?aplicar=true" : "")}", ct);

    private Task<T> GetAsync<T>(string rota, CancellationToken ct) => EnviarAsync<T>(HttpMethod.Get, rota, ct);

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string rota, CancellationToken ct)
    {
        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(new HttpRequestMessage(metodo, rota), ct);
        }
        catch (HttpRequestException e)
        {
            throw new ApiClimaException($"Não foi possível conectar à api-clima em {Endereco}.", true, e);
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ApiClimaException($"A api-clima em {Endereco} não respondeu a tempo.", true, e);
        }

        using (resp)
        {
            var corpo = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                var indisponivel = resp.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
                throw new ApiClimaException(MensagemDeErro(corpo, (int)resp.StatusCode), indisponivel);
            }
            try
            {
                return JsonSerializer.Deserialize<T>(corpo, Json)
                       ?? throw new ApiClimaException("A api-clima devolveu uma resposta vazia.", false);
            }
            catch (JsonException e)
            {
                throw new ApiClimaException($"Resposta inesperada da api-clima ({rota}): {e.Message}", false, e);
            }
        }
    }

    private static string MensagemDeErro(string corpo, int status)
    {
        try
        {
            using var doc = JsonDocument.Parse(corpo);
            if (doc.RootElement.TryGetProperty("message", out var m))
            {
                if (m.ValueKind == JsonValueKind.String) return m.GetString()!;
                if (m.ValueKind == JsonValueKind.Array) return string.Join(" ", m.EnumerateArray().Select(x => x.ToString()));
            }
        }
        catch (JsonException) {  }
        return status >= 500
            ? $"A api-clima teve um erro interno (HTTP {status}). Veja o terminal onde ela está rodando."
            : $"A api-clima recusou o pedido (HTTP {status}).";
    }
}
