(function () {
  "use strict";
  var fonte = document.getElementById("cm-dados-mapa");
  var elMapa = document.getElementById("cm-mapa");
  if (!fonte || !elMapa || typeof L === "undefined") return;

  var d = JSON.parse(fonte.textContent);
  var ESTILO_CAR = { color: "#5e9a42", weight: 2.5, fillOpacity: 0.05 };
  var ESTILO_RL = { color: "#2f7a54", weight: 1, fillOpacity: 0.18 };
  var ESTILO_APP = { color: "#2f6fb2", weight: 1, fillOpacity: 0.18 };
  var ESTILO_ALERTA = { color: "#c0392b", weight: 2, fillColor: "#e57373", fillOpacity: 0.5 };

  var mapa = L.map(elMapa, { zoomControl: true }).setView([-19.0, -46.5], 12);
  L.tileLayer("https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}", {
    maxZoom: 19, attribution: "Tiles &copy; Esri &mdash; Esri, Maxar, Earthstar Geographics, and the GIS User Community"
  }).addTo(mapa);
  L.tileLayer("https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}", {
    maxZoom: 19
  }).addTo(mapa);

  function geo(json, estilo) {
    if (!json) return null;
    try { return L.geoJSON(JSON.parse(json), { style: function () { return estilo; } }); } catch (e) { return null; }
  }

  var camadas = {
    limiteCar: geo(d.car, ESTILO_CAR),
    reservaLegal: geo(d.reservaLegal, ESTILO_RL),
    app: geo(d.app, ESTILO_APP),
    alertasDesmatamento: L.featureGroup(),
    ndviMapaCalor: null
  };

  (d.alertas || []).forEach(function (a) {
    var g = geo(a.geoJson, ESTILO_ALERTA);
    if (!g) return;
    g.bindPopup("Alerta " + (a.codigo || "") + " — " + (a.area || "-") + " ha (" + (a.data || "-") + ")");
    g.on("click", function () { mostrarAlerta(a); });
    camadas.alertasDesmatamento.addLayer(g);
  });

  if (d.ndvi && d.ndvi.url && d.ndvi.bbox) {
    var b = d.ndvi.bbox;
    camadas.ndviMapaCalor = L.imageOverlay(d.ndvi.url, [[b[1], b[0]], [b[3], b[2]]], { opacity: 0.85 });
  }

  Object.keys(camadas).forEach(function (k) { if (camadas[k]) camadas[k].addTo(mapa); });

  if (camadas.limiteCar) mapa.fitBounds(camadas.limiteCar.getBounds(), { padding: [20, 20] });

  document.querySelectorAll("[data-camada]").forEach(function (chk) {
    chk.addEventListener("change", function () {
      var c = camadas[chk.getAttribute("data-camada")];
      if (!c) return;
      if (chk.checked) c.addTo(mapa); else mapa.removeLayer(c);
    });
  });

  var painel = document.getElementById("cm-alerta-painel");
  var corpo = document.getElementById("cm-alerta-corpo");
  function esc(s) { return String(s == null ? "-" : s).replace(/[&<>"']/g, function (c) { return ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]; }); }
  function mostrarAlerta(a) {
    if (!painel) return;
    var html = '<dl><dt>Data de detecção</dt><dd>' + esc(a.data) + '</dd><dt>Área</dt><dd>' + esc(a.area) + ' ha</dd>' +
      '<dt>Fonte</dt><dd>' + esc(a.fonte) + '</dd><dt>Classe</dt><dd>' + esc(a.classe) + '</dd><dt>Status</dt><dd>' + esc(a.status) + '</dd></dl>';
    if (a.antes || a.depois) {
      html += '<div class="cm-imagens" style="grid-template-columns:1fr 1fr">';
      if (a.antes) html += '<figure><img src="' + esc(a.antes) + '" alt="Antes"><figcaption>Antes</figcaption></figure>';
      if (a.depois) html += '<figure><img src="' + esc(a.depois) + '" alt="Depois"><figcaption>Depois</figcaption></figure>';
      html += '</div>';
    }
    document.getElementById("cm-alerta-titulo").textContent = "Alerta " + (a.codigo || "");
    corpo.innerHTML = html;
    painel.hidden = false;
    painel.parentElement.classList.add("tem-alerta");
    setTimeout(function () { mapa.invalidateSize(); }, 50);
  }
  var fechar = document.getElementById("cm-alerta-fechar");
  if (fechar) fechar.addEventListener("click", function () {
    painel.hidden = true;
    painel.parentElement.classList.remove("tem-alerta");
    setTimeout(function () { mapa.invalidateSize(); }, 50);
  });
})();
