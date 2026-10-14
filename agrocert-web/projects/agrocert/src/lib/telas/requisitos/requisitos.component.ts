import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Params, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { Referencia, Requisito } from '../../api/modelos';
import { AvisosService } from '../../compartilhado/avisos';
import { ModalComponent } from '../../compartilhado/modal.component';
import { PilulaComponent } from '../../compartilhado/pilula.component';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { RotaPipe } from '../../util/pipes';
import * as R from '../../util/rotulos';
import { filtrar, normasPresentes, temasPresentes } from './requisitos-filtro';

interface Filtros {
  busca: string;
  tema: string;
  norma: string;
  status: string;
  prioridade: string;
}

interface FormRequisito {
  titulo: string;
  descricao: string;
  tema: string;
  prioridade: string;
  status: string;
  responsavel: string;
  periodicidade: string;
  prazo: string;
  observacao: string;
  referencias: { norma: string; versao: string; codigo: string }[];
}

const CHAVES: (keyof Filtros)[] = ['busca', 'tema', 'norma', 'status', 'prioridade'];

function ouNulo(v: string | null | undefined): string | null {
  return v === null || v === undefined || v === '' ? null : v;
}

const ESTILO_IMPRESSAO =
  'body{font:12pt Roboto,Arial,sans-serif;color:#222;margin:18mm}h3{font-size:15pt;margin:8px 0}h4{font-size:12pt;color:#3e6e2a;margin:14px 0 4px}' +
  '.material-icons{display:none}.cm-tag,.cm-pilula{display:inline-block;border:1px solid #ccc;border-radius:10px;padding:1px 8px;margin:0 4px 4px 0;font-size:9pt}li{margin:3px 0}';

