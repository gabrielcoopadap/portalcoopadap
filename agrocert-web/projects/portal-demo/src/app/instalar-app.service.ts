import { Injectable, NgZone, inject } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { filter } from 'rxjs';

interface PedidoInstalacao extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

declare global {
  interface Window { __pedidoInstalacao?: PedidoInstalacao; }
  interface Navigator { standalone?: boolean; }
}

@Injectable({ providedIn: 'root' })
export class InstalarAppService {
  private readonly zona = inject(NgZone);
  private pedido: PedidoInstalacao | null = window.__pedidoInstalacao ?? null;

  instalado = window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
  versaoNova = false;

  readonly ios = /iphone|ipad|ipod/i.test(navigator.userAgent)
    || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1); // iPad se apresenta como Mac

  constructor() {
    window.addEventListener('beforeinstallprompt', e => {
      e.preventDefault();
      this.zona.run(() => (this.pedido = e as PedidoInstalacao));
    });
    window.addEventListener('appinstalled', () => this.zona.run(() => { this.instalado = true; this.pedido = null; }));

    const sw = inject(SwUpdate);
    if (sw.isEnabled) {
      sw.versionUpdates
        .pipe(filter((e): e is VersionReadyEvent => e.type === 'VERSION_READY'))
        .subscribe(() => (this.versaoNova = true));
      const conferir = () => sw.checkForUpdate().catch(() => {});
      document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') conferir(); });
      this.zona.runOutsideAngular(() => setInterval(conferir, 30 * 60 * 1000));
    }
  }

  get disponivel(): boolean {
    return !this.instalado && (this.pedido !== null || this.ios);
  }

  async instalar(): Promise<boolean> {
    if (!this.pedido) return false;
    const pedido = this.pedido;
    await pedido.prompt();
    const { outcome } = await pedido.userChoice;
    this.pedido = null; // o mesmo pedido só pode ser usado uma vez
    if (outcome === 'accepted') this.instalado = true;
    return true;
  }

  atualizar(): void {
    document.location.reload();
  }
}
