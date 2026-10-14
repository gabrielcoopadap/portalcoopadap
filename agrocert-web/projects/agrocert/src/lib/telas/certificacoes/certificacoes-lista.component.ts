import { Component, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { Certificacao } from '../../api/modelos';
import { PilulaComponent } from '../../compartilhado/pilula.component';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { RotaPipe } from '../../util/pipes';
import * as F from '../../util/formato';
import * as R from '../../util/rotulos';
import { FormCertificacaoComponent } from './form-certificacao.component';

@Component({
  selector: 'ac-certificacoes-lista',
  standalone: true,
  imports: [NgIf, NgFor, RouterLink, PilulaComponent, EstadoComponent, RotaPipe, FormCertificacaoComponent],
  providers: [RotaPipe],
  templateUrl: './certificacoes-lista.component.html',
})
export class CertificacoesListaComponent implements OnInit {
  private readonly api = inject(AgroCertApi);
  private readonly router = inject(Router);
  private readonly rota = inject(RotaPipe);

  readonly F = F;
  readonly R = R;
  lista: Certificacao[] | null = null;
  erro: string | null = null;
  novaAberta = false;

  ngOnInit(): void { this.carregar(); }

  carregar(): void {
    this.erro = null;
    this.lista = null;
    this.api.certificacoes().subscribe({
      next: l => (this.lista = l),
      error: e => (this.erro = mensagemDeErro(e)),
    });
  }

  criada(c: Certificacao): void {
    this.novaAberta = false;
    this.router.navigateByUrl(this.rota.transform(['certificacoes', c.id]));
  }
}
