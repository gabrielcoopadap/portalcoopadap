import { Component, ElementRef, OnDestroy, OnInit, ViewChild, ViewEncapsulation, inject } from '@angular/core';
import { AsyncPipe, NgFor, NgIf, NgTemplateOutlet } from '@angular/common';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, Observable, Subscription, catchError, finalize, map, shareReplay, startWith, switchMap, tap } from 'rxjs';
import * as L from 'leaflet';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { LeituraNdvi } from '../../api/modelos';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { urlImagemApi } from '../../compartilhado/imagem-api.pipe';
import { ArquivoPipe, DataPipe, RotaPipe } from '../../util/pipes';
import { classificarNdvi } from '../../util/rotulos';
import { data, mesAno, numero } from '../../util/formato';
import { imprimirRelatorio } from '../../util/imprimir';
import { HaPorAno, JanelaImagem, LinhaAreaCar, RelatorioConformidade, StatusCriterio } from './conformidade.modelos';

interface ItemFigura {
  rotulo: string;
  src?: string | null;
  satelite?: Observable<string | null | undefined>;
  falhou?: boolean;
}

interface LinhaFiguras {
  titulo: string;
  imovel: boolean;
  janela: JanelaImagem;
  viewBox: string;
  itens: ItemFigura[];
}

interface FigurasArea {
  linhas: LinhaFiguras[];
}

