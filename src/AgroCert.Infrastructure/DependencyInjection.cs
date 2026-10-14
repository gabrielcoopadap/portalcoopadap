using AgroCert.Application.Interfaces;
using AgroCert.Infrastructure.Arquivos;
using AgroCert.Infrastructure.Integracoes;
using AgroCert.Infrastructure.Persistencia;
using AgroCert.Infrastructure.Persistencia.Repositorios;
using AgroCert.Infrastructure.Persistencia.Seed;
using AgroCert.Infrastructure.Relatorios;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AgroCert.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAgroCertInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<OpcoesAgroCert>(config.GetSection(OpcoesAgroCert.Secao));

        var conexao = config.GetConnectionString("AgroCert")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:AgroCert (PostgreSQL).");
        services.AddDbContext<AgroCertDbContext>(o => o.UseNpgsql(conexao,
            pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", AgroCertDbContext.Schema)));

        services.AddScoped<ICertificacaoRepositorio, CertificacaoRepositorio>();
        services.AddScoped<IRequisitoRepositorio, RequisitoRepositorio>();
        services.AddScoped<IVistoriaRepositorio, VistoriaRepositorio>();
        services.AddScoped<IAmbientalRepositorio, AmbientalRepositorio>();
        services.AddScoped<IFaunaFloraRepositorio, FaunaFloraRepositorio>();
        services.AddScoped<InicializadorBanco>();

        services.AddSingleton<ArmazenamentoLocal>();
        services.AddSingleton<IArmazenamentoArquivos>(sp => sp.GetRequiredService<ArmazenamentoLocal>());

        services.AddSingleton<CacheTokenCopernicus>();
        services.AddSingleton<ICoberturaMapBiomas, CoberturaMapBiomasLocal>();
        services.AddSingleton<IUnidadesConservacao, UnidadesConservacaoLocal>();
        services.AddHttpClient<ISicarCliente, SicarCliente>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<OpcoesAgroCert>>().Value.Ambiental.Sicar;
            http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
            http.Timeout = TimeSpan.FromSeconds(60);
            if (!string.IsNullOrWhiteSpace(o.ApiKey)) http.DefaultRequestHeaders.Add("chave-api-dados", o.ApiKey);
        });
        services.AddHttpClient<IMapBiomasCliente, MapBiomasCliente>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<OpcoesAgroCert>>().Value.Ambiental.MapBiomas;
            http.BaseAddress = new Uri(o.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(90);
        });
        services.AddHttpClient<ICopernicusCliente, CopernicusCliente>(http => http.Timeout = TimeSpan.FromSeconds(120));
        services.AddHttpClient("imagens-relatorio", http => http.Timeout = TimeSpan.FromSeconds(20));

        services.AddScoped<CarregadorImagens>();
        services.AddScoped<IRelatorioVistoriaPdf, RelatorioVistoriaPdf>();
        services.AddScoped<IRelatorioAmbientalPdf, RelatorioAmbientalPdf>();
        services.AddSingleton<IExportadorListaMestra, ExportadorListaMestraXlsx>();

        return services;
    }

    public static async Task InicializarBancoAgroCertAsync(this IServiceProvider services, IConfiguration config)
    {
        using var escopo = services.CreateScope();
        var inicializador = escopo.ServiceProvider.GetRequiredService<InicializadorBanco>();
        var migrar = config.GetValue("AgroCert:Banco:AplicarMigracoes", true);
        var dados = config.GetValue("AgroCert:Banco:CarregarDadosIniciais", true);
        await inicializador.ExecutarAsync(migrar, dados);
    }
}
