import { HttpErrorResponse } from '@angular/common/http';
import { mensagemDeErro } from '../../api/agrocert-api.service';

const MESES = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho', 'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro'];

const maiuscula = (s: string) => s.charAt(0).toLocaleUpperCase('pt-BR') + s.slice(1);

export function nomeMes(mes: number): string { return mes >= 1 && mes <= 12 ? maiuscula(MESES[mes - 1]) : String(mes); }
export function mesCurto(mes: number): string { return mes >= 1 && mes <= 12 ? maiuscula(MESES[mes - 1].slice(0, 3)) : String(mes); }
export function mesAno(mes: number, ano: number): string { return `${mesCurto(mes)}/${String(((ano % 100) + 100) % 100).padStart(2, '0')}`; }

export function arredondar(v: number, casas = 0): number {
  const p = Math.pow(10, casas);
  const x = v * p;
  const r = Math.round(x);
  const meio = Math.abs(x % 1) === 0.5;
  const res = meio ? (r % 2 === 0 ? r : r - 1) : r;
  return res / p;
}

export function n(v: number | null | undefined, casas = 1): string {
  if (v === null || v === undefined || isNaN(v)) return '';
  // toFixed com muitas casas devolve a expansão decimal exata do double (até 100 casas)
  const exato = Math.abs(v).toFixed(Math.min(100, casas + 60));
  const [int, frac = ''] = exato.split('.');
  const resto = frac.slice(casas);
  let digitos = (int + frac.slice(0, casas)).replace(/^0+(?=\d)/, '');
  const d0 = resto.charCodeAt(0) - 48;
  const restoNaoZero = /[1-9]/.test(resto.slice(1));
  const impar = (digitos.charCodeAt(digitos.length - 1) - 48) % 2 === 1;
  if (d0 > 5 || (d0 === 5 && (restoNaoZero || impar))) digitos = somarUm(digitos);
  const t = digitos.padStart(casas + 1, '0');
  const parteInt = t.slice(0, t.length - casas);
  const parteDec = casas > 0 ? t.slice(t.length - casas) : '';
  const milhar = parteInt.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  const corpo = parteDec ? `${milhar},${parteDec}` : milhar;
  // como o .NET Core 3.0+, negativo que arredonda para zero mantém o sinal ("-0,0")
  return v < 0 || Object.is(v, -0) ? '-' + corpo : corpo;
}

function somarUm(d: string): string {
  const a = d.split('');
  let i = a.length - 1;
  while (i >= 0 && a[i] === '9') a[i--] = '0';
  if (i < 0) return '1' + a.join('');
  a[i] = String(+a[i] + 1);
  return a.join('');
}

export function s(v: number, casas = 1): string {
  const r = arredondar(v, casas);
  if (r === 0) return n(0, casas);
  return (r > 0 ? '+' : '−') + n(Math.abs(r), casas);
}

export function pct(fracao: number): string { return n(arredondar(fracao * 100), 0).replace(/\./g, '') + '%'; }

export function f(v: number): string {
  const r = Math.round(v * 100) / 100;
  return String(Object.is(r, -0) ? 0 : r);
}

export function nomeFase(fase: string | null | undefined): string {
  return fase === 'NINO' ? 'El Niño' : fase === 'NINA' ? 'La Niña' : 'Neutro';
}

export function classeFase(fase: string | null | undefined): string {
  return fase === 'NINO' ? 'cl-fase--nino' : fase === 'NINA' ? 'cl-fase--nina' : 'cl-fase--neutro';
}

export function rotuloOrigem(origem: string | null | undefined): string {
  switch (origem) {
    case 'observado': return 'ONI observado';
    case 'informado': return 'ONI informado (previsão oficial)';
    case 'modelo': return 'ONI projetado pelo modelo';
    case 'climatologia': return 'Sem sinal de ENSO (além de 24 meses)';
    default: return origem ?? '';
  }
}

