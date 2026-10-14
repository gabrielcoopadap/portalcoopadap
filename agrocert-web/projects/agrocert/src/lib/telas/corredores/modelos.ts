export interface EstudoCorredores {
  geradoEm: string;
  anos: number[];
  distancias: number[];
  vegetacoes: VegetacaoCorredores[];
  parametros: ParametrosCorredores;
  paleta: PaletaCorredores;
  cooperados: { naPlanilha: number; comMapa: number };
  semMapa?: { car: string; nome: string; municipio: string }[];
  avisos?: string[];
  regioes: RegiaoCorredores[];
}

export interface VegetacaoCorredores {
  id: 'matas' | 'nativa' | string;
  nome: string;
  descricao: string;
}

export interface ParametrosCorredores {
  corredorMinHa: number;
  ligacaoMaxM: number;
  bufferRegiaoM: number;
  fonte?: string;
}

export interface PaletaCorredores {
  isolado: string;
  outros: string;
  perda: string;
  ganho: string;
  estavel: string;
}

export interface RegiaoCorredores {
  id: string;
  nome: string;
  limites: number[];
  contornos: string;
  lei?: LeiRegiao | null;
  outrosImoveis?: number;
  fazendas: FazendaCorredores[];
  cenarios: Record<string, CenarioCorredores>;
  mudancas: Record<string, MudancaVegetacao>;
}

export interface LeiRegiao {
  imagem: string;
  appHa: number;
  rlHa: number;
  ano: number;
}

export interface FazendaCorredores {
  id: number;
  car: string;
  nome: string;
  municipio: string;
  lon: number;
  lat: number;
  areaHa: number;
  rlHa: number;
  appRecomporHa: number;
  appRecomporSemVegetacaoHa: number;
  rlSemVegetacaoHa: number;
  caixa?: number[] | null;
}

export interface CenarioCorredores {
  imagem: string;
  nativaHa: number;
  emCorredorHa: number;
  isolados: { quantidade: number; ha: number };
  classes: { grandes: number; medios: number; pequenos: number };
  corredores: Corredor[];
  ligacoes: Ligacao[];
  fazendas: SituacaoFazenda[];
}

export interface Corredor {
  numero: number;
  ha: number;
  cor: string;
  caixa?: number[] | null;
  fazendas: number[];
}

export type TipoLei = 'app' | 'rl' | 'app-rl';

export interface Ligacao {
  de: number;
  para: number;
  deHa: number;
  paraHa: number;
  distanciaM: number;
  juntosHa: number;
  linha: number[][];
  fazendas: number[];
  outrosImoveis: number;
  leiPct: number;
  leiTipo?: TipoLei | null;
  naFazenda?: boolean | null;
}

export type SituacaoRl = 'conectada' | 'isolada' | 'sem-vegetacao' | 'sem-rl';

export interface SituacaoFazenda {
  id: number;
  nativaHa: number;
  emCorredorHa: number;
  corredor: number;
  corredorHa: number;
  rlNativaHa: number;
  rlEmCorredorHa: number;
  ligacoes: Ligacao[];
  situacaoRl: SituacaoRl | string;
}

export interface MudancaVegetacao {
  imagem: string;
  perdaHa: number;
  ganhoHa: number;
  estavelHa: number;
  fazendas: { id: number; perdaHa: number; ganhoHa: number; rlPerdaHa: number }[];
}
