import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { AgroCertApi } from '../../api/agrocert-api.service';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { TooltipDirective } from '../../compartilhado/tooltip.directive';
import { ClimaCabecalhoComponent, ClimaIndisponivelComponent } from './clima-cabecalho.component';
import * as CF from './clima-formato';
import { ResumoValidacao, Validacao, ValidacaoMes } from './modelos';

const W = 360, H = 190, ME = 36, MD = 8, MT = 14, MB = 24;
const LARG = (W - ME - MD) / 12;
const X = (mes: number) => ME + LARG * (mes - 1) + LARG / 2;
const ORDEM = ['chuva', 'maxima', 'minima', 'temp_ambiente', 'umidade_relativa'];
const PAINEIS: [string, string][] = [['chuva', 'Chuva'], ['maxima', 'Temperatura máxima']];

interface LinhaVariavel { nome: string; r: ResumoValidacao; u: string; casas: number; icone: string; }

interface Painel {
  titulo: string;
  grade: { y: string; yTexto: string; rotulo: string; cor: string }[];
  barras: { d: string; x: string; letra: string; rotulo: { y: string; texto: string } | null; dica: string; xHit: string }[];
}

@Component({
  selector: 'ac-clima-validacao',
  standalone: true,
  imports: [NgFor, NgIf, FormsModule, EstadoComponent, TooltipDirective, ClimaCabecalhoComponent, ClimaIndisponivelComponent],
  templateUrl: './clima-validacao.component.html',
})
export class ClimaValidacaoComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly cf = CF;
  readonly W = W; readonly H = H; readonly ME = ME; readonly MD = MD; readonly MT = MT; readonly MB = MB;
  readonly larg = CF.f(LARG);
  readonly opcoes = [3, 6, 9, 12];

  antecedencia = 6;
  selecionada: number | null = 6;

  v: Validacao | null = null;
  carregando = false;
  erro: string | null = null;
  indisponivel = false;
  endereco = 'http://localhost:3030';

  linhas: LinhaVariavel[] = [];
  paineis: Painel[] = [];
  porFase: { variavel: string; fase: string; r: ResumoValidacao }[] = [];

  private subRota?: Subscription;
  private sub?: Subscription;

  ngOnInit(): void {
    this.subRota = this.rota.queryParamMap.subscribe(q => {
      const t = q.get('antecedencia');
      this.antecedencia = t !== null && /^\s*[-+]?\d+\s*$/.test(t) ? parseInt(t, 10) : 6;
      this.selecionada = this.opcoes.includes(this.antecedencia) ? this.antecedencia : this.opcoes[0];
      this.carregar();
    });
  }

  ngOnDestroy(): void { this.subRota?.unsubscribe(); this.sub?.unsubscribe(); }

  mudarAntecedencia(): void {
    this.router.navigate([], { relativeTo: this.rota, queryParams: { antecedencia: this.selecionada }, queryParamsHandling: 'merge' });
  }

  ganho(g: number): string { return g >= 0.05 ? `erra ${CF.n(g)}% menos` : g <= -0.05 ? `erra ${CF.n(-g)}% mais` : 'empata'; }

  carregar(): void {
    this.sub?.unsubscribe();
    this.carregando = true;
    this.erro = null;
    this.indisponivel = false;
    this.v = null;
    this.sub = this.api.clima<Validacao>('validacao', { antecedencia: Math.min(12, Math.max(1, this.antecedencia)) }).subscribe({
      next: v => { this.carregando = false; this.montar(v); },
      error: e => {
        const x = CF.erroClima(e);
        this.carregando = false;
        this.erro = x.mensagem;
        this.indisponivel = x.indisponivel;
        this.endereco = x.endereco;
      },
    });
  }

  private montar(v: Validacao): void {
    this.v = v;
    if (!v) return;
    const porVariavel = v.porVariavel ?? {};
    this.linhas = ORDEM.filter(o => porVariavel[o]).map(nome => {
      const r = porVariavel[nome];
      const g = r.ganhoVsClimatologiaPct;
      return {
        nome, r, u: CF.unidade(nome),
        casas: nome === 'chuva' || nome === 'umidade_relativa' ? 1 : 2,
        icone: g >= 0.05 ? 'arrow_downward' : g <= -0.05 ? 'arrow_upward' : 'remove',
      };
    });

    const porMes = (variavel: string): { mes: number; d: ValidacaoMes }[] =>
      Object.entries(v.porMes ?? {})
        .filter(([k]) => k.startsWith(variavel + '|'))
        .map(([k, d]) => ({ mes: parseInt(k.split('|')[1], 10), d }))
        .sort((a, b) => a.mes - b.mes);
    const todos = PAINEIS.flatMap(([variavel]) => porMes(variavel)).map(t => Math.abs(t.d.ganhoPct));
    const lim = Math.max(5, todos.length ? Math.max(...todos) : 5);
    const topo = Math.ceil(lim / 5) * 5;
    const Y = (g: number) => MT + (H - MT - MB) * (topo - g) / (2 * topo);

    this.paineis = PAINEIS.map(([variavel, titulo]) => {
      const grade = [];
      for (let g = -topo; g <= topo + 1e-6; g += topo / 2) {
        grade.push({ y: CF.f(Y(g)), yTexto: CF.f(Y(g) + 3), rotulo: CF.s(g, 0) + '%', cor: Math.abs(g) < 1e-6 ? '#c3c2b7' : '#f0f0f0' });
      }
      const u = CF.unidade(variavel);
      const barras = porMes(variavel).map(({ mes, d }) => {
        const bw = Math.min(16, LARG * 0.6);
        return {
          d: CF.barraDivergente(X(mes) - bw / 2, bw, Y(0), Y(d.ganhoPct), d.ganhoPct >= 0),
          x: CF.f(X(mes)),
          letra: CF.mesCurto(mes).slice(0, 1),
          rotulo: Math.abs(d.ganhoPct) >= topo * 0.4
            ? { y: CF.f(d.ganhoPct >= 0 ? Y(d.ganhoPct) - 4 : Y(d.ganhoPct) + 11), texto: CF.s(d.ganhoPct, 0) }
            : null,
          dica: `${CF.nomeMes(mes)} · ${titulo}\nErro só com a média: ${CF.n(d.erroClimatologia, 2)} ${u}\nErro com ENSO: ${CF.n(d.erroEnso, 2)} ${u}\nGanho: ${CF.s(d.ganhoPct)}%`,
          xHit: CF.f(X(mes) - LARG / 2),
        };
      });
      return { titulo, grade, barras };
    });

    const idx = (k: string) => ORDEM.indexOf(k.split('|')[0]);
    this.porFase = Object.entries(v.porFase ?? {})
      .sort(([a], [b]) => (idx(a) - idx(b)) || (a < b ? -1 : a > b ? 1 : 0))
      .map(([k, r]) => { const p = k.split('|'); return { variavel: p[0], fase: p[1], r }; });
  }
}
