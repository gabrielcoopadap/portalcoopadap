import { Component, ElementRef, NgZone, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, forkJoin } from 'rxjs';
import * as L from 'leaflet';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { Alerta, LeituraNdvi, RelatorioAmbiental } from '../../api/modelos';
import { AvisosService } from '../../compartilhado/avisos';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { AGROCERT_CONFIG } from '../../config';
import { ArquivoPipe, RotaPipe } from '../../util/pipes';
import { data, lerData, mesAno, numero } from '../../util/formato';
import { classificarNdvi, rotuloStatusAlerta } from '../../util/rotulos';

type ChaveCamada = 'limiteCar' | 'reservaLegal' | 'app' | 'alertasDesmatamento' | 'ndviMapaCalor';

const CAMADAS: { chave: ChaveCamada; rotulo: string; cor: string }[] = [
  { chave: 'limiteCar', rotulo: 'Limite do CAR', cor: '#5e9a42' },
  { chave: 'reservaLegal', rotulo: 'Reserva Legal', cor: '#2f7a54' },
  { chave: 'app', rotulo: 'APP', cor: '#2f6fb2' },
  { chave: 'alertasDesmatamento', rotulo: 'Alertas de desmatamento', cor: '#c0392b' },
  { chave: 'ndviMapaCalor', rotulo: 'Mapa de calor NDVI', cor: '#d9a441' },
];

const ESTILO_CAR: L.PathOptions = { color: '#5e9a42', weight: 2.5, fillOpacity: 0.05 };
const ESTILO_RL: L.PathOptions = { color: '#2f7a54', weight: 1, fillOpacity: 0.18 };
const ESTILO_APP: L.PathOptions = { color: '#2f6fb2', weight: 1, fillOpacity: 0.18 };
const ESTILO_ALERTA: L.PathOptions = { color: '#c0392b', weight: 2, fillColor: '#e57373', fillOpacity: 0.5 };

const G = { L: 640, A: 170, mTopo: 12, mBase: 26, mEsq: 8, mDir: 8 };

interface PontoNdvi {
  leitura: LeituraNdvi;
  x: string;
  y: string;
  cor: string;
  sel: boolean;
  mes: string;
  rotulo: string;
}

interface GrupoAno { ano: string; alertas: Alerta[]; }

