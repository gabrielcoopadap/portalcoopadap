import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { Subscription } from 'rxjs';
import { AgroCertApi } from '../../api/agrocert-api.service';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { TooltipDirective } from '../../compartilhado/tooltip.directive';
import { ClimaCabecalhoComponent, ClimaIndisponivelComponent } from './clima-cabecalho.component';
import * as CF from './clima-formato';
import { EnsoTela, ImpactoEnso, ImpactoLinha, SituacaoEnso } from './modelos';

const W = 640, H = 250, ME = 40, MD = 12, MT = 18, MB = 34;

interface Ponto { ano: number; mes: number; oni: number; inc: number; proj: boolean; desc: string; }

interface Celula { texto: string; cor: string; classe: string; dica: string | null; }

const COLUNAS: { variavel: string; nino: boolean; titulo: string }[] = [
  { variavel: 'chuva', nino: true, titulo: 'El Niño' }, { variavel: 'chuva', nino: false, titulo: 'La Niña' },
  { variavel: 'maxima', nino: true, titulo: 'El Niño' }, { variavel: 'maxima', nino: false, titulo: 'La Niña' },
  { variavel: 'minima', nino: true, titulo: 'El Niño' }, { variavel: 'minima', nino: false, titulo: 'La Niña' },
];

@Component({
  selector: 'ac-clima-enso',
  standalone: true,
  imports: [NgFor, NgIf, EstadoComponent, TooltipDirective, ClimaCabecalhoComponent, ClimaIndisponivelComponent],
  templateUrl: './clima-enso.component.html',
})
export class ClimaEnsoComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  readonly cf = CF;
  readonly W = W; readonly H = H; readonly ME = ME; readonly MD = MD; readonly MT = MT; readonly MB = MB;
  readonly colunas = COLUNAS;

  s: SituacaoEnso | null = null;
  imp: ImpactoEnso | null = null;
  carregando = false;
  erro: string | null = null;
  indisponivel = false;
  endereco = 'http://localhost:3030';

  larg = '0';
  grade: { y: string; yTexto: string; rotulo: string; cor: string }[] = [];
  yMais = '0';
  yMenos = '0';
  divisa: { x: string; xa: string; xb: string } | null = null;
  barras: { d: string; cor: string; x: string; xHit: string; inc: { y1: string; y2: string } | null; letra: string; ano: number | null; dica: string }[] = [];
  rotulos: { x: string; y: string; texto: string }[] = [];
  mapa: { mes: number; celulas: Celula[] }[] = [];

  private sub?: Subscription;

  ngOnInit(): void { this.carregar(); }
  ngOnDestroy(): void { this.sub?.unsubscribe(); }

  carregar(): void {
    this.sub?.unsubscribe();
    this.carregando = true;
    this.erro = null;
    this.indisponivel = false;
    this.s = null;
    this.imp = null;
    this.sub = this.api.clima<EnsoTela>('enso').subscribe({
      next: t => { this.carregando = false; this.montar(t); },
      error: e => {
        const x = CF.erroClima(e);
        this.carregando = false;
        this.erro = x.mensagem;
        this.indisponivel = x.indisponivel;
        this.endereco = x.endereco;
      },
    });
  }

  private montar(t: EnsoTela): void {
    const s = t?.situacao ?? null;
    const imp = t?.impacto ?? null;
    this.s = s;
    this.imp = imp;
    if (!s || !imp) return;

    const pontos: Ponto[] = [];
    for (const o of s.ultimos_12_meses ?? []) {
      if (o.oni === null || o.oni === undefined) continue;
      pontos.push({ ano: o.ano, mes: o.mes, oni: o.oni, inc: 0, proj: false, desc: `${CF.nomeFase(o.fase)} (ONI ${CF.s(o.oni)})` });
    }
    for (const o of s.proximos_12_meses ?? []) pontos.push({ ano: o.ano, mes: o.mes, oni: o.oni, inc: o.incerteza, proj: true, desc: o.descricao });

    const n = Math.max(pontos.length, 1);
    const larg = (W - ME - MD) / n;
    this.larg = CF.f(larg);
    const X = (i: number) => ME + larg * i + larg / 2;
    const lim = pontos.length === 0 ? 1 : Math.max(...pontos.map(p => Math.abs(p.oni) + p.inc));
    const topo = Math.max(1.0, Math.ceil(lim * 2) / 2);
    const Y = (v: number) => MT + (H - MT - MB) * (topo - v) / (2 * topo);
    const y0 = Y(0);
    const primeiroProj = pontos.findIndex(p => p.proj);

    this.grade = [];
    for (let v = -topo; v <= topo + 1e-6; v += 0.5) {
      this.grade.push({ y: CF.f(Y(v)), yTexto: CF.f(Y(v) + 3), rotulo: CF.s(v), cor: Math.abs(v) < 1e-6 ? '#c3c2b7' : '#f0f0f0' });
    }
    this.yMais = CF.f(Y(0.5));
    this.yMenos = CF.f(Y(-0.5));
    const xd = ME + larg * primeiroProj;
    this.divisa = primeiroProj > 0 ? { x: CF.f(xd), xa: CF.f(xd - 5), xb: CF.f(xd + 5) } : null;

    this.barras = pontos.map((p, i) => {
      const bw = Math.min(20, larg * 0.62);
      const cor = p.oni >= 0 ? (p.proj ? '#f0a2a1' : '#e34948') : (p.proj ? '#9ec5f4' : '#2a78d6');
      return {
        d: CF.barraDivergente(X(i) - bw / 2, bw, y0, Y(p.oni), p.oni >= 0),
        cor,
        x: CF.f(X(i)),
        xHit: CF.f(X(i) - larg / 2),
        inc: p.proj && p.inc > 0 ? { y1: CF.f(Y(p.oni + p.inc)), y2: CF.f(Y(p.oni - p.inc)) } : null,
        letra: CF.mesCurto(p.mes).slice(0, 1),
        ano: p.mes === 1 || i === 0 ? p.ano : null,
        dica: `${CF.nomeMes(p.mes)}/${p.ano} · ${p.proj ? 'projetado' : 'observado'}\n${p.desc}` + (p.proj ? `\nIncerteza: ±${CF.n(p.inc, 2)}` : ''),
      };
    });

    this.rotulos = [];
    let u = -1;
    pontos.forEach((p, i) => { if (!p.proj) u = i; });
    if (u >= 0) {
      const pu = pontos[u];
      this.rotulos.push({ x: CF.f(X(u)), y: CF.f(pu.oni >= 0 ? Y(pu.oni) - 5 : Y(pu.oni) + 12), texto: CF.s(pu.oni) });
    }
    let pico = -1;
    pontos.forEach((p, i) => { if (p.proj && (pico < 0 || Math.abs(p.oni) > Math.abs(pontos[pico].oni))) pico = i; });
    if (pico >= 0) {
      const pp = pontos[pico];
      this.rotulos.push({ x: CF.f(X(pico)), y: CF.f(pp.oni >= 0 ? Y(pp.oni + pp.inc) - 5 : Y(pp.oni - pp.inc) + 12), texto: CF.s(pp.oni) });
    }

    const linhas = imp.linhas ?? [];
    const L = (mes: number, v: string) => linhas.find(l => l.mes === mes && l.variavel === v) ?? null;
    this.mapa = [];
    for (let mes = 1; mes <= 12; mes++) {
      this.mapa.push({ mes, celulas: COLUNAS.map(c => celula(mes, c, L(mes, c.variavel))) });
    }
  }
}

