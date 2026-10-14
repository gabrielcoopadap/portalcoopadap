using AgroCert.Domain.Excecoes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace AgroCert.Certificacao.Infra;

public sealed class TratarErrosAttribute : ExceptionFilterAttribute
{
    public const string ChaveErro = "cm-erro";
    public const string ChaveSucesso = "cm-ok";

    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not RegraNegocioException erro) return;

        var ehApi = context.HttpContext.Request.Path.StartsWithSegments("/api");
        var status = erro is NaoEncontradoException ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;

        if (ehApi)
        {
            context.Result = new ObjectResult(new { mensagem = erro.Message }) { StatusCode = status };
        }
        else if (HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            var viewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
            {
                ["Mensagem"] = erro.Message,
            };
            context.Result = new ViewResult { ViewName = "~/Areas/Certificacao/Views/Shared/Erro.cshtml", ViewData = viewData, StatusCode = status };
        }
        else
        {
            var tempData = context.HttpContext.RequestServices.GetService(typeof(ITempDataDictionaryFactory)) as ITempDataDictionaryFactory;
            if (tempData != null) tempData.GetTempData(context.HttpContext)[ChaveErro] = erro.Message;
            context.Result = new RedirectResult(Voltar(context.HttpContext));
        }
        context.ExceptionHandled = true;
    }

    public static string Voltar(HttpContext http, string padrao = "/certificacao")
    {
        var voltar = http.Request.HasFormContentType ? http.Request.Form["voltarPara"].ToString() : "";
        if (EhLocal(voltar)) return voltar;
        var referer = http.Request.Headers.Referer.ToString();
        if (Uri.TryCreate(referer, UriKind.Absolute, out var uri) && uri.Host == http.Request.Host.Host)
            return uri.PathAndQuery;
        return padrao;
    }

    public static bool EhLocal(string? url) =>
        !string.IsNullOrEmpty(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");
}