export function risco(tipo: string): { icone: string; rotulo: string } {
  switch (tipo) {
    case 'SECA': return { icone: 'wb_sunny', rotulo: 'Chuva abaixo do normal' };
    case 'EXCESSO_CHUVA': return { icone: 'thunderstorm', rotulo: 'Chuva acima do normal' };
    case 'CALOR': return { icone: 'device_thermostat', rotulo: 'Calor acima do normal' };
    case 'FRIO': return { icone: 'ac_unit', rotulo: 'Frio / geada' };
    case 'UMIDADE_BAIXA': return { icone: 'water_drop', rotulo: 'Umidade baixa' };
    default: return { icone: 'info', rotulo: tipo };
  }
}

export function nomeVariavel(v: string): string {
  switch (v) {
    case 'chuva': return 'Chuva';
    case 'maxima': return 'Temp. máxima';
    case 'minima': return 'Temp. mínima';
    case 'temp_ambiente': return 'Temp. média';
    case 'umidade_relativa': return 'Umidade relativa';
    default: return v;
  }
}

export function unidade(v: string): string { return v === 'chuva' ? 'mm' : v === 'umidade_relativa' ? '%' : '°C'; }

export function nomeProblema(tipo: string): string {
  switch (tipo) {
    case 'ENSO_INCONSISTENTE': return 'ENSO gravado errado';
    case 'MES_FALTANDO': return 'Mês faltando';
    case 'DUPLICADO': return 'Registro duplicado';
    case 'FORA_DO_PADRAO': return 'Valor fora do padrão';
    case 'VALOR_IMPOSSIVEL': return 'Valor impossível';
    default: return tipo;
  }
}

const AZUL = ['#dbe8f9', '#b7d3f6', '#86b6ef', '#5598e7'];
const VERMELHO = ['#f9dcdb', '#f4bcbb', '#ec8f8e', '#e56a69'];
export const NEUTRO = '#f0efec';

export function corDivergente(valor: number, escala: number, quente: boolean): string {
  const a = Math.abs(valor) / (escala <= 0 ? 1 : escala);
  if (a < 0.12) return NEUTRO;
  const i = a < 0.35 ? 0 : a < 0.6 ? 1 : a < 0.85 ? 2 : 3;
  return quente ? VERMELHO[i] : AZUL[i];
}

export function passo(max: number, qtd = 4): number {
  const bruto = Math.max(max, 1e-9) / qtd;
  const pot = Math.pow(10, Math.floor(Math.log10(bruto)));
  for (const k of [1, 2, 2.5, 5, 10]) if (k * pot >= bruto) return k * pot;
  return 10 * pot;
}

export function barraArredondada(x: number, y0: number, y1: number, w: number): string {
  const h = y0 - y1;
  if (h <= 0.5) return '';
  const r = Math.min(4, Math.min(w / 2, h));
  return `M${f(x)},${f(y0)} V${f(y1 + r)} Q${f(x)},${f(y1)} ${f(x + r)},${f(y1)} H${f(x + w - r)} Q${f(x + w)},${f(y1)} ${f(x + w)},${f(y1 + r)} V${f(y0)} Z`;
}

export function barraDivergente(x: number, w: number, y0: number, y1: number, positivo: boolean): string {
  const h = Math.abs(y1 - y0);
  if (h < 0.5) return '';
  const r = Math.min(3, Math.min(w / 2, h));
  return positivo
    ? `M${f(x)},${f(y0)} V${f(y1 + r)} Q${f(x)},${f(y1)} ${f(x + r)},${f(y1)} H${f(x + w - r)} Q${f(x + w)},${f(y1)} ${f(x + w)},${f(y1 + r)} V${f(y0)} Z`
    : `M${f(x)},${f(y0)} V${f(y1 - r)} Q${f(x)},${f(y1)} ${f(x + r)},${f(y1)} H${f(x + w - r)} Q${f(x + w)},${f(y1)} ${f(x + w)},${f(y1 - r)} V${f(y0)} Z`;
}

export interface ErroClima {
  indisponivel: boolean;
  mensagem: string;
  endereco: string;
}

export function erroClima(e: unknown): ErroClima {
  const corpo = e instanceof HttpErrorResponse && e.error && typeof e.error === 'object' ? e.error : null;
  return {
    indisponivel: e instanceof HttpErrorResponse && e.status === 503,
    mensagem: mensagemDeErro(e),
    endereco: (corpo && typeof corpo.endereco === 'string' && corpo.endereco) || 'http://localhost:3030',
  };
}