@Component({
  selector: 'ac-conformidade',
  standalone: true,
  imports: [NgIf, NgFor, NgTemplateOutlet, AsyncPipe, RouterLink, EstadoComponent, RotaPipe, DataPipe, ArquivoPipe],
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['./conformidade.component.css'],
  templateUrl: './conformidade.component.html',
})
export class ConformidadeComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly rota = inject(ActivatedRoute);
  private readonly titulo = inject(Title);
  private sub?: Subscription;
  private mapa?: L.Map;
  private urlsSatelite: string[] = [];

  id = 0;
  r: RelatorioConformidade | null = null;
  erro: string | null = null;
  hoje = data(new Date());
  logoFalhou = false;

  conclusaoClasse = 'ok';
  conclusao = '';
  notaMarcos = '';
  anoMarco2008: number | null = null;
  serieNdvi: LeituraNdvi[] = [];
  figurasImovel: LinhaFiguras[] = [];
  figurasAreas = new Map<number, FigurasArea>();
  pendentes = 0;

  @ViewChild('mapaEl') set mapaEl(el: ElementRef<HTMLElement> | undefined) {
    if (el && this.r && !this.mapa) this.criarMapa(el.nativeElement, this.r);
  }

  ngOnInit(): void {
    this.sub = this.rota.paramMap.pipe(
      map(p => Number(p.get('id'))),
      tap(id => {
        this.id = id;
        this.limpar();
        this.r = null;
        this.erro = null;
      }),
      switchMap(id => this.api.conformidade<RelatorioConformidade>(id).pipe(
        catchError(e => { this.erro = mensagemDeErro(e); return EMPTY; }),
      )),
    ).subscribe(r => this.mostrar(r));
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    this.limpar();
  }

  carregar(): void {
    this.erro = null;
    this.api.conformidade<RelatorioConformidade>(this.id).subscribe({
      next: r => this.mostrar(r),
      error: e => (this.erro = mensagemDeErro(e)),
    });
  }

  imprimir(): void {
    imprimirRelatorio();
  }

  private mostrar(r: RelatorioConformidade): void {
    this.limpar();
    this.hoje = data(new Date());
    this.titulo.setTitle(`Conformidade ambiental — ${r.nome}`);

    const piores = r.criterios.filter(c => c.status === 'IndicioNaoConformidade').length;
    const verificar = r.criterios.filter(c => c.status === 'Verificar').length;
    const semDados = r.criterios.filter(c => c.status === 'SemDados').length;
    if (piores > 0) {
      this.conclusaoClasse = 'nc';
      this.conclusao = `Há ${piores} critério(s) com indício de não conformidade. Confira as áreas indicadas em campo e reúna os documentos (autorizações, termos de regularização) antes da auditoria.`;
    } else if (verificar > 0) {
      this.conclusaoClasse = 'ver';
      this.conclusao = `Nenhum indício de não conformidade, mas ${verificar} critério(s) precisam de verificação ou de documento.`;
    } else if (semDados > 0) {
      this.conclusaoClasse = 'sd';
      this.conclusao = `Nenhum problema encontrado nos critérios avaliados; ${semDados} critério(s) não puderam ser avaliados por falta de dados.`;
    } else {
      this.conclusaoClasse = 'ok';
      this.conclusao = 'Nenhuma conversão de vegetação nativa nem sobreposição com unidade de conservação foi encontrada nas fontes consultadas.';
    }

    this.notaMarcos = r.marcos
      .map(m => `${m.rotulo} → mapa de ${m.anoUsado ?? '—'}${m.aproximado ? ' (aprox.)' : ''}`)
      .join(' · ');
    this.anoMarco2008 = r.marcos.find(m => m.chave === '2008')?.anoUsado ?? null;
    this.serieNdvi = r.ndvi.filter(n => n.ndviMedio !== null && n.ndviMedio !== undefined);

    this.figurasImovel = [];
    if (r.imovel) {
      const j = r.imovel;
      this.figurasImovel.push(this.linha(j, true, 'MapBiomas (classes de uso)', j.mapBiomas.map(m => ({ rotulo: String(m.ano), src: m.dataUri }))));
      if (r.copernicusConfigurado) {
        this.figurasImovel.push(this.linha(j, true, 'Sentinel-2 — cor real (jun–set)', this.satelite(r, 'cor', j.caixa)));
        this.figurasImovel.push(this.linha(j, true, 'Sentinel-2 — NDVI (verde = vegetação, vermelho = solo)', this.satelite(r, 'ndvi', j.caixa)));
      }
    }
    this.figurasAreas = new Map();
    for (const a of r.areas) {
      if (a.janela.mapBiomas.length === 0) continue;
      const j = a.janela;
      const linhas = [this.linha(j, false, 'MapBiomas', j.mapBiomas.map(m => ({ rotulo: String(m.ano), src: m.dataUri })))];
      if (r.copernicusConfigurado && a.comSatelite) {
        linhas.push(this.linha(j, false, 'Sentinel-2 — cor real', this.satelite(r, 'cor', j.caixa)));
        linhas.push(this.linha(j, false, 'Sentinel-2 — NDVI', this.satelite(r, 'ndvi', j.caixa)));
      }
      this.figurasAreas.set(a.numero, { linhas });
    }

    this.r = r;
  }

  private linha(j: JanelaImagem, imovel: boolean, titulo: string, itens: ItemFigura[]): LinhaFiguras {
    const c = j.caixa;
    const viewBox = [c[0], -c[3], c[2] - c[0], c[3] - c[1]].map(num6).join(' ');
    return { titulo, imovel, janela: j, viewBox, itens };
  }

  private satelite(r: RelatorioConformidade, tipo: 'cor' | 'ndvi', caixa: number[]): ItemFigura[] {
    return r.anosSatelite.map(ano => {
      this.pendentes++;
      const caminho = this.api.caminhoSatelite(r.propriedadeId, tipo, ano, caixa);
      const satelite = urlImagemApi(this.api, caminho).pipe(
        tap(url => { if (url) this.urlsSatelite.push(url); }),
        finalize(() => (this.pendentes = Math.max(0, this.pendentes - 1))),
        startWith(undefined),
        shareReplay(1),
      );
      return { rotulo: String(ano), satelite };
    });
  }

  private limpar(): void {
    this.mapa?.remove();
    this.mapa = undefined;
    this.urlsSatelite.forEach(u => URL.revokeObjectURL(u));
    this.urlsSatelite = [];
    this.pendentes = 0;
  }

  private criarMapa(el: HTMLElement, r: RelatorioConformidade): void {
    const mapa = L.map(el, { zoomControl: true, scrollWheelZoom: false, zoomSnap: 0.25 });
    this.mapa = mapa;
    mapa.attributionControl.setPrefix(false);
    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
      maxZoom: 18, attribution: 'Satélite © Esri · MapBiomas · SICAR',
    }).addTo(mapa);

    const geo = (txt: string | null, estilo: L.PathOptions): L.GeoJSON | null => {
      if (!txt) return null;
      try { return L.geoJSON(JSON.parse(txt), { style: () => estilo, interactive: false }); } catch { return null; }
    };
    const ucs = geo(r.ucsGeoJson, { color: '#7b3fbf', weight: 2, dashArray: '8 5', fillColor: '#7b3fbf', fillOpacity: 0.08 });
    if (ucs) ucs.addTo(mapa);
    const b = r.mapaMudancaLimites;
    if (r.mapaMudanca && b) {
      L.imageOverlay(r.mapaMudanca, [[b[1], b[0]], [b[3], b[2]]], { className: 'rc-pixel', opacity: 0.9 }).addTo(mapa);
    }
    const app = geo(r.geoJsonApp, { color: '#29a3ff', weight: 1.5, fill: false });
    const rl = geo(r.geoJsonReservaLegal, { color: '#ffe45c', weight: 2, dashArray: '5 4', fill: false });
    const imovel = geo(r.geoJson, { color: '#ffffff', weight: 2.5, fill: false });
    [app, rl, imovel].forEach(c => { if (c) c.addTo(mapa); });

    for (const a of r.areas) {
      L.marker([a.lat, a.lon], {
        icon: L.divIcon({ className: 'rc-num rc-num--mapa', html: `<span style="background:${this.corMarco(a.marco)}">${a.numero}</span>`, iconSize: [22, 22] }),
        interactive: false,
      }).addTo(mapa);
    }

    if (imovel) mapa.fitBounds(imovel.getBounds(), { padding: [16, 16] });
    else if (b) mapa.fitBounds([[b[1], b[0]], [b[3], b[2]]]);
  }

  get dica(): string {
    return this.pendentes > 0
      ? `Carregando imagens de satélite… (${this.pendentes} restante${this.pendentes > 1 ? 's' : ''})`
      : 'Pronto: clique em Gerar PDF e escolha "Salvar como PDF" (A4, retrato).';
  }

  ha(v: number | null | undefined): string {
    if (v === null || v === undefined) return '—';
    const x = Math.round(v * 1e6) / 1e6;
    return numero(x, Math.abs(x) < 10 ? 1 : 0) + ' ha';
  }

  pct(a: number, b: number): string {
    return b > 0 ? numero((100 * a) / b, 0) + '%' : '—';
  }

  v(d: HaPorAno | null | undefined, ano: number | null | undefined): number {
    return ano === null || ano === undefined ? 0 : d?.[String(ano)] ?? 0;
  }

  variacao(l: LinhaAreaCar): number {
    const anos = this.r?.anosComparados ?? [];
    if (anos.length === 0) return 0;
    return Math.round((this.v(l.nativaHa, anos[anos.length - 1]) - this.v(l.nativaHa, anos[0])) * 1e6) / 1e6;
  }

  rotuloStatus(s: StatusCriterio): string {
    switch (s) {
      case 'Conforme': return 'Conforme';
      case 'Verificar': return 'Verificar';
      case 'IndicioNaoConformidade': return 'Indício de não conformidade';
      default: return 'Sem dados';
    }
  }

  classeStatus(s: StatusCriterio): string {
    switch (s) {
      case 'Conforme': return 'ok';
      case 'Verificar': return 'ver';
      case 'IndicioNaoConformidade': return 'nc';
      default: return 'sd';
    }
  }

  iconeStatus(s: StatusCriterio): string {
    switch (s) {
      case 'Conforme': return 'check_circle';
      case 'Verificar': return 'error_outline';
      case 'IndicioNaoConformidade': return 'cancel';
      default: return 'help_outline';
    }
  }

  corMarco(m: string): string {
    return m === '2020' ? '#d7261e' : m === '2014' ? '#f07d1a' : '#f2c81d';
  }

  get nativaAtual(): string {
    const t = this.r?.porArea[0];
    const u = this.r?.anoAtual;
    return t && u !== null && u !== undefined ? this.ha(this.v(t.nativaHa, u)) : '—';
  }

  distancia(m: number): string {
    return m < 1000 ? `${Math.round(m)} m` : numero(m / 1000, 1) + ' km';
  }

  raioKm(m: number): string {
    const km = Math.round((m / 1000) * 10) / 10;
    return Number.isInteger(km) ? numero(km, 0) : numero(km, 1);
  }

  coord(v: number): string {
    return v.toFixed(5);
  }

  ndvi(v: number | null): number {
    return v ?? 0;
  }

  ndviTexto(v: number | null): string {
    return numero(v ?? 0, 2);
  }

  ndviAltura(v: number | null): string {
    return Math.round(Math.min(1, Math.max(0, v ?? 0)) * 100) + '%';
  }

  ndviCor(v: number | null): string {
    return classificarNdvi(v).cor;
  }

  mesAno(d: string): string {
    return mesAno(d);
  }

  figurasDaArea(numero: number): LinhaFiguras[] {
    return this.figurasAreas.get(numero)?.linhas ?? [];
  }

  get areasComFiguras() {
    return (this.r?.areas ?? []).filter(a => a.janela.mapBiomas.length > 0);
  }

  get classesTabela() {
    return (this.r?.classes ?? []).slice(0, 12);
  }

  get classesLegenda() {
    return (this.r?.classes ?? []).slice(0, 14);
  }
}

function num6(v: number): string {
  return String(Number(v.toFixed(6)));
}
