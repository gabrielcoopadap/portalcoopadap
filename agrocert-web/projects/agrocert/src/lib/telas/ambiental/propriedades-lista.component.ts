import { Component, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { Propriedade } from '../../api/modelos';
import { AvisosService } from '../../compartilhado/avisos';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { ModalComponent } from '../../compartilhado/modal.component';
import { RotaPipe } from '../../util/pipes';
import { AGROCERT_CONFIG } from '../../config';
import { numero } from '../../util/formato';

@Component({
  selector: 'ac-propriedades-lista',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, RouterLink, EstadoComponent, ModalComponent, RotaPipe],
  templateUrl: './propriedades-lista.component.html',
})
export class PropriedadesListaComponent implements OnInit {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);
  private readonly router = inject(Router);
  private readonly config = inject(AGROCERT_CONFIG);

  propriedades: Propriedade[] | null = null;
  erro: string | null = null;
  excluindo: number | null = null;

  modalAberto = false;
  salvando = false;
  nome = '';
  carCodigo = '';
  geometriaManual = false;
  geoJson = '';

  readonly numero = numero;
  porId = (_: number, p: Propriedade) => p.id;

  ngOnInit(): void { this.carregar(); }

  carregar(): void {
    this.erro = null;
    this.propriedades = null;
    this.api.propriedades().subscribe({
      next: l => (this.propriedades = l),
      error: e => (this.erro = mensagemDeErro(e)),
    });
  }

  abrirModal(): void {
    this.nome = '';
    this.carCodigo = '';
    this.geometriaManual = false;
    this.geoJson = '';
    this.modalAberto = true;
  }

  cadastrar(): void {
    if (this.salvando || !this.nome.trim() || !this.carCodigo.trim()) return;
    this.salvando = true;
    this.api.cadastrarPropriedade({
      nome: this.nome.trim(),
      carCodigo: this.carCodigo.trim(),
      geoJson: this.geometriaManual && this.geoJson.trim() ? this.geoJson : null,
    }).subscribe({
      next: p => {
        this.salvando = false;
        this.modalAberto = false;
        this.avisos.ok(`Propriedade "${p.nome}" cadastrada.`);
        this.router.navigate([this.config.rotaCertificacao, 'ambiental', p.id]);
      },
      error: e => {
        this.salvando = false;
        this.avisos.erro(e);
      },
    });
  }

  excluir(p: Propriedade): void {
    if (!window.confirm('Excluir esta propriedade e seus alertas?')) return;
    this.excluindo = p.id;
    this.api.excluirPropriedade(p.id).subscribe({
      next: () => {
        this.excluindo = null;
        this.avisos.ok('Propriedade excluída.');
        this.carregar();
      },
      error: e => {
        this.excluindo = null;
        this.avisos.erro(e);
      },
    });
  }
}
