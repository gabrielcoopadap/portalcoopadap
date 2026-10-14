import { ChangeDetectorRef, Component, ElementRef, OnDestroy, OnInit, ViewChild, ViewEncapsulation, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, Subscription, catchError, combineLatest, map, of, switchMap, throwError } from 'rxjs';
import * as L from 'leaflet';
import { AgroCertApi } from '../../api/agrocert-api.service';
import { urlImagemApi } from '../../compartilhado/imagem-api.pipe';
import { imprimirRelatorio } from '../../util/imprimir';
import { RotaPipe } from '../../util/pipes';
import {
  CenarioCorredores, Corredor, EstudoCorredores, FazendaCorredores, Ligacao, RegiaoCorredores, SituacaoFazenda,
} from './modelos';
import { TILE_SATELITE, TIPO_LEI, cap, ehLegal, fmt0, fmt1, num, pct } from './comum';

const LETRAS = 'ABCDEFGH';
const REGIAO_VALIDA = /^r\d{1,3}$/;

interface Parametros { regiao: string; fazenda: number; veg: string; dist: number }

interface LinhaTab { a: string; b: string; corQ?: string }

interface Relatorio {
  d: EstudoCorredores;
  r: RegiaoCorredores;
  f: FazendaCorredores;
  gj: GeoJSON.GeoJsonObject | null;
  anoA: string;
  anoB: string;
  cB: CenarioCorredores;
  s: SituacaoFazenda;
  nomeVeg: string;
  vegNome: string;
  ligs: Ligacao[];
  lei: number;
  maior: Corredor | null;
  frases: string[];
  sit: [string, string];
  numeros: { v: string; sitHtml?: boolean; t: string; s: string }[];
  tabLei: LinhaTab[];
  tabMudanca: LinhaTab[];
  temMudanca: boolean;
  hoje: string;
  geradoEm: string;
}

