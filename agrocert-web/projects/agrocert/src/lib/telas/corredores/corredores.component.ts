import { Component, ChangeDetectorRef, ViewEncapsulation, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf, NgSwitch, NgSwitchCase, Location } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { Observable, Subscription, shareReplay, tap } from 'rxjs';
import * as L from 'leaflet';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { SituacaoCorredores } from '../../api/modelos';
import { urlImagemApi } from '../../compartilhado/imagem-api.pipe';
import { RotaPipe } from '../../util/pipes';
import {
  CenarioCorredores, Corredor, EstudoCorredores, FazendaCorredores, Ligacao, MudancaVegetacao, RegiaoCorredores, SituacaoFazenda,
} from './modelos';
import { TILE_ROTULOS, TILE_SATELITE, TIPO_LEI, cap, ehLegal, esc, fmt0, icone, num, pct } from './comum';

type Aba = 'fazendas' | 'ligacoes' | 'corredores' | 'metodo';
type Camada = 'rl' | 'fazendas' | 'ligacoes' | 'lei';

interface Estado {
  regiao: number;
  ano: string;
  veg: string;
  dist: number;
  aba: Aba;
  busca: string;
  ordem: { i: number; dir: number } | null;
}

interface LinhaFazenda {
  f: FazendaCorredores;
  s: SituacaoFazenda;
  a: SituacaoFazenda | null;
  m: { perdaHa: number; ganhoHa: number; rlPerdaHa: number } | null;
}

interface Coluna {
  id: string;
  t: string;
  n?: boolean;
  k?: (x: LinhaFazenda) => number | string;
}

interface Opcao { v: string; t: string; title?: string }
interface Stat { cor: string; valor: string; rotulo: string; sub?: string }

const SITUACAO: Record<string, { t: string; c: string; cor: string }> = {
  'conectada': { t: 'Ligada a corredor', c: 'co-sit--ok', cor: '#2e9d57' },
  'isolada': { t: 'Isolada', c: 'co-sit--iso', cor: '#eb6834' },
  'sem-vegetacao': { t: 'RL sem mata', c: 'co-sit--sem', cor: '#c0392b' },
  'sem-rl': { t: 'Sem RL no CAR', c: 'co-sit--nada', cor: '#9e9e9e' },
};

