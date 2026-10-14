import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgIf } from '@angular/common';

@Component({
  selector: 'ac-estado',
  standalone: true,
  imports: [NgIf],
  template: `
    <div *ngIf="carregando" class="ac-carregando" role="status">
      <span class="ac-giro" aria-hidden="true"></span> {{ texto }}
    </div>
    <div *ngIf="erro" class="cm-alerta cm-alerta--erro" role="alert">
      <span class="material-icons" aria-hidden="true">error_outline</span>
      <div style="flex:1">{{ erro }}</div>
      <button *ngIf="tentarDeNovo.observed" type="button" class="cm-btn cm-btn--contorno cm-btn--pequeno" (click)="tentarDeNovo.emit()">
        <span class="material-icons" aria-hidden="true">refresh</span> Tentar de novo
      </button>
    </div>
  `,
})
export class EstadoComponent {
  @Input() carregando = false;
  @Input() erro: string | null = null;
  @Input() texto = 'Carregando...';
  @Output() tentarDeNovo = new EventEmitter<void>();
}
