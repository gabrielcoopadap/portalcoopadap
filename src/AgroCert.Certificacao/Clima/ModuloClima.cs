using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgroCert.Certificacao.Clima;

public static class ModuloClima
{
    public const string Politica = "ModuloClima";

    public static IServiceCollection AddModuloClima(this IServiceCollection services, IConfiguration config)
    {
        var url = config["Clima:ApiUrl"];
        if (string.IsNullOrWhiteSpace(url)) url = "http://localhost:3030";
        var timeout = config.GetValue("Clima:TimeoutSegundos", 60);

        services.AddHttpClient<ApiClimaCliente>(c =>
        {
            c.BaseAddress = new Uri(url.TrimEnd('/') + "/");
            c.Timeout = TimeSpan.FromSeconds(timeout);
        });

        var exigirLogin = config.GetValue("Clima:Acesso:ExigirLogin", false);
        var perfis = config.GetSection("Clima:Acesso:Perfis").Get<string[]>() ?? Array.Empty<string>();
        services.AddAuthorization(o => o.AddPolicy(Politica, p =>
        {
            if (!exigirLogin) { p.RequireAssertion(_ => true); return; }
            p.RequireAuthenticatedUser();
            if (perfis.Length > 0) p.RequireRole(perfis);
        }));

        services.AddControllersWithViews();
        return services;
    }
}
