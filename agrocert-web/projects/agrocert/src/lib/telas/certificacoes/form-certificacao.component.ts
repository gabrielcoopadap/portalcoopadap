import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { NgFor } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AgroCertApi } from '../../api/agrocert-api.service';
import { CategoriaConformidade, Certificacao } from '../../api/modelos';
import { AvisosService } from '../../compartilhado/avisos';
import { ModalComponent } from '../../compartilhado/modal.component';
import { STATUS_CERTIFICACAO } from '../../util/rotulos';

interface FormCertificacao {
  nome: string;
  sigla: string;
  escopo: string;
  status: string;
  cor: string;
  conformidade: number | null;
  totalRequisitos: number | null;
  conformes: number | null;
  pendentes: number | null;
  naoConformes: number | null;
  organismo: string;
  validoAte: string;
  ultimaAuditoria: string;
  proximaAuditoria: string;
  categorias: CategoriaConformidade[];
}

function ouNulo(v: string | null | undefined): string | null {
  return v === null || v === undefined || v === '' ? null : v;
}

function numOuNulo(v: number | string | null | undefined): number | null {
  return v === null || v === undefined || v === '' || isNaN(Number(v)) ? null : Number(v);
}

@Component({
  selector: 'ac-form-certificacao',
  standalone: true,
  imports: [NgFor, FormsModule, ModalComponent],
  templateUrl: './form-certificacao.component.html',
})
export class FormCertificacaoComponent implements OnChanges {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);

  @Input() aberto = false;
  @Input() certificacao: Certificacao | null | undefined = null;
  @Output() fechar = new EventEmitter<void>();
  @Output() salvo = new EventEmitter<Certificacao>();

  readonly statusLista = STATUS_CERTIFICACAO;
  f: FormCertificacao = this.montar(null);
  salvando = false;

  get edicao(): boolean { return !!this.certificacao && this.certificacao.id > 0; }

  ngOnChanges(m: SimpleChanges): void {
    if ((m['aberto'] && this.aberto) || (m['certificacao'] && !this.aberto)) {
      this.f = this.montar(this.certificacao ?? null);
      this.salvando = false;
    }
  }

  adicionarCategoria(): void { this.f.categorias.push({ nome: null, conformes: 0, total: 0 }); }
  removerCategoria(i: number): void { this.f.categorias.splice(i, 1); }

  salvar(): void {
    if (this.salvando) return;
    const f = this.f;
    const corpo: Partial<Certificacao> = {
      nome: f.nome ?? '',
      sigla: ouNulo(f.sigla),
      escopo: ouNulo(f.escopo),
      status: ouNulo(f.status),
      cor: ouNulo(f.cor),
      conformidade: numOuNulo(f.conformidade),
      totalRequisitos: numOuNulo(f.totalRequisitos),
      conformes: numOuNulo(f.conformes),
      pendentes: numOuNulo(f.pendentes),
      naoConformes: numOuNulo(f.naoConformes),
      organismo: ouNulo(f.organismo),
      validoAte: ouNulo(f.validoAte),
      ultimaAuditoria: ouNulo(f.ultimaAuditoria),
      proximaAuditoria: ouNulo(f.proximaAuditoria),
      categorias: f.categorias.map(x => ({ nome: ouNulo(x.nome), conformes: numOuNulo(x.conformes), total: numOuNulo(x.total) })),
    };
    const edicao = this.edicao;
    const pedido = edicao
      ? this.api.atualizarCertificacao(this.certificacao!.id, corpo)
      : this.api.criarCertificacao(corpo);
    this.salvando = true;
    pedido.subscribe({
      next: c => {
        this.salvando = false;
        this.avisos.ok(edicao ? 'Certificação atualizada.' : `Certificação "${c.nome}" cadastrada.`);
        this.salvo.emit(c);
      },
      error: e => {
        this.salvando = false;
        this.avisos.erro(e);
      },
    });
  }

  private montar(c: Certificacao | null): FormCertificacao {
    return {
      nome: c?.nome ?? '',
      sigla: c?.sigla ?? '',
      escopo: c?.escopo ?? '',
      status: c?.status && STATUS_CERTIFICACAO.includes(c.status) ? c.status : 'Conforme',
      cor: c?.cor ?? '#5e9a42',
      conformidade: c?.conformidade ?? 0,
      totalRequisitos: c?.totalRequisitos ?? 0,
      conformes: c?.conformes ?? 0,
      pendentes: c?.pendentes ?? 0,
      naoConformes: c?.naoConformes ?? 0,
      organismo: c?.organismo ?? '',
      validoAte: c?.validoAte ?? '',
      ultimaAuditoria: c?.ultimaAuditoria ?? '',
      proximaAuditoria: c?.proximaAuditoria ?? '',
      categorias: (c?.categorias ?? []).map(x => ({ ...x })),
    };
  }
}
