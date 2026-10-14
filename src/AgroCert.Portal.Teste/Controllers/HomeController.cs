using Microsoft.AspNetCore.Mvc;

namespace AgroCert.Portal.Teste.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    [Route("portal/{item}")]
    public IActionResult Item(string item)
    {
        ViewData["Item"] = item;
        return View();
    }

    public IActionResult Erro() => View();
}
