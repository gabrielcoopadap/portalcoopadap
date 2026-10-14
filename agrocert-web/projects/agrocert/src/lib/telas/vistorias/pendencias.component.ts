import { Component, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { Apontamento } from '../../api/modelos';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { PilulaComponent } from '../../compartilhado/pilula.component';
import { DataPipe, RotaPipe } from '../../util/pipes';
import { estiloGrauRisco, rotuloGrauRisco } from '../../util/rotulos';
import { ModalApontamentoComponent } from './modal-apontamento.component';

@Component({
  selector: 'ac-pendencias',
  standalone: true,
  imports: [NgIf, NgFor, RouterLink, EstadoComponent, PilulaComponent, DataPipe, RotaPipe, ModalApontamentoComponent],
  templateUrl: './pendencias.component.html',
})
export class PendenciasComponent implements OnInit {
  private readonly api = inject(AgroCertApi);

  lista: Apontamento[] | null = null;
  erro: string | null = null;
  aberto: Apontamento | null = null;

  readonly rotuloGrauRisco = rotuloGrauRisco;
  readonly estiloGrauRisco = estiloGrauRisco;
  readonly abs = Math.abs;

  ngOnInit(): void { this.carregar(); }

  carregar(): void {
    this.erro = null;
    this.api.pendencias().subscribe({
      next: l => (this.lista = l),
      error: e => { this.lista = null; this.erro = mensagemDeErro(e); },
    });
  }
}
