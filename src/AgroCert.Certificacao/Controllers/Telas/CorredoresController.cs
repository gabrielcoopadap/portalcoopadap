using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AgroCert.Certificacao.Controllers.Telas;

[Route("certificacao/corredores")]
public partial class CorredoresController : TelaBaseController
{
    private readonly string _pasta;

    public CorredoresController(IConfiguration config, IWebHostEnvironment env)
    {
        var p = config["AgroCert:Corredores:Pasta"];
        if (string.IsNullOrWhiteSpace(p)) p = Path.Combine("..", "..", "dados-corredores", "resultado");
        _pasta = Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(env.ContentRootPath, p));
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var json = Path.Combine(_pasta, "corredores.json");
        var m = new CorredoresViewModel { Pasta = _pasta, TemDados = System.IO.File.Exists(json) };
        if (m.TemDados)
        {
            m.AtualizadoEm = System.IO.File.GetLastWriteTime(json);
            try
            {
                using var doc = JsonDocument.Parse(System.IO.File.ReadAllBytes(json));
                m.Regioes = doc.RootElement.GetProperty("regioes").GetArrayLength();
            }
            catch (Exception)
            {
                m.TemDados = false;
                m.Erro = "O arquivo corredores.json está incompleto ou corrompido. Rode o ProcessarCorredores de novo.";
            }
        }
        return View(m);
    }

    [HttpGet("relatorio/{regiao}/{fazenda:int}")]
    public IActionResult Relatorio(string regiao, int fazenda, string? veg, int? dist)
    {
        if (!RegiaoValida().IsMatch(regiao) || fazenda < 0) return NotFound();
        if (!System.IO.File.Exists(Path.Combine(_pasta, "corredores.json"))) return Redirect("/certificacao/corredores");
        return View(new RelatorioCorredoresViewModel
        {
            Regiao = regiao,
            Fazenda = fazenda,
            Vegetacao = veg is "matas" or "nativa" ? veg : "matas",
            Distancia = dist is > 0 and <= 5000 ? dist.Value : 200,
        });
    }

    [GeneratedRegex(@"^r\d{1,3}$")]
    private static partial Regex RegiaoValida();

    [HttpGet("arquivo/{nome}")]
    [ResponseCache(Duration = 300)]
    public IActionResult Arquivo(string nome)
    {
        if (!NomeValido().IsMatch(nome)) return NotFound();
        var caminho = Path.Combine(_pasta, nome);
        if (!System.IO.File.Exists(caminho)) return NotFound();
        var tipo = Path.GetExtension(nome).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".geojson" => "application/geo+json",
            _ => "application/json",
        };
        return PhysicalFile(caminho, tipo);
    }

    [GeneratedRegex(@"^[a-z0-9_]{1,80}\.(png|geojson|json)$")]
    private static partial Regex NomeValido();
}

public sealed class RelatorioCorredoresViewModel
{
    public string Regiao { get; set; } = "";
    public int Fazenda { get; set; }
    public string Vegetacao { get; set; } = "matas";
    public int Distancia { get; set; } = 200;
}

public sealed class CorredoresViewModel
{
    public string Pasta { get; set; } = "";
    public bool TemDados { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public int Regioes { get; set; }
    public string? Erro { get; set; }
}
