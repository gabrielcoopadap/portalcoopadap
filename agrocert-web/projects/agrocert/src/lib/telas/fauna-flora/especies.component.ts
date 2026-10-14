import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, Subscription, forkJoin } from 'rxjs';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { Especie } from '../../api/modelos';
import { AvisosService } from '../../compartilhado/avisos';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { ModalComponent } from '../../compartilhado/modal.component';
import { PilulaComponent } from '../../compartilhado/pilula.component';
import { RotaPipe } from '../../util/pipes';
import { data, lerData } from '../../util/formato';
import {
  Estilo, GRUPOS_FAUNA, GRUPOS_FLORA, GrupoBio, STATUS_CONSERVACAO, ameacada, corSuave, grupo, grupos, rotuloReino,
  rotuloStatusConservacao,
} from '../../util/rotulos';
import { FotoBioComponent } from './foto.component';
import { ModalRegistroComponent } from './modal-registro.component';
import { PropriedadeItem } from './modelos';

interface CartaoEspecie {
  e: Especie;
  g: GrupoBio;
  pilulaGrupo: { texto: string; estilo: Estilo };
  pilulaStatus: { texto: string; estilo: Estilo } | null;
  total: number;
  ultimo: string | null;
}

interface FormEspecie {
  nomePopular: string;
  nomeCientifico: string;
  reino: string;
  grupo: string;
  statusConservacao: string;
  descricao: string;
}

