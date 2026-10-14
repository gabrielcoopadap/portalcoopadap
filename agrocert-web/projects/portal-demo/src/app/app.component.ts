import { Component, HostListener, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { AgroCertApi, MenuCertificacaoComponent, MenuClimaComponent } from 'agrocert';
import * as M from './menu-portal';
import { InstalarAppService } from './instalar-app.service';

@Component({
  selector: 'demo-root',
  standalone: true,
  imports: [NgIf, NgFor, RouterOutlet, RouterLink, RouterLinkActive, MenuCertificacaoComponent, MenuClimaComponent],
  templateUrl: './app.component.html',
})
export class AppComponent {
  usuario = 'USUÁRIO';
  menuUsuario = false;
  aberto: Record<string, boolean> = { atendimentos: false, planejamento: false, servicos: false, comunicacao: false };
  readonly m = M;
  readonly app = inject(InstalarAppService);
  ajudaIos = false;

  constructor() {
    inject(AgroCertApi).sessao().subscribe({ next: s => { if (s.nome) this.usuario = s.nome.toUpperCase(); }, error: () => {} });
    inject(Router).events.pipe(filter(e => e instanceof NavigationEnd)).subscribe(e => {
      const url = (e as NavigationEnd).urlAfterRedirects;
      if (url.startsWith('/certificacao') || url.startsWith('/clima')) this.aberto['atendimentos'] = true;
      const dentro = (lista: M.ItemPortal[]) => lista.some(i => url.startsWith('/' + i.caminho));
      if (dentro(M.PLANEJAMENTO)) this.aberto['atendimentos'] = this.aberto['planejamento'] = true;
      if (dentro(M.SERVICOS)) this.aberto['atendimentos'] = this.aberto['servicos'] = true;
      if (dentro([M.MEU_ESTOQUE, M.MINHAS_VENDAS, M.RELATORIOS, M.ROMANEIO, M.VISITA_TECNICA])) this.aberto['atendimentos'] = true;
      if (dentro(M.COMUNICACAO)) this.aberto['comunicacao'] = true;
      this.menuUsuario = false;
      if (window.innerWidth < 960) document.body.classList.remove('menu-aberto');
    });
  }

  alternarMenu(): void {
    document.body.classList.toggle(window.innerWidth < 960 ? 'menu-aberto' : 'menu-recolhido');
  }

  alternar(grupo: string): void {
    this.aberto[grupo] = !this.aberto[grupo];
  }

  @HostListener('document:click', ['$event'])
  fora(e: MouseEvent): void {
    if (!(e.target as HTMLElement).closest('.pt-conta')) this.menuUsuario = false;
  }

  async instalar(): Promise<void> {
    this.menuUsuario = false;
    if (!(await this.app.instalar())) this.ajudaIos = true;
  }

  sair(): void {
    this.menuUsuario = false;
    window.location.href = '/';
  }
}
