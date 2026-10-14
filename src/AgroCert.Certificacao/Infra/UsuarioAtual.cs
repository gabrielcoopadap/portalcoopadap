using System.Security.Claims;

namespace AgroCert.Certificacao.Infra;

public static class UsuarioAtual
{
    public static string? Id(ClaimsPrincipal u) =>
        u.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
        ?? u.FindFirstValue("oid")
        ?? u.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string? Nome(ClaimsPrincipal u) =>
        u.FindFirstValue("name") ?? u.Identity?.Name;

    public static bool EhConsultor(ClaimsPrincipal u) => u.IsInRole("Consultor");
}