@Component({
  selector: 'ac-especies',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, RouterLink, EstadoComponent, ModalComponent, PilulaComponent, FotoBioComponent,
    ModalRegistroComponent, RotaPipe],
  templateUrl: './especies.component.html',
})
export class EspeciesComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private sub?: Subscription;

  @ViewChild('modalRegistro', { static: true }) modalRegistro!: ModalRegistroComponent;
  @ViewChild('fotoEspecie', { static: true }) fotoEspecie!: ElementRef<HTMLInputElement>;

  especies: Especie[] | null = null;
  propriedades: PropriedadeItem[] = [];
  erro: string | null = null;
  cartoes: CartaoEspecie[] = [];
  private contagem = new Map<number, { total: number; ultimo: number }>();
  filtrosFechados = false;

  aplicado = { reino: '', grupo: '', busca: '', status: '' };
  filtro = { ...this.aplicado };

  modalEspecie = false;
  editandoId: number | null = null;
  salvando = false;
  arquivo: File | null = null;
  form: FormEspecie = this.formNovo();
  gruposDoReino: GrupoBio[] = GRUPOS_FAUNA;

  readonly statusConservacao = STATUS_CONSERVACAO;
  readonly nomesGrupos = [...new Set([...GRUPOS_FAUNA, ...GRUPOS_FLORA].map(g => g.nome))];

  get temFiltros(): boolean {
    const a = this.aplicado;
    return !!a.reino || !!a.grupo || !!a.busca || !!a.status;
  }

  ngOnInit(): void {
    this.sub = this.rota.queryParamMap.subscribe(q => {
      this.aplicado = { reino: q.get('reino') ?? '', grupo: q.get('grupo') ?? '', busca: q.get('busca') ?? '', status: q.get('status') ?? '' };
      this.filtro = { ...this.aplicado };
      this.aplicar();
    });
    this.carregar();
  }

  ngOnDestroy(): void { this.sub?.unsubscribe(); }

  carregar(): void {
    this.erro = null;
    forkJoin({ especies: this.api.especies(), registros: this.api.registros(), propriedades: this.api.propriedades() }).subscribe({
      next: d => {
        this.contagem.clear();
        for (const r of d.registros) {
          const t = lerData(r.dataHora)?.getTime() ?? 0;
          const c = this.contagem.get(r.especieId);
          if (c) { c.total++; c.ultimo = Math.max(c.ultimo, t); } else this.contagem.set(r.especieId, { total: 1, ultimo: t });
        }
        this.especies = d.especies;
        this.propriedades = d.propriedades.map(p => ({ id: p.id, nome: p.nome }));
        this.aplicar();
      },
      error: e => { this.erro = mensagemDeErro(e); },
    });
  }

  filtrar(): void {
    const f = this.filtro;
    this.router.navigate([], {
      relativeTo: this.rota,
      queryParams: { busca: f.busca || null, reino: f.reino || null, grupo: f.grupo || null, status: f.status || null },
    });
  }

  porId(_: number, c: CartaoEspecie): number { return c.e.id; }

  novaEspecie(): void {
    this.editandoId = null;
    this.form = this.formNovo();
    this.gruposDoReino = grupos(this.form.reino);
    this.limparFoto();
    this.modalEspecie = true;
  }

  editar(e: Especie, ev: Event): void {
    if ((ev.target as HTMLElement).closest('button, a, form')) return;
    this.editandoId = e.id;
    this.gruposDoReino = grupos(e.reino);
    this.form = {
      nomePopular: e.nomePopular ?? '',
      nomeCientifico: e.nomeCientifico ?? '',
      reino: e.reino,
      grupo: e.grupo && this.gruposDoReino.some(g => g.nome === e.grupo) ? e.grupo : this.gruposDoReino[0].nome,
      statusConservacao: e.statusConservacao ?? 'LC',
      descricao: e.descricao ?? '',
    };
    this.limparFoto();
    this.modalEspecie = true;
  }

  trocarReino(): void {
    this.gruposDoReino = grupos(this.form.reino);
    this.form.grupo = this.gruposDoReino[0].nome;
  }

  salvarEspecie(): void {
    if (this.salvando) return;
    const f = this.form;
    const fd = new FormData();
    fd.append('NomePopular', f.nomePopular);
    if (f.nomeCientifico) fd.append('NomeCientifico', f.nomeCientifico);
    fd.append('Reino', f.reino);
    if (f.grupo) fd.append('Grupo', f.grupo);
    if (f.statusConservacao) fd.append('StatusConservacao', f.statusConservacao);
    if (f.descricao) fd.append('Descricao', f.descricao);
    if (this.arquivo) fd.append('Foto', this.arquivo, this.arquivo.name);

    const id = this.editandoId;
    const req: Observable<Especie> = id === null ? this.api.criarEspecie(fd) : this.api.atualizarEspecie(id, fd);
    this.salvando = true;
    req.subscribe({
      next: e => {
        this.salvando = false;
        this.avisos.ok(id === null ? `Espécie "${e.nomePopular}" cadastrada.` : 'Espécie atualizada.');
        this.modalEspecie = false;
        this.carregar();
      },
      error: err => { this.salvando = false; this.avisos.erro(err); },
    });
  }

  excluirEspecie(): void {
    const id = this.editandoId;
    if (id === null || !window.confirm('Excluir esta espécie do catálogo?')) return;
    this.salvando = true;
    this.api.excluirEspecie(id).subscribe({
      next: () => {
        this.salvando = false;
        this.avisos.ok('Espécie excluída.');
        this.modalEspecie = false;
        this.carregar();
      },
      error: err => { this.salvando = false; this.avisos.erro(err); },
    });
  }

  private limparFoto(): void {
    this.arquivo = null;
    if (this.fotoEspecie) this.fotoEspecie.nativeElement.value = '';
  }

  private formNovo(): FormEspecie {
    return { nomePopular: '', nomeCientifico: '', reino: 'FAUNA', grupo: GRUPOS_FAUNA[0].nome, statusConservacao: 'LC', descricao: '' };
  }

  private aplicar(): void {
    if (!this.especies) return;
    const { reino, grupo: grupoFiltro, busca, status } = this.aplicado;
    const termo = busca.trim().toLocaleLowerCase('pt-BR');
    this.cartoes = this.especies.filter(e =>
      (!reino || e.reino === reino) &&
      (!grupoFiltro || e.grupo === grupoFiltro) &&
      (!status || (status === 'AMEACADA' ? ameacada(e.statusConservacao) : e.statusConservacao === status)) &&
      (!busca.trim() || `${e.nomePopular} ${e.nomeCientifico ?? ''}`.toLocaleLowerCase('pt-BR').includes(termo)))
      .map(e => {
        const g = grupo(e.reino, e.grupo);
        const c = this.contagem.get(e.id);
        let pilulaStatus: CartaoEspecie['pilulaStatus'] = null;
        if (ameacada(e.statusConservacao)) {
          pilulaStatus = { texto: rotuloStatusConservacao(e.statusConservacao), estilo: { bg: '#fdeceb', cor: '#c0392b' } };
        } else if (e.statusConservacao === 'NT') {
          pilulaStatus = { texto: 'Quase ameaçada', estilo: { bg: '#fdf6da', cor: '#8a6508' } };
        }
        return {
          e, g,
          pilulaGrupo: { texto: `${rotuloReino(e.reino)} · ${g.nome}`, estilo: { bg: corSuave(g.cor, '22'), cor: 'rgba(0,0,0,.75)' } },
          pilulaStatus,
          total: c?.total ?? 0,
          ultimo: c ? data(new Date(c.ultimo)) : null,
        };
      });
  }
}
