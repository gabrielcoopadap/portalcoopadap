import { Component, HostBinding, Input } from '@angular/core';
import { NgIf } from '@angular/common';
import { GrupoBio } from '../../util/rotulos';
import { ArquivoPipe } from '../../util/pipes';

@Component({
  selector: '[acFfFoto]',
  standalone: true,
  imports: [NgIf, ArquivoPipe],
  template: `
    <img *ngIf="temFoto; else icone" [src]="url | acArquivo" [alt]="alt || ''" loading="lazy" />
    <ng-template #icone><span class="material-icons" aria-hidden="true">{{ grupo?.icone }}</span></ng-template>
  `,
})
export class FotoBioComponent {
  @Input() url: string | null | undefined;
  @Input() grupo: GrupoBio | null | undefined;
  @Input() alt: string | null | undefined;

  @HostBinding('class.ff-foto') readonly ffFoto = true;
  @HostBinding('style.--cor') get cor(): string | null { return this.grupo?.cor ?? null; }

  get temFoto(): boolean { return !!this.url && this.url.trim().length > 0; }
}
