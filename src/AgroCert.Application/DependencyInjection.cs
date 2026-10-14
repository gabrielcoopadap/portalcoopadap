using AgroCert.Application.Servicos;
using Microsoft.Extensions.DependencyInjection;

namespace AgroCert.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAgroCertApplication(this IServiceCollection services)
    {
        services.AddScoped<CertificacaoServico>();
        services.AddScoped<RequisitoServico>();
        services.AddScoped<VistoriaServico>();
        services.AddScoped<MonitoramentoAmbientalServico>();
        services.AddScoped<ConformidadeAmbientalServico>();
        services.AddScoped<PainelServico>();
        services.AddScoped<FaunaFloraServico>();
        return services;
    }
}
