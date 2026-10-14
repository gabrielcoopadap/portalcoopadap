(function () {
  "use strict";

  function $(sel, ctx) { return (ctx || document).querySelector(sel); }
  function $$(sel, ctx) { return Array.prototype.slice.call((ctx || document).querySelectorAll(sel)); }

  document.addEventListener("click", function (e) {
    var abrir = e.target.closest("[data-abrir-modal]");
    if (abrir) {
      e.preventDefault();
      var dlg = document.getElementById(abrir.getAttribute("data-abrir-modal"));
      if (dlg && typeof dlg.showModal === "function") {
        if (abrir.hasAttribute("data-novo")) prepararNovo(dlg);
        dlg.showModal();
      }
      return;
    }
    if (e.target.closest("[data-fechar-modal]")) {
      e.preventDefault();
      var d = e.target.closest("dialog");
      if (d) d.close();
      return;
    }
    if (e.target.tagName === "DIALOG" && e.target.classList.contains("cm-modal")) {
      var r = e.target.getBoundingClientRect();
      var dentro = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
      if (!dentro) e.target.close();
    }
  });

  $$("dialog[data-abrir-ao-carregar]").forEach(function (d) { if (d.showModal) d.showModal(); });

  document.addEventListener("click", function (e) {
    var el = e.target.closest("[data-link]");
    if (!el || e.target.closest("a, button, form, input, select, label")) return;
    window.location.href = el.getAttribute("data-link");
  });
  document.addEventListener("keydown", function (e) {
    if (e.key !== "Enter") return;
    var el = e.target.closest("[data-link]");
    if (el && e.target === el) window.location.href = el.getAttribute("data-link");
  });

  document.addEventListener("click", function (e) {
    var gatilho = e.target.closest("[data-dropdown]");
    $$(".cm-dropdown.is-aberto").forEach(function (d) {
      if (!gatilho || d !== gatilho.closest(".cm-dropdown")) d.classList.remove("is-aberto");
    });
    if (gatilho) { e.preventDefault(); gatilho.closest(".cm-dropdown").classList.toggle("is-aberto"); }
  });

  $$("[data-alternar-filtros]").forEach(function (b) {
    b.addEventListener("click", function () {
      var card = b.closest(".cm-filtros");
      var fechado = card.classList.toggle("is-fechado");
      b.querySelector("span.rot").textContent = fechado ? "Mostrar filtros" : "Esconder filtros";
      b.querySelector(".material-icons").textContent = fechado ? "expand_more" : "expand_less";
    });
  });

  document.addEventListener("submit", function (e) {
    var form = e.target;
    var msg = form.getAttribute("data-confirmar");
    if (msg && !window.confirm(msg)) { e.preventDefault(); return; }
    renumerarListas(form);
    var botao = form.querySelector("[type=submit][data-carregando]") ||
                (e.submitter && e.submitter.hasAttribute("data-carregando") ? e.submitter : null);
    if (botao) {
      botao.classList.add("is-carregando");
      botao.setAttribute("data-texto-original", botao.innerHTML);
      botao.innerHTML = botao.getAttribute("data-carregando");
    }
  });

  document.addEventListener("click", function (e) {
    var add = e.target.closest("[data-adicionar-linha]");
    if (add) {
      e.preventDefault();
      adicionarLinha(document.getElementById(add.getAttribute("data-adicionar-linha")),
                     document.getElementById(add.getAttribute("data-template")), null);
      return;
    }
    var rem = e.target.closest("[data-remover-linha]");
    if (rem) { e.preventDefault(); rem.closest(".cm-lista-edit__linha").remove(); }
  });

  function adicionarLinha(container, template, valores) {
    if (!container || !template) return null;
    var frag = template.content.cloneNode(true);
    var linha = frag.querySelector(".cm-lista-edit__linha");
    if (valores) {
      $$("[data-campo]", linha).forEach(function (inp) {
        var v = valores[inp.getAttribute("data-campo")];
        if (v !== undefined && v !== null) inp.value = v;
      });
    }
    container.appendChild(frag);
    return linha;
  }

  function renumerarListas(form) {
    $$(".cm-lista-edit", form).forEach(function (lista) {
      $$(".cm-lista-edit__linha", lista).forEach(function (linha, i) {
        $$("[name]", linha).forEach(function (inp) {
          inp.name = inp.name.replace(/\[(__i__|\d+)\]/, "[" + i + "]");
        });
      });
    });
  }

  document.addEventListener("click", function (e) {
    var el = e.target.closest("[data-editar]");
    if (!el || e.target.closest("button, a, form")) return;
    var dlg = document.getElementById(el.getAttribute("data-modal"));
    if (!dlg) return;
    var dados = JSON.parse(el.getAttribute("data-editar"));
    preencher(dlg, dados);
    dlg.showModal();
  });

  function prepararNovo(dlg) {
    var form = $("form", dlg);
    if (!form || !form.hasAttribute("data-acao-nova")) return;
    form.reset();
    form.action = form.getAttribute("data-acao-nova");
    $$("[data-titulo-novo]", dlg).forEach(function (t) { t.textContent = t.getAttribute("data-titulo-novo"); });
    $$("[data-somente-edicao]", dlg).forEach(function (t) { t.hidden = true; });
    $$("input[type=hidden][data-limpar]", dlg).forEach(function (i) { i.value = ""; });
    $$(".cm-lista-edit", dlg).forEach(function (l) { l.innerHTML = ""; });
    $$("[data-padrao]", dlg).forEach(function (i) { i.value = i.getAttribute("data-padrao"); });
  }

  function preencher(dlg, dados) {
    var form = $("form", dlg);
    form.reset();
    if (dados.__acao) form.action = dados.__acao;
    $$("[data-titulo-edicao]", dlg).forEach(function (t) { t.textContent = t.getAttribute("data-titulo-edicao"); });
    $$("[data-somente-edicao]", dlg).forEach(function (t) { t.hidden = false; });
    Object.keys(dados).forEach(function (k) {
      var campo = form.elements.namedItem(k);
      if (campo && campo.tagName && dados[k] !== null && !Array.isArray(dados[k])) campo.value = dados[k];
    });
    $$(".cm-lista-edit", dlg).forEach(function (lista) {
      lista.innerHTML = "";
      var itens = dados[lista.getAttribute("data-lista")] || [];
      var tpl = document.getElementById(lista.getAttribute("data-template"));
      itens.forEach(function (item) { adicionarLinha(lista, tpl, item); });
    });
  }

  $$("[data-mostrar-se-marcado]").forEach(function (chk) {
    var alvo = document.getElementById(chk.getAttribute("data-mostrar-se-marcado"));
    var alvoInverso = document.getElementById(chk.getAttribute("data-esconder-se-marcado") || "");
    function atualizar() {
      if (alvo) alvo.hidden = !chk.checked;
      if (alvoInverso) alvoInverso.hidden = chk.checked;
    }
    chk.addEventListener("change", atualizar);
    atualizar();
  });
})();

