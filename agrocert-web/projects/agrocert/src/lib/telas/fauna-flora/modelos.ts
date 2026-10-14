import { Especie, RegistroBio } from '../../api/modelos';

export interface KpiBio {
  rotulo: string;
  valor: string;
  icone: string;
  variacao: number | null;
  comparacao: string;
  nota: string | null;
}

export interface EspecieDestaque {
  especie: Especie;
  registros: number;
  variacao: number | null;
  fotoUrl: string | null;
}

export interface PontoMes {
  mes: string;
  fauna: number;
  flora: number;
}

export interface FatiaGrupo {
  grupo: string;
  cor: string;
  quantidade: number;
  percentual: number;
}

export interface PropriedadeMapa {
  id: number;
  nome: string;
  geoJson: string | null;
  geoJsonReservaLegal: string | null;
  geoJsonApp: string | null;
}

export interface PainelFaunaFlora {
  indicadores: KpiBio[];
  destaquesHoje: RegistroBio[];
  cameras: RegistroBio[];
  destaquesFauna: EspecieDestaque[];
  destaquesFlora: EspecieDestaque[];
  evolucao: PontoMes[];
  distribuicaoFauna: FatiaGrupo[];
  totalEspeciesFauna: number;
  registrosMapa: RegistroBio[];
  propriedades: PropriedadeMapa[];
  especies: Especie[];
}

export interface PropriedadeItem {
  id: number;
  nome: string;
}
