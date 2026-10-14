export interface Estilo {
  bg: string;
  cor: string;
}

const NEUTRO: Estilo = { bg: '#eeeeee', cor: '#555555' };

export const PRIORIDADES = ['Crítica', 'Maior', 'Menor', 'Recomendado'];
export const STATUS_REQUISITO = ['Conforme', 'Em Andamento', 'Pendente', 'Não Conforme'];
export const STATUS_CERTIFICACAO = ['Conforme', 'Em Andamento', 'Pendente'];
export const NORMAS = ['Rainforest Alliance', 'GlobalG.A.P.', 'GRASP', 'C.A.F.E. Practices', 'SMETA', 'ATITUDES'];
export const GRAUS_RISCO = ['CRITICO', 'ALTO', 'MEDIO', 'BAIXO'];

export const TIPOS_VISTORIA: { valor: string; rotulo: string }[] = [
  { valor: 'MENSAL_CONSULTORIA', rotulo: 'Mensal — Consultoria' },
  { valor: 'DIARIA_FUNCIONARIO', rotulo: 'Diária — Funcionário' },
];

export const SIGLA_NORMA: Record<string, string> = {
  'Rainforest Alliance': 'RA', 'GlobalG.A.P.': 'GG', 'GRASP': 'GR',
  'C.A.F.E. Practices': 'CP', 'SMETA': 'SM', 'ATITUDES': 'AT',
};

export const SITE_NORMA: Record<string, string> = {
  'Rainforest Alliance': 'https://knowledge.rainforest-alliance.org/docs/rainforest-alliance-sustainable-agriculture-standard-farm-requirements',
  'GlobalG.A.P.': 'https://globalgap.org/what-we-offer/solutions/ifa-fruit-and-vegetables/',
  'GRASP': 'https://globalgap.org/what-we-offer/solutions/grasp/',
};

export function sigla(norma: string | null | undefined): string {
  return norma ? SIGLA_NORMA[norma] ?? norma : '';
}

export function estiloPrioridade(p: string | null | undefined): Estilo {
  switch (p) {
    case 'Crítica': return { bg: '#fdeceb', cor: '#c0392b' };
    case 'Maior': return { bg: '#fbeedd', cor: '#bd7d15' };
    case 'Menor': return { bg: '#eef1f4', cor: '#5b6673' };
    case 'Recomendado': return { bg: '#e6f4f1', cor: '#2a7f6f' };
    default: return NEUTRO;
  }
}

export function estiloStatusRequisito(s: string | null | undefined): Estilo {
  switch (s) {
    case 'Conforme': return { bg: '#e4f3ea', cor: '#1f8a4c' };
    case 'Em Andamento': return { bg: '#e5eef8', cor: '#1f6fb2' };
    case 'Pendente': return { bg: '#fdf6da', cor: '#b8860b' };
    case 'Não Conforme': return { bg: '#fdeceb', cor: '#c0392b' };
    default: return NEUTRO;
  }
}

export function estiloStatusCertificacao(s: string | null | undefined): Estilo {
  switch (s) {
    case 'Conforme': return { bg: '#e4f3ea', cor: '#1f8a4c' };
    case 'Em Andamento': return { bg: '#e5eef8', cor: '#1f6fb2' };
    case 'Pendente': return { bg: '#fbeedd', cor: '#bd7d15' };
    default: return NEUTRO;
  }
}

export function estiloGrauRisco(g: string | null | undefined): Estilo {
  switch (g) {
    case 'CRITICO': return { bg: '#fdeceb', cor: '#c0392b' };
    case 'ALTO': return { bg: '#fdf6da', cor: '#b8860b' };
    case 'MEDIO': return { bg: '#e9f1fb', cor: '#1f6fb2' };
    case 'BAIXO': return { bg: '#e4f3ea', cor: '#1f8a4c' };
    default: return NEUTRO;
  }
}

export function rotuloGrauRisco(g: string | null | undefined): string {
  switch (g) {
    case 'CRITICO': return 'Crítico';
    case 'ALTO': return 'Alto';
    case 'MEDIO': return 'Médio';
    case 'BAIXO': return 'Baixo';
    default: return g ?? '-';
  }
}

export function estiloStatusApontamento(s: string | null | undefined): Estilo {
  return s === 'ENCERRADO' ? { bg: '#e4f3ea', cor: '#1f8a4c' } : { bg: '#fbeedd', cor: '#bd7d15' };
}

export function rotuloStatusApontamento(s: string | null | undefined): string {
  return s === 'ABERTO' ? 'Em aberto' : s === 'ENCERRADO' ? 'Encerrado' : s ?? '-';
}

export function rotuloTipoVistoria(tipo: string | null | undefined): string {
  return TIPOS_VISTORIA.find(t => t.valor === tipo)?.rotulo ?? tipo ?? '-';
}

export function rotuloTipoVistoriaLongo(tipo: string | null | undefined): string {
  switch (tipo) {
    case 'MENSAL_CONSULTORIA': return 'Vistoria Mensal (Consultoria)';
    case 'DIARIA_FUNCIONARIO': return 'Vistoria Diária (Funcionário)';
    default: return tipo ?? '-';
  }
}

