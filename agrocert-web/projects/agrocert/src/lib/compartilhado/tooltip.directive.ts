import { Directive, HostListener, Input, OnDestroy } from '@angular/core';

let caixa: HTMLDivElement | null = null;

function obterCaixa(): HTMLDivElement {
  if (!caixa) {
    caixa = document.createElement('div');
    caixa.className = 'cm-tooltip';
    document.body.appendChild(caixa);
  }
  return caixa;
}

@Directive({ selector: '[acTip]', standalone: true })
export class TooltipDirective implements OnDestroy {
  @Input('acTip') texto: string | null | undefined;

  @HostListener('mousemove', ['$event'])
  mover(e: MouseEvent): void {
    if (!this.texto) return;
    const tip = obterCaixa();
    tip.textContent = this.texto;
    tip.style.display = 'block';
    let x = e.clientX + 14, y = e.clientY + 14;
    if (x + tip.offsetWidth > window.innerWidth - 8) x = e.clientX - tip.offsetWidth - 14;
    if (y + tip.offsetHeight > window.innerHeight - 8) y = e.clientY - tip.offsetHeight - 14;
    tip.style.left = `${x}px`;
    tip.style.top = `${y}px`;
  }

  @HostListener('mouseleave')
  sair(): void { if (caixa) caixa.style.display = 'none'; }

  ngOnDestroy(): void { this.sair(); }
}
