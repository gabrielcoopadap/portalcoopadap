import { Component, Injectable, inject } from '@angular/core';
import { NgFor } from '@angular/common';
import { mensagemDeErro } from '../api/agrocert-api.service';

export interface Aviso {
  id: number;
  tipo: 'ok' | 'erro' | 'info';
  texto: string;
}

@Injectable({ providedIn: 'root' })
export class AvisosService {
  lista: Aviso[] = [];
  private seq = 0;

  ok(texto: string): void { this.mostrar('ok', texto, 6000); }
  info(texto: string): void { this.mostrar('info', texto, 8000); }
  erro(e: unknown): void { this.mostrar('erro', typeof e === 'string' ? e : mensagemDeErro(e), 12000); }

  fechar(id: number): void { this.lista = this.lista.filter(a => a.id !== id); }

  private mostrar(tipo: Aviso['tipo'], texto: string, ms: number): void {
    const a = { id: ++this.seq, tipo, texto };
    this.lista = [...this.lista.filter(x => x.texto !== texto), a];
    setTimeout(() => this.fechar(a.id), ms);
  }
}

@Component({
  selector: 'ac-avisos',
  standalone: true,
  imports: [NgFor],
  template: `
    <div class="ac-avisos" aria-live="polite">
      <div *ngFor="let a of avisos.lista" class="cm-alerta" [class.cm-alerta--ok]="a.tipo === 'ok'"
           [class.cm-alerta--erro]="a.tipo === 'erro'" [class.cm-alerta--info]="a.tipo === 'info'"
           [attr.role]="a.tipo === 'erro' ? 'alert' : 'status'">
        <span class="material-icons" aria-hidden="true">{{ a.tipo === 'ok' ? 'check_circle' : a.tipo === 'erro' ? 'error_outline' : 'info' }}</span>
        <div style="flex:1">{{ a.texto }}</div>
        <button type="button" class="cm-icone-btn" aria-label="Fechar aviso" (click)="avisos.fechar(a.id)">
          <span class="material-icons" aria-hidden="true">close</span>
        </button>
      </div>
    </div>
  `,
})
export class AvisosComponent {
  readonly avisos = inject(AvisosService);
}
