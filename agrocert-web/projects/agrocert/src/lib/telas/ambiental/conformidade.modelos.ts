// IReadOnlyDictionary<int, decimal> chega como objeto com chaves texto: { "2008": 12.3 }.

import { Alerta, LeituraNdvi } from '../../api/modelos';

export type StatusCriterio = 'Conforme' | 'Verificar' | 'IndicioNaoConformidade' | 'SemDados';

export type HaPorAno = Record<string, number>;

export interface CriterioConformidade {
  norma: string;
  titulo: string;
  exigencia: string;
  status: StatusCriterio;
  resultado: string;
  observacao: string | null;
}

export interface MarcoTemporal {
  chave: string;
  rotulo: string;
  anoCorte: number;
  anoUsado: number | null;
  aproximado: boolean;
}

export interface LinhaAreaCar {
  area: string;
  areaHa: number;
  nativaHa: HaPorAno;
}

export interface LinhaClasse {
  codigo: number;
  nome: string;
  cor: string;
  natural: boolean;
  ha: HaPorAno;
}

export interface ImagemAno {
  ano: number;
  dataUri: string | null;
}

export interface JanelaImagem {
  caixa: number[];
  proporcaoLarguraAltura: number;
  mapBiomas: ImagemAno[];
  contornoPrincipal: string;
  contornoImovel: string | null;
  contornoRl: string | null;
  contornoApp: string | null;
}

export interface AreaConversao {
  numero: number;
  areaHa: number;
  naReservaLegalHa: number;
  naAppHa: number;
  anoNatural: number | null;
  anoConvertida: number | null;
  classeAntes: string;
  classeDepois: string;
  marco: string;
  lon: number;
  lat: number;
  janela: JanelaImagem;
  comSatelite: boolean;
  periodo: string;
  anosNatural: number[] | null;
}

export interface ItemLegenda {
  cor: string;
  texto: string;
}

export interface UcProxima {
  nome: string;
  categoria: string | null;
  grupo: string;
  esfera: string | null;
  ehZonaAmortecimento: boolean;
  dentro: boolean;
  areaDentroHa: number | null;
  distanciaM: number;
  status: StatusCriterio;
  situacao: string;
}

export interface RelatorioConformidade {
  propriedadeId: number;
  nome: string;
  car: string;
  municipio: string | null;
  uf: string | null;
  areaHa: number | null;
  areaReservaLegalHa: number | null;
  areaAppHa: number | null;
  areaVegetacaoNativaHa: number | null;
  geoJson: string | null;
  geoJsonReservaLegal: string | null;
  geoJsonApp: string | null;

  anosMapBiomas: number[];
  anosComparados: number[];
  anoAtual: number | null;
  marcos: MarcoTemporal[];
  criterios: CriterioConformidade[];

  porArea: LinhaAreaCar[];
  classes: LinhaClasse[];
  imovel: JanelaImagem | null;
  mapaMudanca: string | null;
  mapaMudancaLimites: number[] | null;
  legendaMudanca: ItemLegenda[];

  areas: AreaConversao[];
  conversaoTotalHa: number;
  manchasPequenas: number;
  manchasPequenasHa: number;
  regeneracaoHa: number;

  ucComDados: boolean;
  raioAmortecimentoM: number;
  ucs: UcProxima[];
  ucsGeoJson: string | null;

  alertas: Alerta[];
  ndvi: LeituraNdvi[];
  copernicusConfigurado: boolean;
  anosSatelite: number[];

  avisos: string[];
  fontes: string[];
  pastaMapBiomas: string;
  pastaUcs: string;
  geradoEm: string;
}
