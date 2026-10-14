using AgroCert.Application;
using AgroCert.Infrastructure;
using AgroCert.Infrastructure.Arquivos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace AgroCert.Certificacao.Infra;

public static class ModuloCertificacao
{
    public const string Politica = "ModuloCertificacao";
    public const string PoliticaCors = "AgroCertCors";

    public static IServiceCollection AddModuloCertificacao(this IServiceCollection services, IConfiguration config)
    {
        services.AddAgroCertApplication();
        services.AddAgroCertInfrastructure(config);

        var exigirLogin = config.GetValue("AgroCert:Acesso:ExigirLogin", false);
        var perfis = config.GetSection("AgroCert:Acesso:Perfis").Get<string[]>() ?? Array.Empty<string>();
        services.AddAuthorization(o => o.AddPolicy(Politica, p =>
        {
            if (!exigirLogin) { p.RequireAssertion(_ => true); return; }
            p.RequireAuthenticatedUser();
            if (perfis.Length > 0) p.RequireRole(perfis);
        }));

        services.AddScoped<ImagemSateliteConformidade>();
        services.AddHealthChecks().AddCheck<BancoHealthCheck>("postgres");

        var origens = config.GetSection("AgroCert:Cors:Origens").Get<string[]>() ?? Array.Empty<string>();
        services.AddCors(o => o.AddPolicy(PoliticaCors, p =>
        {
            if (origens.Length > 0) p.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Content-Disposition");
        }));

        services.AddControllersWithViews();
        services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 60L * 1024 * 1024);
        return services;
    }

    public static WebApplication UseModuloCertificacao(this WebApplication app)
    {
        var armazenamento = app.Services.GetRequiredService<ArmazenamentoLocal>();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(armazenamento.DiretorioBase),
            RequestPath = "/uploads",
        });
        return app;
    }

    public static Task InicializarModuloCertificacaoAsync(this WebApplication app) =>
        app.Services.InicializarBancoAgroCertAsync(app.Configuration);
}
