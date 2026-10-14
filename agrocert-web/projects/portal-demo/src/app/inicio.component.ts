import { Component, inject } from '@angular/core';
import { NgClass, NgFor, NgIf } from '@angular/common';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { AgroCertApi } from 'agrocert';
import { Visita, VisitasService, dataBr } from './visitas/visitas.service';

interface Cotacao { produto: string; pagamento: string | null; pequeno: number | null; grande: number | null; }
interface Venda { data: string; cliente: string; cultura: string; ov: number; valor: number; situacao: 'Integrada e não Faturada' | 'Faturada e Efetivada'; }
type Periodo = 'hoje' | 'semana' | 'mes';

interface ItemEstoque {
  produto: string;
  quantidade: number;
  unidade: string;
  fundo: string;
  graos: string[];
  forma: 'grao' | 'bola' | 'semente' | 'espiga' | 'raiz' | 'cubo';
  foto?: SafeUrl;
  arquivo?: string | SafeUrl;
  tentativa?: number;
}

@Component({
  selector: 'demo-inicio',
  standalone: true,
  imports: [NgFor, NgIf, NgClass, RouterLink],
  templateUrl: './inicio.component.html',
  styleUrls: ['./inicio.component.css'],
})
export class InicioComponent {
  private readonly sanitizer = inject(DomSanitizer);
  nome = 'USUÁRIO';
  readonly EXTENSOES = ['png', 'jpg', 'jpeg', 'webp'];
  readonly BANNERS = ['banner-coopadap', 'banner'].flatMap(n => this.EXTENSOES.map(e => `img/${n}.${e}`));
  bannerTentativa = 0;
  readonly atualizacao = formatarAgora();

  readonly estoque: ItemEstoque[] = [
    { produto: 'Cafe', quantidade: 6374848, unidade: 'KG', fundo: '#3b2216', graos: ['#5a3220', '#6e3d24', '#4a2a1a'], forma: 'grao' },
    { produto: 'Beterraba', quantidade: 2453, unidade: 'SAC', fundo: '#6b4a2e', graos: ['#9b1747', '#b3205a', '#7e123a'], forma: 'raiz' },
    { produto: 'Triticale', quantidade: 293480, unidade: 'KG', fundo: '#8a7458', graos: ['#b49a76', '#a0875f', '#c4ad8a'], forma: 'semente' },
    { produto: 'Soja', quantidade: 4821623, unidade: 'KG', fundo: '#b0702e', graos: ['#d99a52', '#e3a965', '#c8853d'], forma: 'bola' },
    { produto: 'Sorgo', quantidade: 5841753, unidade: 'KG', fundo: '#7a2414', graos: ['#b8391d', '#c94b26', '#a02e18'], forma: 'bola' },
    { produto: 'Milho', quantidade: 19378912, unidade: 'KG', fundo: '#d98b06', graos: ['#f2b51c', '#f8c636', '#e8a10f'], forma: 'cubo' },
    { produto: 'Aveia', quantidade: 1287450, unidade: 'KG', fundo: '#a89571', graos: ['#d8c9a3', '#cbb88c', '#e3d7b6'], forma: 'semente' },
    { produto: 'Trigo', quantidade: 3105960, unidade: 'KG', fundo: '#9a6a2c', graos: ['#c99248', '#d6a258', '#b9823b'], forma: 'espiga' },
    { produto: 'Cenoura', quantidade: 1842, unidade: 'SAC', fundo: '#c4470f', graos: ['#ef6c1a', '#f57f2a', '#e05a12'], forma: 'raiz' },
  ];

  readonly dataBr = dataBr;

  readonly cotacoes: Cotacao[] = [
    { produto: 'Aveia Em Graos', pagamento: null, pequeno: 45, grande: 40 },
    { produto: 'Milho Em Graos', pagamento: null, pequeno: 74, grande: 71 },
    { produto: 'Soja Em Graos', pagamento: diasAFrente(36), pequeno: null, grande: 145 },
    { produto: 'Sorgo Em Graos', pagamento: null, pequeno: 64, grande: 60 },
    { produto: 'Trigo Em Graos', pagamento: diasAFrente(19), pequeno: null, grande: 96 },
    { produto: 'Triticale Em Graos', pagamento: null, pequeno: null, grande: 66 },
  ];
  porPagina = 10;
  paginaCot = 0;

  readonly ultimasVisitas: Visita[] = [...inject(VisitasService).todas()]
    .sort((a, b) => b.data.localeCompare(a.data) || b.numero - a.numero).slice(0, 3);

  readonly ultimasVendas: Venda[] = [
    { data: diasAFrente(0), cliente: 'Empório Serra Verde LTDA', cultura: 'Culturas-alho', ov: 7720, valor: 10325, situacao: 'Integrada e não Faturada' },
    { data: diasAFrente(0), cliente: 'Comercial Vale do Sol Alimentos', cultura: 'Culturas-alho', ov: 7716, valor: 4000, situacao: 'Integrada e não Faturada' },
    { data: diasAFrente(-1), cliente: 'Cerealista Horizonte LTDA', cultura: 'Culturas-milho', ov: 7713, valor: 130980.72, situacao: 'Faturada e Efetivada' },
  ];

  periodo: Periodo = 'hoje';
  readonly resumo: Record<Periodo, { vendas: number; valor: number; produtor: string }> = {
    hoje: { vendas: 3, valor: 145305.72, produtor: 'Gustavo Henrique Rezende' },
    semana: { vendas: 9, valor: 412870.4, produtor: 'Carlos Eduardo Nunes' },
    mes: { vendas: 31, valor: 1873402.15, produtor: 'Beatriz Lima Moreira' },
  };

