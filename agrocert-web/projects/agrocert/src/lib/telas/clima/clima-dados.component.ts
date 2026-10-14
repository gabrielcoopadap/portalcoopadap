import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { Observable, Subscription } from 'rxjs';
import { switchMap, map } from 'rxjs/operators';
import { AgroCertApi } from '../../api/agrocert-api.service';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { ClimaCabecalhoComponent, ClimaIndisponivelComponent } from './clima-cabecalho.component';
import * as CF from './clima-formato';
import { CorrecaoEnso, ProblemaDado, Qualidade } from './modelos';

const TIPOS = ['ENSO_INCONSISTENTE', 'FORA_DO_PADRAO', 'VALOR_IMPOSSIVEL', 'MES_FALTANDO', 'DUPLICADO'];
const ICONES: Record<string, string> = {
  ENSO_INCONSISTENTE: 'waves', FORA_DO_PADRAO: 'priority_high', VALOR_IMPOSSIVEL: 'block',
  MES_FALTANDO: 'event_busy', DUPLICADO: 'content_copy',
};
const LIMITE = 300;

@Component({
  selector: 'ac-clima-dados',
  standalone: true,
  imports: [NgFor, NgIf, EstadoComponent, ClimaCabecalhoComponent, ClimaIndisponivelComponent],
  templateUrl: './clima-dados.component.html',
})
export class ClimaDadosComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  readonly cf = CF;
  readonly limite = LIMITE;

  q: Qualidade | null = null;
  c: CorrecaoEnso | null = null;
  problemas: ProblemaDado[] = [];
  ensoErrado = 0;
  carregando = false;
  simulando = false;
  gravando = false;
  erro: string | null = null;
  indisponivel = false;
  endereco = 'http://localhost:3030';
  private sub?: Subscription;

  ngOnInit(): void { this.carregar(); }
  ngOnDestroy(): void { this.sub?.unsubscribe(); }

  qtd(tipo: string): number { return this.q?.porTipo?.[tipo] ?? 0; }
  icone(tipo: string): string { return ICONES[tipo] ?? 'info'; }

  carregar(): void {
    this.executar(this.api.clima<Qualidade>('dados').pipe(map(q => ({ q, c: null as CorrecaoEnso | null }))));
  }

  simular(): void {
    this.simulando = true;
    this.corrigir(false);
  }

  aplicar(): void {
    if (!this.c) return;
    if (!window.confirm(`Gravar a correção do ENSO em ${this.c.registrosACorrigir} registro(s) da tabela dados_climaticos?`)) return;
    this.gravando = true;
    this.corrigir(true);
  }

  private corrigir(aplicar: boolean): void {
    this.executar(this.api.corrigirEnso<CorrecaoEnso>(aplicar).pipe(
      switchMap(c => this.api.clima<Qualidade>('dados').pipe(map(q => ({ q, c })))),
    ), false);
  }

  private executar(obs: Observable<{ q: Qualidade; c: CorrecaoEnso | null }>, limpar = true): void {
    this.sub?.unsubscribe();
    this.erro = null;
    this.indisponivel = false;
    if (limpar) {
      this.carregando = true;
      this.q = null;
      this.c = null;
    }
    this.sub = obs.subscribe({
      next: ({ q, c }) => {
        this.fim();
        this.c = c;
        this.montar(q);
      },
      error: e => {
        const x = CF.erroClima(e);
        this.fim();
        this.erro = x.mensagem;
        this.indisponivel = x.indisponivel;
        this.endereco = x.endereco;
        this.q = null;
        this.c = null;
        this.problemas = [];
      },
    });
  }

  private fim(): void { this.carregando = false; this.simulando = false; this.gravando = false; }

  private montar(q: Qualidade): void {
    this.q = q;
    if (!q) { this.problemas = []; this.ensoErrado = 0; return; }
    this.ensoErrado = q.porTipo?.['ENSO_INCONSISTENTE'] ?? 0;
    const ordemTipo = (t: string) => TIPOS.indexOf(t);
    this.problemas = [...(q.problemas ?? [])]
      .sort((a, b) => (ordemTipo(a.tipo) - ordemTipo(b.tipo)) || (a.ano - b.ano) || (a.mes - b.mes))
      .slice(0, LIMITE);
  }
}
