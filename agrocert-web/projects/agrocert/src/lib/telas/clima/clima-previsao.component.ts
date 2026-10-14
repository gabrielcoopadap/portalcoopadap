import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { AgroCertApi } from '../../api/agrocert-api.service';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { TooltipDirective } from '../../compartilhado/tooltip.directive';
import { ClimaCabecalhoComponent, ClimaIndisponivelComponent } from './clima-cabecalho.component';
import * as CF from './clima-formato';
import { Faixa, ParametrosPrevisao, PrevisaoAno, PrevisaoMes, Risco } from './modelos';

const W = 760, H = 262, ME = 44, MD = 12, MT = 12, MB = 62;
const HT = 230, MBT = 26;
const LARG = (W - ME - MD) / 12;
const X = (mes: number) => ME + LARG * (mes - 1) + LARG / 2;

interface Grade { y: string; rotulo: string; yTexto: string; cor: string; }

interface BarraChuva {
  mes: PrevisaoMes;
  x: string; xTexto: string; barra: string;
  yMax: string; yMin: string; xa: string; xb: string;
  yNormal: string; xn0: string; xn1: string;
  obs: { cx: string; cy: string } | null;
  mesCurto: string; yMes: string;
  xFase: string; yFase: string; classeFase: string; yOni: string; oni: string;
  dica: string; xHit: string;
}

interface PontoTemp { x: string; yMax: string; yMin: string; mesCurto: string; dica: string; xHit: string; }

interface LinhaRisco { mes: PrevisaoMes; risco: Risco; icone: string; rotulo: string; alto: boolean; }

