import { Ligacao, TipoLei } from './modelos';

export const fmt0 = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 });
export const fmt1 = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 });

export function num(v: number | null | undefined): string { return fmt0.format(v || 0); }
export function pct(a: number, b: number): string { return b > 0 ? fmt0.format(100 * a / b) + '%' : '—'; }
export function cap(s: string): string { return s.charAt(0).toUpperCase() + s.slice(1); }

export function esc(s: unknown): string {
  return String(s == null ? '' : s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
}
export function icone(n: string): string { return '<span class="material-icons" aria-hidden="true">' + n + '</span>'; }

export const TIPO_LEI: Record<TipoLei, string> = {
  'app': 'APP a recompor',
  'rl': 'Reserva Legal sem vegetação',
  'app-rl': 'APP a recompor e Reserva Legal sem vegetação',
};

export function ehLegal(l: Ligacao): boolean { return l.leiPct >= 50; }

export const TILE_SATELITE = 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}';
export const TILE_ROTULOS = 'https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}';
