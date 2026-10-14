using AgroCert.Application.Servicos;
using Microsoft.AspNetCore.Mvc;

namespace AgroCert.Certificacao.Controllers.Telas;

[Route("certificacao")]
public class PainelController : TelaBaseController
{
    private readonly PainelServico _painel;
    public PainelController(PainelServico painel) => _painel = painel;

    [HttpGet("")]
    [HttpGet("gestao")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _painel.MontarAsync(ct));
}