const f1 = (n: number) => String(Math.round(n * 10) / 10);
const f3 = (n: number | null | undefined) => (n === null || n === undefined ? '-' : n.toFixed(3));
const mesIso = (d: string) => d.slice(0, 7);
const esc = (s: unknown) => String(s ?? '-').replace(/[&<>"']/g, c =>
  ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);

@Component({
  selector: 'ac-propriedade-detalhe',
  standalone: true,
  imports: [NgIf, NgFor, RouterLink, EstadoComponent, RotaPipe, ArquivoPipe],
  styles: [`
    .cm-mapa { position: relative; z-index: 0; }
    .cm-lt-lista { min-width: 0; }
    .cm-lt-item { max-width: 100%; box-sizing: border-box; }
  `],
  templateUrl: './propriedade-detalhe.component.html',
})
export class PropriedadeDetalheComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly zone = inject(NgZone);
  private readonly config = inject(AGROCERT_CONFIG);
  private subs = new Subscription();
  private carga?: Subscription;

  readonly camadas = CAMADAS;
  readonly G = G;
  readonly linhasRef = [0, 0.2, 0.5, 0.8, 1].map(v => f1(this.y(v)));
  readonly numero = numero;
  readonly data = data;
  readonly mesAno = mesAno;
  readonly mesIso = mesIso;
  readonly f3 = f3;
  readonly abs = Math.abs;
  readonly rotuloStatusAlerta = rotuloStatusAlerta;

  id = 0;
  mes: string | null = null;
  r: RelatorioAmbiental | null = null;
  leituras: LeituraNdvi[] = [];
  erro: string | null = null;

  atual: LeituraNdvi | null = null;
  anterior: LeituraNdvi | null = null;
  classe = classificarNdvi(null);
  variacao: number | null = null;
  nuvensAltas = false;
  pontos: PontoNdvi[] = [];
  linhaNdvi = '';
  comCorReal: LeituraNdvi[] = [];
  linhaDoTempo: GrupoAno[] = [];

  alertaAberto: Alerta | null = null;
  sincAlertas = false;
  sincNdvi = false;
  abrindoPdf = false;
  excluindo = false;

  visivel: Record<ChaveCamada, boolean> = { limiteCar: true, reservaLegal: true, app: true, alertasDesmatamento: true, ndviMapaCalor: true };
  private mapa: L.Map | null = null;
  private elMapa: HTMLDivElement | null = null;
  private camadasMapa: Partial<Record<ChaveCamada, L.Layer | null>> = {};

  @ViewChild('mapa') set refMapa(ref: ElementRef<HTMLDivElement> | undefined) {
    const el = ref?.nativeElement ?? null;
    if (el === this.elMapa) return;
    this.destruirMapa();
    this.elMapa = el;
    if (el) setTimeout(() => this.montarMapa(el));
  }

  ngOnInit(): void {
    this.subs.add(this.rota.paramMap.subscribe(p => {
      const id = Number(p.get('id'));
      if (id !== this.id) {
        this.id = id;
        this.r = null;
        this.leituras = [];
        this.alertaAberto = null;
        this.carregar();
      }
    }));
    this.subs.add(this.rota.queryParamMap.subscribe(q => {
      this.mes = q.get('mes');
      this.selecionarLeitura();
      this.atualizarNdviNoMapa();
    }));
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    this.carga?.unsubscribe();
    this.destruirMapa();
  }

  carregar(): void {
    this.erro = null;
    this.carga?.unsubscribe();
    this.carga = forkJoin({ ficha: this.api.fichaAmbiental(this.id), ndvi: this.api.ndvi(this.id) }).subscribe({
      next: ({ ficha, ndvi }) => {
        this.r = ficha;
        this.leituras = [...ndvi].sort((a, b) => a.dataImagem.localeCompare(b.dataImagem));
        this.montarLinhaDoTempo();
        this.selecionarLeitura();
        if (this.alertaAberto) this.alertaAberto = ficha.alertas.find(a => a.id === this.alertaAberto!.id) ?? null;
        this.atualizarCamadas();
      },
      error: e => {
        if (this.r) this.avisos.erro(e);
        else this.erro = mensagemDeErro(e);
      },
    });
  }

  private selecionarLeitura(): void {
    const l = this.leituras;
    const sel = l.find(x => mesIso(x.dataImagem) === this.mes) ?? (l.length ? l[l.length - 1] : null);
    const i = sel ? l.indexOf(sel) : -1;
    this.atual = sel;
    this.anterior = i > 0 ? l[i - 1] : null;
    this.classe = classificarNdvi(sel?.ndviMedio);
    this.variacao = sel?.ndviMedio != null && this.anterior?.ndviMedio != null ? sel.ndviMedio - this.anterior.ndviMedio : null;
    this.nuvensAltas = sel?.percentualAmostrasValidas != null && sel.percentualAmostrasValidas < 40;
    this.comCorReal = l.filter(x => x.imagemCorRealUrl);

    const passo = l.length > 1 ? (G.L - G.mEsq - G.mDir) / (l.length - 1) : 0;
    const x = (i: number) => (l.length === 1 ? G.L / 2 : G.mEsq + passo * i);
    this.pontos = l.map((leitura, i) => ({
      leitura,
      x: f1(x(i)),
      y: f1(this.y(leitura.ndviMedio)),
      cor: classificarNdvi(leitura.ndviMedio).cor,
      sel: !!sel && leitura.id === sel.id,
      mes: mesIso(leitura.dataImagem),
      rotulo: mesAno(leitura.dataImagem),
    }));
    this.linhaNdvi = this.pontos.map(p => `${p.x},${p.y}`).join(' ');
  }

  private y(v: number | null | undefined): number {
    const areaA = G.A - G.mTopo - G.mBase;
    return G.mTopo + areaA * (1 - Math.min(1, Math.max(0, v ?? 0)));
  }

  selecionarMes(mes: string, ev?: Event): void {
    ev?.preventDefault();
    this.router.navigate([], { relativeTo: this.rota, queryParams: { mes }, queryParamsHandling: 'merge' });
  }

  private montarLinhaDoTempo(): void {
    const com = (this.r?.alertas ?? []).filter(a => a.imagemAntesUrl || a.imagemDepoisUrl);
    const grupos = new Map<string, Alerta[]>();
    for (const a of com) {
      const ano = lerData(a.dataDeteccao)?.getFullYear().toString() ?? 'Sem data';
      grupos.set(ano, [...(grupos.get(ano) ?? []), a]);
    }
    this.linhaDoTempo = [...grupos.entries()]
      .sort(([x], [y]) => (x < y ? -1 : x > y ? 1 : 0))
      .map(([ano, alertas]) => ({
        ano,
        alertas: [...alertas].sort((a, b) => (a.dataDeteccao ?? '0000').localeCompare(b.dataDeteccao ?? '0000')),
      }));
  }

  sincronizarAlertas(): void {
    this.sincAlertas = true;
    this.api.sincronizarAlertas(this.id).subscribe({
      next: alertas => {
        this.sincAlertas = false;
        this.avisos.ok(`MapBiomas Alerta consultado: ${alertas.length} alerta(s) dentro do perímetro do CAR.`);
        this.carregar();
      },
      error: e => {
        this.sincAlertas = false;
        this.avisos.erro(e);
      },
    });
  }

  sincronizarNdvi(): void {
    this.sincNdvi = true;
    this.api.sincronizarNdvi(this.id).subscribe({
      next: leituras => {
        this.sincNdvi = false;
        this.avisos.ok(`NDVI atualizado: ${leituras.length} mês(es) com leitura.`);
        this.carregar();
      },
      error: e => {
        this.sincNdvi = false;
        this.avisos.erro(e);
      },
    });
  }

  abrirFicha(): void {
    this.abrindoPdf = true;
    this.api.abrirFichaPdf(this.id).subscribe({
      next: () => (this.abrindoPdf = false),
      error: e => {
        this.abrindoPdf = false;
        this.avisos.erro(e);
      },
    });
  }

  excluir(): void {
    if (!window.confirm('Excluir esta propriedade e seus alertas?')) return;
    this.excluindo = true;
    this.api.excluirPropriedade(this.id).subscribe({
      next: () => {
        this.avisos.ok('Propriedade excluída.');
        this.router.navigate([this.config.rotaCertificacao, 'ambiental']);
      },
      error: e => {
        this.excluindo = false;
        this.avisos.erro(e);
      },
    });
  }

  private montarMapa(el: HTMLDivElement): void {
    if (el !== this.elMapa || this.mapa) return;
    this.zone.runOutsideAngular(() => {
      const mapa = L.map(el, { zoomControl: true }).setView([-19.0, -46.5], 12);
      L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 19, attribution: 'Tiles &copy; Esri &mdash; Esri, Maxar, Earthstar Geographics, and the GIS User Community',
      }).addTo(mapa);
      L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 19,
      }).addTo(mapa);
      this.mapa = mapa;
    });
    this.atualizarCamadas(true);
  }

  private destruirMapa(): void {
    this.mapa?.remove();
    this.mapa = null;
    this.camadasMapa = {};
  }

  private geo(json: string | null | undefined, estilo: L.PathOptions): L.GeoJSON | null {
    if (!json) return null;
    try { return L.geoJSON(JSON.parse(json), { style: () => estilo }); } catch { return null; }
  }

  private atualizarCamadas(enquadrar = false): void {
    const mapa = this.mapa;
    const r = this.r;
    if (!mapa || !r) return;
    this.zone.runOutsideAngular(() => {
      for (const c of Object.values(this.camadasMapa)) if (c) mapa.removeLayer(c);
      const alertas = L.featureGroup();
      for (const a of r.alertas) {
        const g = this.geo(a.geoJson, ESTILO_ALERTA);
        if (!g) continue;
        g.bindPopup(`Alerta ${esc(a.codigoAlerta || '')} — ${esc(numero(a.areaHa))} ha (${esc(data(a.dataDeteccao))})`);
        g.on('click', () => this.zone.run(() => this.mostrarAlerta(a)));
        alertas.addLayer(g);
      }
      this.camadasMapa = {
        limiteCar: this.geo(r.geoJson, ESTILO_CAR),
        reservaLegal: this.geo(r.geoJsonReservaLegal, ESTILO_RL),
        app: this.geo(r.geoJsonApp, ESTILO_APP),
        alertasDesmatamento: alertas,
      };
      for (const k of ['limiteCar', 'reservaLegal', 'app', 'alertasDesmatamento'] as ChaveCamada[]) {
        const c = this.camadasMapa[k];
        if (c && this.visivel[k]) c.addTo(mapa);
      }
      const car = this.camadasMapa.limiteCar as L.GeoJSON | null;
      if (enquadrar && car) mapa.fitBounds(car.getBounds(), { padding: [20, 20] });
    });
    this.atualizarNdviNoMapa();
  }

  private atualizarNdviNoMapa(): void {
    const mapa = this.mapa;
    if (!mapa) return;
    this.zone.runOutsideAngular(() => {
      const antigo = this.camadasMapa.ndviMapaCalor;
      if (antigo) mapa.removeLayer(antigo);
      const a = this.atual;
      let nova: L.ImageOverlay | null = null;
      if (a?.imagemMapaCalorUrl && a.bboxMinLon != null && a.bboxMinLat != null && a.bboxMaxLon != null && a.bboxMaxLat != null) {
        nova = L.imageOverlay(this.api.arquivo(a.imagemMapaCalorUrl)!, [[a.bboxMinLat, a.bboxMinLon], [a.bboxMaxLat, a.bboxMaxLon]], { opacity: 0.85 });
        if (this.visivel.ndviMapaCalor) nova.addTo(mapa);
      }
      this.camadasMapa.ndviMapaCalor = nova;
    });
  }

  alternarCamada(chave: ChaveCamada, ligada: boolean): void {
    this.visivel[chave] = ligada;
    const c = this.camadasMapa[chave];
    if (!c || !this.mapa) return;
    if (ligada) c.addTo(this.mapa); else this.mapa.removeLayer(c);
  }

  private mostrarAlerta(a: Alerta): void {
    this.alertaAberto = a;
    this.redimensionar();
  }

  fecharAlerta(): void {
    this.alertaAberto = null;
    this.redimensionar();
  }

  private redimensionar(): void {
    setTimeout(() => this.mapa?.invalidateSize(), 50);
  }
}