  constructor() {
    for (const p of this.estoque) {
      p.foto = this.sanitizer.bypassSecurityTrustUrl(desenharFoto(p));
      p.tentativa = 0;
      p.arquivo = this.caminho(p);
    }
    inject(AgroCertApi).sessao().subscribe({ next: s => { if (s.nome) this.nome = s.nome.toUpperCase(); }, error: () => {} });
  }

  semFoto(p: ItemEstoque): void {
    if (p.arquivo === p.foto) return;
    p.tentativa = (p.tentativa ?? 0) + 1;
    p.arquivo = p.tentativa < this.EXTENSOES.length ? this.caminho(p) : p.foto;
  }

  private caminho(p: ItemEstoque): string {
    return `img/estoque/${nomeArquivo(p.produto)}.${this.EXTENSOES[p.tentativa ?? 0]}`;
  }

  formatar(v: number): string {
    return v.toLocaleString('pt-BR');
  }

  moeda(v: number | null): string {
    return v == null ? '-' : v.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  totalPagCot(): number {
    return Math.max(1, Math.ceil(this.cotacoes.length / this.porPagina));
  }

  paginaCotacoes(): Cotacao[] {
    const i = this.paginaCot * this.porPagina;
    return this.cotacoes.slice(i, i + this.porPagina);
  }

  atualizarResumo(): void {
  }
}

function diasAFrente(n: number): string {
  const d = new Date(Date.now() + n * 86400000);
  const z = (x: number) => String(x).padStart(2, '0');
  return `${d.getFullYear()}-${z(d.getMonth() + 1)}-${z(d.getDate())}`;
}

function nomeArquivo(produto: string): string {
  return produto.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim().replace(/[^a-z0-9]+/g, '-');
}

function formatarAgora(): string {
  const d = new Date();
  const z = (n: number) => String(n).padStart(2, '0');
  return `${z(d.getDate())}/${z(d.getMonth() + 1)}/${d.getFullYear()} ${z(d.getHours())}:${z(d.getMinutes())}`;
}

function desenharFoto(p: ItemEstoque): string {
  let semente = p.produto.split('').reduce((a, c) => a * 31 + c.charCodeAt(0), 7) >>> 0;
  const rnd = () => ((semente = (semente * 1664525 + 1013904223) >>> 0) / 4294967296);
  const formas: string[] = [];
  const n = p.forma === 'raiz' ? 7 : p.forma === 'espiga' ? 26 : 70;
  for (let i = 0; i < n; i++) {
    const x = rnd() * 100, y = rnd() * 110, a = Math.round(rnd() * 180), cor = p.graos[i % p.graos.length];
    switch (p.forma) {
      case 'grao':
        formas.push(`<g transform="translate(${x} ${y}) rotate(${a})"><ellipse rx="7.5" ry="5.2" fill="${cor}"/><path d="M-6 0 Q0 1.5 6 0" stroke="#1f120b" stroke-width="1.1" fill="none"/><ellipse cx="-2" cy="-2.4" rx="3" ry="1.2" fill="#fff" opacity=".18"/></g>`);
        break;
      case 'bola':
        formas.push(`<g transform="translate(${x} ${y})"><circle r="${p.produto === 'Sorgo' ? 3.6 : 5.6}" fill="${cor}"/><circle cx="-1.6" cy="-1.6" r="1.6" fill="#fff" opacity=".28"/></g>`);
        break;
      case 'semente':
        formas.push(`<g transform="translate(${x} ${y}) rotate(${a})"><ellipse rx="7" ry="2.6" fill="${cor}"/><path d="M-6 0 H6" stroke="#000" stroke-opacity=".18" stroke-width=".8"/></g>`);
        break;
      case 'cubo':
        formas.push(`<g transform="translate(${x} ${y}) rotate(${a % 30 - 15})"><rect x="-4.5" y="-5" width="9" height="10" rx="2.5" fill="${cor}"/><rect x="-2.5" y="-3.5" width="3" height="3" rx="1" fill="#fff" opacity=".3"/></g>`);
        break;
      case 'espiga':
        formas.push(`<g transform="translate(${x} ${y}) rotate(${a})"><ellipse rx="10" ry="3.2" fill="${cor}"/><ellipse cx="4" rx="4" ry="2" fill="#fff" opacity=".15"/></g>`);
        break;
      case 'raiz':
        formas.push(p.produto === 'Beterraba'
          ? `<g transform="translate(${x} ${y})"><path d="M0 -14 L-4 -26 M0 -14 L5 -27" stroke="#3f8a2e" stroke-width="3"/><circle r="13" fill="${cor}"/><circle cx="-4" cy="-4" r="4" fill="#fff" opacity=".18"/></g>`
          : `<g transform="translate(${x} ${y}) rotate(${a})"><path d="M-22 -4 Q0 -6 24 0 Q0 6 -22 4 Z" fill="${cor}"/><path d="M-10 -3 V3 M2 -3 V3" stroke="#000" stroke-opacity=".15"/></g>`);
        break;
    }
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 110" preserveAspectRatio="xMidYMid slice">`
    + `<rect width="100" height="110" fill="${p.fundo}"/>${formas.join('')}`
    + `<rect width="100" height="110" fill="url(#v)" opacity=".35"/>`
    + `<defs><radialGradient id="v"><stop offset=".6" stop-opacity="0"/><stop offset="1" stop-color="#000"/></radialGradient></defs></svg>`;
  return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
}
