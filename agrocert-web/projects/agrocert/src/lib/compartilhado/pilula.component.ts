import { Component, Input } from '@angular/core';
import { Estilo } from '../util/rotulos';

@Component({
  selector: 'ac-pilula',
  standalone: true,
  template: `<span class="cm-pilula" [style.background]="estilo?.bg" [style.color]="estilo?.cor">{{ texto || '-' }}</span>`,
})
export class PilulaComponent {
  @Input() texto: string | null | undefined;
  @Input() estilo: Estilo | null | undefined;
}