@Component({
  selector: 'ac-clima-previsao',
  standalone: true,
  imports: [NgFor, NgIf, FormsModule, EstadoComponent, TooltipDirective, ClimaCabecalhoComponent, ClimaIndisponivelComponent],
  templateUrl: './clima-previsao.component.html',
})
export class ClimaPrevisaoComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly cf = CF;
  readonly W = W; readonly H = H; readonly HT = HT; readonly ME = ME; readonly MD = MD; readonly MT = MT; readonly MBT = MBT;
  readonly larg = CF.f(LARG);
  readonly pesos = [0, 0.25, 0.5, 0.75, 1];
  readonly niveis = [80, 90, 95];

  p: ParametrosPrevisao = padrao();
  form: { ano: number | null; oni: string; ciclo: number | null; pesoCiclo: number; nivel: number } = { ano: null, oni: '', ciclo: 60, pesoCiclo: 0.5, nivel: 80 };

  m: PrevisaoAno | null = null;
  meses: PrevisaoMes[] = [];
  carregando = false;
  erro: string | null = null;
  indisponivel = false;
  endereco = 'http://localhost:3030';

  nivelTxt = '';
  pico = '-';
  chuvaAno = 0;
  normalAno = 0;
  riscos: LinhaRisco[] = [];
  altos = 0;
  gradeChuva: Grade[] = [];
  barras: BarraChuva[] = [];
  baseC = '0';
  yOniEixo = '0';
  gradeTemp: Grade[] = [];
  pontosTemp: PontoTemp[] = [];
  temp: { faixaMax: string; faixaMin: string; normalMax: string; normalMin: string; linhaMax: string; linhaMin: string; xRotulo: string; yRotuloMax: string; yRotuloMin: string } | null = null;

  private subRota?: Subscription;
  private sub?: Subscription;

  ngOnInit(): void {
    this.subRota = this.rota.queryParamMap.subscribe(q => {
      this.p = lerParametros(q);
      this.form = { ano: this.p.ano, oni: this.p.oni ?? '', ciclo: this.p.ciclo, pesoCiclo: this.p.pesoCiclo, nivel: this.p.nivel };
      this.carregar();
    });
  }

  ngOnDestroy(): void { this.subRota?.unsubscribe(); this.sub?.unsubscribe(); }

  mesmoPeso = (a: number, b: number) => Math.abs(a - b) < 0.001;

  filtrar(): void {
    const f = this.form;
    const destino = this.router.createUrlTree([], {
      relativeTo: this.rota,
      queryParams: { ano: f.ano ?? null, oni: f.oni?.trim() ? f.oni : null, ciclo: f.ciclo ?? null, pesoCiclo: f.pesoCiclo, nivel: f.nivel },
      queryParamsHandling: 'merge',
    });
    if (this.router.serializeUrl(destino) === this.router.url) this.carregar();
    else this.router.navigateByUrl(destino);
  }

  carregar(): void {
    this.sub?.unsubscribe();
    this.carregando = true;
    this.erro = null;
    this.indisponivel = false;
    this.m = null;
    this.meses = [];
    const p = this.p;
    this.nivelTxt = p.nivel + '%';
    this.sub = this.api.clima<PrevisaoAno>('previsao', { ano: p.ano, oni: p.oni, ciclo: p.ciclo, pesoCiclo: p.pesoCiclo, nivel: p.nivel }).subscribe({
      next: m => { this.carregando = false; this.montar(m); },
      error: e => {
        const x = CF.erroClima(e);
        this.carregando = false;
        this.erro = x.mensagem;
        this.indisponivel = x.indisponivel;
        this.endereco = x.endereco;
      },
    });
  }

  private montar(m: PrevisaoAno): void {
    const meses = m.meses ?? [];
    this.m = m;
    this.meses = meses;
    this.nivelTxt = arredondarC(m.nivelIntervalo * 100) + '%';
    const picoMes = meses.reduce<PrevisaoMes | null>((a, x) => (a === null || Math.abs(x.enso.oni) > Math.abs(a.enso.oni) ? x : a), null);
    this.pico = picoMes ? picoMes.enso.descricao : '-';
    this.chuvaAno = meses.reduce((a, x) => a + x.chuva.valor, 0);
    this.normalAno = meses.reduce((a, x) => a + x.chuva.normal, 0);
    this.riscos = meses
      .flatMap(x => x.riscos.map(r => ({ mes: x, risco: r, alto: r.nivel === 'ALTO', ...CF.risco(r.tipo) })))
      .sort((a, b) => (Number(b.alto) - Number(a.alto)) || (a.mes.mes - b.mes.mes));
    this.altos = this.riscos.filter(r => r.alto).length;

    const maxChuva = meses.length === 0 ? 100 : Math.max(...meses.map(x => Math.max(x.chuva.max, x.chuva.normal, x.chuva.observado ?? 0)));
    const passoC = CF.passo(maxChuva);
    const topoC = passoC * Math.ceil(maxChuva / passoC);
    const YC = (v: number) => MT + (H - MT - MB) * (1 - v / topoC);
    const baseC = YC(0);
    this.baseC = CF.f(baseC);
    this.yOniEixo = CF.f(baseC + 34.5);
    this.gradeChuva = [];
    for (let t = 0; t <= Math.round(topoC / passoC); t++) {
      const v = passoC * t;
      this.gradeChuva.push({ y: CF.f(YC(v)), yTexto: CF.f(YC(v) + 3), rotulo: CF.n(v, 0), cor: t === 0 ? '#c3c2b7' : '#ececec' });
    }
    this.barras = meses.map(x => {
      const c = x.chuva;
      const bw = Math.min(26, LARG * 0.52);
      const bx = X(x.mes) - bw / 2;
      const proj = x.enso.origem === 'modelo' || x.enso.origem === 'climatologia';
      return {
        mes: x,
        x: CF.f(X(x.mes)), xTexto: CF.f(X(x.mes)),
        barra: CF.barraArredondada(bx, baseC, YC(c.valor), bw),
        yMax: CF.f(YC(c.max)), yMin: CF.f(YC(c.min)), xa: CF.f(X(x.mes) - 4), xb: CF.f(X(x.mes) + 4),
        yNormal: CF.f(YC(c.normal)), xn0: CF.f(bx - 5), xn1: CF.f(bx + bw + 5),
        obs: c.observado !== null && c.observado !== undefined ? { cx: CF.f(X(x.mes) + bw / 2 + 9), cy: CF.f(YC(c.observado)) } : null,
        mesCurto: CF.mesCurto(x.mes), yMes: CF.f(baseC + 14),
        xFase: CF.f(X(x.mes) - 19), yFase: CF.f(baseC + 22),
        classeFase: `cl-fase-svg ${CF.classeFase(x.enso.fase)} ${proj ? 'cl-fase--proj' : ''}`,
        yOni: CF.f(baseC + 34.5), oni: CF.s(x.enso.oni),
        dica: `${CF.nomeMes(x.mes)}/${x.ano} · ${x.enso.descricao}\n` +
          `Prevista: ${CF.n(c.valor, 0)} mm (faixa ${CF.n(c.min, 0)}–${CF.n(c.max, 0)})\n` +
          `Normal: ${CF.n(c.normal, 0)} mm` + (temValor(c.anomaliaPct) ? ` · ${CF.s(c.anomaliaPct!, 0)}%` : '') + '\n' +
          `Efeito do ENSO: ${CF.s(c.efeitoEnso, 0)} mm` + (temValor(c.efeitoCiclo) ? ` · ciclo: ${CF.s(c.efeitoCiclo!, 0)} mm` : '') + '\n' +
          (temValor(c.observado) ? `Observado: ${CF.n(c.observado!, 0)} mm\n` : '') +
          `Chance abaixo / normal / acima: ${CF.pct(c.probAbaixoNormal)} / ${CF.pct(c.probDentroNormal)} / ${CF.pct(c.probAcimaNormal)}`,
        xHit: CF.f(X(x.mes) - LARG / 2),
      };
    });

    const tMin = meses.length === 0 ? 0 : Math.min(...meses.map(x => Math.min(x.minima.min, x.minima.normal)));
    const tMax = meses.length === 0 ? 30 : Math.max(...meses.map(x => Math.max(x.maxima.max, x.maxima.normal)));
    const passoT = CF.passo(tMax - tMin, 6);
    const yT0 = passoT * Math.floor((tMin - 0.5) / passoT);
    const yT1 = passoT * Math.ceil((tMax + 0.5) / passoT);
    const YT = (v: number) => MT + (HT - MT - MBT) * (1 - (v - yT0) / (yT1 - yT0));
    this.gradeTemp = [];
    for (let v = yT0; v <= yT1 + 1e-6; v += passoT) {
      this.gradeTemp.push({ y: CF.f(YT(v)), yTexto: CF.f(YT(v) + 3), rotulo: CF.n(v, 0), cor: '#ececec' });
    }
    const linha = (sel: (x: PrevisaoMes) => number) => meses.map(x => `${CF.f(X(x.mes))},${CF.f(YT(sel(x)))}`).join(' ');
    const faixa = (sel: (x: PrevisaoMes) => Faixa) =>
      meses.map(x => `${CF.f(X(x.mes))},${CF.f(YT(sel(x).max))}`).join(' ') + ' ' +
      [...meses].reverse().map(x => `${CF.f(X(x.mes))},${CF.f(YT(sel(x).min))}`).join(' ');
    const ult = meses[meses.length - 1];
    this.temp = ult ? {
      faixaMax: faixa(x => x.maxima), faixaMin: faixa(x => x.minima),
      normalMax: linha(x => x.maxima.normal), normalMin: linha(x => x.minima.normal),
      linhaMax: linha(x => x.maxima.valor), linhaMin: linha(x => x.minima.valor),
      xRotulo: CF.f(X(ult.mes) + 8), yRotuloMax: CF.f(YT(ult.maxima.valor) + 3), yRotuloMin: CF.f(YT(ult.minima.valor) + 3),
    } : null;
    this.pontosTemp = meses.map(x => ({
      x: CF.f(X(x.mes)), yMax: CF.f(YT(x.maxima.valor)), yMin: CF.f(YT(x.minima.valor)), mesCurto: CF.mesCurto(x.mes),
      xHit: CF.f(X(x.mes) - LARG / 2),
      dica: `${CF.nomeMes(x.mes)}/${x.ano} · ${x.enso.descricao}\n` +
        `Máxima: ${CF.n(x.maxima.valor)} °C (normal ${CF.n(x.maxima.normal)}, faixa ${CF.n(x.maxima.min)}–${CF.n(x.maxima.max)})\n` +
        `Mínima: ${CF.n(x.minima.valor)} °C (normal ${CF.n(x.minima.normal)}, faixa ${CF.n(x.minima.min)}–${CF.n(x.minima.max)})\n` +
        `Efeito do ENSO: máx ${CF.s(x.maxima.efeitoEnso, 2)} °C · mín ${CF.s(x.minima.efeitoEnso, 2)} °C` +
        (temValor(x.maxima.observado) ? `\nObservado: máx ${CF.n(x.maxima.observado!)} °C · mín ${CF.n(x.minima.observado ?? 0)} °C` : ''),
    }));
  }
}

