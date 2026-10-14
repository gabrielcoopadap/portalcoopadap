import { Component } from '@angular/core';
import { NgFor } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LISTA_RELATORIOS } from './menu-portal';

@Component({
  selector: 'demo-relatorios',
  standalone: true,
  imports: [NgFor, RouterLink],
  template: `
    <div class="pt-tela">
      <h1 class="pt-tela__titulo"><span class="material-icons">insert_chart</span> Relatórios</h1>
      <div class="pt-relatorios">
        <a *ngFor="let r of relatorios" class="pt-relatorio" [routerLink]="'/' + r.caminho">
          <span class="material-icons">insert_chart</span>
          <span>{{ r.texto }}</span>
        </a>
      </div>
    </div>
  `,
})
export class RelatoriosComponent {
  readonly relatorios = LISTA_RELATORIOS;
}
