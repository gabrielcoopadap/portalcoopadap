using System.Globalization;
using AgroCert.Certificacao.Clima;
using AgroCert.Certificacao.Infra;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Localization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// "Telas": login do Entra ID com cookie (Razor). "Api": o Angular envia o token Bearer.
var autenticacaoHabilitada = builder.Configuration.GetValue("Autenticacao:Habilitada", false);
var modoApi = string.Equals(builder.Configuration["Autenticacao:Modo"], "Api", StringComparison.OrdinalIgnoreCase);
var mvc = builder.Services.AddControllersWithViews();
if (autenticacaoHabilitada && modoApi)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}
else if (autenticacaoHabilitada)
{
    builder.Services
        .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));
    mvc.AddMicrosoftIdentityUI();
}

builder.Services.AddModuloCertificacao(builder.Configuration);
builder.Services.AddModuloClima(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AgroCert API", Version = "v1" });
    c.DocInclusionPredicate((_, api) => api.RelativePath?.StartsWith("api/", StringComparison.OrdinalIgnoreCase) == true);
    c.CustomSchemaIds(t => t.FullName?.Replace("+", "."));
});

var app = builder.Build();

var ptBr = new CultureInfo("pt-BR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(ptBr),
    SupportedCultures = new[] { ptBr },
    SupportedUICultures = new[] { ptBr },
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AgroCert API v1"));
}
else
{
    app.UseExceptionHandler("/Home/Erro");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseModuloCertificacao();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
if (autenticacaoHabilitada && !modoApi) app.MapRazorPages();

await app.InicializarModuloCertificacaoAsync();

app.Run();

public partial class Program { }
