import { Component, ViewEncapsulation, inject } from '@angular/core';
import { ActivatedRoute, RouterOutlet } from '@angular/router';
import { AvisosComponent } from '../compartilhado/avisos';

@Component({
  selector: 'ac-shell',
  standalone: true,
  imports: [RouterOutlet, AvisosComponent],
  encapsulation: ViewEncapsulation.None,
  styleUrls: ['../estilos/certificacao.css', '../estilos/clima.css', '../estilos/corredores.css', '../estilos/agrocert-extra.css'],
  template: `
    <div class="cert-modulo" [class.clima-modulo]="modulo === 'clima'">
      <ac-avisos></ac-avisos>
      <router-outlet></router-outlet>
    </div>
  `,
})
export class ShellComponent {
  readonly modulo: 'certificacao' | 'clima' = inject(ActivatedRoute).snapshot.data['modulo'] ?? 'certificacao';
}
