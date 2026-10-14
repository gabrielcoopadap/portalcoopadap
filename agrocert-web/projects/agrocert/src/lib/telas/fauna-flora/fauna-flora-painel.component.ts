import { AfterViewInit, Component, ElementRef, NgZone, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import * as L from 'leaflet';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { RegistroBio } from '../../api/modelos';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { ModalComponent } from '../../compartilhado/modal.component';
import { PilulaComponent } from '../../compartilhado/pilula.component';
import { TooltipDirective } from '../../compartilhado/tooltip.directive';
import { ArquivoPipe, RotaPipe } from '../../util/pipes';
import { dataHora, mesAno, data as dataBr } from '../../util/formato';
import {
  COR_FAUNA, COR_FLORA, Estilo, GRUPOS_FAUNA, GRUPOS_FLORA, GrupoBio, corSuave, grupo, rotuloOrigem, resumoOrigem,
} from '../../util/rotulos';
import { FotoBioComponent } from './foto.component';
import { ModalRegistroComponent } from './modal-registro.component';
import { EspecieDestaque, PainelFaunaFlora } from './modelos';

const F = (v: number) => String(Math.round(v * 10) / 10);
const esc = (s: unknown) => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]!);
const pctVar = (v: number) => Math.abs(v).toLocaleString('pt-BR', { maximumFractionDigits: 4 });

interface CartaoRegistro { r: RegistroBio; g: GrupoBio; foto: string | null; pilula: Estilo; meta: string; quando: string; }
interface SwitchGrupo { reino: string; g: GrupoBio; chave: string; }
interface PontoMapa {
  especieId: number; lat: number; lon: number; reino: string; grupo: string; ts: number; marcador: L.Marker;
}

const LW = 360, LH = 178, M_ESQ = 28, M_DIR = 26, M_TOPO = 12, M_BASE = 22;
const R = 52, C = 2 * Math.PI * R;

