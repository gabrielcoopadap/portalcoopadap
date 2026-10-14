using System.Text.Encodings.Web;
using AgroCert.Application.Util;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AgroCert.Certificacao.Infra;

public static class HtmlExtensoes
{
    public static IHtmlContent Pilula(this IHtmlHelper _, string? texto, Estilo estilo) =>
        new HtmlString($"<span class=\"cm-pilula\" style=\"background:{estilo.Bg};color:{estilo.Cor}\">{HtmlEncoder.Default.Encode(texto ?? "-")}</span>");

    public static IHtmlContent Icone(this IHtmlHelper _, string nome, string? estilo = null) =>
        new HtmlString($"<span class=\"material-icons\" aria-hidden=\"true\"{(estilo is null ? "" : $" style=\"{estilo}\"")}>{nome}</span>");

    public static IHtmlContent Foto(this IHtmlHelper _, string? url, GrupoBio grupo, string classe = "", string? alt = null)
    {
        var enc = HtmlEncoder.Default;
        var conteudo = string.IsNullOrWhiteSpace(url)
            ? $"<span class=\"material-icons\" aria-hidden=\"true\">{grupo.Icone}</span>"
            : $"<img src=\"{enc.Encode(url)}\" alt=\"{enc.Encode(alt ?? "")}\" loading=\"lazy\" />";
        return new HtmlString($"<div class=\"ff-foto {classe}\" style=\"--cor:{grupo.Cor}\">{conteudo}</div>");
    }

    public static string CorSuave(string? hex, string alfa = "14") =>
        string.IsNullOrWhiteSpace(hex) || !hex.StartsWith('#') || hex.Length != 7 ? "#5e9a4214" : hex + alfa;
}