export function rotuloStatusAlerta(s: string | null | undefined): string {
  switch (s) {
    case 'NECESSITA_VERIFICACAO': return 'Necessita verificação';
    case 'VERIFICADO_MANEJO': return 'Verificado — manejo autorizado';
    case 'VERIFICADO_IRREGULAR': return 'Verificado — irregular';
    case 'DESCARTADO': return 'Descartado';
    default: return s ?? '-';
  }
}

export function classificarNdvi(ndvi: number | null | undefined): { rotulo: string; cor: string } {
  if (ndvi === null || ndvi === undefined) return { rotulo: '-', cor: '#9a9384' };
  if (ndvi < 0.2) return { rotulo: 'Solo exposto / água', cor: '#b0392b' };
  if (ndvi < 0.5) return { rotulo: 'Vegetação esparsa', cor: '#b8860b' };
  if (ndvi < 0.8) return { rotulo: 'Vegetação moderada', cor: '#2f7a54' };
  return { rotulo: 'Vegetação densa', cor: '#15533a' };
}

export function corSuave(hex: string | null | undefined, alfa = '14'): string {
  return !hex || !hex.startsWith('#') || hex.length !== 7 ? '#5e9a4214' : hex + alfa;
}

export function linhas(texto: string | null | undefined): string[] {
  return (texto ?? '').split(/\r?\n/).map(l => l.trim()).filter(l => l.length > 0);
}

export interface GrupoBio {
  nome: string;
  plural: string;
  cor: string;
  icone: string;
}

export const COR_FAUNA = '#5e9a42';
export const COR_FLORA = '#2a78d6';
export const COR_OUTROS = '#9aa0a6';

export const GRUPOS_FAUNA: GrupoBio[] = [
  { nome: 'Ave', plural: 'Aves', cor: '#2a78d6', icone: 'flutter_dash' },
  { nome: 'Mamífero', plural: 'Mamíferos', cor: '#eb6834', icone: 'pets' },
  { nome: 'Réptil', plural: 'Répteis', cor: '#1baf7a', icone: 'egg' },
  { nome: 'Anfíbio', plural: 'Anfíbios', cor: '#eda100', icone: 'water_drop' },
  { nome: 'Inseto', plural: 'Insetos', cor: '#e87ba4', icone: 'bug_report' },
  { nome: 'Peixe', plural: 'Peixes', cor: '#4a3aa7', icone: 'set_meal' },
  { nome: 'Outro', plural: 'Outros', cor: COR_OUTROS, icone: 'cruelty_free' },
];

export const GRUPOS_FLORA: GrupoBio[] = [
  { nome: 'Árvore', plural: 'Árvores', cor: '#008300', icone: 'park' },
  { nome: 'Palmeira', plural: 'Palmeiras', cor: '#1baf7a', icone: 'nature' },
  { nome: 'Arbusto', plural: 'Arbustos', cor: '#eda100', icone: 'grass' },
  { nome: 'Herbácea', plural: 'Herbáceas e flores', cor: '#e87ba4', icone: 'local_florist' },
  { nome: 'Outro', plural: 'Outros', cor: COR_OUTROS, icone: 'eco' },
];

export function grupos(reino: string | null | undefined): GrupoBio[] {
  return reino === 'FAUNA' ? GRUPOS_FAUNA : GRUPOS_FLORA;
}

export function grupo(reino: string | null | undefined, nome: string | null | undefined): GrupoBio {
  const lista = grupos(reino);
  return lista.find(g => g.nome === nome) ?? lista[lista.length - 1];
}

export const AREAS_BIO = ['Reserva Legal', 'APP', 'Área de conservação', 'Lavoura', 'Estrada / acesso', 'Outra'];

export const STATUS_CONSERVACAO: { codigo: string; rotulo: string }[] = [
  { codigo: 'LC', rotulo: 'Pouco preocupante' }, { codigo: 'NT', rotulo: 'Quase ameaçada' },
  { codigo: 'VU', rotulo: 'Vulnerável' }, { codigo: 'EN', rotulo: 'Em perigo' },
  { codigo: 'CR', rotulo: 'Criticamente em perigo' }, { codigo: 'NA', rotulo: 'Não avaliada' },
];

export function rotuloStatusConservacao(codigo: string | null | undefined): string {
  return STATUS_CONSERVACAO.find(s => s.codigo === codigo)?.rotulo ?? 'Não avaliada';
}

export function ameacada(codigo: string | null | undefined): boolean {
  return codigo === 'VU' || codigo === 'EN' || codigo === 'CR';
}

export function rotuloReino(r: string | null | undefined): string {
  return r === 'FAUNA' ? 'Fauna' : 'Flora';
}

export const ORIGENS_REGISTRO = ['AVISTAMENTO', 'CAMERA', 'VESTIGIO', 'COLETA'];

export function rotuloOrigem(o: string | null | undefined): string {
  switch (o) {
    case 'CAMERA': return 'Câmera de monitoramento';
    case 'AVISTAMENTO': return 'Avistamento';
    case 'VESTIGIO': return 'Vestígio (pegada, fezes...)';
    case 'COLETA': return 'Registro de flora / coleta';
    default: return o ?? '-';
  }
}

export function resumoOrigem(o: string | null | undefined, r: string | null | undefined): string {
  switch (o) {
    case 'CAMERA': return 'Registrada por câmera';
    case 'AVISTAMENTO': return 'Avistamento';
    case 'VESTIGIO': return 'Vestígio';
    default: return r === 'FLORA' ? 'Registro de flora' : 'Coleta';
  }
}
