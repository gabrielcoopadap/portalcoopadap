(function () {
  "use strict";
  var dica = document.getElementById("rc-dica");
  var botao = document.getElementById("rc-imprimir");
  botao.addEventListener("click", function () { window.print(); });

  var imgs = Array.prototype.slice.call(document.querySelectorAll("img[data-rc-img]"));
  var pendentes = imgs.filter(function (i) { return !i.complete; }).length;
  function atualizar() {
    dica.textContent = pendentes > 0
      ? "Carregando imagens de satélite… (" + pendentes + " restante" + (pendentes > 1 ? "s" : "") + ")"
      : "Pronto: clique em Gerar PDF e escolha \"Salvar como PDF\" (A4, retrato).";
  }
  imgs.forEach(function (img) {
    function fim() { pendentes = Math.max(0, pendentes - 1); atualizar(); }
    img.addEventListener("error", function () {
      var d = document.createElement("div");
      d.className = "rc-fig__falta";
      d.textContent = "imagem indisponível";
      img.replaceWith(d);
    });
    if (!img.complete) { img.addEventListener("load", fim); img.addEventListener("error", fim); }
  });
  atualizar();

  var fonte = document.getElementById("rc-dados");
  var el = document.getElementById("rc-mapa");
  if (!fonte || !el || typeof L === "undefined") return;
  var d = JSON.parse(fonte.textContent);

  var mapa = L.map(el, { zoomControl: true, scrollWheelZoom: false, zoomSnap: 0.25 });
  mapa.attributionControl.setPrefix(false);
  L.tileLayer("https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}", {
    maxZoom: 18, attribution: "Satélite © Esri · MapBiomas · SICAR"
  }).addTo(mapa);

  function geo(txt, estilo) {
    if (!txt) return null;
    try { return L.geoJSON(JSON.parse(txt), { style: function () { return estilo; }, interactive: false }); } catch (e) { return null; }
  }
  var ucs = geo(d.ucs, { color: "#7b3fbf", weight: 2, dashArray: "8 5", fillColor: "#7b3fbf", fillOpacity: 0.08 });
  if (ucs) ucs.addTo(mapa);
  if (d.mudanca && d.limites) {
    var b = d.limites;
    L.imageOverlay(d.mudanca, [[b[1], b[0]], [b[3], b[2]]], { className: "rc-pixel", opacity: 0.9 }).addTo(mapa);
  }
  var app = geo(d.app, { color: "#29a3ff", weight: 1.5, fill: false });
  var rl = geo(d.rl, { color: "#ffe45c", weight: 2, dashArray: "5 4", fill: false });
  var imovel = geo(d.imovel, { color: "#ffffff", weight: 2.5, fill: false });
  [app, rl, imovel].forEach(function (c) { if (c) c.addTo(mapa); });

  (d.areas || []).forEach(function (a) {
    L.marker([a.lat, a.lon], {
      icon: L.divIcon({ className: "rc-num rc-num--mapa", html: '<span style="background:' + a.cor + '">' + a.n + "</span>", iconSize: [22, 22] }),
      interactive: false
    }).addTo(mapa);
  });

  if (imovel) mapa.fitBounds(imovel.getBounds(), { padding: [16, 16] });
  else if (d.limites) mapa.fitBounds([[d.limites[1], d.limites[0]], [d.limites[3], d.limites[2]]]);
})();
