import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, forkJoin } from 'rxjs';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { Apontamento, Requisito, Vistoria } from '../../api/modelos';
import { AvisosService } from '../../compartilhado/avisos';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { ModalComponent } from '../../compartilhado/modal.component';
import { PilulaComponent } from '../../compartilhado/pilula.component';
import { DataPipe, RotaPipe } from '../../util/pipes';
import {
  GRAUS_RISCO, estiloGrauRisco, estiloStatusApontamento, rotuloGrauRisco, rotuloTipoVistoria, sigla,
} from '../../util/rotulos';
import { ModalApontamentoComponent } from './modal-apontamento.component';

const CORES_RISCO: Record<string, string> = { CRITICO: '#c0392b', ALTO: '#b8860b', MEDIO: '#1f6fb2', BAIXO: '#1f8a4c' };

interface NovoApontamentoForm {
  anomalia: string;
  localizacao: string;
  requisitoId: string;
  grauRisco: string;
  situacaoEncontrada: string;
  situacaoRequerida: string;
  riscoAssociado: string;
  prazo: string;
  responsavelExecucao: string;
}

@Component({
  selector: 'ac-vistoria-detalhe',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, RouterLink, EstadoComponent, ModalComponent, PilulaComponent, DataPipe, RotaPipe,
    ModalApontamentoComponent],
  templateUrl: './vistoria-detalhe.component.html',
})
export class VistoriaDetalheComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly rotaPipe = new RotaPipe();
  private sub?: Subscription;

  @ViewChild('foto1') foto1?: ElementRef<HTMLInputElement>;
  @ViewChild('foto2') foto2?: ElementRef<HTMLInputElement>;

  readonly grausRisco = GRAUS_RISCO;
  readonly rotuloGrauRisco = rotuloGrauRisco;
  readonly estiloGrauRisco = estiloGrauRisco;
  readonly estiloStatusApontamento = estiloStatusApontamento;
  readonly rotuloTipoVistoria = rotuloTipoVistoria;

  id = 0;
  v: Vistoria | null = null;
  erro: string | null = null;
  opcoesRequisito: { id: number; texto: string }[] = [];
  aberto: Apontamento | null = null;
  abrindoPdf = false;
  excluindo = false;

  novoAberto = false;
  salvando = false;
  novo: NovoApontamentoForm = this.novoVazio();

  ngOnInit(): void {
    this.sub = this.rota.paramMap.subscribe(p => {
      this.id = Number(p.get('id'));
      this.v = null;
      this.aberto = null;
      this.novoAberto = false;
      this.carregar();
    });
  }

  ngOnDestroy(): void { this.sub?.unsubscribe(); }

  carregar(): void {
    this.erro = null;
    forkJoin({ v: this.api.vistoria(this.id), reqs: this.api.requisitos() }).subscribe({
      next: r => { this.v = r.v; this.opcoesRequisito = this.montarOpcoes(r.reqs); },
      error: e => { this.v = null; this.erro = mensagemDeErro(e); },
    });
  }

  corRisco(g: string): string { return CORES_RISCO[g] ?? '#ccc'; }

  abrirPdf(): void {
    this.abrindoPdf = true;
    this.api.abrirRelatorioVistoria(this.id).subscribe({
      next: () => (this.abrindoPdf = false),
      error: e => { this.abrindoPdf = false; this.avisos.erro(e); },
    });
  }

  excluir(): void {
    if (!window.confirm('Excluir esta vistoria e todos os seus apontamentos?')) return;
    this.excluindo = true;
    this.api.excluirVistoria(this.id).subscribe({
      next: () => {
        this.excluindo = false;
        this.avisos.ok('Vistoria excluída.');
        this.router.navigateByUrl(this.rotaPipe.transform('vistorias'));
      },
      error: e => { this.excluindo = false; this.avisos.erro(e); },
    });
  }

  abrirNovo(): void {
    this.novo = this.novoVazio();
    if (this.foto1) this.foto1.nativeElement.value = '';
    if (this.foto2) this.foto2.nativeElement.value = '';
    this.novoAberto = true;
  }

  registrar(): void {
    if (this.salvando) return;
    const n = this.novo;
    const fd = new FormData();
    const campo = (nome: string, valor: string) => { if (valor && valor.trim() !== '') fd.append(nome, valor); };
    campo('Anomalia', n.anomalia);
    campo('Localizacao', n.localizacao);
    campo('GrauRisco', n.grauRisco);
    campo('Prazo', n.prazo);
    campo('ResponsavelExecucao', n.responsavelExecucao);
    campo('SituacaoEncontrada', n.situacaoEncontrada);
    campo('SituacaoRequerida', n.situacaoRequerida);
    campo('RiscoAssociado', n.riscoAssociado);
    campo('RequisitoId', n.requisitoId);
    const f1 = this.foto1?.nativeElement.files?.[0];
    const f2 = this.foto2?.nativeElement.files?.[0];
    if (f1) fd.append('ImagemEncontrada', f1, f1.name);
    if (f2) fd.append('ImagemRequerida', f2, f2.name);
    this.salvando = true;
    this.api.adicionarApontamento(this.id, fd).subscribe({
      next: () => {
        this.salvando = false;
        this.novoAberto = false;
        this.avisos.ok('Apontamento registrado.');
        this.carregar();
      },
      error: e => { this.salvando = false; this.avisos.erro(e); },
    });
  }

  private montarOpcoes(reqs: Requisito[]): { id: number; texto: string }[] {
    const cmp = (a: string | null, b: string | null) => (a ?? '').localeCompare(b ?? '', 'pt-BR');
    return [...reqs]
      .sort((a, b) => cmp(a.tema, b.tema) || cmp(a.titulo, b.titulo))
      .map(r => {
        const ref = r.referencias?.[0];
        return { id: r.id, texto: `${ref ? `[${sigla(ref.norma)} ${ref.codigo}] ` : ''}${r.titulo ?? ''}` };
      });
  }

  private novoVazio(): NovoApontamentoForm {
    return {
      anomalia: '', localizacao: '', requisitoId: '', grauRisco: 'MEDIO', situacaoEncontrada: '', situacaoRequerida: '',
      riscoAssociado: '', prazo: '', responsavelExecucao: '',
    };
  }
}
