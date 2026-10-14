import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Subscription, forkJoin, of } from 'rxjs';
import { catchError, map, switchMap } from 'rxjs/operators';
import { AgroCertApi } from '../../api/agrocert-api.service';
import { RotaPipe } from '../../util/pipes';
import { imprimirRelatorio } from '../../util/imprimir';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { ClimaCabecalhoComponent, ClimaIndisponivelComponent } from './clima-cabecalho.component';
import * as CF from './clima-formato';
import { AlertaResumo, MesResumo, NivelCalor, NivelChuva, PrevisaoAno, ResumoClimaTela } from './modelos';

interface MesTela extends MesResumo {
  nivelChuva: { classe: string; icone: string; texto: string };
  nivelCalor: { classe: string; icone: string; texto: string };
  listaAlertas: AlertaResumo[];
}

@Component({
  selector: 'ac-clima-resumo',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, RotaPipe, EstadoComponent, ClimaCabecalhoComponent, ClimaIndisponivelComponent],
  templateUrl: './clima-resumo.component.html',
})
export class ClimaResumoComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  readonly cf = CF;

  r: ResumoClimaTela | null = null;
  meses: MesTela[] = [];
  carregando = false;
  erro: string | null = null;
  indisponivel = false;
  endereco = 'http://localhost:3030';
  private maxBarra = 1;
  private sub?: Subscription;

  ngOnInit(): void { this.carregar(); }
  ngOnDestroy(): void { this.sub?.unsubscribe(); }

  carregar(): void {
    this.sub?.unsubscribe();
    this.carregando = true;
    this.erro = null;
    this.indisponivel = false;
    this.r = null;
    this.meses = [];
    this.sub = this.api.clima<ResumoClimaTela>('resumo').pipe(
      switchMap(r => this.completarAlertas(r).pipe(map(alertas => ({ r, alertas })))),
    ).subscribe({
      next: ({ r, alertas }) => {
        this.carregando = false;
        this.r = r;
        this.meses = r.meses.map((m, i) => ({
          ...m,
          nivelChuva: nivelChuva(m.chuva),
          nivelCalor: nivelCalor(m.calor),
          listaAlertas: alertas[i],
        }));
        this.maxBarra = this.meses.length === 0 ? 1 : Math.max(...this.meses.map(m => Math.max(m.chuvaMm, m.chuvaNormalMm)));
      },
      error: e => {
        const x = CF.erroClima(e);
        this.carregando = false;
        this.erro = x.mensagem;
        this.indisponivel = x.indisponivel;
        this.endereco = x.endereco;
      },
    });
  }

  mm(v: number): string {
    const r = CF.arredondar(v / 5) * 5;
    return r < 5 ? CF.n(v, 0) : CF.n(r, 0);
  }

  largura(v: number): string { return CF.f(Math.max(0.5, v / this.maxBarra * 100)) + '%'; }

  imprimir(): void { imprimirRelatorio(); }

  private completarAlertas(r: ResumoClimaTela) {
    const lidos = r.meses.map(m => (m.alertas ?? []).map(a => ({
      alto: a.alto ?? a.item1 ?? false,
      texto: a.texto ?? a.item2 ?? '',
    })));
    const faltando = lidos.some(l => l.some(a => !a.texto));
    if (!faltando) return of(lidos);
    const anos = [...new Set(r.meses.map(m => m.ano))];
    return forkJoin(anos.map(ano => this.api.clima<PrevisaoAno>('previsao', { ano, ciclo: 60, pesoCiclo: 0.5, nivel: 80 }))).pipe(
      map(previsoes => r.meses.map(m => {
        const p = previsoes.flatMap(x => x.meses).find(x => x.mes === m.mes && x.ano === m.ano);
        if (!p) return [];
        return [...p.riscos]
          .sort((a, b) => Number(b.nivel === 'ALTO') - Number(a.nivel === 'ALTO'))
          .map(x => ({ alto: x.nivel === 'ALTO', texto: alertaSimples(x.tipo) }))
          .filter((a): a is AlertaResumo => a.texto !== null);
      })),
      catchError(() => of(lidos.map(l => l.filter(a => a.texto)))),
    );
  }
}

function nivelChuva(n: NivelChuva) {
  switch (n) {
    case 'Menos': return { classe: 'cl-nivel--seco', icone: 'wb_sunny', texto: 'Menos chuva que o normal' };
    case 'Mais': return { classe: 'cl-nivel--chuva', icone: 'thunderstorm', texto: 'Mais chuva que o normal' };
    default: return { classe: 'cl-nivel--ok', icone: 'check_circle', texto: 'Chuva normal' };
  }
}

function nivelCalor(n: NivelCalor) {
  switch (n) {
    case 'MaisQuente': return { classe: 'cl-nivel--quente', icone: 'local_fire_department', texto: 'Mais quente que o normal' };
    case 'MaisFrio': return { classe: 'cl-nivel--frio', icone: 'ac_unit', texto: 'Mais frio que o normal' };
    default: return { classe: 'cl-nivel--ok', icone: 'check_circle', texto: 'Temperatura normal' };
  }
}

function alertaSimples(tipo: string): string | null {
  switch (tipo) {
    case 'SECA': return 'Risco de pouca chuva e veranico. Planeje o plantio e a irrigação.';
    case 'EXCESSO_CHUVA': return 'Risco de chuva demais. Atenção a doenças, erosão e colheita.';
    case 'CALOR': return 'Calor acima do normal. Atenção à água para as plantas e os animais.';
    case 'FRIO': return 'Noites mais frias. Atenção a geadas.';
    case 'UMIDADE_BAIXA': return 'Ar mais seco que o normal.';
    default: return null;
  }
}