function celula(mes: number, c: { variavel: string; nino: boolean }, l: ImpactoLinha | null): Celula {
  if (!l) return { texto: '—', cor: '', classe: 'cl-centro', dica: null };
  const chuva = c.variavel === 'chuva';
  const valor = chuva ? (c.nino ? l.elNinoModeradoPct : l.laNinaModeradaPct) : (c.nino ? l.elNinoModerado : l.laNinaModerada);
  const bruto = c.nino ? l.elNinoModerado : l.laNinaModerada;
  const evid = c.nino ? l.evidenciaElNino : l.evidenciaLaNina;
  const t = c.nino ? l.t_el_nino : l.t_la_nina;
  const anos = c.nino ? l.anosElNino : l.anosLaNina;
  const v = valor ?? 0;
  const quente = chuva ? v < 0 : v > 0;
  const zero = Math.abs(v) < (chuva ? 0.5 : 0.05);
  const cor = zero ? 'transparent' : CF.corDivergente(v, chuva ? 20 : 0.8, quente);
  const texto = zero ? '—' : (evid === 'fraca' ? '~' : '') + (chuva ? CF.s(v, 0) + '%' : CF.s(v, 2));
  const fase = c.nino ? 'El Niño' : 'La Niña';
  const dica = `${CF.nomeMes(mes)} · ${fase} moderado(a)\n` +
    `${CF.nomeVariavel(c.variavel)}: ${chuva ? CF.s(v, 0) + '% (' + CF.s(bruto, 0) + ' mm)' : CF.s(v, 2) + ' °C'} em relação à normal (${CF.n(l.normal, chuva ? 0 : 1)} ${CF.unidade(c.variavel)})\n` +
    `Evidência ${evid} (t = ${CF.n(t, 1)}; ${anos} meses de ${fase} na série)`;
  const classe = ['cl-centro', 'cl-celula', evid === 'fraca' || zero ? 'is-fraca' : '', evid === 'forte' ? 'is-forte' : ''].filter(Boolean).join(' ');
  return { texto, cor, classe, dica };
}
