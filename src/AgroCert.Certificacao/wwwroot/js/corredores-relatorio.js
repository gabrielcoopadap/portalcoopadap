(function () {
  "use strict";
  var raiz = document.getElementById("rl-relatorio");
  if (!raiz || typeof L === "undefined") return;
  var BASE = raiz.getAttribute("data-base");
  var P = {
    regiao: raiz.getAttribute("data-regiao"),
    fazenda: +raiz.getAttribute("data-fazenda"),
    veg: raiz.getAttribute("data-veg"),
    dist: +raiz.getAttribute("data-dist"),
    logo: raiz.getAttribute("data-logo")
  };
  var dica = document.getElementById("rl-dica");
  var botao = document.getElementById("rl-imprimir");

  var fmt0 = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 0 });
  var fmt1 = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 });
  function ha(v) { return (v > 0 && v < 10 ? fmt1 : fmt0).format(v || 0) + " ha"; }
  function num(v) { return fmt0.format(v || 0); }
  function pct(a, b) { return b > 0 ? fmt0.format(100 * a / b) + "%" : "—"; }
  function esc(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }
  function icone(n) { return '<span class="material-icons" aria-hidden="true">' + n + "</span>"; }
  var LETRAS = "ABCDEFGH";
  var TIPO_LEI = { "app": "APP a recompor", "rl": "Reserva Legal sem vegetação", "app-rl": "APP a recompor e Reserva Legal sem vegetação" };

  Promise.all([
    fetch(BASE + "corredores.json", { cache: "no-cache" }).then(function (r) { if (!r.ok) throw new Error(); return r.json(); })
  ]).then(function (x) {
    var d = x[0];
    var r = d.regioes.filter(function (y) { return y.id === P.regiao; })[0];
    var f = r && r.fazendas[P.fazenda];
    if (!f) throw new Error("fazenda");
    if (d.distancias.indexOf(P.dist) < 0) P.dist = d.distancias[0];
    return fetch(BASE + r.contornos).then(function (y) { return y.json(); }).catch(function () { return null; })
      .then(function (gj) { montar(d, r, f, gj); });
  }).catch(function () {
    dica.textContent = "Não foi possível montar o relatório desta fazenda. Volte ao mapa e tente de novo.";
  });

  function montar(d, r, f, gj) {
    var anoA = String(d.anos[0]), anoB = String(d.anos[d.anos.length - 1]);
    var chave = function (ano) { return ano + "|" + P.veg + "|" + P.dist; };
    var cB = r.cenarios[chave(anoB)], cA = r.cenarios[chave(anoA)];
    var s = cB.fazendas[f.id], sA = cA.fazendas[f.id];
    var mu = r.mudancas && r.mudancas[P.veg] ? r.mudancas[P.veg].fazendas[f.id] : null;
    var nomeVeg = P.veg === "matas" ? "mata" : "vegetação nativa";
    var vegInfo = d.vegetacoes.filter(function (v) { return v.id === P.veg; })[0] || { nome: P.veg };
    var ligs = s.ligacoes || [];
    var lei = f.appRecomporHa + f.rlSemVegetacaoHa;
    var maior = s.corredor ? cB.corredores[s.corredor - 1] : null;
    var corredoresFaz = cB.corredores.filter(function (k) { return k.fazendas.indexOf(f.id) >= 0; });

    document.getElementById("rl-opcoes").innerHTML =
      '<label>Vegetação <select id="rl-veg">' + d.vegetacoes.map(function (v) { return '<option value="' + v.id + '"' + (v.id === P.veg ? " selected" : "") + ">" + esc(v.nome) + "</option>"; }).join("") + "</select></label>" +
      '<label>Distância <select id="rl-dist">' + d.distancias.map(function (x) { return '<option value="' + x + '"' + (x === P.dist ? " selected" : "") + ">" + x + " m</option>"; }).join("") + "</select></label>";
    ["rl-veg", "rl-dist"].forEach(function (id) {
      document.getElementById(id).addEventListener("change", function () {
        location.search = "?veg=" + document.getElementById("rl-veg").value + "&dist=" + document.getElementById("rl-dist").value;
      });
    });

    var frases = [];
    frases.push("A fazenda tem <b>" + ha(s.nativaHa) + "</b> de " + nomeVeg + " (" + pct(s.nativaHa, f.areaHa) + " da área).");
    if (s.emCorredorHa > 0)
      frases.push("<b>" + pct(s.emCorredorHa, s.nativaHa) + "</b> dessa " + nomeVeg + " faz parte de corredores ecológicos" +
        (maior ? " — o maior que passa pela fazenda tem <b>" + ha(maior.ha) + "</b>." : "."));
    else if (s.nativaHa > 0) frases.push("Nenhuma " + nomeVeg + " da fazenda está ligada a um corredor: são fragmentos isolados.");
    frases.push({
      "conectada": "A <b>Reserva Legal está ligada</b> a um corredor.",
      "isolada": "A Reserva Legal tem vegetação, mas está <b>isolada</b> (fora de corredor).",
      "sem-vegetacao": "A Reserva Legal tem <b>pouca " + nomeVeg + "</b> no mapa (menos de 20% da área declarada).",
      "sem-rl": "Não há Reserva Legal declarada no CAR desta fazenda."
    }[s.situacaoRl] || "");
    if (lei >= 0.5) frases.push("Pelo CAR, a lei já pede recompor <b>" + ha(lei) + "</b> nesta fazenda.");
    var na = ligs.filter(function (l) { return l.naFazenda; });
    if (na.length) frases.push("Há <b>" + na.length + " trecho" + (na.length > 1 ? "s" : "") + " dentro da fazenda</b> que, restaurado" + (na.length > 1 ? "s" : "") + ", ligaria" + (na.length > 1 ? "m" : "") + " corredores" +
      (na.some(function (l) { return l.leiPct >= 50; }) ? " — e já é obrigação legal." : "."));
    else if (ligs.length) frases.push("Há " + ligs.length + " trecho" + (ligs.length > 1 ? "s" : "") + " a restaurar a até 1,5 km da fazenda, em áreas vizinhas.");
    if (mu) frases.push("Desde " + anoA + ", a fazenda perdeu <b>" + ha(mu.perdaHa) + "</b> e recuperou <b>" + ha(mu.ganhoHa) + "</b> de " + nomeVeg + ".");

    var hoje = new Date().toLocaleDateString("pt-BR");
    var sit = {
      "conectada": ["Ligada a corredor", "ok"], "isolada": ["Isolada", "iso"],
      "sem-vegetacao": [P.veg === "matas" ? "RL sem mata" : "RL sem vegetação", "sem"], "sem-rl": ["Sem RL no CAR", "nada"]
    }[s.situacaoRl] || ["—", "nada"];

    var h = '<section class="rl-pagina">' +
      cabecalho(hoje) +
      '<div class="rl-fazenda"><div><div class="rl-fazenda__nome">' + esc(f.nome) + '</div>' +
      '<div class="rl-fazenda__dados">' + esc(f.municipio) + "/MG · " + ha(f.areaHa) + " · CAR " + esc(f.car) + "</div></div>" +
      '<div class="rl-param">' + esc(vegInfo.nome) + " · travessia de até " + P.dist + " m · MapBiomas " + anoB + "</div></div>" +
      '<div class="rl-resumo"><div class="rl-resumo__tit">' + icone("eco") + " Em resumo</div><ul>" + frases.filter(Boolean).map(function (x) { return "<li>" + x + "</li>"; }).join("") + "</ul></div>" +
      '<div class="rl-numeros">' +
      numero(ha(s.nativaHa), cap(nomeVeg) + " na fazenda", pct(s.nativaHa, f.areaHa) + " da área") +
      numero(pct(s.emCorredorHa, s.nativaHa), "Em corredor", ha(s.emCorredorHa)) +
      numero(maior ? ha(maior.ha) : "—", "Maior corredor que toca", maior ? "corredor nº " + maior.numero : "nenhum") +
      numero('<span class="rl-sit rl-sit--' + sit[1] + '">' + sit[0] + "</span>", "Reserva Legal", f.rlHa > 0 ? ha(f.rlHa) + " declarados" : "") +
      numero(lei >= 0.5 ? ha(lei) : "—", "A lei pede recompor", "APP + RL sem vegetação") +
      "</div>" +
      '<div class="rl-mapa" id="rl-mapa1"></div>' +
      '<div class="rl-legenda">' +
      leg("cor", "#2e9d57", "Corredores (cada cor é um)") + leg("cor", d.paleta.isolado, "Fragmento isolado") +
      leg("hach-app", "", "APP a recompor") + leg("hach-rl", "", "RL sem vegetação") +
      leg("limite", "", "Limite da fazenda") + leg("rl", "", "Reserva Legal") + leg("lig", "", "Trecho a restaurar (A, B, C)") +
      "</div>" +
      "</section>";

    h += '<section class="rl-pagina">' +
      '<h2 class="rl-h2">' + icone("gavel") + " O que a lei já pede nesta fazenda</h2>" +
      '<table class="rl-tab"><tbody>' +
      linha("APP a recompor (declarada no CAR)", ha(f.appRecomporHa)) +
      linha("Reserva Legal declarada", f.rlHa > 0 ? ha(f.rlHa) : "não declarada") +
      linha("Reserva Legal com vegetação nativa (MapBiomas " + anoB + ")", f.rlHa > 0 ? ha(f.rlHa - f.rlSemVegetacaoHa) + " (" + pct(f.rlHa - f.rlSemVegetacaoHa, f.rlHa) + ")" : "—") +
      linha("Reserva Legal sem vegetação nativa", f.rlHa > 0 ? ha(f.rlSemVegetacaoHa) : "—") +
      '<tr class="rl-tab__total"><td>Total que a lei já pede recompor</td><td>' + ha(lei) + "</td></tr>" +
      "</tbody></table>" +
      '<p class="rl-nota">A RL sem vegetação é estimada pelo mapa de satélite (30 m) e pode incluir áreas em regeneração ainda não vistas. Confirme em campo.</p>' +

      '<h2 class="rl-h2">' + icone("link") + " Onde restaurar para ligar corredores</h2>" +
      (ligs.length ? '<table class="rl-tab rl-tab--lig"><thead><tr><th></th><th>Onde</th><th>Falha sem ' + nomeVeg + '</th><th>Liga os corredores</th><th>Resultado</th><th>Já é obrigação legal?</th></tr></thead><tbody>' +
        ligs.map(function (l, i) {
          var areaRest = l.distanciaM * 30 / 10000;
          return "<tr><td><span class='rl-letra" + (l.leiPct >= 50 ? " rl-letra--lei" : "") + "'>" + LETRAS[i] + "</span></td>" +
            "<td>" + (l.naFazenda ? "<b>Dentro da fazenda</b>" : "Vizinhança (até 1,5 km)") + "</td>" +
            "<td><b>" + num(l.distanciaM) + " m</b><div class='rl-sub'>≈ " + fmt1.format(areaRest) + " ha numa faixa de 30 m · ~" + num(areaRest * 1667) + " mudas</div></td>" +
            "<td>nº " + l.de + " (" + ha(l.deHa) + ")<br />nº " + l.para + " (" + ha(l.paraHa) + ")</td>" +
            "<td>um corredor de <b>" + ha(l.juntosHa) + "</b></td>" +
            "<td>" + (l.leiTipo && l.leiPct > 0 ? "<b>" + l.leiPct + "%</b> do trecho é " + TIPO_LEI[l.leiTipo] : "não") + "</td></tr>";
        }).join("") + "</tbody></table>" +
        '<p class="rl-nota">Mudas estimadas no espaçamento 3 × 2 m (1.667 por hectare). A largura e o tipo de restauração (plantio, enriquecimento ou só cercar e deixar regenerar) devem ser definidos em campo.</p>'
        : '<p class="rl-texto">Nenhum trecho curto (até ' + num(d.parametros.ligacaoMaxM) + " m) entre corredores passa pela fazenda ou fica a menos de 1,5 km dela" +
          (s.emCorredorHa > 0 ? ". A " + nomeVeg + " da fazenda já está ligada à rede de corredores." : " nesta combinação de vegetação e distância.") + "</p>") +

      '<h2 class="rl-h2">' + icone("history") + " Como a " + nomeVeg + " mudou desde " + anoA + "</h2>" +
      '<div class="rl-mudanca"><div class="rl-mapa rl-mapa--p" id="rl-mapa2"></div><div>' +
      '<table class="rl-tab"><tbody>' +
      linha(cap(nomeVeg) + " em " + anoA, ha(sA.nativaHa)) +
      linha(cap(nomeVeg) + " em " + anoB, ha(s.nativaHa)) +
      (mu ? linha('<i class="rl-q" style="background:' + d.paleta.perda + '"></i>Perdida', ha(mu.perdaHa)) +
        linha('<i class="rl-q" style="background:' + d.paleta.ganho + '"></i>Voltou (regenerou)', ha(mu.ganhoHa)) +
        linha("Perdida dentro da Reserva Legal", ha(mu.rlPerdaHa)) : "") +
      linha("Em corredor", pct(sA.emCorredorHa, sA.nativaHa) + " → " + pct(s.emCorredorHa, s.nativaHa)) +
      linha("Reserva Legal", rotSit(sA.situacaoRl) + " → " + rotSit(s.situacaoRl)) +
      "</tbody></table></div></div>" +

      '<h2 class="rl-h2">' + icone("menu_book") + " Como foi calculado</h2>" +
      '<p class="rl-texto rl-texto--p">Corredor ecológico é um conjunto de ' + (P.veg === "matas" ? "matas" : "áreas de vegetação nativa") + " encostadas ou separadas por até " + P.dist + " m de área aberta, que os animais conseguem atravessar, com pelo menos " +
      num(d.parametros.corredorMinHa) + " ha no total. Vegetação: MapBiomas (" + d.anos.join(" e ") + ", pixels de 30 m). Limites, Reserva Legal e APP: CAR/SICAR. " +
      "Trechos a restaurar são as menores falhas (até " + num(d.parametros.ligacaoMaxM) + " m) entre dois corredores; ganham prioridade quando já são APP a recompor ou Reserva Legal sem vegetação. " +
      "Os números são estimativas para planejamento e não substituem vistoria de campo.</p>" +
      '<div class="rl-assinaturas"><div>Responsável técnico</div><div>Produtor</div></div>' +
      '<div class="rl-rodape">Estudo gerado em ' + new Date(d.geradoEm).toLocaleDateString("pt-BR") + " · relatório emitido em " + hoje + " · CAR " + esc(f.car) + "</div>" +
      "</section>";

    raiz.innerHTML = h;
    document.title = "Corredores Ecológicos — " + f.nome;

    var limites = r.limites, bounds = L.latLngBounds([limites[1], limites[0]], [limites[3], limites[2]]);
    var caixa = f.caixa ? L.latLngBounds([f.caixa[1], f.caixa[0]], [f.caixa[3], f.caixa[2]]) : L.latLng(f.lat, f.lon).toBounds(1500);
    var foco = L.latLngBounds(caixa.getSouthWest(), caixa.getNorthEast());
    ligs.forEach(function (l) { l.linha.forEach(function (p) { foco.extend([p[1], p[0]]); }); });

    var pendentes = 0;
    function esperar(camada) {
      pendentes++;
      camada.once("load", function () { pendentes--; pronto(); });
    }
    function pronto() {
      if (pendentes > 0) return;
      botao.disabled = false;
      dica.innerHTML = icone("info") + " Clique em <b>Gerar PDF</b> e, na janela de impressão, escolha <b>Salvar como PDF</b> (papel A4, retrato).";
    }

    function mapa(id, img, comLei, comLigacoes) {
      var m = L.map(id, { zoomControl: false, attributionControl: true, dragging: false, scrollWheelZoom: false, doubleClickZoom: false, boxZoom: false, keyboard: false, touchZoom: false, zoomSnap: 0.25 });
      m.attributionControl.setPrefix(false);
      var sat = L.tileLayer("https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}", { maxZoom: 18, attribution: "Satélite © Esri · MapBiomas · SICAR" }).addTo(m);
      esperar(sat);
      m.fitBounds(id === "rl-mapa1" ? foco : caixa, { padding: [18, 18] });
      var ov = L.imageOverlay(BASE + img, bounds, { opacity: 0.85, className: "rl-pixel" }).addTo(m);
      esperar(ov);
      if (comLei && r.lei) { var lv = L.imageOverlay(BASE + r.lei.imagem, bounds, { className: "rl-pixel" }).addTo(m); esperar(lv); }
      if (gj) {
        L.geoJSON(gj, {
          filter: function (ft) { return ft.properties.fazenda === f.id; },
          style: function (ft) { return ft.properties.tipo === "rl" ? { color: "#ffe45c", weight: 2, fill: false, dashArray: "5 4" } : { color: "#fff", weight: 2.5, fill: false }; }
        }).addTo(m);
      }
      if (comLigacoes) ligs.forEach(function (l, i) {
        var pts = l.linha.map(function (p) { return [p[1], p[0]]; });
        L.polyline(pts, { color: "#000", weight: 8, opacity: 0.35 }).addTo(m);
        L.polyline(pts, { color: l.leiPct >= 50 ? "#00d5ff" : "#ffd400", weight: 4.5, dashArray: "2 6", lineCap: "round" }).addTo(m);
        L.marker([(pts[0][0] + pts[1][0]) / 2, (pts[0][1] + pts[1][1]) / 2], {
          icon: L.divIcon({ className: "rl-letra rl-letra--mapa" + (l.leiPct >= 50 ? " rl-letra--lei" : ""), html: LETRAS[i], iconSize: [24, 24] })
        }).addTo(m);
      });
      return m;
    }
    mapa("rl-mapa1", cB.imagem, true, true);
    if (r.mudancas && r.mudancas[P.veg]) mapa("rl-mapa2", r.mudancas[P.veg].imagem, false, false);
    // se o satélite não carregar (sem internet), libera mesmo assim
    setTimeout(function () { pendentes = 0; pronto(); }, 12000);

    botao.addEventListener("click", function () { window.print(); });

    function rotSit(x) { return { "conectada": "ligada", "isolada": "isolada", "sem-vegetacao": "sem vegetação", "sem-rl": "sem RL" }[x] || "—"; }
  }

  function cabecalho(hoje) {
    return '<header class="rl-cab">' +
      (P.logo ? '<img class="rl-logo" src="' + esc(P.logo) + '" alt="Coopadap" />' : '<div class="rl-logo-txt">' + icone("spa") + " coopadap</div>") +
      '<div class="rl-cab__tit"><div class="rl-cab__h1">Relatório de Corredores Ecológicos</div><div class="rl-cab__sub">Certificação · emitido em ' + hoje + "</div></div></header>";
  }
  function numero(v, t, sub) { return '<div class="rl-num"><div class="rl-num__v">' + v + '</div><div class="rl-num__t">' + t + "</div>" + (sub ? '<div class="rl-num__s">' + sub + "</div>" : "") + "</div>"; }
  function linha(a, b) { return "<tr><td>" + a + "</td><td>" + b + "</td></tr>"; }
  function leg(tipo, cor, t) { return '<span class="rl-leg"><i class="rl-leg-' + tipo + '"' + (cor ? ' style="background:' + cor + '"' : "") + "></i>" + t + "</span>"; }
  function cap(s) { return s.charAt(0).toUpperCase() + s.slice(1); }
})();
