import { Component, ElementRef, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AgroCertApi } from 'agrocert';
import {
  Arquivo, COOPERADOS, CULTURAS, Recomendacao, TECNICOS, TIPOS_VISITA, UNIDADES, Visita, VisitasService, dataBr, dataHoraBr,
} from './visitas.service';

@Component({
  selector: 'demo-visita-form',
  standalone: true,
  imports: [NgFor, NgIf, FormsModule, RouterLink],
  templateUrl: './visita-form.component.html',
})
export class VisitaFormComponent implements OnInit, OnDestroy {
  private readonly servico = inject(VisitasService);
  private readonly router = inject(Router);
  @ViewChild('editor', { static: true }) editorRef!: ElementRef<HTMLDivElement>;

  readonly tipos = TIPOS_VISITA;
  readonly culturas = CULTURAS;
  readonly cooperados = COOPERADOS;
  readonly tecnicos = TECNICOS;
  readonly unidades = UNIDADES;
  readonly dataBr = dataBr;
  readonly dataHoraBr = dataHoraBr;

  editando: Visita | undefined;
  tipo: string | null = null;
  cultura = '';
  cooperado = '';
  propriedade = '';
  data = '';
  tecnico = '';
  descricao = '';
  detalhes = '';
  recomendacoes: Recomendacao[] = [];
  arquivos: Arquivo[] = [];

  rec: Recomendacao | null = null;
  recIndice: number | null = null;
  private usuario = 'USUÁRIO';
  private selecao: Range | null = null;

  constructor() {
    const id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));
    this.editando = id ? this.servico.obter(id) : undefined;
    if (id && !this.editando) this.router.navigate(['/portal/visita-tecnica']);
    if (this.editando) {
      const v = this.editando;
      Object.assign(this, {
        tipo: v.tipo, cultura: v.cultura, cooperado: v.cooperado, propriedade: v.propriedade, data: v.data, tecnico: v.tecnico,
        descricao: v.descricao, detalhes: v.detalhes, recomendacoes: v.recomendacoes.map(r => ({ ...r })), arquivos: [...v.arquivos],
      });
    }
    inject(AgroCertApi).sessao().subscribe({ next: s => { if (s.nome) this.usuario = s.nome.toUpperCase(); }, error: () => {} });
  }

  ngOnInit(): void {
    this.editorRef.nativeElement.innerHTML = this.detalhes;
  }

  ngOnDestroy(): void {
    this.arquivos.filter(a => !this.editando?.arquivos.includes(a)).forEach(a => URL.revokeObjectURL(a.url));
  }

  propriedades(): string[] {
    return COOPERADOS.find(c => c.nome === this.cooperado)?.propriedades ?? [];
  }

  valido(): boolean {
    const texto = this.editorRef?.nativeElement.textContent?.trim() ?? '';
    return !!(this.cultura && this.cooperado && this.propriedade && this.data && this.tecnico && this.descricao.trim() && texto);
  }

  formatar(e: Event, comando: string): void {
    e.preventDefault();
    document.execCommand(comando);
    this.detalhes = this.editorRef.nativeElement.innerHTML;
  }

  guardarSelecao(): void {
    const s = window.getSelection();
    this.selecao = s && s.rangeCount && this.editorRef.nativeElement.contains(s.anchorNode) ? s.getRangeAt(0).cloneRange() : null;
  }

  cor(comando: 'foreColor' | 'hiliteColor', e: Event): void {
    const valor = (e.target as HTMLInputElement).value;
    this.editorRef.nativeElement.focus();
    if (this.selecao) {
      const s = window.getSelection()!;
      s.removeAllRanges();
      s.addRange(this.selecao);
    }
    document.execCommand('styleWithCSS', false, 'true');
    document.execCommand(comando, false, valor);
    this.detalhes = this.editorRef.nativeElement.innerHTML;
  }

  novaRecomendacao(): void {
    this.recIndice = null;
    this.rec = { plantio: '', produto: '', unidade: '', dose: null, areaHa: null, volumeTotal: null, dataAplicacao: null, observacao: '' };
  }

  editarRecomendacao(i: number): void {
    this.recIndice = i;
    this.rec = { ...this.recomendacoes[i] };
  }

  calcularVolume(): void {
    if (this.rec && this.rec.dose !== null && this.rec.areaHa !== null) {
      this.rec.volumeTotal = Math.round(this.rec.dose * this.rec.areaHa * 100) / 100;
    }
  }

  confirmarRecomendacao(): void {
    if (!this.rec) return;
    if (this.recIndice === null) this.recomendacoes.push(this.rec);
    else this.recomendacoes[this.recIndice] = this.rec;
    this.rec = null;
  }

  ultimas(): Recomendacao[] {
    return this.cooperado && this.propriedade ? this.servico.ultimasRecomendacoes(this.cooperado, this.propriedade, this.editando?.id) : [];
  }

  usarUltimas(): void {
    this.recomendacoes.push(...this.ultimas().map(r => ({ ...r, dataAplicacao: null })));
  }

  adicionarArquivos(e: Event): void {
    const input = e.target as HTMLInputElement;
    for (const f of Array.from(input.files ?? [])) this.arquivos.push({ nome: f.name, tamanho: f.size, url: URL.createObjectURL(f) });
    input.value = '';
  }

  tamanho(b: number): string {
    return b < 1024 * 1024 ? `${Math.max(1, Math.round(b / 1024))} KB` : `${(b / 1024 / 1024).toFixed(1).replace('.', ',')} MB`;
  }

  num(v: number | null): string {
    return v === null || v === undefined ? '-' : v.toLocaleString('pt-BR', { maximumFractionDigits: 2 });
  }

  salvar(): void {
    if (!this.valido()) return;
    const v = this.servico.salvar({
      id: this.editando?.id, tipo: this.tipo, cultura: this.cultura, cooperado: this.cooperado, propriedade: this.propriedade,
      data: this.data, tecnico: this.tecnico, descricao: this.descricao.trim(), detalhes: this.editorRef.nativeElement.innerHTML,
      recomendacoes: this.recomendacoes, arquivos: this.arquivos,
    }, this.usuario);
    this.editando = v;
    this.router.navigate(['/portal/visita-tecnica']);
  }
}
