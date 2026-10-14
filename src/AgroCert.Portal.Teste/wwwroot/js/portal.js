(function () {
  "use strict";
  var telaPequena = window.matchMedia("(max-width: 959px)");

  document.addEventListener("click", function (e) {
    if (e.target.closest("[data-alternar-menu]")) {
      document.body.classList.toggle(telaPequena.matches ? "menu-aberto" : "menu-recolhido");
      return;
    }
    var botao = e.target.closest("[data-alternar-submenu]");
    if (botao) {
      var grupo = botao.closest("[data-menu-grupo]");
      var aberto = grupo.classList.toggle("is-aberto");
      botao.setAttribute("aria-expanded", aberto ? "true" : "false");
    }
  });
})();
