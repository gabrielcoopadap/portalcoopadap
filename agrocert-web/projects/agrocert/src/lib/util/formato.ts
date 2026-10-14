const MESES_ABREV = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];
const MESES = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

export function lerData(v: string | Date | null | undefined): Date | null {
  if (!v) return null;
  if (v instanceof Date) return v;
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(v);
  if (m) return new Date(+m[1], +m[2] - 1, +m[3]);
  const d = new Date(v);
  return isNaN(d.getTime()) ? null : d;
}

const dois = (n: number) => String(n).padStart(2, '0');

export function data(v: string | Date | null | undefined): string {
  const d = lerData(v);
  return d ? `${dois(d.getDate())}/${dois(d.getMonth() + 1)}/${d.getFullYear()}` : '—';
}

export function dataHora(v: string | Date | null | undefined): string {
  const d = lerData(v);
  return d ? `${data(d)} ${dois(d.getHours())}:${dois(d.getMinutes())}` : '—';
}

export function hora(v: string | Date | null | undefined): string {
  const d = lerData(v);
  return d ? `${dois(d.getHours())}:${dois(d.getMinutes())}` : '';
}

export function dataCurta(v: string | Date | null | undefined): string {
  const d = lerData(v);
  return d ? `${d.getDate()} de ${MESES_ABREV[d.getMonth()]}.` : '';
}

export function mesAno(v: string | Date | null | undefined): string {
  const d = lerData(v);
  return d ? `${MESES_ABREV[d.getMonth()]}/${dois(d.getFullYear() % 100)}` : '';
}

export function mesAnoLongo(v: string | Date | null | undefined): string {
  const d = lerData(v);
  return d ? `${MESES[d.getMonth()]} de ${d.getFullYear()}` : '';
}

export function nomeMes(mes: number, abreviado = false): string {
  return (abreviado ? MESES_ABREV : MESES)[(mes - 1 + 12) % 12];
}

export function hojeIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${dois(d.getMonth() + 1)}-${dois(d.getDate())}`;
}

export function numero(v: number | null | undefined, casas = 2): string {
  if (v === null || v === undefined || isNaN(v)) return '-';
  return v.toLocaleString('pt-BR', { minimumFractionDigits: casas, maximumFractionDigits: casas });
}

export function inteiro(v: number | null | undefined): string {
  return numero(v, 0);
}

export function pct(v: number | null | undefined, casas = 0): string {
  return v === null || v === undefined ? '-' : `${numero(v, casas)}%`;
}

export function plural(n: number, singular: string, pluralTxt?: string): string {
  return `${inteiro(n)} ${n === 1 ? singular : pluralTxt ?? singular + 's'}`;
}
