(function () {
  "use strict";
  var app = document.getElementById("co-app");
  if (!app || typeof L === "undefined") return;
  var BASE = app.getAttribute("data-base");

  var fmt0 = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 0 });
  var fmt1 = new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 1 });
  function ha(v) { return fmt0.format(v || 0) + " ha"; }
  function num(v) { return fmt0.format(v || 0); }
  function pct(a, b) { return b > 0 ? fmt0.format(100 * a / b) + "%" : "—"; }
  function esc(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]; }); }
  function icone(n) { return '<span class="material-icons" aria-hidden="true">' + n + "</span>"; }
  function $(id) { return document.getElementById(id); }

  var dados = null;
  var estado = { regiao: 0, ano: null, veg: "matas", dist: 200, aba: "fazendas", busca: "", ordem: null };
  var mapa, overlay, overlayLei, camadaRl, camadaFazendas, camadaLigacoes, camadaLei, destaque;
  var contornosCache = {};
  var pixels = null;

  fetch(BASE + "corredores.json", { cache: "no-cache" })
    .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
    .then(function (d) { dados = d; iniciar(); })
    .catch(function () {
      app.innerHTML = '<div class="cm-alerta cm-alerta--erro">' + icone("error_outline") + "<div>Não foi possível carregar o estudo (corredores.json).</div></div>";
    });

  function iniciar() {
    estado.ano = String(dados.anos[dados.anos.length - 1]);
    if (dados.distancias.indexOf(estado.dist) < 0) estado.dist = dados.distancias[0];

    if (dados.regioes.length > 1) {
      $("co-ctl-regiao").hidden = false;
      $("co-regiao").innerHTML = dados.regioes.map(function (r, i) {
        return '<option value="' + i + '">' + esc(r.nome) + " (" + r.fazendas.length + (r.fazendas.length === 1 ? " fazenda)" : " fazendas)") + "</option>";
      }).join("");
      $("co-regiao").addEventListener("change", function () { estado.regiao = +this.value; trocarRegiao(); });
    }

    var anos = dados.anos.map(function (a) { return { v: String(a), t: String(a) }; });
    if (dados.anos.length >= 2) anos.push({ v: "comparar", t: "Comparar " + dados.anos[0] + " → " + dados.anos[dados.anos.length - 1] });
    pilulas("co-ano", "ano", anos);
    pilulas("co-veg", "veg", dados.vegetacoes.map(function (v) { return { v: v.id, t: v.nome, title: v.descricao }; }));
    pilulas("co-dist", "dist", dados.distancias.map(function (d) { return { v: d, t: d + " m" }; }));

    document.querySelectorAll("[data-aba]").forEach(function (b) {
      b.addEventListener("click", function () { estado.aba = b.getAttribute("data-aba"); abas(); });
    });
    $("co-busca").addEventListener("input", function () { estado.busca = this.value; tabelaFazendas(); });
    $("co-csv").addEventListener("click", baixarCsv);

    criarMapa();
    avisos();
    metodo();
    trocarRegiao();
  }

  function pilulas(id, chave, opcoes) {
    var el = $(id);
    el.innerHTML = opcoes.map(function (o, i) {
      var idi = id + "-" + i;
      return '<input type="radio" name="' + id + '" id="' + idi + '" value="' + o.v + '"' + (String(estado[chave]) === String(o.v) ? " checked" : "") + ' />' +
        '<label for="' + idi + '"' + (o.title ? ' title="' + esc(o.title) + '"' : "") + ">" + esc(o.t) + "</label>";
    }).join("");
    el.addEventListener("change", function (e) {
      var v = e.target.value;
      estado[chave] = chave === "dist" ? +v : v;
      atualizar();
    });
  }

  function regiao() { return dados.regioes[estado.regiao]; }
  function comparando() { return estado.ano === "comparar"; }
  function anoFinal() { return String(dados.anos[dados.anos.length - 1]); }
  function anoInicial() { return String(dados.anos[0]); }
  function cenario(ano) { return regiao().cenarios[(ano || (comparando() ? anoFinal() : estado.ano)) + "|" + estado.veg + "|" + estado.dist]; }
  function nomeVeg() { return estado.veg === "matas" ? "mata" : "vegetação nativa"; }

  function criarMapa() {
    mapa = L.map("co-mapa", { zoomControl: true, preferCanvas: true });
    L.tileLayer("https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}", {
      maxZoom: 18, attribution: "Satélite &copy; Esri | Vegetação: MapBiomas | Imóveis: SICAR"
    }).addTo(mapa);
    L.tileLayer("https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}", { maxZoom: 18 }).addTo(mapa);
    mapa.createPane("co-imagem").style.zIndex = 350;
    mapa.createPane("co-lei").style.zIndex = 360;
    camadaLei = L.layerGroup().addTo(mapa);
    camadaRl = L.layerGroup().addTo(mapa);
    camadaLigacoes = L.layerGroup().addTo(mapa);
    camadaFazendas = L.layerGroup().addTo(mapa);
    mapa.on("click", identificar);

    document.querySelectorAll("[data-camada]").forEach(function (chk) {
      chk.addEventListener("change", function () {
        var c = { rl: camadaRl, fazendas: camadaFazendas, ligacoes: camadaLigacoes, lei: camadaLei }[chk.getAttribute("data-camada")];
        if (chk.checked) c.addTo(mapa); else mapa.removeLayer(c);
      });
    });
  }

  function limitesRegiao() {
    var b = regiao().limites;
    return L.latLngBounds([b[1], b[0]], [b[3], b[2]]);
  }

  function trocarRegiao() {
    estado.ordem = null;
    camadaLei.clearLayers();
    var lei = regiao().lei;
    if (lei) L.imageOverlay(BASE + lei.imagem, limitesRegiao(), { pane: "co-lei", className: "co-overlay", opacity: 0.95 }).addTo(camadaLei);
    mapa.fitBounds(limitesRegiao());
    carregarContornos();
    atualizar();
  }

  function carregarContornos() {
    var r = regiao();
    camadaRl.clearLayers();
    var desenhar = function (gj) {
      if (r !== regiao()) return;
      camadaRl.clearLayers();
      L.geoJSON(gj, {
        style: function (f) {
          return f.properties.tipo === "rl"
            ? { color: "#ffe45c", weight: 1.6, fill: false, dashArray: "4 3" }
            : { color: "#ffffff", weight: 1.4, fill: false };
        },
        interactive: false
      }).addTo(camadaRl);
    };
    if (contornosCache[r.id]) return desenhar(contornosCache[r.id]);
    fetch(BASE + r.contornos).then(function (x) { return x.json(); }).then(function (gj) { contornosCache[r.id] = gj; desenhar(gj); }).catch(function () { });
  }

  function atualizar() {
    var r = regiao();
    var url = comparando() ? r.mudancas[estado.veg].imagem : cenario().imagem;
    var limites = limitesRegiao();
    if (!overlay) overlay = L.imageOverlay(BASE + url, limites, { opacity: 0.88, pane: "co-imagem", className: "co-overlay" }).addTo(mapa);
    else { overlay.setUrl(BASE + url); overlay.setBounds(limites); }
    carregarPixels(BASE + url);
    if (destaque) { mapa.removeLayer(destaque); destaque = null; }

    kpis();
    legenda();
    fazendasNoMapa();
    ligacoesNoMapa();
    tabelaFazendas();
    tabelaLigacoes();
    tabelaCorredores();
    abas();
  }

  function carregarPixels(url) {
    pixels = null;
    var img = new Image();
    img.onload = function () {
      var c = document.createElement("canvas");
      c.width = img.naturalWidth; c.height = img.naturalHeight;
      var ctx = c.getContext("2d", { willReadFrequently: true });
      ctx.drawImage(img, 0, 0);
      pixels = { ctx: ctx, w: c.width, h: c.height, url: url };
    };
    img.src = url;
  }

  function merc(lat) { return Math.log(Math.tan(Math.PI / 4 + lat * Math.PI / 360)); }

  function corEm(latlng) {
    if (!pixels) return null;
    var b = regiao().limites;
    var x = Math.floor((latlng.lng - b[0]) / (b[2] - b[0]) * pixels.w);
    var y = Math.floor((merc(b[3]) - merc(latlng.lat)) / (merc(b[3]) - merc(b[1])) * pixels.h);
    if (x < 0 || y < 0 || x >= pixels.w || y >= pixels.h) return null;
    // olha o pixel e os vizinhos (fica mais fácil acertar uma mata estreita)
    var melhor = null;
    for (var dy = -2; dy <= 2; dy++) for (var dx = -2; dx <= 2; dx++) {
      var xx = x + dx, yy = y + dy;
      if (xx < 0 || yy < 0 || xx >= pixels.w || yy >= pixels.h) continue;
      var p = pixels.ctx.getImageData(xx, yy, 1, 1).data;
      if (p[3] < 20) continue;
      var d = dx * dx + dy * dy + (p[3] > 180 ? 0 : 3); // prefere a mata à faixa de travessia
      if (!melhor || d < melhor.d) melhor = { d: d, r: p[0], g: p[1], b: p[2], a: p[3] };
    }
    return melhor;
  }

  function hexRgb(h) { return [parseInt(h.substr(1, 2), 16), parseInt(h.substr(3, 2), 16), parseInt(h.substr(5, 2), 16)]; }
  function distCor(p, h) { var c = hexRgb(h); return Math.abs(p.r - c[0]) + Math.abs(p.g - c[1]) + Math.abs(p.b - c[2]); }

  function identificar(e) {
    var p = corEm(e.latlng);
    if (!p) return;
    var html;
    if (comparando()) {
      var tipos = [
        { cor: dados.paleta.perda, t: "<b>" + cap(nomeVeg()) + " perdida</b> entre " + anoInicial() + " e " + anoFinal() },
        { cor: dados.paleta.ganho, t: "<b>" + cap(nomeVeg()) + " que voltou</b> (regenerou) entre " + anoInicial() + " e " + anoFinal() },
        { cor: dados.paleta.estavel, t: cap(nomeVeg()) + " que continua de pé desde " + anoInicial() }
      ];
      tipos.sort(function (a, b) { return distCor(p, a.cor) - distCor(p, b.cor); });
      html = tipos[0].t;
    } else {
      var c = cenario();
      var cand = [{ cor: dados.paleta.isolado, tipo: "iso" }];
      c.corredores.forEach(function (k) { if (k.cor !== dados.paleta.outros) cand.push({ cor: k.cor, tipo: "c", k: k }); });
      cand.push({ cor: dados.paleta.outros, tipo: "outros" });
      var m = null;
      cand.forEach(function (x) { var d = distCor(p, x.cor); if (!m || d < m.d) m = { d: d, x: x }; });
      if (!m || m.d > 40) return;
      var trav = p.a < 180 ? '<div class="co-pop-sub">Faixa de travessia: área aberta estreita (até ' + estado.dist + " m) que os animais atravessam entre as matas.</div>" : "";
      if (m.x.tipo === "iso") html = "<b>Fragmento isolado</b><div class='co-pop-sub'>Mata pequena (menos de " + num(dados.parametros.corredorMinHa) + " ha no conjunto) sem ligação com um corredor.</div>";
      else if (m.x.tipo === "outros") html = "<b>Corredor pequeno</b><div class='co-pop-sub'>Conjunto de matas ligadas com 50 a algumas centenas de hectares.</div>" + trav;
      else {
        var k = m.x.k;
        html = '<b><i class="co-cor" style="background:' + k.cor + '"></i> Corredor ' + k.numero + "</b>" +
          '<div class="co-pop-grande">' + ha(k.ha) + " de " + nomeVeg() + " interligada</div>" +
          '<div class="co-pop-sub">' + classe(k.ha) + " · " + (k.fazendas.length ? k.fazendas.length + " fazenda(s) de cooperados" : "nenhuma fazenda de cooperado") + "</div>" + trav;
        realcar(k);
      }
    }
    L.popup().setLatLng(e.latlng).setContent(html).openOn(mapa);
  }

  function realcar(k) {
    if (destaque) mapa.removeLayer(destaque);
    destaque = null;
    if (!k || !k.caixa) return;
    destaque = L.rectangle([[k.caixa[1], k.caixa[0]], [k.caixa[3], k.caixa[2]]], { color: "#fff", weight: 2, dashArray: "6 4", fill: false, interactive: false }).addTo(mapa);
  }

  var TIPO_LEI = { "app": "APP a recompor", "rl": "Reserva Legal sem vegetação", "app-rl": "APP a recompor e Reserva Legal sem vegetação" };
  function ehLegal(l) { return l.leiPct >= 50; }
  function textoLei(l) {
    if (!l.leiTipo || l.leiPct <= 0) return "";
    return l.leiPct + "% do trecho já é " + TIPO_LEI[l.leiTipo] + " — o dono já tem obrigação de restaurar";
  }
  function pilulaLei(l) {
    if (!l.leiTipo || l.leiPct <= 0) return '<span class="cm-t-sub">não</span>';
    return '<span class="co-sit ' + (ehLegal(l) ? "co-sit--lei" : "co-sit--lei-parcial") + '">' + icone("gavel") + " " + l.leiPct + "% " +
      (l.leiTipo === "app" ? "APP" : l.leiTipo === "rl" ? "RL" : "APP + RL") + "</span>";
  }
  function urlRelatorio(f) {
    return "/certificacao/corredores/relatorio/" + regiao().id + "/" + f.id + "?veg=" + estado.veg + "&dist=" + estado.dist;
  }

  function cap(s) { return s.charAt(0).toUpperCase() + s.slice(1); }
  function classe(v) { return v >= 5000 ? "Corredor grande" : v >= 500 ? "Corredor médio" : "Corredor pequeno"; }

  var SITUACAO = {
    "conectada": { t: "Ligada a corredor", c: "co-sit--ok", cor: "#2e9d57" },
    "isolada": { t: "Isolada", c: "co-sit--iso", cor: "#eb6834" },
    "sem-vegetacao": { t: "RL sem mata", c: "co-sit--sem", cor: "#c0392b" },
    "sem-rl": { t: "Sem RL no CAR", c: "co-sit--nada", cor: "#9e9e9e" }
  };
  function situacao(s) {
    var x = SITUACAO[s] || SITUACAO["sem-rl"];
    var t = s === "sem-vegetacao" && estado.veg !== "matas" ? "RL sem vegetação" : x.t;
    return '<span class="co-sit ' + x.c + '">' + t + "</span>";
  }

  function fazendasNoMapa() {
    camadaFazendas.clearLayers();
    var r = regiao(), c = cenario(), lig = ligacoesPorFazenda(c);
    r.fazendas.forEach(function (f) {
      var s = c.fazendas[f.id];
      var m = L.circleMarker([f.lat, f.lon], {
        radius: 8, color: "#fff", weight: 2, fillColor: (SITUACAO[s.situacaoRl] || SITUACAO["sem-rl"]).cor, fillOpacity: 1
      });
      m.bindPopup(function () { return popupFazenda(f); }, { maxWidth: 320 });
      m.bindTooltip(esc(f.nome), { direction: "top", offset: [0, -8] });
      m.addTo(camadaFazendas);
      f._marcador = m;
      f._ligacoes = lig[f.id] || [];
    });
  }

  function popupFazenda(f) {
    var c = cenario(), s = c.fazendas[f.id];
    var h = '<div class="co-pop-titulo">' + esc(f.nome) + "</div>" +
      '<div class="co-pop-sub">' + esc(f.municipio) + " · " + ha(f.areaHa) + "</div>" +
      '<table class="co-pop-tab">' +
      "<tr><td>" + cap(nomeVeg()) + " na fazenda</td><td>" + ha(s.nativaHa) + "</td></tr>" +
      "<tr><td>Dentro de corredor</td><td>" + pct(s.emCorredorHa, s.nativaHa) + "</td></tr>" +
      "<tr><td>Maior corredor que toca</td><td>" + (s.corredor ? "nº " + s.corredor + " · " + ha(s.corredorHa) : "nenhum") + "</td></tr>" +
      "<tr><td>Reserva Legal</td><td>" + situacao(s.situacaoRl) + "</td></tr>";
    if (comparando()) {
      var mu = regiao().mudancas[estado.veg].fazendas[f.id];
      h += "<tr><td>Perdida desde " + anoInicial() + "</td><td>" + ha(mu.perdaHa) + "</td></tr>" +
        "<tr><td>Regenerou</td><td>" + ha(mu.ganhoHa) + "</td></tr>";
    }
    h += "</table>";
    var aRecompor = f.appRecomporHa + f.rlSemVegetacaoHa;
    if (aRecompor >= 0.5) h += '<div class="co-pop-lig co-pop-lig--lei">' + icone("gavel") + " A lei já pede recompor " + ha(aRecompor) + " (APP " + ha(f.appRecomporHa) + " · RL sem vegetação " + ha(f.rlSemVegetacaoHa) + ")</div>";
    if (f._ligacoes && f._ligacoes.length)
      h += '<div class="co-pop-lig">' + icone("link") + " Um trecho a restaurar passa aqui (ponto de ligação nº " + f._ligacoes.join(", ") + ")</div>";
    h += '<a class="co-pop-rel" href="' + urlRelatorio(f) + '" target="_blank" rel="noopener">' + icone("picture_as_pdf") + " Relatório da fazenda (PDF)</a>";
    h += '<div class="co-pop-car">CAR ' + esc(f.car) + "</div>";
    return h;
  }

  function ligacoesPorFazenda(c) {
    var m = {};
    c.ligacoes.forEach(function (l, i) { l.fazendas.forEach(function (f) { (m[f] = m[f] || []).push(i + 1); }); });
    return m;
  }

  function ligacoesNoMapa() {
    camadaLigacoes.clearLayers();
    var c = cenario();
    c.ligacoes.forEach(function (l, i) {
      var pts = l.linha.map(function (p) { return [p[1], p[0]]; });
      var fundo = L.polyline(pts, { color: "#000", weight: 8, opacity: 0.35 });
      var linha = L.polyline(pts, { color: ehLegal(l) ? "#00d5ff" : "#ffd400", weight: 4, dashArray: "2 6", lineCap: "round" });
      var meio = L.latLng((pts[0][0] + pts[1][0]) / 2, (pts[0][1] + pts[1][1]) / 2);
      var rot = L.marker(meio, { icon: L.divIcon({ className: "co-lig-num" + (ehLegal(l) ? " co-lig-num--lei" : ""), html: String(i + 1), iconSize: [24, 24] }) });
      [fundo, linha, rot].forEach(function (x) { x.bindPopup(function () { return popupLigacao(l, i); }); x.addTo(camadaLigacoes); });
      l._meio = meio; l._camada = rot;
    });
  }

  function popupLigacao(l, i) {
    var r = regiao();
    var nomes = l.fazendas.map(function (f) { return esc(r.fazendas[f].nome); });
    return '<div class="co-pop-titulo">Ponto de ligação nº ' + (i + 1) + "</div>" +
      '<div class="co-pop-grande">Falha de ' + num(l.distanciaM) + " m sem " + nomeVeg() + "</div>" +
      '<div class="co-pop-sub">Liga o corredor ' + l.de + " (" + ha(l.deHa) + ") ao corredor " + l.para + " (" + ha(l.paraHa) + "). Restaurando, vira um corredor de " + ha(l.juntosHa) + ".</div>" +
      (textoLei(l) ? '<div class="co-pop-lig co-pop-lig--lei">' + icone("gavel") + " " + textoLei(l) + "</div>" : "") +
      (nomes.length ? '<div class="co-pop-lig">' + icone("agriculture") + " " + nomes.join(", ") + "</div>" : "") +
      (l.outrosImoveis ? '<div class="co-pop-sub">+ ' + l.outrosImoveis + " imóvel(is) de não cooperados no trecho</div>" : "") +
      (!nomes.length && !l.outrosImoveis ? '<div class="co-pop-sub">Trecho fora de imóveis do CAR (estrada, área pública ou sem cadastro).</div>' : "");
  }

  function stat(cor, valor, rotulo, sub) {
    return '<div class="cm-card cm-stat" style="--cor:' + cor + '"><div class="cm-stat__valor">' + valor + '</div><div class="cm-stat__rotulo">' + rotulo + "</div>" +
      (sub ? '<div class="co-stat-sub">' + sub + "</div>" : "") + "</div>";
  }

  function rlConectadas(c) {
    var com = 0, total = 0;
    c.fazendas.forEach(function (s) { if (s.situacaoRl !== "sem-rl") { total++; if (s.situacaoRl === "conectada") com++; } });
    return { com: com, total: total };
  }

  function kpis() {
    var el = $("co-kpis");
    if (!comparando()) {
      var c = cenario();
      var maior = c.corredores.length ? c.corredores[0].ha : 0;
      var rl = rlConectadas(c);
      el.innerHTML =
        stat("#2e9d57", num(c.corredores.length), "Corredores de " + nomeVeg(),
          c.classes.grandes + " grandes · " + c.classes.medios + " médios · " + c.classes.pequenos + " pequenos") +
        stat("#1f6fb2", ha(maior), "Maior corredor", pct(maior, c.nativaHa) + " de toda a " + nomeVeg() + " da região") +
        stat("#eb6834", num(c.isolados.quantidade), "Fragmentos isolados", ha(c.isolados.ha) + " de " + nomeVeg() + " sem ligação") +
        stat("#5e9a42", pct(c.emCorredorHa, c.nativaHa), "da " + nomeVeg() + " está em corredor", ha(c.emCorredorHa) + " de " + ha(c.nativaHa)) +
        stat("#8e44ad", rl.com + " de " + rl.total, "Reservas Legais ligadas", "fazendas de cooperados com RL em corredor") +
        stat("#0097b2", c.ligacoes.filter(ehLegal).length + " de " + c.ligacoes.length, "Ligações que a lei já exige", "trechos que já são APP a recompor ou RL sem vegetação");
    } else {
      var a = cenario(anoInicial()), b = cenario(anoFinal()), mu = regiao().mudancas[estado.veg];
      var ma = a.corredores.length ? a.corredores[0].ha : 0, mb = b.corredores.length ? b.corredores[0].ha : 0;
      var ra = rlConectadas(a), rb = rlConectadas(b);
      var dn = b.nativaHa - a.nativaHa;
      el.innerHTML =
        stat(dn < 0 ? "#e34948" : "#2a78d6", (dn < 0 ? "−" : "+") + ha(Math.abs(dn)), cap(nomeVeg()) + " na região", ha(a.nativaHa) + " em " + anoInicial() + " → " + ha(b.nativaHa) + " em " + anoFinal()) +
        stat("#e34948", ha(mu.perdaHa), "Perdida (vermelho)", "desmatada ou convertida desde " + anoInicial()) +
        stat("#2a78d6", ha(mu.ganhoHa), "Voltou (azul)", "regeneração ou plantio desde " + anoInicial()) +
        stat("#1f6fb2", seta(ma, mb), "Maior corredor", ha(ma) + " → " + ha(mb)) +
        stat("#eb6834", num(a.isolados.quantidade) + " → " + num(b.isolados.quantidade), "Fragmentos isolados", num(a.corredores.length) + " → " + num(b.corredores.length) + " corredores") +
        stat("#8e44ad", ra.com + " → " + rb.com, "Reservas Legais ligadas", "de " + rb.total + " fazendas com RL");
    }
  }

  function seta(a, b) {
    if (a <= 0) return ha(b);
    var d = (b - a) / a * 100;
    var s = Math.abs(d) < 0.5 ? "igual" : (d > 0 ? "+" : "−") + fmt0.format(Math.abs(d)) + "%";
    return s;
  }

  function legenda() {
    var el = $("co-legenda");
    if (comparando()) {
      el.innerHTML =
        item(dados.paleta.perda, cap(nomeVeg()) + " perdida desde " + anoInicial()) +
        item(dados.paleta.ganho, "Voltou (regenerou)") +
        item(dados.paleta.estavel, "Continua de pé", true) + legendaLei();
      return;
    }
    var c = cenario();
    var tops = c.corredores.slice(0, 4).map(function (k) { return item(k.cor, "Corredor " + k.numero + " · " + ha(k.ha)); }).join("");
    el.innerHTML = '<span class="co-leg-titulo">Cada cor é um corredor (matas ligadas):</span>' + tops +
      (c.corredores.length > 4 ? '<span class="co-leg-mais">+ ' + (c.corredores.length - 4) + " corredores</span>" : "") +
      item(dados.paleta.isolado, "Fragmento isolado") +
      '<span class="co-leg"><i class="co-leg-linha"></i>Ponto de ligação (restaurar)</span>' +
      '<span class="co-leg"><i class="co-leg-rl"></i>Reserva Legal</span>' +
      '<span class="co-leg"><i class="co-leg-limite"></i>Limite da fazenda</span>' + legendaLei();
  }
  function legendaLei() {
    if (!regiao().lei) return "";
    return '<span class="co-leg"><i class="co-hach co-hach--app"></i>APP a recompor (CAR)</span>' +
      '<span class="co-leg"><i class="co-hach co-hach--rl"></i>Reserva Legal sem vegetação</span>';
  }
  function item(cor, t, claro) { return '<span class="co-leg"><i class="co-cor" style="background:' + cor + (claro ? ";opacity:.5" : "") + '"></i>' + esc(t) + "</span>"; }

  function abas() {
    document.querySelectorAll("[data-aba]").forEach(function (b) {
      var ativo = b.getAttribute("data-aba") === estado.aba;
      b.classList.toggle("is-ativa", ativo);
      b.setAttribute("aria-selected", ativo ? "true" : "false");
    });
    document.querySelectorAll("[data-painel]").forEach(function (p) { p.hidden = p.getAttribute("data-painel") !== estado.aba; });
  }

  function linhasFazendas() {
    var r = regiao(), c = cenario();
    var a = comparando() ? cenario(anoInicial()) : null;
    var mu = comparando() ? r.mudancas[estado.veg] : null;
    var busca = estado.busca.trim().toLowerCase();
    return r.fazendas.filter(function (f) {
      return !busca || (f.nome + " " + f.car + " " + f.municipio).toLowerCase().indexOf(busca) >= 0;
    }).map(function (f) {
      return { f: f, s: c.fazendas[f.id], a: a ? a.fazendas[f.id] : null, m: mu ? mu.fazendas[f.id] : null };
    });
  }

  function colunasFazendas() {
    var nome = { t: "Fazenda", k: function (x) { return x.f.nome.toLowerCase(); }, h: function (x) {
      return '<div class="cm-t-titulo">' + esc(x.f.nome) + '</div><div class="cm-t-sub">' + esc(x.f.municipio) + " · " + ha(x.f.areaHa) + "</div>";
    } };
    if (comparando()) {
      return [nome,
        { t: cap(nomeVeg()) + " " + anoInicial(), n: 1, k: function (x) { return x.a.nativaHa; }, h: function (x) { return ha(x.a.nativaHa); } },
        { t: cap(nomeVeg()) + " " + anoFinal(), n: 1, k: function (x) { return x.s.nativaHa; }, h: function (x) { return ha(x.s.nativaHa); } },
        { t: "Perdida", n: 1, k: function (x) { return x.m.perdaHa; }, h: function (x) { return x.m.perdaHa >= 0.5 ? '<b class="co-perda">' + ha(x.m.perdaHa) + "</b>" : "—"; } },
        { t: "Voltou", n: 1, k: function (x) { return x.m.ganhoHa; }, h: function (x) { return x.m.ganhoHa >= 0.5 ? '<b class="co-ganho">' + ha(x.m.ganhoHa) + "</b>" : "—"; } },
        { t: "Em corredor", n: 1, k: function (x) { return x.s.nativaHa ? x.s.emCorredorHa / x.s.nativaHa : 0; }, h: function (x) { return pct(x.a.emCorredorHa, x.a.nativaHa) + " → " + pct(x.s.emCorredorHa, x.s.nativaHa); } },
        { t: "Reserva Legal " + anoInicial(), k: function (x) { return x.a.situacaoRl; }, h: function (x) { return situacao(x.a.situacaoRl); } },
        { t: "Reserva Legal " + anoFinal(), k: function (x) { return x.s.situacaoRl; }, h: function (x) { return situacao(x.s.situacaoRl); } }
      ];
    }
    return [nome,
      { t: cap(nomeVeg()), n: 1, k: function (x) { return x.s.nativaHa; }, h: function (x) { return ha(x.s.nativaHa) + '<div class="cm-t-sub">' + pct(x.s.nativaHa, x.f.areaHa) + " da fazenda</div>"; } },
      { t: "Em corredor", n: 1, k: function (x) { return x.s.nativaHa ? x.s.emCorredorHa / x.s.nativaHa : -1; }, h: function (x) {
        var p = x.s.nativaHa > 0 ? x.s.emCorredorHa / x.s.nativaHa : 0;
        return x.s.nativaHa > 0 ? '<div class="co-barra"><i style="width:' + Math.round(p * 100) + '%"></i></div>' + pct(x.s.emCorredorHa, x.s.nativaHa) : "—";
      } },
      { t: "Maior corredor que toca", n: 1, k: function (x) { return x.s.corredorHa; }, h: function (x) {
        if (!x.s.corredor) return '<span class="cm-t-sub">nenhum</span>';
        var k = cenario().corredores[x.s.corredor - 1];
        return '<i class="co-cor" style="background:' + k.cor + '"></i> nº ' + x.s.corredor + '<div class="cm-t-sub">' + ha(x.s.corredorHa) + "</div>";
      } },
      { t: "Reserva Legal", k: function (x) { return x.s.situacaoRl; }, h: function (x) {
        return situacao(x.s.situacaoRl) + (x.f.rlHa > 0 ? '<div class="cm-t-sub">' + ha(x.f.rlHa) + " declarados · " + pct(x.s.rlNativaHa, x.f.rlHa) + " com " + nomeVeg() + "</div>" : "");
      } },
      { t: "A lei pede recompor", n: 1, k: function (x) { return x.f.appRecomporHa + x.f.rlSemVegetacaoHa; }, h: function (x) {
        var t = x.f.appRecomporHa + x.f.rlSemVegetacaoHa;
        return t < 0.5 ? "—" : "<b>" + ha(t) + '</b><div class="cm-t-sub">APP ' + ha(x.f.appRecomporHa) + " · RL " + ha(x.f.rlSemVegetacaoHa) + "</div>";
      } },
      { t: "Onde restaurar", k: function (x) { return ordemRestaurar(x.s); }, h: function (x) {
        var l = x.s.ligacoes || [];
        var na = l.filter(function (y) { return y.naFazenda; }), legal = l.some(ehLegal);
        if (!l.length) return '<span class="cm-t-sub">nenhuma ligação perto</span>';
        return '<span class="co-sit ' + (na.length ? "co-sit--lig" : "co-sit--nada") + '">' + icone("link") + " " +
          (na.length ? na.length + " na fazenda" : l.length + " a até 1,5 km") + "</span>" +
          (legal ? '<div class="cm-t-sub co-lei-txt">' + icone("gavel") + " já é obrigação legal</div>" : "");
      } },
      { t: "", h: function (x) {
        return '<a class="co-rel-link" href="' + urlRelatorio(x.f) + '" target="_blank" rel="noopener" title="Relatório da fazenda (PDF)" data-parar>' + icone("picture_as_pdf") + "</a>";
      } }
    ];
  }

  function ordemRestaurar(s) {
    var l = s.ligacoes || [];
    return (l.some(function (y) { return y.naFazenda; }) ? 2 : l.length ? 1 : 0) + (l.some(ehLegal) ? 0.5 : 0);
  }

  function tabelaFazendas() {
    var cols = colunasFazendas(), linhas = linhasFazendas();
    var o = estado.ordem;
    if (o && cols[o.i]) {
      var k = cols[o.i].k || function () { return 0; };
      linhas.sort(function (a, b) { var x = k(a), y = k(b); return (x < y ? -1 : x > y ? 1 : 0) * o.dir; });
    } else linhas.sort(function (a, b) { return b.s.nativaHa - a.s.nativaHa; });
    var t = $("co-tab-fazendas");
    t.innerHTML = "<thead><tr>" + cols.map(function (c, i) {
      var seta = o && o.i === i ? (o.dir > 0 ? " ▲" : " ▼") : "";
      if (!c.k) return "<th></th>";
      return '<th class="co-ordenavel' + (c.n ? " co-num" : "") + '" data-col="' + i + '" tabindex="0">' + esc(c.t) + seta + "</th>";
    }).join("") + "</tr></thead><tbody>" +
      (linhas.length ? linhas.map(function (x) {
        return '<tr class="is-clicavel" data-faz="' + x.f.id + '">' + cols.map(function (c) { return "<td" + (c.n ? ' class="co-num"' : "") + ">" + c.h(x) + "</td>"; }).join("") + "</tr>";
      }).join("") : '<tr><td class="cm-tabela__vazio" colspan="' + cols.length + '">Nenhuma fazenda encontrada.</td></tr>') + "</tbody>";
    t.querySelectorAll("th[data-col]").forEach(function (th) {
      var ordenar = function () {
        var i = +th.getAttribute("data-col");
        estado.ordem = estado.ordem && estado.ordem.i === i ? { i: i, dir: -estado.ordem.dir } : { i: i, dir: i === 0 ? 1 : -1 };
        tabelaFazendas();
      };
      th.addEventListener("click", ordenar);
      th.addEventListener("keydown", function (e) { if (e.key === "Enter") ordenar(); });
    });
    t.querySelectorAll("tr[data-faz]").forEach(function (tr) {
      tr.addEventListener("click", function (e) {
        if (e.target.closest("[data-parar]")) return;
        var f = regiao().fazendas[+tr.getAttribute("data-faz")];
        mapa.setView([f.lat, f.lon], 14);
        if (f._marcador) f._marcador.openPopup();
        $("co-mapa").scrollIntoView({ behavior: "smooth", block: "center" });
      });
    });
  }

  function tabelaLigacoes() {
    var r = regiao(), c = cenario(), t = $("co-tab-ligacoes");
    var cor = function (n) { var k = c.corredores[n - 1]; return k ? '<i class="co-cor" style="background:' + k.cor + '"></i>' : ""; };
    t.innerHTML = '<thead><tr><th>Nº</th><th>Liga os corredores</th><th class="co-num">Falha a restaurar</th><th class="co-num">Corredor unido</th><th>Já é obrigação legal?</th><th>Fazendas no trecho</th></tr></thead><tbody>' +
      (c.ligacoes.length ? c.ligacoes.map(function (l, i) {
        var nomes = l.fazendas.map(function (f) { return esc(r.fazendas[f].nome); });
        return '<tr class="is-clicavel" data-lig="' + i + '"><td><span class="co-lig-num co-lig-num--tab' + (ehLegal(l) ? " co-lig-num--lei" : "") + '">' + (i + 1) + "</span></td>" +
          "<td>" + cor(l.de) + " nº " + l.de + " (" + ha(l.deHa) + ")<br />" + cor(l.para) + " nº " + l.para + " (" + ha(l.paraHa) + ")</td>" +
          '<td class="co-num"><b>' + num(l.distanciaM) + " m</b></td>" +
          '<td class="co-num">' + ha(l.juntosHa) + "</td>" +
          "<td>" + pilulaLei(l) + "</td>" +
          "<td>" + (nomes.length ? '<div class="cm-t-titulo">' + nomes.join("<br />") + "</div>" : "") +
          (l.outrosImoveis ? '<div class="cm-t-sub">' + l.outrosImoveis + " imóvel(is) de não cooperados</div>" : "") +
          (!nomes.length && !l.outrosImoveis ? '<span class="cm-t-sub">fora de imóveis do CAR</span>' : "") + "</td></tr>";
      }).join("") : '<tr><td class="cm-tabela__vazio" colspan="6">Nenhuma falha de até ' + num(dados.parametros.ligacaoMaxM) + " m entre corredores nesta combinação.</td></tr>") + "</tbody>";
    t.querySelectorAll("tr[data-lig]").forEach(function (tr) {
      tr.addEventListener("click", function () {
        var l = c.ligacoes[+tr.getAttribute("data-lig")];
        mapa.setView(l._meio, 15);
        l._camada.openPopup();
        $("co-mapa").scrollIntoView({ behavior: "smooth", block: "center" });
      });
    });
  }

  function tabelaCorredores() {
    var r = regiao(), c = cenario(), t = $("co-tab-corredores");
    var lista = c.corredores.slice(0, 60);
    t.innerHTML = '<thead><tr><th>Corredor</th><th class="co-num">Tamanho</th><th>Tipo</th><th class="co-num">% da ' + nomeVeg() + '</th><th>Fazendas de cooperados</th></tr></thead><tbody>' +
      lista.map(function (k) {
        var nomes = k.fazendas.slice(0, 4).map(function (f) { return esc(r.fazendas[f].nome); });
        return '<tr class="is-clicavel" data-cor="' + k.numero + '"><td><i class="co-cor co-cor--g" style="background:' + k.cor + '"></i> <b>nº ' + k.numero + "</b></td>" +
          '<td class="co-num"><b>' + ha(k.ha) + "</b></td><td>" + classe(k.ha) + '</td><td class="co-num">' + pct(k.ha, c.nativaHa) + "</td>" +
          "<td>" + (k.fazendas.length ? k.fazendas.length + ": " + nomes.join(", ") + (k.fazendas.length > 4 ? "…" : "") : '<span class="cm-t-sub">nenhuma</span>') + "</td></tr>";
      }).join("") +
      (c.corredores.length > lista.length ? '<tr><td colspan="5" class="cm-t-sub">+ ' + (c.corredores.length - lista.length) + " corredores menores</td></tr>" : "") +
      '<tr><td><i class="co-cor co-cor--g" style="background:' + dados.paleta.isolado + '"></i> <b>Fragmentos isolados</b></td><td class="co-num">' + ha(c.isolados.ha) + "</td><td>" + num(c.isolados.quantidade) + ' matas pequenas soltas</td><td class="co-num">' + pct(c.isolados.ha, c.nativaHa) + "</td><td></td></tr></tbody>";
    t.querySelectorAll("tr[data-cor]").forEach(function (tr) {
      tr.addEventListener("click", function () {
        var k = c.corredores[+tr.getAttribute("data-cor") - 1];
        if (!k.caixa) return;
        mapa.fitBounds([[k.caixa[1], k.caixa[0]], [k.caixa[3], k.caixa[2]]], { padding: [20, 20] });
        realcar(k);
        $("co-mapa").scrollIntoView({ behavior: "smooth", block: "center" });
      });
    });
  }

  function avisos() {
    var el = $("co-avisos");
    var lista = (dados.avisos || []);
    if (!lista.length) return;
    el.innerHTML = '<div class="cm-alerta cm-alerta--info">' + icone("info") + "<div><b>Observações sobre os dados</b><ul>" +
      lista.map(function (a) { return "<li>" + esc(a) + "</li>"; }).join("") + "</ul></div></div>";
  }

  function metodo() {
    var p = dados.parametros;
    $("co-metodo").innerHTML =
      "<h3>O que é um corredor ecológico aqui</h3>" +
      "<p>É um conjunto de matas que estão <b>encostadas ou muito perto umas das outras</b>, de modo que os animais conseguem passar de uma para outra. " +
      "A distância escolhida no topo da tela (" + dados.distancias.join(", ") + " m) é o maior trecho aberto — pasto, lavoura, estrada — que se considera que eles atravessam.</p>" +
      "<ul><li><b>Corredor</b>: conjunto ligado com pelo menos " + num(p.corredorMinHa) + " ha de vegetação. Cada corredor tem uma cor no mapa.</li>" +
      "<li><b>Fragmento isolado</b> (laranja): mata pequena, sem ligação com nenhum corredor.</li>" +
      "<li><b>Ponto de ligação</b> (linha amarela): o menor trecho sem mata entre dois corredores, até " + num(p.ligacaoMaxM) + " m. Restaurar esse trecho une os dois. Estão em ordem de ganho (mais mata unida por metro restaurado).</li>" +
      "<li><b>O que a lei já exige</b> (hachurado): <b>APP a recompor</b> é a APP que o próprio CAR marca como sem vegetação; " +
      "<b>Reserva Legal sem vegetação</b> é a parte da RL declarada que o MapBiomas não mostra com vegetação nativa em " + (regiao().lei ? regiao().lei.ano : "") + ". " +
      "Pontos de ligação que passam por essas áreas sobem na prioridade (até o dobro), porque o produtor restauraria de qualquer jeito — e ainda une dois corredores.</li>" +
      "<li><b>Reserva Legal ligada</b>: pelo menos metade da vegetação da RL da fazenda está dentro de um corredor. " +
      "“Isolada” quer dizer que a RL tem vegetação, mas solta. “RL sem mata” quer dizer que menos de 20% da RL declarada tem vegetação no mapa.</li></ul>" +
      "<h3>De onde vêm os dados</h3>" +
      "<ul><li><b>Vegetação</b>: MapBiomas (mapa de cobertura e uso da terra, pixels de 30 m), anos " + dados.anos.join(" e ") + ". " +
      "“Só matas” usa a classe Formação Florestal; “Toda vegetação nativa” soma também cerrado (savana), campo nativo e áreas úmidas.</li>" +
      "<li><b>Fazendas e Reservas Legais</b>: CAR (SICAR), cruzado com a planilha de cooperados — " + dados.cooperados.comMapa + " de " + dados.cooperados.naPlanilha + " CAR encontrados nos mapas baixados.</li>" +
      "<li>O estudo cobre as fazendas dos cooperados mais " + num(p.bufferRegiaoM / 1000) + " km em volta, porque os corredores atravessam as divisas.</li></ul>" +
      "<h3>Cuidados na leitura</h3>" +
      "<ul><li>O mapa de 30 m não enxerga matas ciliares muito estreitas (menos de ~30 m de largura) — alguns corredores reais podem aparecer quebrados.</li>" +
      "<li>Os números são estimativas para planejamento; para laudo ou certificação, confirme em campo.</li></ul>" +
      '<p class="cm-ajuda">Estudo gerado em ' + new Date(dados.geradoEm).toLocaleString("pt-BR") + ".</p>";
  }

  function baixarCsv() {
    var cols = colunasFazendas().filter(function (c) { return c.t; });
    var txt = function (h) { var d = document.createElement("div"); d.innerHTML = h.replace(/<br\s*\/?>/g, " / ").replace(/<\/div>/g, " — "); return d.textContent.replace(/\s+/g, " ").replace(/ — $/, "").trim(); };
    var linhas = linhasFazendas();
    var csv = ["CAR;" + cols.map(function (c) { return c.t; }).join(";")].concat(linhas.map(function (x) {
      return x.f.car + ";" + cols.map(function (c) { return '"' + txt(c.h(x)).replace(/"/g, '""') + '"'; }).join(";");
    })).join("\r\n");
    var blob = new Blob(["﻿" + csv], { type: "text/csv;charset=utf-8" });
    var a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = "corredores_" + regiao().id + "_" + estado.ano + "_" + estado.veg + "_" + estado.dist + "m.csv";
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
  }
})();