@Component({
  selector: 'ac-requisitos',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, RouterLink, ModalComponent, PilulaComponent, EstadoComponent, RotaPipe],
  templateUrl: './requisitos.component.html',
})
export class RequisitosComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);
  private readonly rotaAtiva = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private subParams?: Subscription;

  @ViewChild('corpoGuia', { static: true }) corpoGuia!: ElementRef<HTMLElement>;

  readonly R = R;

  todos: Requisito[] = [];
  filtrados: Requisito[] = [];
  temas: string[] = [];
  normas: string[] = [];
  normasComSite: { site: string; rotulo: string }[] = [];
  conformes = 0;
  carregando = true;
  private carregado = false;
  erro: string | null = null;

  params: Filtros = { busca: '', tema: '', norma: '', status: '', prioridade: '' };
  filtro: Filtros = { ...this.params };
  filtrosFechados = false;
  dropdown: 'baixar' | 'normas' | null = null;

  guia: Requisito | null = null;
  guiaPassos: string[] = [];
  guiaEvidencias: string[] = [];
  guiaAuto: string[] = [];
  guiaErros: string[] = [];
  guiaAjuda: string[] = [];

  formAberto = false;
  editandoId: number | null = null;
  f: FormRequisito = this.formVazio();
  salvando = false;

  get filtrosAtivos(): boolean { return CHAVES.some(k => !!this.params[k]); }

  ngOnInit(): void {
    this.subParams = this.rotaAtiva.queryParamMap.subscribe(q => {
      const p = {} as Filtros;
      for (const k of CHAVES) p[k] = q.get(k) ?? '';
      this.params = p;
      this.filtro = { ...p };
      this.aplicar();
    });
    this.carregar();
  }

  ngOnDestroy(): void { this.subParams?.unsubscribe(); }

  carregar(): void {
    this.carregando = !this.carregado;
    this.erro = null;
    this.api.requisitos().subscribe({
      next: l => {
        this.todos = l;
        this.carregado = true;
        this.carregando = false;
        this.aplicar();
      },
      error: e => {
        this.carregando = false;
        this.erro = mensagemDeErro(e);
      },
    });
  }

  private aplicar(): void {
    const p = this.params;
    const todos = this.todos;
    this.filtrados = filtrar(todos, p.busca, p.tema, p.status, p.norma, p.prioridade);
    this.temas = temasPresentes(todos);
    this.normas = normasPresentes(todos);
    this.conformes = todos.filter(r => r.status === 'Conforme').length;
    this.normasComSite = this.normas
      .filter(n => n in R.SITE_NORMA)
      .map(n => {
        const versoes = Array.from(new Set(
          todos.flatMap(r => r.referencias ?? []).filter(x => x.norma === n)
            .map(x => x.versao).filter((v): v is string => !!v && v.trim() !== ''),
        )).join(', ');
        return { site: R.SITE_NORMA[n], rotulo: versoes.length > 0 ? `${n} — em uso: ${versoes}` : n };
      });
  }

  aplicarFiltros(): void {
    const queryParams: Params = {};
    for (const k of CHAVES) queryParams[k] = this.filtro[k] ? this.filtro[k] : null;
    this.router.navigate([], { relativeTo: this.rotaAtiva, queryParams, queryParamsHandling: 'merge' });
  }

  alternarDropdown(nome: 'baixar' | 'normas', e: Event): void {
    e.stopPropagation();
    this.dropdown = this.dropdown === nome ? null : nome;
  }

  @HostListener('document:click')
  fecharDropdowns(): void { this.dropdown = null; }

  baixar(norma: string | null, e: Event): void {
    e.preventDefault();
    this.api.baixarListaMestra(norma).subscribe({ error: err => this.avisos.erro(err) });
  }

  resumo(obs: string): string { return obs.length > 90 ? obs.slice(0, 90) + '…' : obs; }
  tituloTag(x: Referencia): string { return `${x.norma ?? ''} ${x.versao ?? ''}`; }
  maiusculas(t: string | null | undefined): string | null { return t ? t.toLocaleUpperCase('pt-BR') : null; }

  excluir(r: Requisito, e: Event): void {
    e.preventDefault();
    if (!window.confirm('Excluir este requisito?')) return;
    this.api.excluirRequisito(r.id).subscribe({
      next: () => {
        this.avisos.ok('Requisito excluído.');
        this.carregar();
      },
      error: e => this.avisos.erro(e),
    });
  }

  abrirGuia(r: Requisito): void {
    this.guia = r;
    this.guiaPassos = R.linhas(r.passoAPasso);
    this.guiaEvidencias = R.linhas(r.evidencias);
    this.guiaAuto = R.linhas(r.autoverificacao);
    this.guiaErros = R.linhas(r.errosComuns);
    this.guiaAjuda = R.linhas(r.ondeBuscarAjuda);
    setTimeout(() => (this.corpoGuia.nativeElement.scrollTop = 0));
  }

  imprimirGuia(): void {
    const w = window.open('', '_blank');
    if (!w) return;
    const html = this.corpoGuia.nativeElement.innerHTML.replace(/<!--[\s\S]*?-->/g, '');
    w.document.write(
      `<!doctype html><html lang="pt-BR"><head><meta charset="utf-8"><title>Como cumprir</title><style>${ESTILO_IMPRESSAO}</style></head><body>${html}</body></html>`,
    );
    w.document.close();
    w.focus();
    w.print();
  }

  novo(): void {
    this.editandoId = null;
    this.f = this.formVazio();
    this.salvando = false;
    this.formAberto = true;
  }

  teclaLinha(r: Requisito, e: Event): void {
    if (e.target !== e.currentTarget) return;
    e.preventDefault();
    this.editar(r, e);
  }

  editar(r: Requisito, e: Event): void {
    if ((e.target as HTMLElement).closest('button, a, form')) return;
    this.editandoId = r.id;
    this.f = {
      titulo: r.titulo ?? '',
      descricao: r.descricao ?? '',
      tema: r.tema ?? '',
      prioridade: r.prioridade ?? 'Maior',
      status: r.status ?? 'Pendente',
      responsavel: r.responsavel ?? '',
      periodicidade: r.periodicidade ?? '',
      prazo: r.prazo ?? '',
      observacao: r.observacao ?? '',
      referencias: (r.referencias ?? []).map(x => ({ norma: x.norma ?? R.NORMAS[0], versao: x.versao ?? '', codigo: x.codigo ?? '' })),
    };
    this.salvando = false;
    this.formAberto = true;
  }

  adicionarReferencia(): void { this.f.referencias.push({ norma: R.NORMAS[0], versao: '', codigo: '' }); }

  opcoesNorma(atual: string): string[] {
    return !atual || R.NORMAS.includes(atual) ? R.NORMAS : [...R.NORMAS, atual];
  }

  salvar(): void {
    if (this.salvando) return;
    const f = this.f;
    const corpo: Partial<Requisito> = {
      titulo: ouNulo(f.titulo),
      descricao: ouNulo(f.descricao),
      tema: ouNulo(f.tema),
      prioridade: ouNulo(f.prioridade),
      status: ouNulo(f.status),
      responsavel: ouNulo(f.responsavel),
      periodicidade: ouNulo(f.periodicidade),
      prazo: ouNulo(f.prazo),
      observacao: ouNulo(f.observacao),
      referencias: f.referencias.map(x => ({ norma: ouNulo(x.norma), versao: ouNulo(x.versao), codigo: ouNulo(x.codigo) })),
    };
    const id = this.editandoId;
    const pedido = id ? this.api.atualizarRequisito(id, corpo) : this.api.criarRequisito(corpo);
    this.salvando = true;
    pedido.subscribe({
      next: () => {
        this.salvando = false;
        this.formAberto = false;
        this.avisos.ok(id ? 'Requisito atualizado.' : 'Requisito cadastrado.');
        this.carregar();
      },
      error: e => {
        this.salvando = false;
        this.avisos.erro(e);
      },
    });
  }

  private formVazio(): FormRequisito {
    return {
      titulo: '', descricao: '', tema: '', prioridade: 'Maior', status: 'Pendente',
      responsavel: '', periodicidade: '', prazo: '', observacao: '', referencias: [],
    };
  }
}