function temValor(v: number | null | undefined): boolean { return v !== null && v !== undefined; }

function arredondarC(v: number): string { return String(CF.arredondar(v)); }

function padrao(): ParametrosPrevisao {
  return { ano: new Date().getFullYear(), oni: null, ciclo: 60, pesoCiclo: 0.5, nivel: 80 };
}

function lerParametros(q: ParamMap): ParametrosPrevisao {
  const inteiro = (k: string): number | null => {
    const v = q.get(k);
    return v !== null && /^\s*[-+]?\d+\s*$/.test(v) ? parseInt(v, 10) : null;
  };
  const real = (k: string): number | null => {
    const v = q.get(k);
    return v !== null && v.trim() !== '' && isFinite(Number(v)) ? Number(v) : null;
  };
  const ano = inteiro('ano');
  const ciclo = inteiro('ciclo') ?? 60;
  const peso = real('pesoCiclo') ?? 0.5;
  const nivel = inteiro('nivel') ?? 80;
  const oni = q.get('oni');
  return {
    ano: ano !== null && ano >= 1900 && ano <= 2200 ? ano : new Date().getFullYear(),
    oni: oni && oni.trim() !== '' ? oni : null,
    ciclo: Math.min(200, Math.max(1, ciclo)),
    pesoCiclo: Math.min(1, Math.max(0, peso)),
    nivel: nivel === 80 || nivel === 90 || nivel === 95 ? nivel : 80,
  };
}