@Component({
  selector: 'ac-fauna-flora-painel',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, RouterLink, EstadoComponent, ModalComponent, PilulaComponent, TooltipDirective,
    FotoBioComponent, ModalRegistroComponent, RotaPipe, ArquivoPipe],
  templateUrl: './fauna-flora-painel.component.html',
})
export class FaunaFloraPainelComponent implements OnInit, AfterViewInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly zona = inject(NgZone);
  private sub?: Subscription;
  private carga?: Subscription;

  @ViewChild('mapaEl', { static: true }) mapaEl!: ElementRef<HTMLDivElement>;
  @ViewChild('modalRegistro', { static: true }) modalRegistro!: ModalRegistroComponent;

  dados: PainelFaunaFlora | null = null;
  erro: string | null = null;
  meses = 6;
  boasPraticas = false;

  destaquesHoje: CartaoRegistro[] = [];
  cameras: CartaoRegistro[] = [];
  slide = 0;
  abaDestaque: 'fauna' | 'flora' = 'fauna';
  paineisDestaque: { chave: 'fauna' | 'flora'; lista: EspecieDestaque[] }[] = [];

  readonly LW = LW;
  readonly LH = LH;
  readonly raio = R;
  readonly corFauna = COR_FAUNA;
  readonly corFlora = COR_FLORA;
  graf = this.montarGrafico([]);
  fatias: { cor: string; dash: string; offset: string; tip: string }[] = [];
  totalFatias = 0;

  readonly grupoExploreFauna = GRUPOS_FAUNA[1];
  readonly grupoExploreFlora = GRUPOS_FLORA[3];
  readonly grupoExploreBoas = GRUPOS_FLORA[0];
  readonly mesAno = mesAno;
  readonly pctVar = pctVar;

  private mapa: L.Map | null = null;
  private areas: Record<'car' | 'rl' | 'app', L.FeatureGroup> | null = null;
  private camadaPontos: L.FeatureGroup | null = null;
  private pontos: PontoMapa[] = [];
  private vistaInicial = false;
  readonly switches: SwitchGrupo[] = [
    ...GRUPOS_FAUNA.map(g => ({ reino: 'FAUNA', g, chave: `FAUNA|${g.nome}` })),
    ...GRUPOS_FLORA.map(g => ({ reino: 'FLORA', g, chave: `FLORA|${g.nome}` })),
  ];
  reinoAtivo = 'FAUNA';
  desligados: Record<string, boolean> = {};
  diasPeriodo = 90;
  modo: 'ultimo' | 'todos' = 'ultimo';
  areasVisiveis = { car: true, rl: true, app: true };
  contagem: Record<string, number> = {};
  todosLigados = true;

  ngOnInit(): void {
    this.sub = this.rota.queryParamMap.subscribe(q => {
      this.meses = Number(q.get('meses')) === 12 ? 12 : 6;
      this.carregar();
    });
  }

  ngAfterViewInit(): void {
    this.criarMapa();
    if (this.dados) this.montarPontosEAreas();
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    this.carga?.unsubscribe();
    this.mapa?.remove();
    this.mapa = null;
  }

  carregar(): void {
    this.erro = null;
    this.carga?.unsubscribe();
    this.carga = this.api.painelFaunaFlora<PainelFaunaFlora>(this.meses).subscribe({
      next: d => this.receber(d),
      error: e => { this.erro = mensagemDeErro(e); },
    });
  }

  trocarMeses(m: number): void {
    this.router.navigate([], { relativeTo: this.rota, queryParams: { meses: m }, queryParamsHandling: 'merge' });
  }

  mostrarSlide(n: number): void {
    if (!this.cameras.length) return;
    this.slide = (n + this.cameras.length) % this.cameras.length;
  }

  grupoDe(d: EspecieDestaque): GrupoBio { return grupo(d.especie.reino, d.especie.grupo); }

  private receber(d: PainelFaunaFlora): void {
    this.dados = d;
    const cartao = (r: RegistroBio): CartaoRegistro => {
      const g = grupo(r.reino, r.grupo);
      return {
        r, g, foto: r.fotoUrl ?? r.especieFotoUrl, pilula: { bg: corSuave(g.cor, '22'), cor: 'rgba(0,0,0,.75)' },
        meta: resumoOrigem(r.origem, r.reino), quando: dataHora(r.dataHora),
      };
    };
    this.destaquesHoje = d.destaquesHoje.map(cartao);
    this.cameras = d.cameras.map(cartao);
    this.slide = Math.min(this.slide, Math.max(this.cameras.length - 1, 0));
    this.paineisDestaque = [{ chave: 'fauna', lista: d.destaquesFauna }, { chave: 'flora', lista: d.destaquesFlora }];
    this.graf = this.montarGrafico(d.evolucao);
    this.montarRosca(d);
    if (this.mapa) this.montarPontosEAreas();
  }

  private montarGrafico(evo: PainelFaunaFlora['evolucao']) {
    const maxBruto = evo.length === 0 ? 0 : Math.max(...evo.map(p => Math.max(p.fauna, p.flora)));
    const passos = [5, 10, 20, 25, 50, 100, 200, 250, 500, 1000];
    const passo = passos.find(s => s * 4 >= maxBruto) ?? 1000;
    const maxY = Math.max(passo * 4, 4);
    const n = evo.length;
    const LX = (i: number) => n <= 1 ? (M_ESQ + LW - M_DIR) / 2 : M_ESQ + (LW - M_ESQ - M_DIR) * i / (n - 1);
    const LY = (v: number) => M_TOPO + (LH - M_TOPO - M_BASE) * (1 - v / maxY);
    const linha = (sel: (p: { fauna: number; flora: number }) => number) => evo.map((p, i) => `${F(LX(i))},${F(LY(sel(p)))}`).join(' ');
    const larg = n > 1 ? (LW - M_ESQ - M_DIR) / (n - 1) : 40;
    const ticks = [0, 1, 2, 3, 4].map(t => {
      const v = Math.trunc(maxY * t / 4);
      return { v, y: F(LY(v)), yTexto: F(LY(v) + 3) };
    });
    const ult = n > 0 ? evo[n - 1] : null;
    return {
      mEsq: F(M_ESQ), xFim: F(LW - M_DIR), xRotuloY: F(M_ESQ - 6), mTopo: F(M_TOPO), yBase: F(LH - M_BASE), yMes: F(LH - 6),
      larg: F(larg),
      ticks,
      areaFauna: n === 0 ? '' : `${F(LX(0))},${F(LY(0))} ${linha(p => p.fauna)} ${F(LX(n - 1))},${F(LY(0))}`,
      linhaFauna: linha(p => p.fauna),
      linhaFlora: linha(p => p.flora),
      pontos: evo.map((p, i) => ({
        x: F(LX(i)), xHit: F(LX(i) - larg / 2), yFauna: F(LY(p.fauna)), yFlora: F(LY(p.flora)),
        mes: mesAno(p.mes).split('/')[0],
        tip: `${mesAno(p.mes)}\nFauna: ${p.fauna} registros\nFlora: ${p.flora} registros`,
      })),
      ultimo: ult ? { x: F(LX(n - 1) + 7), yFauna: F(LY(ult.fauna) + 3), yFlora: F(LY(ult.flora) + 3), fauna: ult.fauna, flora: ult.flora } : null,
    };
  }

  private montarRosca(d: PainelFaunaFlora): void {
    const lista = d.distribuicaoFauna;
    this.totalFatias = lista.reduce((s, f) => s + f.quantidade, 0);
    let acumulado = 0;
    this.fatias = this.totalFatias === 0 ? [] : lista.map(f => {
      const comp = C * f.quantidade / this.totalFatias;
      const visivel = Math.max(comp - (lista.length > 1 ? 2 : 0), 0.5);
      const fatia = {
        cor: f.cor, dash: `${F(visivel)} ${F(C - visivel)}`, offset: F(-acumulado),
        tip: `${f.grupo}: ${f.quantidade} espécie(s) · ${f.percentual}%`,
      };
      acumulado += comp;
      return fatia;
    });
  }

  private criarMapa(): void {
    this.zona.runOutsideAngular(() => {
      const mapa = L.map(this.mapaEl.nativeElement, { zoomControl: true, zoomSnap: 0.25 }).setView([-19.2, -46.24], 13);
      L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
        maxZoom: 19, attribution: 'Tiles &copy; Esri &mdash; Esri, Maxar, Earthstar Geographics, and the GIS User Community',
      }).addTo(mapa);
      L.control.scale({ imperial: false }).addTo(mapa);
      this.areas = { car: L.featureGroup(), rl: L.featureGroup(), app: L.featureGroup() };
      (Object.keys(this.areas) as ('car' | 'rl' | 'app')[]).forEach(k => this.areas![k].addTo(mapa));
      this.camadaPontos = L.featureGroup().addTo(mapa);

      mapa.on('click', (e: L.LeafletMouseEvent) => {
        const lat = e.latlng.lat.toFixed(6), lon = e.latlng.lng.toFixed(6);
        const div = document.createElement('div');
        div.className = 'ff-popup';
        div.style.minWidth = '0';
        div.innerHTML = `<div><b>Registrar ocorrência aqui?</b><span>${lat}, ${lon}</span>` +
          '<button type="button" class="cm-btn cm-btn--primario cm-btn--pequeno" style="margin-top:6px">Novo registro</button></div>';
        div.querySelector('button')!.addEventListener('click', () => {
          mapa.closePopup();
          this.zona.run(() => this.modalRegistro.abrir(lat, lon));
        });
        L.popup().setLatLng(e.latlng).setContent(div).openOn(mapa);
      });
      this.mapa = mapa;
    });
    setTimeout(() => this.mapa?.invalidateSize(), 0);
  }

  private geo(json: string | null, estilo: L.PathOptions): L.GeoJSON | null {
    if (!json) return null;
    try { return L.geoJSON(JSON.parse(json), { style: () => estilo }); } catch { return null; }
  }

  private montarPontosEAreas(): void {
    const d = this.dados;
    if (!d || !this.mapa || !this.areas) return;
    this.zona.runOutsideAngular(() => {
      const areas = this.areas!;
      Object.values(areas).forEach(a => a.clearLayers());
      d.propriedades.forEach(a => {
        const c = this.geo(a.geoJson, { color: '#9be27a', weight: 2.5, fillOpacity: 0.04 });
        const r = this.geo(a.geoJsonReservaLegal, { color: '#2f7a54', weight: 1.5, fillColor: '#5e9a42', fillOpacity: 0.28 });
        const p = this.geo(a.geoJsonApp, { color: '#1f6fb2', weight: 1.5, fillColor: '#2a78d6', fillOpacity: 0.28 });
        if (c) { c.bindTooltip(esc(a.nome)); areas.car.addLayer(c); }
        if (r) { r.bindTooltip('Reserva Legal — ' + esc(a.nome)); areas.rl.addLayer(r); }
        if (p) { p.bindTooltip('APP — ' + esc(a.nome)); areas.app.addLayer(p); }
      });

      const ordenados = d.registrosMapa
        .filter(r => r.latitude !== null && r.longitude !== null)
        .map(r => ({ r, ts: Date.parse(r.dataHora as unknown as string) }))
        .sort((a, b) => b.ts - a.ts);
      this.pontos = ordenados.map(({ r, ts }) => {
        const g = grupo(r.reino, r.grupo);
        const icone = L.divIcon({
          className: 'ff-pin-wrap',
          html: `<span class="ff-pin" style="background:${g.cor}"><span class="material-icons">${g.icone}</span></span>`,
          iconSize: [30, 30], iconAnchor: [15, 15], popupAnchor: [0, -14],
        });
        const url = this.api.arquivo(r.fotoUrl ?? r.especieFotoUrl);
        const foto = url
          ? `<img src="${esc(url)}" alt="">`
          : `<span class="ff-foto" style="--cor:${g.cor}"><span class="material-icons">${g.icone}</span></span>`;
        const marcador = L.marker([r.latitude!, r.longitude!], { icon: icone, title: r.especie ?? '', keyboard: true })
          .bindPopup(`<div class="ff-popup">${foto}<div><b>${esc(r.especie)}</b><span><i>${esc(r.nomeCientifico)}</i></span>` +
            `<span>Registro: ${esc(dataBr(r.dataHora))}</span><span>Área: ${esc(r.area || '-')}</span><span>${esc(rotuloOrigem(r.origem))}</span></div></div>`);
        return { especieId: r.especieId, lat: r.latitude!, lon: r.longitude!, reino: r.reino ?? '', grupo: g.nome, ts, marcador };
      });
    });
    this.atualizarMapa();

    if (!this.vistaInicial) {
      this.vistaInicial = true;
      const limites = this.areas.car.getLayers().length ? this.areas.car.getBounds() : this.camadaPontos!.getBounds();
      setTimeout(() => {
        this.mapa?.invalidateSize();
        if (limites.isValid()) this.mapa?.fitBounds(limites, { padding: [20, 20] });
      }, 0);
    }
  }

  atualizarMapa(): void {
    if (!this.camadaPontos) return;
    const camada = this.camadaPontos;
    camada.clearLayers();
    const limite = this.diasPeriodo > 0 ? Date.now() - this.diasPeriodo * 86_400_000 : 0;
    const contagem: Record<string, number> = {};
    const vistos = new Set<number>();
    for (const p of this.pontos) {
      if (p.reino !== this.reinoAtivo || p.ts < limite) continue;
      if (this.modo === 'ultimo') {
        if (vistos.has(p.especieId)) continue;
        vistos.add(p.especieId);
      }
      contagem[p.grupo] = (contagem[p.grupo] || 0) + 1;
      if (this.desligados[`${p.reino}|${p.grupo}`]) continue;
      camada.addLayer(p.marcador);
    }
    this.contagem = contagem;
    this.todosLigados = this.switches.filter(s => s.reino === this.reinoAtivo).every(s => !this.desligados[s.chave]);
  }

  trocarReinoMapa(reino: string): void {
    this.reinoAtivo = reino;
    this.atualizarMapa();
  }

  alternarGrupo(chave: string, ligado: boolean): void {
    this.desligados[chave] = !ligado;
    this.atualizarMapa();
  }

  alternarTodos(ligado: boolean): void {
    this.switches.filter(s => s.reino === this.reinoAtivo).forEach(s => this.desligados[s.chave] = !ligado);
    this.atualizarMapa();
  }

  alternarArea(k: 'car' | 'rl' | 'app', ligado: boolean): void {
    this.areasVisiveis[k] = ligado;
    if (!this.mapa || !this.areas) return;
    if (ligado) this.areas[k].addTo(this.mapa); else this.mapa.removeLayer(this.areas[k]);
  }
}
