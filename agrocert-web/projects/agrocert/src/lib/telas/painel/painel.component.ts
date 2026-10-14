import { Component, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { Certificacao, Painel, Requisito } from '../../api/modelos';
import { PilulaComponent } from '../../compartilhado/pilula.component';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { RotaPipe } from '../../util/pipes';
import * as F from '../../util/formato';
import * as R from '../../util/rotulos';

const ICONES: Record<string, string> = {
  sparkle: 'workspace_premium', alerta: 'report_problem', relogio: 'schedule', vistoria: 'fact_check',
};

@Component({
  selector: 'ac-painel',
  standalone: true,
  imports: [NgIf, NgFor, RouterLink, PilulaComponent, EstadoComponent, RotaPipe],
  providers: [RotaPipe],
  templateUrl: './painel.component.html',
})
export class PainelComponent implements OnInit {
  private readonly api = inject(AgroCertApi);
  private readonly router = inject(Router);
  private readonly rota = inject(RotaPipe);

  readonly F = F;
  readonly R = R;
  dados: Painel | null = null;
  erro: string | null = null;

  ngOnInit(): void { this.carregar(); }

  carregar(): void {
    this.erro = null;
    this.dados = null;
    this.api.painel().subscribe({
      next: p => (this.dados = p),
      error: e => (this.erro = mensagemDeErro(e)),
    });
  }

  icone(nome: string): string { return ICONES[nome] ?? 'info'; }
  limitar(v: number): number { return Math.min(100, Math.max(0, v)); }
  maiusculas(t: string | null | undefined): string | null { return t ? t.toLocaleUpperCase('pt-BR') : null; }
  vazio(t: string | null | undefined): string { return t && t.trim() ? t : '—'; }

  referencia(r: Requisito): string {
    const x = r.referencias[0];
    return x ? `${x.norma} · ${x.codigo}` : '—';
  }

  abrir(c: Certificacao): void {
    this.router.navigateByUrl(this.rota.transform(['certificacoes', c.id]));
  }
}
