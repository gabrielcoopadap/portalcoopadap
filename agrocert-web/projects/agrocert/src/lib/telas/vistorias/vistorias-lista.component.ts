import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, forkJoin } from 'rxjs';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { NovaVistoria, Vistoria } from '../../api/modelos';
import { AvisosService } from '../../compartilhado/avisos';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { ModalComponent } from '../../compartilhado/modal.component';
import { PilulaComponent } from '../../compartilhado/pilula.component';
import { hojeIso } from '../../util/formato';
import { DataPipe, RotaPipe } from '../../util/pipes';
import { Estilo, TIPOS_VISTORIA, rotuloTipoVistoria } from '../../util/rotulos';

type Aba = 'TODAS' | 'MENSAL_CONSULTORIA' | 'DIARIA_FUNCIONARIO';

@Component({
  selector: 'ac-vistorias-lista',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, RouterLink, EstadoComponent, ModalComponent, PilulaComponent, DataPipe, RotaPipe],
  templateUrl: './vistorias-lista.component.html',
})
export class VistoriasListaComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly rotaPipe = new RotaPipe();
  private sub?: Subscription;
  private tipoSugerido = 'DIARIA_FUNCIONARIO';
  private nomeUsuario = '';

  readonly abas: { valor: Aba; rotulo: string }[] = [
    { valor: 'TODAS', rotulo: 'Todas' },
    { valor: 'MENSAL_CONSULTORIA', rotulo: 'Mensais (Consultoria)' },
    { valor: 'DIARIA_FUNCIONARIO', rotulo: 'Diárias (Funcionário)' },
  ];
  readonly tiposVistoria = TIPOS_VISTORIA;
  readonly rotuloTipoVistoria = rotuloTipoVistoria;
  readonly estiloAberto: Estilo = { bg: '#fbeedd', cor: '#bd7d15' };
  readonly estiloEncerrado: Estilo = { bg: '#e4f3ea', cor: '#1f8a4c' };

  todas: Vistoria[] | null = null;
  pendencias = 0;
  erro: string | null = null;
  aba: Aba = 'TODAS';

  novaAberta = false;
  salvando = false;
  nova: NovaVistoria = this.novaVazia();

  get filtradas(): Vistoria[] {
    const t = this.todas ?? [];
    return this.aba === 'TODAS' ? t : t.filter(v => v.tipo === this.aba);
  }
  get mensais(): number { return (this.todas ?? []).filter(v => v.tipo === 'MENSAL_CONSULTORIA').length; }
  get diarias(): number { return (this.todas ?? []).filter(v => v.tipo === 'DIARIA_FUNCIONARIO').length; }

  ngOnInit(): void {
    this.sub = this.rota.queryParamMap.subscribe(q => {
      const tipo = q.get('tipo');
      this.aba = tipo === 'MENSAL_CONSULTORIA' || tipo === 'DIARIA_FUNCIONARIO' ? tipo : 'TODAS';
    });
    this.api.sessao().subscribe({
      next: s => {
        this.tipoSugerido = s.consultor ? 'MENSAL_CONSULTORIA' : 'DIARIA_FUNCIONARIO';
        this.nomeUsuario = s.nome ?? '';
        if (!this.novaAberta) this.nova = this.novaVazia();
      },
      error: () => {  },
    });
    this.carregar();
  }

  ngOnDestroy(): void { this.sub?.unsubscribe(); }

  carregar(): void {
    this.erro = null;
    forkJoin({ todas: this.api.vistorias(), pend: this.api.pendencias() }).subscribe({
      next: r => { this.todas = r.todas; this.pendencias = r.pend.length; },
      error: e => { this.todas = null; this.erro = mensagemDeErro(e); },
    });
  }

  abrir(v: Vistoria): void {
    this.router.navigateByUrl(this.rotaPipe.transform(['vistorias', v.id]));
  }

  abrirNova(): void {
    this.nova = this.novaVazia();
    this.novaAberta = true;
  }

  criar(): void {
    if (this.salvando) return;
    this.salvando = true;
    this.api.criarVistoria(semVazios(this.nova)).subscribe({
      next: v => {
        this.salvando = false;
        this.novaAberta = false;
        this.avisos.ok(`Vistoria ${v.codigoDocumento} aberta. Registre os apontamentos encontrados.`);
        this.abrir(v);
      },
      error: e => { this.salvando = false; this.avisos.erro(e); },
    });
  }

  private novaVazia(): NovaVistoria {
    return {
      tipo: this.tipoSugerido, unidadePropriedade: '', setorArea: '', dataVistoria: hojeIso(),
      motivo: '', tecnicoResponsavel: this.nomeUsuario, cargoTecnico: '', aprovadoPor: '', cargoAprovador: '',
    };
  }
}

function semVazios<T extends object>(o: T): T {
  const r = { ...o } as Record<string, unknown>;
  for (const k of Object.keys(r)) if (typeof r[k] === 'string' && (r[k] as string).trim() === '') r[k] = null;
  return r as unknown as T;
}
