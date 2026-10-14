import { Component, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  COOPERADOS, CULTURAS, TECNICOS, TIPOS_VISITA, Visita, VisitasService, dataBr, dataHoraBr,
} from './visitas.service';

interface Filtros {
  tipo: string; cultura: string; cooperado: string; propriedade: string; data: string; tecnico: string;
  descricao: string; numero: string; criadoPor: string; criadoEm: string; alteradoPor: string; alteradoEm: string;
}

const VAZIO: Filtros = {
  tipo: '', cultura: '', cooperado: '', propriedade: '', data: '', tecnico: '',
  descricao: '', numero: '', criadoPor: '', criadoEm: '', alteradoPor: '', alteradoEm: '',
};

@Component({
  selector: 'demo-visitas-lista',
  standalone: true,
  imports: [NgFor, NgIf, FormsModule, RouterLink],
  templateUrl: './visitas-lista.component.html',
})
export class VisitasListaComponent {
  private readonly servico = inject(VisitasService);
  private readonly router = inject(Router);

  readonly tipos = TIPOS_VISITA;
  readonly culturas = CULTURAS;
  readonly cooperados = COOPERADOS;
  readonly tecnicos = TECNICOS;
  readonly usuarios = [...new Set(this.servico.todas().flatMap(v => [v.criadoPor, v.alteradoPor]).filter((u): u is string => !!u))].sort();
  readonly dataBr = dataBr;
  readonly dataHoraBr = dataHoraBr;

  mostrarFiltros = true;
  f: Filtros = { ...VAZIO };
  filtradas: Visita[] = this.servico.todas();
  porPagina = 10;
  paginaAtual = 0;

  propriedades(cooperado: string): string[] {
    return cooperado ? COOPERADOS.find(c => c.nome === cooperado)?.propriedades ?? [] : COOPERADOS.flatMap(c => c.propriedades);
  }

  temFiltro(): boolean {
    return Object.values(this.f).some(v => v !== '');
  }

  buscar(): void {
    const f = this.f;
    const contem = (a: string, b: string) => !b || a.toLowerCase().includes(b.trim().toLowerCase());
    this.filtradas = this.servico.todas().filter(v =>
      (!f.tipo || v.tipo === f.tipo) && (!f.cultura || v.cultura === f.cultura) && (!f.cooperado || v.cooperado === f.cooperado) &&
      (!f.propriedade || v.propriedade === f.propriedade) && (!f.data || v.data === f.data) && (!f.tecnico || v.tecnico === f.tecnico) &&
      contem(v.descricao, f.descricao) && (!f.numero || String(v.numero) === f.numero.trim()) &&
      (!f.criadoPor || v.criadoPor === f.criadoPor) && (!f.criadoEm || v.criadoEm.slice(0, 10) === f.criadoEm) &&
      (!f.alteradoPor || v.alteradoPor === f.alteradoPor) && (!f.alteradoEm || (v.alteradoEm ?? '').slice(0, 10) === f.alteradoEm));
    this.paginaAtual = 0;
  }

  limpar(): void {
    this.f = { ...VAZIO };
    this.buscar();
  }

  totalPaginas(): number {
    return Math.max(1, Math.ceil(this.filtradas.length / this.porPagina));
  }

  pagina(): Visita[] {
    const i = this.paginaAtual * this.porPagina;
    return this.filtradas.slice(i, i + this.porPagina);
  }

  abrir(v: Visita): void {
    this.router.navigate(['/portal/visita-tecnica', v.id]);
  }
}