@Component({
  selector: 'ac-relatorio-corredores',
  standalone: true,
  imports: [NgIf, NgFor, RouterLink, RotaPipe],
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['./relatorio-corredores.css'],
  templateUrl: './relatorio-corredores.component.html',
})
export class RelatorioCorredoresComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly titulo = inject(Title);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly rota = new RotaPipe();
  private readonly subs = new Subscription();
  private montagem = new Subscription();

  @ViewChild('mapa1') mapa1?: ElementRef<HTMLDivElement>;
  @ViewChild('mapa2') mapa2?: ElementRef<HTMLDivElement>;

  p: Parametros = { regiao: '', fazenda: 0, veg: 'matas', dist: 200 };
  rel: Relatorio | null = null;
  erro = false;
  pronto = false;
  semLogo = false;

  private mapas: L.Map[] = [];
  private blobs: string[] = [];
  private pendentes = 0;
  private relogio: ReturnType<typeof setTimeout> | null = null;

  readonly ha = haRel;
  readonly num = num;
  readonly fmt1 = (v: number) => fmt1.format(v);
  letra(i: number): string { return LETRAS[i]; }
  tipoLei(l: Ligacao): string { return l.leiTipo ? TIPO_LEI[l.leiTipo] : ''; }

  ngOnInit(): void {
    this.subs.add(combineLatest([this.route.paramMap, this.route.queryParamMap]).subscribe(([pm, qm]) => {
      this.limpar();
      const regiao = pm.get('regiao') ?? '';
      const fazendaTxt = pm.get('fazenda') ?? '';
      if (!REGIAO_VALIDA.test(regiao) || !/^\d+$/.test(fazendaTxt)) { this.erro = true; return; }
      const veg = qm.get('veg');
      const dist = Number(qm.get('dist'));
      this.p = {
        regiao,
        fazenda: +fazendaTxt,
        veg: veg === 'matas' || veg === 'nativa' ? veg : 'matas',
        dist: Number.isInteger(dist) && dist > 0 && dist <= 5000 ? dist : 200,
      };
      this.carregar();
    }));
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    this.limpar();
  }

  private limpar(): void {
    this.montagem.unsubscribe();
    this.montagem = new Subscription();
    this.mapas.forEach(m => m.remove());
    this.mapas = [];
    this.blobs.forEach(u => URL.revokeObjectURL(u));
    this.blobs = [];
    if (this.relogio) clearTimeout(this.relogio);
    this.relogio = null;
    this.rel = null;
    this.erro = false;
    this.pronto = false;
    this.pendentes = 0;
  }

  private carregar(): void {
    const P = this.p;
    this.montagem.add(this.api.situacaoCorredores().pipe(
      switchMap(sit => {
        if (!sit.temDados) { this.router.navigateByUrl(this.rota.transform('corredores')); return of(null); }
        return this.api.corredores<EstudoCorredores>().pipe(switchMap(d => {
          const r = d.regioes.filter(y => y.id === P.regiao)[0];
          const f = r && r.fazendas[P.fazenda];
          if (!f) return throwError(() => new Error('fazenda'));
          if (d.distancias.indexOf(P.dist) < 0) P.dist = d.distancias[0];
          return this.api.get<GeoJSON.GeoJsonObject>(this.api.caminhoArquivoCorredores(r.contornos)).pipe(
            catchError(() => of(null)),
            map(gj => ({ d, r, f, gj })),
          );
        }));
      }),
    ).subscribe({
      next: x => { if (x) this.montar(x.d, x.r, x.f, x.gj); },
      error: () => { this.erro = true; },
    }));
  }

  trocarOpcoes(veg: string, dist: string): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: { veg, dist } });
  }

  imprimir(): void { imprimirRelatorio(); }

  private montar(d: EstudoCorredores, r: RegiaoCorredores, f: FazendaCorredores, gj: GeoJSON.GeoJsonObject | null): void {
    const P = this.p;
    const anoA = String(d.anos[0]), anoB = String(d.anos[d.anos.length - 1]);
    const chave = (ano: string) => ano + '|' + P.veg + '|' + P.dist;
    const cB = r.cenarios[chave(anoB)], cA = r.cenarios[chave(anoA)];
    const s = cB.fazendas[f.id], sA = cA.fazendas[f.id];
    const mu = r.mudancas && r.mudancas[P.veg] ? r.mudancas[P.veg].fazendas[f.id] : null;
    const nomeVeg = P.veg === 'matas' ? 'mata' : 'vegetação nativa';
    const vegInfo = d.vegetacoes.filter(v => v.id === P.veg)[0] || { nome: P.veg };
    const ligs = s.ligacoes || [];
    const lei = f.appRecomporHa + f.rlSemVegetacaoHa;
    const maior = s.corredor ? cB.corredores[s.corredor - 1] : null;
    const ha = haRel;

    const frases: string[] = [];
    frases.push('A fazenda tem <b>' + ha(s.nativaHa) + '</b> de ' + nomeVeg + ' (' + pct(s.nativaHa, f.areaHa) + ' da área).');
    if (s.emCorredorHa > 0)
      frases.push('<b>' + pct(s.emCorredorHa, s.nativaHa) + '</b> dessa ' + nomeVeg + ' faz parte de corredores ecológicos' +
        (maior ? ' — o maior que passa pela fazenda tem <b>' + ha(maior.ha) + '</b>.' : '.'));
    else if (s.nativaHa > 0) frases.push('Nenhuma ' + nomeVeg + ' da fazenda está ligada a um corredor: são fragmentos isolados.');
    frases.push(({
      'conectada': 'A <b>Reserva Legal está ligada</b> a um corredor.',
      'isolada': 'A Reserva Legal tem vegetação, mas está <b>isolada</b> (fora de corredor).',
      'sem-vegetacao': 'A Reserva Legal tem <b>pouca ' + nomeVeg + '</b> no mapa (menos de 20% da área declarada).',
      'sem-rl': 'Não há Reserva Legal declarada no CAR desta fazenda.',
    } as Record<string, string>)[s.situacaoRl] || '');
    if (lei >= 0.5) frases.push('Pelo CAR, a lei já pede recompor <b>' + ha(lei) + '</b> nesta fazenda.');
    const na = ligs.filter(l => l.naFazenda);
    if (na.length) frases.push('Há <b>' + na.length + ' trecho' + (na.length > 1 ? 's' : '') + ' dentro da fazenda</b> que, restaurado' + (na.length > 1 ? 's' : '') + ', ligaria' + (na.length > 1 ? 'm' : '') + ' corredores' +
      (na.some(l => l.leiPct >= 50) ? ' — e já é obrigação legal.' : '.'));
    else if (ligs.length) frases.push('Há ' + ligs.length + ' trecho' + (ligs.length > 1 ? 's' : '') + ' a restaurar a até 1,5 km da fazenda, em áreas vizinhas.');
    if (mu) frases.push('Desde ' + anoA + ', a fazenda perdeu <b>' + ha(mu.perdaHa) + '</b> e recuperou <b>' + ha(mu.ganhoHa) + '</b> de ' + nomeVeg + '.');

    const hoje = new Date().toLocaleDateString('pt-BR');
    const sit = (({
      'conectada': ['Ligada a corredor', 'ok'], 'isolada': ['Isolada', 'iso'],
      'sem-vegetacao': [P.veg === 'matas' ? 'RL sem mata' : 'RL sem vegetação', 'sem'], 'sem-rl': ['Sem RL no CAR', 'nada'],
    } as Record<string, [string, string]>)[s.situacaoRl]) || ['—', 'nada'];

    const rotSit = (x: string) => ({ 'conectada': 'ligada', 'isolada': 'isolada', 'sem-vegetacao': 'sem vegetação', 'sem-rl': 'sem RL' } as Record<string, string>)[x] || '—';

    const tabMudanca: LinhaTab[] = [
      { a: cap(nomeVeg) + ' em ' + anoA, b: ha(sA.nativaHa) },
      { a: cap(nomeVeg) + ' em ' + anoB, b: ha(s.nativaHa) },
    ];
    if (mu) tabMudanca.push(
      { a: 'Perdida', b: ha(mu.perdaHa), corQ: d.paleta.perda },
      { a: 'Voltou (regenerou)', b: ha(mu.ganhoHa), corQ: d.paleta.ganho },
      { a: 'Perdida dentro da Reserva Legal', b: ha(mu.rlPerdaHa) },
    );
    tabMudanca.push(
      { a: 'Em corredor', b: pct(sA.emCorredorHa, sA.nativaHa) + ' → ' + pct(s.emCorredorHa, s.nativaHa) },
      { a: 'Reserva Legal', b: rotSit(sA.situacaoRl) + ' → ' + rotSit(s.situacaoRl) },
    );

    this.rel = {
      d, r, f, gj, anoA, anoB, cB, s, nomeVeg, vegNome: vegInfo.nome, ligs, lei, maior,
      frases: frases.filter(Boolean),
      sit,
      numeros: [
        { v: ha(s.nativaHa), t: cap(nomeVeg) + ' na fazenda', s: pct(s.nativaHa, f.areaHa) + ' da área' },
        { v: pct(s.emCorredorHa, s.nativaHa), t: 'Em corredor', s: ha(s.emCorredorHa) },
        { v: maior ? ha(maior.ha) : '—', t: 'Maior corredor que toca', s: maior ? 'corredor nº ' + maior.numero : 'nenhum' },
        { v: '', sitHtml: true, t: 'Reserva Legal', s: f.rlHa > 0 ? ha(f.rlHa) + ' declarados' : '' },
        { v: lei >= 0.5 ? ha(lei) : '—', t: 'A lei pede recompor', s: 'APP + RL sem vegetação' },
      ],
      tabLei: [
        { a: 'APP a recompor (declarada no CAR)', b: ha(f.appRecomporHa) },
        { a: 'Reserva Legal declarada', b: f.rlHa > 0 ? ha(f.rlHa) : 'não declarada' },
        { a: 'Reserva Legal com vegetação nativa (MapBiomas ' + anoB + ')', b: f.rlHa > 0 ? ha(f.rlHa - f.rlSemVegetacaoHa) + ' (' + pct(f.rlHa - f.rlSemVegetacaoHa, f.rlHa) + ')' : '—' },
        { a: 'Reserva Legal sem vegetação nativa', b: f.rlHa > 0 ? ha(f.rlSemVegetacaoHa) : '—' },
      ],
      tabMudanca,
      temMudanca: !!(r.mudancas && r.mudancas[P.veg]),
      hoje,
      geradoEm: new Date(d.geradoEm).toLocaleDateString('pt-BR'),
    };
    this.titulo.setTitle('Corredores Ecológicos — ' + f.nome);

    this.cdr.detectChanges();
    this.criarMapas(this.rel);
  }

  private esperar(): () => void {
    this.pendentes++;
    let feito = false;
    return () => { if (feito) return; feito = true; this.pendentes--; this.verificarPronto(); };
  }

  private verificarPronto(): void {
    if (this.pendentes > 0) return;
    this.pronto = true;
  }

  private imagem(nome: string): Observable<string | null> {
    return urlImagemApi(this.api, this.api.caminhoArquivoCorredores(nome));
  }

  private overlay(m: L.Map, nome: string, bounds: L.LatLngBounds, opcoes: L.ImageOverlayOptions): void {
    const fim = this.esperar();
    this.montagem.add(this.imagem(nome).subscribe(url => {
      if (!url) { fim(); return; }
      this.blobs.push(url);
      const ov = L.imageOverlay(url, bounds, opcoes);
      ov.once('load error', fim);
      ov.addTo(m);
    }));
  }

  private criarMapas(x: Relatorio): void {
    const { r, f, gj, ligs, cB } = x;
    const P = this.p;
    const limites = r.limites, bounds = L.latLngBounds([limites[1], limites[0]], [limites[3], limites[2]]);
    const caixa = f.caixa ? L.latLngBounds([f.caixa[1], f.caixa[0]], [f.caixa[3], f.caixa[2]]) : L.latLng(f.lat, f.lon).toBounds(1500);
    const foco = L.latLngBounds(caixa.getSouthWest(), caixa.getNorthEast());
    ligs.forEach(l => l.linha.forEach(p => foco.extend([p[1], p[0]])));

    const mapa = (el: HTMLElement | undefined, principal: boolean, img: string, comLei: boolean, comLigacoes: boolean) => {
      if (!el) return;
      const m = L.map(el, { zoomControl: false, attributionControl: true, dragging: false, scrollWheelZoom: false, doubleClickZoom: false, boxZoom: false, keyboard: false, touchZoom: false, zoomSnap: 0.25 });
      this.mapas.push(m);
      m.attributionControl.setPrefix(false);
      const sat = L.tileLayer(TILE_SATELITE, { maxZoom: 18, attribution: 'Satélite © Esri · MapBiomas · SICAR' });
      sat.once('load', this.esperar());
      sat.addTo(m);
      m.fitBounds(principal ? foco : caixa, { padding: [18, 18] });
      this.overlay(m, img, bounds, { opacity: 0.85, className: 'rl-pixel' });
      if (comLei && r.lei) this.overlay(m, r.lei.imagem, bounds, { className: 'rl-pixel' });
      if (gj) {
        L.geoJSON(gj, {
          filter: ft => ft.properties?.fazenda === f.id,
          style: ft => ft?.properties?.tipo === 'rl' ? { color: '#ffe45c', weight: 2, fill: false, dashArray: '5 4' } : { color: '#fff', weight: 2.5, fill: false },
        }).addTo(m);
      }
      if (comLigacoes) ligs.forEach((l, i) => {
        const pts = l.linha.map(p => [p[1], p[0]] as L.LatLngTuple);
        L.polyline(pts, { color: '#000', weight: 8, opacity: 0.35 }).addTo(m);
        L.polyline(pts, { color: ehLegal(l) ? '#00d5ff' : '#ffd400', weight: 4.5, dashArray: '2 6', lineCap: 'round' }).addTo(m);
        L.marker([(pts[0][0] + pts[1][0]) / 2, (pts[0][1] + pts[1][1]) / 2], {
          icon: L.divIcon({ className: 'rl-letra rl-letra--mapa' + (ehLegal(l) ? ' rl-letra--lei' : ''), html: LETRAS[i], iconSize: [24, 24] }),
        }).addTo(m);
      });
    };
    mapa(this.mapa1?.nativeElement, true, cB.imagem, true, true);
    if (x.temMudanca) mapa(this.mapa2?.nativeElement, false, r.mudancas[P.veg].imagem, false, false);
    // se o satélite não carregar (sem internet), libera mesmo assim
    this.relogio = setTimeout(() => { this.pendentes = 0; this.verificarPronto(); }, 12000);
  }
}

function haRel(v: number | null | undefined): string { return ((v as number) > 0 && (v as number) < 10 ? fmt1 : fmt0).format(v || 0) + ' ha'; }
