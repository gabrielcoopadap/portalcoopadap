import { Component, ElementRef, EventEmitter, Input, OnChanges, Output, SimpleChanges, ViewChild, inject } from '@angular/core';
import { NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AgroCertApi } from '../../api/agrocert-api.service';
import { Apontamento } from '../../api/modelos';
import { AvisosService } from '../../compartilhado/avisos';
import { ModalComponent } from '../../compartilhado/modal.component';
import { PilulaComponent } from '../../compartilhado/pilula.component';
import { ArquivoPipe, DataPipe, RotaPipe } from '../../util/pipes';
import { estiloGrauRisco, estiloStatusApontamento, rotuloGrauRisco } from '../../util/rotulos';

@Component({
  selector: 'ac-modal-apontamento',
  standalone: true,
  imports: [NgIf, FormsModule, RouterLink, ModalComponent, PilulaComponent, ArquivoPipe, DataPipe, RotaPipe],
  templateUrl: './modal-apontamento.component.html',
})
export class ModalApontamentoComponent implements OnChanges {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);

  @Input() a: Apontamento | null = null;
  @Output() fechar = new EventEmitter<void>();
  @Output() alterado = new EventEmitter<void>();
  @ViewChild('evidencia') evidencia?: ElementRef<HTMLInputElement>;

  observacoes = '';
  salvando = false;

  readonly rotuloGrauRisco = rotuloGrauRisco;
  readonly estiloGrauRisco = estiloGrauRisco;
  readonly estiloStatusApontamento = estiloStatusApontamento;

  ngOnChanges(m: SimpleChanges): void {
    if (m['a'] && m['a'].previousValue?.id !== m['a'].currentValue?.id) {
      this.observacoes = '';
      this.salvando = false;
    }
  }

  temTexto(s: string | null | undefined): boolean { return !!s && s.trim() !== ''; }

  encerrar(): void {
    if (!this.a || this.salvando) return;
    const form = new FormData();
    form.append('Observacoes', this.observacoes);
    const arq = this.evidencia?.nativeElement.files?.[0];
    if (arq) form.append('Evidencia', arq, arq.name);
    this.salvando = true;
    this.api.encerrarApontamento(this.a.id, form).subscribe({
      next: () => { this.salvando = false; this.avisos.ok('Pendência encerrada.'); this.alterado.emit(); },
      error: e => { this.salvando = false; this.avisos.erro(e); },
    });
  }

  reabrir(): void {
    if (!this.a || this.salvando) return;
    this.salvando = true;
    this.api.reabrirApontamento(this.a.id).subscribe({
      next: () => { this.salvando = false; this.avisos.ok('Apontamento reaberto.'); this.alterado.emit(); },
      error: e => { this.salvando = false; this.avisos.erro(e); },
    });
  }

  excluir(): void {
    if (!this.a || this.salvando || !window.confirm('Excluir este apontamento?')) return;
    this.salvando = true;
    this.api.excluirApontamento(this.a.id).subscribe({
      next: () => { this.salvando = false; this.avisos.ok('Apontamento excluído.'); this.alterado.emit(); },
      error: e => { this.salvando = false; this.avisos.erro(e); },
    });
  }
}
