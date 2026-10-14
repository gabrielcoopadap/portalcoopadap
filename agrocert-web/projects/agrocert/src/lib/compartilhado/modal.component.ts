import { Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, ViewChild } from '@angular/core';
import { NgIf } from '@angular/common';

@Component({
  selector: 'ac-modal',
  standalone: true,
  imports: [NgIf],
  template: `
    <dialog #dlg class="cm-modal" [class]="classe" (cancel)="$event.preventDefault(); fechar.emit()" (click)="cliqueFundo($event)"
            [attr.aria-label]="titulo">
      <div class="cm-modal__cab">
        <h2 class="cm-modal__titulo" style="display:flex;align-items:center;gap:8px">
          <span *ngIf="icone" class="material-icons" aria-hidden="true">{{ icone }}</span>{{ titulo }}
        </h2>
        <div class="cm-acoes">
          <ng-content select="[acoes-cabecalho]"></ng-content>
          <button type="button" class="cm-icone-btn" aria-label="Fechar" (click)="fechar.emit()">
            <span class="material-icons" aria-hidden="true">close</span>
          </button>
        </div>
      </div>
      <ng-content></ng-content>
    </dialog>
  `,
})
export class ModalComponent implements OnChanges, OnDestroy {
  @Input() aberto = false;
  @Input() titulo = '';
  @Input() icone: string | null = null;
  @Input() classe = '';
  @Output() fechar = new EventEmitter<void>();
  @ViewChild('dlg', { static: true }) dlg!: ElementRef<HTMLDialogElement>;

  ngOnChanges(): void {
    const d = this.dlg.nativeElement;
    if (this.aberto && !d.open) d.showModal?.();
    if (!this.aberto && d.open) d.close();
  }

  ngOnDestroy(): void {
    if (this.dlg.nativeElement.open) this.dlg.nativeElement.close();
  }

  cliqueFundo(e: MouseEvent): void {
    if (e.target !== this.dlg.nativeElement) return;
    const r = this.dlg.nativeElement.getBoundingClientRect();
    const dentro = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
    if (!dentro) this.fechar.emit();
  }
}
