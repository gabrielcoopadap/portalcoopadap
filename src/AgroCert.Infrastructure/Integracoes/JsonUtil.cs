using System.Globalization;
using System.Text.Json.Nodes;

namespace AgroCert.Infrastructure.Integracoes;

internal static class JsonUtil
{
    public static string? Texto(JsonNode? n)
    {
        if (n is null) return null;
        if (n is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s;
            return v.ToJsonString();
        }
        return n.ToJsonString();
    }

    public static double? Numero(JsonNode? n)
    {
        if (n is not JsonValue v) return null;
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<string>(out var s) &&
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var ds)) return ds;
        return null;
    }

    public static decimal? Decimal(JsonNode? n)
    {
        var d = Numero(n);
        if (d is null || double.IsNaN(d.Value) || double.IsInfinity(d.Value)) return null;
        return (decimal)d.Value;
    }

    public static string Resumir(string? corpo)
    {
        if (string.IsNullOrWhiteSpace(corpo)) return "(sem corpo de resposta)";
        return corpo.Length > 800 ? corpo[..800] + "..." : corpo;
    }
}
