(function () {
  "use strict";
  var fonte = document.getElementById("ff-dados-mapa");
  var el = document.getElementById("ff-mapa");
  if (!fonte || !el || typeof L === "undefined") return;

  var d = JSON.parse(fonte.textContent);
  var mapa = L.map(el, { zoomControl: true, zoomSnap: 0.25 }).setView([-19.2, -46.24], 13);
  L.tileLayer("https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}", {
    maxZoom: 19, attribution: "Tiles &copy; Esri &mdash; Esri, Maxar, Earthstar Geographics, and the GIS User Community"
  }).addTo(mapa);
  L.control.scale({ imperial: false }).addTo(mapa);

  function esc(s) { return String(s == null ? "" : s).replace(/[&<>"']/g, function (c) { return ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]; }); }
  function geo(json, estilo) {
    if (!json) return null;
    try { return L.geoJSON(JSON.parse(json), { style: function () { return estilo; } }); } catch (e) { return null; }
  }

  var areas = { car: L.featureGroup(), rl: L.featureGroup(), app: L.featureGroup() };
  (d.areas || []).forEach(function (a) {
    var c = geo(a.car, { color: "#9be27a", weight: 2.5, fillOpacity: 0.04 });
    var r = geo(a.rl, { color: "#2f7a54", weight: 1.5, fillColor: "#5e9a42", fillOpacity: 0.28 });
    var p = geo(a.app, { color: "#1f6fb2", weight: 1.5, fillColor: "#2a78d6", fillOpacity: 0.28 });
    if (c) { c.bindTooltip(esc(a.nome)); areas.car.addLayer(c); }
    if (r) { r.bindTooltip("Reserva Legal — " + esc(a.nome)); areas.rl.addLayer(r); }
    if (p) { p.bindTooltip("APP — " + esc(a.nome)); areas.app.addLayer(p); }
  });
  Object.keys(areas).forEach(function (k) { areas[k].addTo(mapa); });

  var reinoAtivo = "FAUNA";
  var desligados = {};
  var diasPeriodo = 90;
  var modo = "ultimo";
  var hoje = Date.now();
  var marcadores = [];

  (d.pontos || []).forEach(function (p) {
    var icone = L.divIcon({
      className: "ff-pin-wrap",
      html: '<span class="ff-pin" style="background:' + p.cor + '"><span class="material-icons">' + p.icone + '</span></span>',
      iconSize: [30, 30], iconAnchor: [15, 15], popupAnchor: [0, -14]
    });
    var foto = p.foto
      ? '<img src="' + esc(p.foto) + '" alt="">'
      : '<span class="ff-foto" style="--cor:' + p.cor + '"><span class="material-icons">' + p.icone + '</span></span>';
    var m = L.marker([p.lat, p.lon], { icon: icone, title: p.nome, keyboard: true })
      .bindPopup('<div class="ff-popup">' + foto + '<div><b>' + esc(p.nome) + '</b><span><i>' + esc(p.cientifico) + '</i></span>' +
        '<span>Registro: ' + esc(p.data) + '</span><span>Área: ' + esc(p.area || "-") + '</span><span>' + esc(p.origem) + '</span></div></div>');
    marcadores.push({ m: m, p: p });
  });

  var camadaPontos = L.featureGroup().addTo(mapa);
  function atualizar() {
    camadaPontos.clearLayers();
    var limite = diasPeriodo > 0 ? hoje - diasPeriodo * 86400000 : 0;
    var contagem = {};
    var vistos = {};
    marcadores.forEach(function (x) {
      var p = x.p;
      if (p.reino !== reinoAtivo || p.ts < limite) return;
      if (modo === "ultimo") {
        if (vistos[p.especieId]) return;
        vistos[p.especieId] = true;
      }
      contagem[p.grupo] = (contagem[p.grupo] || 0) + 1;
      if (desligados[p.reino + "|" + p.grupo]) return;
      camadaPontos.addLayer(x.m);
    });
    document.querySelectorAll("[data-grupo-mapa]").forEach(function (sw) {
      var partes = sw.getAttribute("data-grupo-mapa").split("|");
      sw.closest("label").hidden = partes[0] !== reinoAtivo;
      var c = sw.closest("label").querySelector(".ff-cont");
      if (c) c.textContent = contagem[partes[1]] || 0;
    });
    var todos = document.querySelector("[data-todos-mapa]");
    if (todos) {
      var grupos = Array.prototype.slice.call(document.querySelectorAll('[data-grupo-mapa^="' + reinoAtivo + '|"]'));
      todos.checked = grupos.every(function (g) { return g.checked; });
    }
  }

  document.querySelectorAll("[data-reino-mapa]").forEach(function (b) {
    b.addEventListener("click", function () {
      reinoAtivo = b.getAttribute("data-reino-mapa");
      document.querySelectorAll("[data-reino-mapa]").forEach(function (x) {
        x.classList.toggle("is-ativa", x === b);
        x.setAttribute("aria-pressed", x === b ? "true" : "false");
      });
      atualizar();
    });
  });
  document.querySelectorAll("[data-grupo-mapa]").forEach(function (sw) {
    sw.addEventListener("change", function () { desligados[sw.getAttribute("data-grupo-mapa")] = !sw.checked; atualizar(); });
  });
  var todos = document.querySelector("[data-todos-mapa]");
  if (todos) todos.addEventListener("change", function () {
    document.querySelectorAll('[data-grupo-mapa^="' + reinoAtivo + '|"]').forEach(function (sw) {
      sw.checked = todos.checked;
      desligados[sw.getAttribute("data-grupo-mapa")] = !todos.checked;
    });
    atualizar();
  });
  document.querySelectorAll("[data-area-mapa]").forEach(function (chk) {
    chk.addEventListener("change", function () {
      var c = areas[chk.getAttribute("data-area-mapa")];
      if (chk.checked) c.addTo(mapa); else mapa.removeLayer(c);
    });
  });
  var seletorModo = document.querySelector("[data-modo-mapa]");
  if (seletorModo) seletorModo.addEventListener("change", function () { modo = seletorModo.value; atualizar(); });
  var periodo = document.querySelector("[data-periodo-mapa]");
  if (periodo) periodo.addEventListener("change", function () { diasPeriodo = parseInt(periodo.value, 10); atualizar(); });

  atualizar();

  var limites = areas.car.getLayers().length ? areas.car.getBounds() : camadaPontos.getBounds();
  if (limites.isValid()) mapa.fitBounds(limites, { padding: [20, 20] });

  mapa.on("click", function (e) {
    var lat = e.latlng.lat.toFixed(6), lon = e.latlng.lng.toFixed(6);
    L.popup().setLatLng(e.latlng)
      .setContent('<div class="ff-popup" style="min-width:0"><div><b>Registrar ocorrência aqui?</b><span>' + lat + ", " + lon + '</span>' +
        '<button type="button" class="cm-btn cm-btn--primario cm-btn--pequeno" style="margin-top:6px" data-registrar-aqui="' + lat + "," + lon + '">Novo registro</button></div></div>')
      .openOn(mapa);
  });
  document.addEventListener("click", function (e) {
    var b = e.target.closest("[data-registrar-aqui]");
    if (!b) return;
    var coords = b.getAttribute("data-registrar-aqui").split(",");
    var dlg = document.getElementById("modal-registro");
    if (!dlg) return;
    var form = dlg.querySelector("form");
    form.elements.namedItem("Latitude").value = coords[0];
    form.elements.namedItem("Longitude").value = coords[1];
    mapa.closePopup();
    dlg.showModal();
  });
})();