(function () {
  "use strict";
  function $$(sel, ctx) { return Array.prototype.slice.call((ctx || document).querySelectorAll(sel)); }

  var tip = document.createElement("div");
  tip.className = "cm-tooltip";
  document.body.appendChild(tip);
  document.addEventListener("mousemove", function (e) {
    var alvo = e.target.closest ? e.target.closest("[data-tip]") : null;
    if (!alvo) { tip.style.display = "none"; return; }
    tip.textContent = alvo.getAttribute("data-tip");
    tip.style.display = "block";
    var x = e.clientX + 14, y = e.clientY + 14;
    var w = tip.offsetWidth, h = tip.offsetHeight;
    if (x + w > window.innerWidth - 8) x = e.clientX - w - 14;
    if (y + h > window.innerHeight - 8) y = e.clientY - h - 14;
    tip.style.left = x + "px";
    tip.style.top = y + "px";
  });

  $$("[data-carrossel]").forEach(function (c) {
    var slides = $$(".ff-slide", c);
    var cont = c.querySelector("[data-contador]");
    var i = 0;
    function mostrar(n) {
      if (!slides.length) return;
      i = (n + slides.length) % slides.length;
      slides.forEach(function (s, k) { s.classList.toggle("is-ativo", k === i); });
      if (cont) cont.textContent = (i + 1) + " / " + slides.length;
    }
    c.addEventListener("click", function (e) {
      if (e.target.closest("[data-anterior]")) mostrar(i - 1);
      if (e.target.closest("[data-proximo]")) mostrar(i + 1);
    });
    mostrar(0);
  });

  $$("[data-abas]").forEach(function (grupo) {
    var escopo = grupo.closest("[data-abas-escopo]") || document;
    grupo.addEventListener("click", function (e) {
      var b = e.target.closest("[data-aba]");
      if (!b) return;
      var alvo = b.getAttribute("data-aba");
      $$("[data-aba]", grupo).forEach(function (x) { x.classList.toggle("is-ativa", x === b); x.setAttribute("aria-selected", x === b ? "true" : "false"); });
      $$("[data-painel]", escopo).forEach(function (p) { p.hidden = p.getAttribute("data-painel") !== alvo; });
    });
  });

  function atualizarCondicionais(form) {
    $$("[data-mostrar-se]", form).forEach(function (el) {
      var par = el.getAttribute("data-mostrar-se").split("=");
      var campo = form.elements.namedItem(par[0]);
      var valor = campo ? (campo.value !== undefined ? campo.value : "") : "";
      if (campo && campo.length && !campo.tagName) {
        valor = campo.value;
      }
      el.hidden = valor !== par[1];
    });
  }
  $$("form").forEach(function (f) {
    if (!f.querySelector("[data-mostrar-se]")) return;
    f.addEventListener("change", function () { atualizarCondicionais(f); });
    atualizarCondicionais(f);
  });
  document.addEventListener("click", function (e) {
    if (e.target.closest("[data-abrir-modal]")) setTimeout(function () { $$("form").forEach(function (f) { if (f.querySelector("[data-mostrar-se]")) atualizarCondicionais(f); }); }, 0);
  });

  document.addEventListener("click", function (e) {
    var b = e.target.closest("[data-minha-localizacao]");
    if (!b || !navigator.geolocation) return;
    e.preventDefault();
    var form = b.closest("form");
    b.classList.add("is-carregando");
    navigator.geolocation.getCurrentPosition(function (pos) {
      form.elements.namedItem("Latitude").value = pos.coords.latitude.toFixed(6);
      form.elements.namedItem("Longitude").value = pos.coords.longitude.toFixed(6);
      b.classList.remove("is-carregando");
    }, function () {
      b.classList.remove("is-carregando");
      alert("Não foi possível obter a localização. Verifique a permissão do navegador.");
    }, { enableHighAccuracy: true, timeout: 15000 });
  });
})();
