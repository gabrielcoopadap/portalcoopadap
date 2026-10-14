import { Component, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription, forkJoin } from 'rxjs';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { Especie, RegistroBio } from '../../api/modelos';
import { AvisosService } from '../../compartilhado/avisos';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { ArquivoPipe, RotaPipe } from '../../util/pipes';
import { data, hora, lerData } from '../../util/formato';
import { GrupoBio, ORIGENS_REGISTRO, grupo, rotuloOrigem } from '../../util/rotulos';
import { FotoBioComponent } from './foto.component';
import { ModalRegistroComponent } from './modal-registro.component';
import { PropriedadeItem } from './modelos';

interface LinhaRegistro {
  r: RegistroBio;
  g: GrupoBio;
  foto: string | null;
  data: string;
  hora: string;
  lat: string | null;
  lon: string | null;
}

@Component({
  selector: 'ac-registros',
  standalone: true,
  imports: [NgIf, NgFor, FormsModule, RouterLink, EstadoComponent, FotoBioComponent, ModalRegistroComponent, RotaPipe, ArquivoPipe],
  templateUrl: './registros.component.html',
})
export class RegistrosComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private sub?: Subscription;

  @ViewChild('modalRegistro', { static: true }) modalRegistro!: ModalRegistroComponent;

  todos: RegistroBio[] | null = null;
  especies: Especie[] = [];
  propriedades: PropriedadeItem[] = [];
  erro: string | null = null;
  filtrados: RegistroBio[] = [];
  visiveis: LinhaRegistro[] = [];
  especieFiltro: Especie | null = null;
  filtrosFechados = false;
  excluindo: number | null = null;

  aplicado = { reino: '', origem: '', especieId: '', dias: 0 };
  filtro = { reino: '', origem: '', especieId: '', dias: '0' };

  readonly origens = ORIGENS_REGISTRO;
  readonly rotuloOrigem = rotuloOrigem;

  get temFiltros(): boolean {
    const a = this.aplicado;
    return !!a.reino || !!a.origem || !!a.especieId || a.dias > 0;
  }

  ngOnInit(): void {
    this.sub = this.rota.queryParamMap.subscribe(q => {
      const dias = Number(q.get('dias'));
      const especieId = q.get('especieId') ?? '';
      this.aplicado = {
        reino: q.get('reino') ?? '',
        origem: q.get('origem') ?? '',
        especieId: /^\d+$/.test(especieId) ? especieId : '',
        dias: dias === 30 || dias === 90 || dias === 365 ? dias : 0,
      };
      this.filtro = { ...this.aplicado, dias: String(this.aplicado.dias) };
      this.aplicar();
    });
    this.carregar();
  }

  ngOnDestroy(): void { this.sub?.unsubscribe(); }

  carregar(): void {
    this.erro = null;
    forkJoin({ registros: this.api.registros(), especies: this.api.especies(), propriedades: this.api.propriedades() }).subscribe({
      next: d => {
        this.todos = d.registros;
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
      queryParams: { especieId: f.especieId || null, reino: f.reino || null, origem: f.origem || null, dias: f.dias && f.dias !== '0' ? f.dias : null },
    });
  }

  excluir(r: RegistroBio): void {
    if (!window.confirm('Excluir este registro?')) return;
    this.excluindo = r.id;
    this.api.excluirRegistro(r.id).subscribe({
      next: () => { this.excluindo = null; this.avisos.ok('Registro excluído.'); this.carregar(); },
      error: e => { this.excluindo = null; this.avisos.erro(e); },
    });
  }

  porId(_: number, l: LinhaRegistro): number { return l.r.id; }

  private aplicar(): void {
    if (!this.todos) return;
    const a = this.aplicado;
    const limite = a.dias > 0 ? Date.now() - a.dias * 86_400_000 : -Infinity;
    const especieId = a.especieId ? Number(a.especieId) : null;
    this.filtrados = this.todos.filter(r =>
      (!a.reino || r.reino === a.reino) &&
      (!a.origem || r.origem === a.origem) &&
      (especieId === null || r.especieId === especieId) &&
      (lerData(r.dataHora)?.getTime() ?? 0) >= limite);
    this.especieFiltro = especieId === null ? null : this.especies.find(e => e.id === especieId) ?? null;
    this.visiveis = this.filtrados.slice(0, 500).map(r => {
      const temLocal = r.latitude !== null && r.latitude !== undefined && r.longitude !== null && r.longitude !== undefined;
      return {
        r,
        g: grupo(r.reino, r.grupo),
        foto: r.fotoUrl ?? r.especieFotoUrl,
        data: data(r.dataHora),
        hora: hora(r.dataHora),
        lat: temLocal ? r.latitude!.toFixed(5) : null,
        lon: temLocal ? r.longitude!.toFixed(5) : null,
      };
    });
  }
}
