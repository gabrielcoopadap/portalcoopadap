using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgroCert.Certificacao.Infra;

[AttributeUsage(AttributeTargets.Class)]
public sealed class JsonModuloAttribute : ResultFilterAttribute
{
    public static readonly JsonSerializerOptions Opcoes = Criar();

    private static JsonSerializerOptions Criar()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            // acentos sem \uXXXX (continua escapando < > & ' para não abrir brecha de HTML)
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult r)
            context.Result = new JsonResult(r.Value, Opcoes) { StatusCode = r.StatusCode ?? 200 };
    }
}
