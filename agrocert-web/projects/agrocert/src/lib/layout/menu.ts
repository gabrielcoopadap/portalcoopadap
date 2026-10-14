import { Component, inject } from '@angular/core';
import { NgFor } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { filter } from 'rxjs';
import { AGROCERT_CONFIG } from '../config';

export interface ItemMenu {
  icone: string;
  texto: string;
  caminho: string;
  exato: boolean;
}

export const ITENS_MENU_CERTIFICACAO: ItemMenu[] = [
  { icone: 'dashboard', texto: 'Gestão de Certificações', caminho: '', exato: true },
  { icone: 'verified', texto: 'Certificações Ativas', caminho: 'certificacoes', exato: false },
  { icone: 'checklist', texto: 'Requisitos', caminho: 'requisitos', exato: false },
  { icone: 'fact_check', texto: 'Vistorias', caminho: 'vistorias', exato: false },
  { icone: 'eco', texto: 'Ambiental', caminho: 'ambiental', exato: false },
  { icone: 'park', texto: 'Corredores Ecológicos', caminho: 'corredores', exato: false },
  { icone: 'forest', texto: 'Fauna e Flora', caminho: 'fauna-flora', exato: false },
];

@Component({
  selector: 'ac-menu-certificacao',
  standalone: true,
  imports: [NgFor, RouterLink, RouterLinkActive],
  template: `
    <div class="menu-grupo" [class.is-aberto]="aberto">
      <button type="button" class="menu-item entry" [attr.aria-expanded]="aberto" aria-controls="submenu-certificacao" (click)="aberto = !aberto">
        <span class="material-icons" aria-hidden="true">workspace_premium</span>
        <span class="menu-text menu-route-text">Certificação</span>
        <span class="material-icons menu-seta" aria-hidden="true">keyboard_arrow_down</span>
      </button>
      <div class="submenu-container" id="submenu-certificacao" [hidden]="!aberto">
        <a *ngFor="let i of itens" class="menu-item entry submenu-item" [routerLink]="link(i)"
           routerLinkActive="active-menu-item" [routerLinkActiveOptions]="{ exact: i.exato }" ariaCurrentWhenActive="page">
          <span class="material-icons" aria-hidden="true">{{ i.icone }}</span>
          <span class="menu-text menu-route-text">{{ i.texto }}</span>
        </a>
      </div>
    </div>
  `,
})
export class MenuCertificacaoComponent {
  private readonly config = inject(AGROCERT_CONFIG);
  private readonly router = inject(Router);
  readonly itens = ITENS_MENU_CERTIFICACAO;
  aberto = this.dentro();

  constructor() {
    this.router.events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(() => { if (this.dentro()) this.aberto = true; });
  }

  link(i: ItemMenu): string {
    return i.caminho ? `${this.config.rotaCertificacao}/${i.caminho}` : this.config.rotaCertificacao;
  }

  private dentro(): boolean {
    return this.router.url.startsWith(this.config.rotaCertificacao);
  }
}

@Component({
  selector: 'ac-menu-clima',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <a class="menu-item entry" [routerLink]="config.rotaClima" routerLinkActive="active-menu-item" ariaCurrentWhenActive="page">
      <span class="material-icons" aria-hidden="true">thermostat</span>
      <span class="menu-text menu-route-text">Clima e El Niño</span>
    </a>
  `,
})
export class MenuClimaComponent {
  readonly config = inject(AGROCERT_CONFIG);
}
