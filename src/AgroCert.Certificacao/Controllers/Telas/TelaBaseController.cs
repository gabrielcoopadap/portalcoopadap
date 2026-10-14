using AgroCert.Certificacao.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgroCert.Certificacao.Controllers.Telas;

[Area("Certificacao")]
[Authorize(Policy = ModuloCertificacao.Politica)]
[AutoValidateAntiforgeryToken]
[TratarErros]
public abstract class TelaBaseController : Controller
{
    protected void Sucesso(string mensagem) => TempData[TratarErrosAttribute.ChaveSucesso] = mensagem;
    protected void Erro(string mensagem) => TempData[TratarErrosAttribute.ChaveErro] = mensagem;
    protected string Voltar(string padrao) => TratarErrosAttribute.Voltar(HttpContext, padrao);
}