@Component({
  selector: 'ac-corredores',
  standalone: true,
  imports: [NgIf, NgFor, NgSwitch, NgSwitchCase, RouterLink, RotaPipe],
  encapsulation: ViewEncapsulation.None,
  styles: [`
    .co-modulo .co-opcoes { margin-bottom: 14px; }
    .co-modulo .co-opcoes label { padding: 7px 14px; border-radius: 18px; font-size: 13.5px; }
    .co-modulo .co-kpis { grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); }
    .co-modulo .co-tabela { font-size: 13.5px; }
    .co-modulo .co-tabela th { font-size: 12.5px; }
  `],
  templateUrl: './corredores.component.html',
})
export class CorredoresComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly rota = new RotaPipe();
  private readonly subs = new Subscription();

  @ViewChild('mapaEl') mapaEl?: ElementRef<HTMLDivElement>;

  carregando = true;
  erroSituacao: string | null = null;
  erroEstudo = false;
  situacao: SituacaoCorredores | null = null;
  atualizadoEm = '';
  geradoEm = '';
  dados: EstudoCorredores | null = null;

  estado: Estado = { regiao: 0, ano: '', veg: 'matas', dist: 200, aba: 'fazendas', busca: '', ordem: null };
  opcoesAno: Opcao[] = [];
  opcoesVeg: Opcao[] = [];
  opcoesDist: Opcao[] = [];

  reg!: RegiaoCorredores;
  cen!: CenarioCorredores;
  kpis: Stat[] = [];
  colunas: Coluna[] = [];
  linhas: LinhaFazenda[] = [];
  anoLeiMetodo: number | string = '';

  private mapa?: L.Map;
  private overlay?: L.ImageOverlay;
  private camadas!: Record<Camada, L.LayerGroup>;
  private destaque: L.Layer | null = null;
  private marcadores = new Map<number, L.CircleMarker>();
  private ligacoesPorFaz: Record<number, number[]> = {};
  private rotulosLigacao: { meio: L.LatLng; camada: L.Marker }[] = [];
  private contornosCache: Record<string, GeoJSON.GeoJsonObject> = {};
  private pixels: { ctx: CanvasRenderingContext2D; w: number; h: number } | null = null;
  private imagens = new Map<string, Observable<string | null>>();
  private blobs: string[] = [];
  private imagemPedida = '';
  private leiPedida = '';

  readonly ha = ha;
  readonly num = num;
  readonly pct = pct;
  readonly cap = cap;
  readonly legal = ehLegal;
  readonly round = Math.round;

  ngOnInit(): void {
    this.subs.add(this.api.situacaoCorredores().subscribe({
      next: s => {
        this.situacao = s;
        this.atualizadoEm = dataHoraIso(s.atualizadoEm);
        if (!s.temDados) { this.carregando = false; return; }
        this.subs.add(this.api.corredores<EstudoCorredores>().subscribe({
          next: d => { this.carregando = false; this.dados = d; this.iniciar(); },
          error: () => { this.carregando = false; this.erroEstudo = true; },
        }));
      },
      error: e => { this.carregando = false; this.erroSituacao = mensagemDeErro(e); },
    }));
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    this.mapa?.remove();
    this.blobs.forEach(u => URL.revokeObjectURL(u));
  }

  private iniciar(): void {
    const d = this.dados!;
    this.estado.ano = String(d.anos[d.anos.length - 1]);
    if (d.distancias.indexOf(this.estado.dist) < 0) this.estado.dist = d.distancias[0];
    this.geradoEm = new Date(d.geradoEm).toLocaleString('pt-BR');

    this.opcoesAno = d.anos.map(a => ({ v: String(a), t: String(a) }));
    if (d.anos.length >= 2) this.opcoesAno.push({ v: 'comparar', t: 'Comparar ' + d.anos[0] + ' → ' + d.anos[d.anos.length - 1] });
    this.opcoesVeg = d.vegetacoes.map(v => ({ v: v.id, t: v.nome, title: v.descricao }));
    this.opcoesDist = d.distancias.map(x => ({ v: String(x), t: x + ' m' }));

    this.reg = d.regioes[0];
    this.cen = this.cenario();
    this.cdr.detectChanges();
    this.criarMapa();
    this.trocarRegiao();
  }

  regiao(): RegiaoCorredores { return this.dados!.regioes[this.estado.regiao]; }
  comparando(): boolean { return this.estado.ano === 'comparar'; }
  anoFinal(): string { const a = this.dados!.anos; return String(a[a.length - 1]); }
  anoInicial(): string { return String(this.dados!.anos[0]); }
  cenario(ano?: string): CenarioCorredores {
    return this.regiao().cenarios[(ano || (this.comparando() ? this.anoFinal() : this.estado.ano)) + '|' + this.estado.veg + '|' + this.estado.dist];
  }
  private mudanca(): MudancaVegetacao { return this.regiao().mudancas[this.estado.veg]; }
  nomeVeg(): string { return this.estado.veg === 'matas' ? 'mata' : 'vegetação nativa'; }
  classe(v: number): string { return v >= 5000 ? 'Corredor grande' : v >= 500 ? 'Corredor médio' : 'Corredor pequeno'; }

  sit(s: string): { t: string; c: string } {
    const x = SITUACAO[s] || SITUACAO['sem-rl'];
    return { t: s === 'sem-vegetacao' && this.estado.veg !== 'matas' ? 'RL sem vegetação' : x.t, c: x.c };
  }
  naFazenda(s: SituacaoFazenda): number { return (s.ligacoes || []).filter(y => y.naFazenda).length; }
  algumaLegal(s: SituacaoFazenda): boolean { return (s.ligacoes || []).some(ehLegal); }
  corDe(n: number): string | null { const k = this.cen.corredores[n - 1]; return k ? k.cor : null; }
  nomesFazendas(k: Corredor): string { return k.fazendas.slice(0, 4).map(f => this.reg.fazendas[f].nome).join(', '); }

  trocarRegiaoPor(v: string): void { this.estado.regiao = +v; this.trocarRegiao(); }

  escolher(chave: 'ano' | 'veg' | 'dist', v: string): void {
    if (chave === 'dist') this.estado.dist = +v; else this.estado[chave] = v;
    this.atualizar();
  }

  buscar(v: string): void { this.estado.busca = v; this.tabelaFazendas(); }

  ordenar(i: number): void {
    const o = this.estado.ordem;
    this.estado.ordem = o && o.i === i ? { i, dir: -o.dir } : { i, dir: i === 0 ? 1 : -1 };
    this.tabelaFazendas();
  }

  alternarCamada(c: Camada, ligada: boolean): void {
    if (!this.mapa) return;
    if (ligada) this.camadas[c].addTo(this.mapa); else this.mapa.removeLayer(this.camadas[c]);
  }

  private criarMapa(): void {
    const el = this.mapaEl?.nativeElement;
    if (!el) return;
    const mapa = this.mapa = L.map(el, { zoomControl: true, preferCanvas: true });
    L.tileLayer(TILE_SATELITE, { maxZoom: 18, attribution: 'Satélite &copy; Esri | Vegetação: MapBiomas | Imóveis: SICAR' }).addTo(mapa);
    L.tileLayer(TILE_ROTULOS, { maxZoom: 18 }).addTo(mapa);
    mapa.createPane('co-imagem').style.zIndex = '350';
    mapa.createPane('co-lei').style.zIndex = '360';
    this.camadas = {
      lei: L.layerGroup().addTo(mapa),
      rl: L.layerGroup().addTo(mapa),
      ligacoes: L.layerGroup().addTo(mapa),
      fazendas: L.layerGroup().addTo(mapa),
    };
    mapa.on('click', e => this.identificar(e as L.LeafletMouseEvent));
  }

  private limitesRegiao(): L.LatLngBounds {
    const b = this.regiao().limites;
    return L.latLngBounds([b[1], b[0]], [b[3], b[2]]);
  }

  private imagem(nome: string): Observable<string | null> {
    let o = this.imagens.get(nome);
    if (!o) {
      o = urlImagemApi(this.api, this.api.caminhoArquivoCorredores(nome)).pipe(
        tap(u => { if (u) this.blobs.push(u); }),
        shareReplay(1),
      );
      this.imagens.set(nome, o);
    }
    return o;
  }

  private trocarRegiao(): void {
    this.estado.ordem = null;
    const r = this.regiao();
    this.reg = r;
    this.anoLeiMetodo = r.lei ? r.lei.ano : '';
    if (this.mapa) {
      this.camadas.lei.clearLayers();
      const lei = r.lei;
      this.leiPedida = lei ? lei.imagem : '';
      if (lei) {
        this.subs.add(this.imagem(lei.imagem).subscribe(url => {
          if (!url || this.leiPedida !== lei.imagem || this.regiao() !== r) return;
          L.imageOverlay(url, this.limitesRegiao(), { pane: 'co-lei', className: 'co-overlay', opacity: 0.95 }).addTo(this.camadas.lei);
        }));
      }
      this.mapa.fitBounds(this.limitesRegiao());
      this.carregarContornos();
    }
    this.atualizar();
  }

  private carregarContornos(): void {
    const r = this.regiao();
    this.camadas.rl.clearLayers();
    const desenhar = (gj: GeoJSON.GeoJsonObject) => {
      if (r !== this.regiao()) return;
      this.camadas.rl.clearLayers();
      L.geoJSON(gj, {
        style: f => f?.properties?.tipo === 'rl'
          ? { color: '#ffe45c', weight: 1.6, fill: false, dashArray: '4 3' }
          : { color: '#ffffff', weight: 1.4, fill: false },
        interactive: false,
      }).addTo(this.camadas.rl);
    };
    if (this.contornosCache[r.id]) { desenhar(this.contornosCache[r.id]); return; }
    this.subs.add(this.api.get<GeoJSON.GeoJsonObject>(this.api.caminhoArquivoCorredores(r.contornos)).subscribe({
      next: gj => { this.contornosCache[r.id] = gj; desenhar(gj); },
      error: () => {  },
    }));
  }

  private atualizar(): void {
    const r = this.regiao();
    this.reg = r;
    this.cen = this.cenario();
    const nome = this.comparando() ? this.mudanca().imagem : this.cen.imagem;
    if (this.mapa) {
      this.imagemPedida = nome;
      this.pixels = null;
      this.subs.add(this.imagem(nome).subscribe(url => {
        if (!url || this.imagemPedida !== nome || !this.mapa) return;
        const limites = this.limitesRegiao();
        if (!this.overlay) this.overlay = L.imageOverlay(url, limites, { opacity: 0.88, pane: 'co-imagem', className: 'co-overlay' }).addTo(this.mapa);
        else { this.overlay.setUrl(url); this.overlay.setBounds(limites); }
        this.carregarPixels(url, nome);
      }));
      if (this.destaque) { this.mapa.removeLayer(this.destaque); this.destaque = null; }
      this.fazendasNoMapa();
      this.ligacoesNoMapa();
    }
    this.calcularKpis();
    this.tabelaFazendas();
  }

  private carregarPixels(url: string, nome: string): void {
    this.pixels = null;
    const img = new Image();
    img.onload = () => {
      if (this.imagemPedida !== nome) return;
      const c = document.createElement('canvas');
      c.width = img.naturalWidth; c.height = img.naturalHeight;
      const ctx = c.getContext('2d', { willReadFrequently: true });
      if (!ctx) return;
      ctx.drawImage(img, 0, 0);
      this.pixels = { ctx, w: c.width, h: c.height };
    };
    img.src = url;
  }

  private corEm(latlng: L.LatLng): { d: number; r: number; g: number; b: number; a: number } | null {
    const px = this.pixels;
    if (!px) return null;
    const b = this.regiao().limites;
    const x = Math.floor((latlng.lng - b[0]) / (b[2] - b[0]) * px.w);
    const y = Math.floor((merc(b[3]) - merc(latlng.lat)) / (merc(b[3]) - merc(b[1])) * px.h);
    if (x < 0 || y < 0 || x >= px.w || y >= px.h) return null;
    // olha o pixel e os vizinhos (fica mais fácil acertar uma mata estreita)
    let melhor: { d: number; r: number; g: number; b: number; a: number } | null = null;
    for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) {
      const xx = x + dx, yy = y + dy;
      if (xx < 0 || yy < 0 || xx >= px.w || yy >= px.h) continue;
      const p = px.ctx.getImageData(xx, yy, 1, 1).data;
      if (p[3] < 20) continue;
      const d = dx * dx + dy * dy + (p[3] > 180 ? 0 : 3); // prefere a mata à faixa de travessia
      if (!melhor || d < melhor.d) melhor = { d, r: p[0], g: p[1], b: p[2], a: p[3] };
    }
    return melhor;
  }

  private identificar(e: L.LeafletMouseEvent): void {
    const p = this.corEm(e.latlng);
    if (!p || !this.mapa) return;
    const d = this.dados!;
    let html: string;
    if (this.comparando()) {
      const tipos = [
        { cor: d.paleta.perda, t: '<b>' + cap(this.nomeVeg()) + ' perdida</b> entre ' + this.anoInicial() + ' e ' + this.anoFinal() },
        { cor: d.paleta.ganho, t: '<b>' + cap(this.nomeVeg()) + ' que voltou</b> (regenerou) entre ' + this.anoInicial() + ' e ' + this.anoFinal() },
        { cor: d.paleta.estavel, t: cap(this.nomeVeg()) + ' que continua de pé desde ' + this.anoInicial() },
      ];
      tipos.sort((a, b) => distCor(p, a.cor) - distCor(p, b.cor));
      html = tipos[0].t;
    } else {
      const c = this.cenario();
      const cand: { cor: string; tipo: 'iso' | 'c' | 'outros'; k?: Corredor }[] = [{ cor: d.paleta.isolado, tipo: 'iso' }];
      c.corredores.forEach(k => { if (k.cor !== d.paleta.outros) cand.push({ cor: k.cor, tipo: 'c', k }); });
      cand.push({ cor: d.paleta.outros, tipo: 'outros' });
      let m: { d: number; x: typeof cand[number] } | null = null;
      cand.forEach(x => { const dd = distCor(p, x.cor); if (!m || dd < m.d) m = { d: dd, x }; });
      const melhor = m as { d: number; x: typeof cand[number] } | null;
      if (!melhor || melhor.d > 40) return;
      const trav = p.a < 180 ? '<div class="co-pop-sub">Faixa de travessia: área aberta estreita (até ' + this.estado.dist + ' m) que os animais atravessam entre as matas.</div>' : '';
      if (melhor.x.tipo === 'iso') html = "<b>Fragmento isolado</b><div class='co-pop-sub'>Mata pequena (menos de " + num(d.parametros.corredorMinHa) + ' ha no conjunto) sem ligação com um corredor.</div>';
      else if (melhor.x.tipo === 'outros') html = "<b>Corredor pequeno</b><div class='co-pop-sub'>Conjunto de matas ligadas com 50 a algumas centenas de hectares.</div>" + trav;
      else {
        const k = melhor.x.k!;
        html = '<b><i class="co-cor" style="background:' + k.cor + '"></i> Corredor ' + k.numero + '</b>' +
          '<div class="co-pop-grande">' + ha(k.ha) + ' de ' + this.nomeVeg() + ' interligada</div>' +
          '<div class="co-pop-sub">' + this.classe(k.ha) + ' · ' + (k.fazendas.length ? k.fazendas.length + ' fazenda(s) de cooperados' : 'nenhuma fazenda de cooperado') + '</div>' + trav;
        this.realcar(k);
      }
    }
    L.popup().setLatLng(e.latlng).setContent(html).openOn(this.mapa);
  }

  private realcar(k: Corredor | null): void {
    if (!this.mapa) return;
    if (this.destaque) this.mapa.removeLayer(this.destaque);
    this.destaque = null;
    if (!k || !k.caixa) return;
    this.destaque = L.rectangle([[k.caixa[1], k.caixa[0]], [k.caixa[3], k.caixa[2]]], { color: '#fff', weight: 2, dashArray: '6 4', fill: false, interactive: false }).addTo(this.mapa);
  }

  private urlRelatorio(f: FazendaCorredores): string {
    const arvore = this.router.createUrlTree([this.rota.transform(['corredores', 'relatorio', this.regiao().id, f.id])], {
      queryParams: { veg: this.estado.veg, dist: this.estado.dist },
    });
    return this.location.prepareExternalUrl(this.router.serializeUrl(arvore));
  }

  private fazendasNoMapa(): void {
    this.camadas.fazendas.clearLayers();
    this.marcadores.clear();
    const r = this.regiao(), c = this.cenario();
    this.ligacoesPorFaz = {};
    c.ligacoes.forEach((l, i) => l.fazendas.forEach(f => (this.ligacoesPorFaz[f] = this.ligacoesPorFaz[f] || []).push(i + 1)));
    r.fazendas.forEach(f => {
      const s = c.fazendas[f.id];
      const m = L.circleMarker([f.lat, f.lon], {
        radius: 8, color: '#fff', weight: 2, fillColor: (SITUACAO[s.situacaoRl] || SITUACAO['sem-rl']).cor, fillOpacity: 1,
      });
      m.bindPopup(() => this.popupFazenda(f), { maxWidth: 320 });
      m.bindTooltip(esc(f.nome), { direction: 'top', offset: [0, -8] });
      m.addTo(this.camadas.fazendas);
      this.marcadores.set(f.id, m);
    });
  }

  private popupFazenda(f: FazendaCorredores): string {
    const c = this.cenario(), s = c.fazendas[f.id];
    let h = '<div class="co-pop-titulo">' + esc(f.nome) + '</div>' +
      '<div class="co-pop-sub">' + esc(f.municipio) + ' · ' + ha(f.areaHa) + '</div>' +
      '<table class="co-pop-tab">' +
      '<tr><td>' + cap(this.nomeVeg()) + ' na fazenda</td><td>' + ha(s.nativaHa) + '</td></tr>' +
      '<tr><td>Dentro de corredor</td><td>' + pct(s.emCorredorHa, s.nativaHa) + '</td></tr>' +
      '<tr><td>Maior corredor que toca</td><td>' + (s.corredor ? 'nº ' + s.corredor + ' · ' + ha(s.corredorHa) : 'nenhum') + '</td></tr>' +
      '<tr><td>Reserva Legal</td><td>' + this.situacaoHtml(s.situacaoRl) + '</td></tr>';
    if (this.comparando()) {
      const mu = this.mudanca().fazendas[f.id];
      h += '<tr><td>Perdida desde ' + this.anoInicial() + '</td><td>' + ha(mu.perdaHa) + '</td></tr>' +
        '<tr><td>Regenerou</td><td>' + ha(mu.ganhoHa) + '</td></tr>';
    }
    h += '</table>';
    const aRecompor = f.appRecomporHa + f.rlSemVegetacaoHa;
    if (aRecompor >= 0.5) h += '<div class="co-pop-lig co-pop-lig--lei">' + icone('gavel') + ' A lei já pede recompor ' + ha(aRecompor) + ' (APP ' + ha(f.appRecomporHa) + ' · RL sem vegetação ' + ha(f.rlSemVegetacaoHa) + ')</div>';
    const lig = this.ligacoesPorFaz[f.id] || [];
    if (lig.length) h += '<div class="co-pop-lig">' + icone('link') + ' Um trecho a restaurar passa aqui (ponto de ligação nº ' + lig.join(', ') + ')</div>';
    h += '<a class="co-pop-rel" href="' + esc(this.urlRelatorio(f)) + '" target="_blank" rel="noopener">' + icone('picture_as_pdf') + ' Relatório da fazenda (PDF)</a>';
    h += '<div class="co-pop-car">CAR ' + esc(f.car) + '</div>';
    return h;
  }

  private situacaoHtml(s: string): string {
    const x = this.sit(s);
    return '<span class="co-sit ' + x.c + '">' + x.t + '</span>';
  }

  private ligacoesNoMapa(): void {
    this.camadas.ligacoes.clearLayers();
    this.rotulosLigacao = [];
    this.cenario().ligacoes.forEach((l, i) => {
      const pts = l.linha.map(p => [p[1], p[0]] as L.LatLngTuple);
      const fundo = L.polyline(pts, { color: '#000', weight: 8, opacity: 0.35 });
      const linha = L.polyline(pts, { color: ehLegal(l) ? '#00d5ff' : '#ffd400', weight: 4, dashArray: '2 6', lineCap: 'round' });
      const meio = L.latLng((pts[0][0] + pts[1][0]) / 2, (pts[0][1] + pts[1][1]) / 2);
      const rot = L.marker(meio, { icon: L.divIcon({ className: 'co-lig-num' + (ehLegal(l) ? ' co-lig-num--lei' : ''), html: String(i + 1), iconSize: [24, 24] }) });
      [fundo, linha, rot].forEach((x: L.Layer) => { x.bindPopup(() => this.popupLigacao(l, i)); x.addTo(this.camadas.ligacoes); });
      this.rotulosLigacao.push({ meio, camada: rot });
    });
  }

  private popupLigacao(l: Ligacao, i: number): string {
    const r = this.regiao();
    const nomes = l.fazendas.map(f => esc(r.fazendas[f].nome));
    const lei = textoLei(l);
    return '<div class="co-pop-titulo">Ponto de ligação nº ' + (i + 1) + '</div>' +
      '<div class="co-pop-grande">Falha de ' + num(l.distanciaM) + ' m sem ' + this.nomeVeg() + '</div>' +
      '<div class="co-pop-sub">Liga o corredor ' + l.de + ' (' + ha(l.deHa) + ') ao corredor ' + l.para + ' (' + ha(l.paraHa) + '). Restaurando, vira um corredor de ' + ha(l.juntosHa) + '.</div>' +
      (lei ? '<div class="co-pop-lig co-pop-lig--lei">' + icone('gavel') + ' ' + lei + '</div>' : '') +
      (nomes.length ? '<div class="co-pop-lig">' + icone('agriculture') + ' ' + nomes.join(', ') + '</div>' : '') +
      (l.outrosImoveis ? '<div class="co-pop-sub">+ ' + l.outrosImoveis + ' imóvel(is) de não cooperados no trecho</div>' : '') +
      (!nomes.length && !l.outrosImoveis ? '<div class="co-pop-sub">Trecho fora de imóveis do CAR (estrada, área pública ou sem cadastro).</div>' : '');
  }

  private rolarParaMapa(): void {
    this.mapaEl?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  irParaFazenda(f: FazendaCorredores): void {
    if (!this.mapa) return;
    this.mapa.setView([f.lat, f.lon], 14);
    this.marcadores.get(f.id)?.openPopup();
    this.rolarParaMapa();
  }

  irParaLigacao(i: number): void {
    const x = this.rotulosLigacao[i];
    if (!this.mapa || !x) return;
    this.mapa.setView(x.meio, 15);
    x.camada.openPopup();
    this.rolarParaMapa();
  }

  irParaCorredor(k: Corredor): void {
    if (!k.caixa || !this.mapa) return;
    this.mapa.fitBounds([[k.caixa[1], k.caixa[0]], [k.caixa[3], k.caixa[2]]], { padding: [20, 20] });
    this.realcar(k);
    this.rolarParaMapa();
  }

  private calcularKpis(): void {
    const nv = this.nomeVeg();
    if (!this.comparando()) {
      const c = this.cenario();
      const maior = c.corredores.length ? c.corredores[0].ha : 0;
      const rl = rlConectadas(c);
      this.kpis = [
        { cor: '#2e9d57', valor: num(c.corredores.length), rotulo: 'Corredores de ' + nv, sub: c.classes.grandes + ' grandes · ' + c.classes.medios + ' médios · ' + c.classes.pequenos + ' pequenos' },
        { cor: '#1f6fb2', valor: ha(maior), rotulo: 'Maior corredor', sub: pct(maior, c.nativaHa) + ' de toda a ' + nv + ' da região' },
        { cor: '#eb6834', valor: num(c.isolados.quantidade), rotulo: 'Fragmentos isolados', sub: ha(c.isolados.ha) + ' de ' + nv + ' sem ligação' },
        { cor: '#5e9a42', valor: pct(c.emCorredorHa, c.nativaHa), rotulo: 'da ' + nv + ' está em corredor', sub: ha(c.emCorredorHa) + ' de ' + ha(c.nativaHa) },
        { cor: '#8e44ad', valor: rl.com + ' de ' + rl.total, rotulo: 'Reservas Legais ligadas', sub: 'fazendas de cooperados com RL em corredor' },
        { cor: '#0097b2', valor: c.ligacoes.filter(ehLegal).length + ' de ' + c.ligacoes.length, rotulo: 'Ligações que a lei já exige', sub: 'trechos que já são APP a recompor ou RL sem vegetação' },
      ];
    } else {
      const a = this.cenario(this.anoInicial()), b = this.cenario(this.anoFinal()), mu = this.mudanca();
      const ma = a.corredores.length ? a.corredores[0].ha : 0, mb = b.corredores.length ? b.corredores[0].ha : 0;
      const ra = rlConectadas(a), rb = rlConectadas(b);
      const dn = b.nativaHa - a.nativaHa;
      const ai = this.anoInicial(), af = this.anoFinal();
      this.kpis = [
        { cor: dn < 0 ? '#e34948' : '#2a78d6', valor: (dn < 0 ? '−' : '+') + ha(Math.abs(dn)), rotulo: cap(nv) + ' na região', sub: ha(a.nativaHa) + ' em ' + ai + ' → ' + ha(b.nativaHa) + ' em ' + af },
        { cor: '#e34948', valor: ha(mu.perdaHa), rotulo: 'Perdida (vermelho)', sub: 'desmatada ou convertida desde ' + ai },
        { cor: '#2a78d6', valor: ha(mu.ganhoHa), rotulo: 'Voltou (azul)', sub: 'regeneração ou plantio desde ' + ai },
        { cor: '#1f6fb2', valor: seta(ma, mb), rotulo: 'Maior corredor', sub: ha(ma) + ' → ' + ha(mb) },
        { cor: '#eb6834', valor: num(a.isolados.quantidade) + ' → ' + num(b.isolados.quantidade), rotulo: 'Fragmentos isolados', sub: num(a.corredores.length) + ' → ' + num(b.corredores.length) + ' corredores' },
        { cor: '#8e44ad', valor: ra.com + ' → ' + rb.com, rotulo: 'Reservas Legais ligadas', sub: 'de ' + rb.total + ' fazendas com RL' },
      ];
    }
  }

  private linhasFazendas(): LinhaFazenda[] {
    const r = this.regiao(), c = this.cenario();
    const a = this.comparando() ? this.cenario(this.anoInicial()) : null;
    const mu = this.comparando() ? this.mudanca() : null;
    const busca = this.estado.busca.trim().toLowerCase();
    return r.fazendas
      .filter(f => !busca || (f.nome + ' ' + f.car + ' ' + f.municipio).toLowerCase().indexOf(busca) >= 0)
      .map(f => ({ f, s: c.fazendas[f.id], a: a ? a.fazendas[f.id] : null, m: mu ? mu.fazendas[f.id] : null }));
  }

  private colunasFazendas(): Coluna[] {
    const nome: Coluna = { id: 'nome', t: 'Fazenda', k: x => x.f.nome.toLowerCase() };
    const nv = cap(this.nomeVeg());
    if (this.comparando()) {
      const ai = this.anoInicial(), af = this.anoFinal();
      return [nome,
        { id: 'vegA', t: nv + ' ' + ai, n: true, k: x => x.a!.nativaHa },
        { id: 'vegB', t: nv + ' ' + af, n: true, k: x => x.s.nativaHa },
        { id: 'perda', t: 'Perdida', n: true, k: x => x.m!.perdaHa },
        { id: 'ganho', t: 'Voltou', n: true, k: x => x.m!.ganhoHa },
        { id: 'emCorredorAB', t: 'Em corredor', n: true, k: x => x.s.nativaHa ? x.s.emCorredorHa / x.s.nativaHa : 0 },
        { id: 'rlA', t: 'Reserva Legal ' + ai, k: x => x.a!.situacaoRl },
        { id: 'rlB', t: 'Reserva Legal ' + af, k: x => x.s.situacaoRl },
      ];
    }
    return [nome,
      { id: 'veg', t: nv, n: true, k: x => x.s.nativaHa },
      { id: 'emCorredor', t: 'Em corredor', n: true, k: x => x.s.nativaHa ? x.s.emCorredorHa / x.s.nativaHa : -1 },
      { id: 'maior', t: 'Maior corredor que toca', n: true, k: x => x.s.corredorHa },
      { id: 'rl', t: 'Reserva Legal', k: x => x.s.situacaoRl },
      { id: 'lei', t: 'A lei pede recompor', n: true, k: x => x.f.appRecomporHa + x.f.rlSemVegetacaoHa },
      { id: 'restaurar', t: 'Onde restaurar', k: x => ordemRestaurar(x.s) },
      { id: 'relatorio', t: '' },
    ];
  }

  private tabelaFazendas(): void {
    const cols = this.colunasFazendas(), linhas = this.linhasFazendas();
    const o = this.estado.ordem;
    if (o && cols[o.i]) {
      const k = cols[o.i].k || (() => 0);
      linhas.sort((a, b) => { const x = k(a), y = k(b); return (x < y ? -1 : x > y ? 1 : 0) * o.dir; });
    } else linhas.sort((a, b) => b.s.nativaHa - a.s.nativaHa);
    this.colunas = cols;
    this.linhas = linhas;
  }

  private textoCelula(id: string, x: LinhaFazenda): string {
    const nv = this.nomeVeg();
    switch (id) {
      case 'nome': return x.f.nome + ' — ' + x.f.municipio + ' · ' + ha(x.f.areaHa);
      case 'vegA': return ha(x.a!.nativaHa);
      case 'vegB': return ha(x.s.nativaHa);
      case 'perda': return x.m!.perdaHa >= 0.5 ? ha(x.m!.perdaHa) : '—';
      case 'ganho': return x.m!.ganhoHa >= 0.5 ? ha(x.m!.ganhoHa) : '—';
      case 'emCorredorAB': return pct(x.a!.emCorredorHa, x.a!.nativaHa) + ' → ' + pct(x.s.emCorredorHa, x.s.nativaHa);
      case 'rlA': return this.sit(x.a!.situacaoRl).t;
      case 'rlB': return this.sit(x.s.situacaoRl).t;
      case 'veg': return ha(x.s.nativaHa) + ' — ' + pct(x.s.nativaHa, x.f.areaHa) + ' da fazenda';
      case 'emCorredor': return x.s.nativaHa > 0 ? pct(x.s.emCorredorHa, x.s.nativaHa) : '—';
      case 'maior': return x.s.corredor ? 'nº ' + x.s.corredor + ' — ' + ha(x.s.corredorHa) : 'nenhum';
      case 'rl': return this.sit(x.s.situacaoRl).t + (x.f.rlHa > 0 ? ' — ' + ha(x.f.rlHa) + ' declarados · ' + pct(x.s.rlNativaHa, x.f.rlHa) + ' com ' + nv : '');
      case 'lei': {
        const t = x.f.appRecomporHa + x.f.rlSemVegetacaoHa;
        return t < 0.5 ? '—' : ha(t) + ' — APP ' + ha(x.f.appRecomporHa) + ' · RL ' + ha(x.f.rlSemVegetacaoHa);
      }
      case 'restaurar': {
        const l = x.s.ligacoes || [];
        if (!l.length) return 'nenhuma ligação perto';
        const na = this.naFazenda(x.s);
        return (na ? na + ' na fazenda' : l.length + ' a até 1,5 km') + (l.some(ehLegal) ? ' — já é obrigação legal' : '');
      }
      default: return '';
    }
  }

  baixarCsv(): void {
    const cols = this.colunasFazendas().filter(c => c.t);
    const linhas = this.linhasFazendas();
    const csv = ['CAR;' + cols.map(c => c.t).join(';')].concat(linhas.map(x =>
      x.f.car + ';' + cols.map(c => '"' + this.textoCelula(c.id, x).replace(/"/g, '""') + '"').join(';'),
    )).join('\r\n');
    const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'corredores_' + this.regiao().id + '_' + this.estado.ano + '_' + this.estado.veg + '_' + this.estado.dist + 'm.csv';
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  }
}

function ha(v: number | null | undefined): string { return fmt0.format(v || 0) + ' ha'; }
function merc(lat: number): number { return Math.log(Math.tan(Math.PI / 4 + lat * Math.PI / 360)); }
function hexRgb(h: string): number[] { return [parseInt(h.substr(1, 2), 16), parseInt(h.substr(3, 2), 16), parseInt(h.substr(5, 2), 16)]; }
function distCor(p: { r: number; g: number; b: number }, h: string): number {
  const c = hexRgb(h);
  return Math.abs(p.r - c[0]) + Math.abs(p.g - c[1]) + Math.abs(p.b - c[2]);
}
function textoLei(l: Ligacao): string {
  if (!l.leiTipo || l.leiPct <= 0) return '';
  return l.leiPct + '% do trecho já é ' + TIPO_LEI[l.leiTipo] + ' — o dono já tem obrigação de restaurar';
}
function rlConectadas(c: CenarioCorredores): { com: number; total: number } {
  let com = 0, total = 0;
  c.fazendas.forEach(s => { if (s.situacaoRl !== 'sem-rl') { total++; if (s.situacaoRl === 'conectada') com++; } });
  return { com, total };
}
function seta(a: number, b: number): string {
  if (a <= 0) return ha(b);
  const d = (b - a) / a * 100;
  return Math.abs(d) < 0.5 ? 'igual' : (d > 0 ? '+' : '−') + fmt0.format(Math.abs(d)) + '%';
}
function ordemRestaurar(s: SituacaoFazenda): number {
  const l = s.ligacoes || [];
  return (l.some(y => y.naFazenda) ? 2 : l.length ? 1 : 0) + (l.some(ehLegal) ? 0.5 : 0);
}
function dataHoraIso(v: string | null | undefined): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(v || '');
  return m ? `${m[3]}/${m[2]}/${m[1]} às ${m[4]}:${m[5]}` : '';
}
