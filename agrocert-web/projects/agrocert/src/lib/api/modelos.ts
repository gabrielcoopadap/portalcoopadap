export type DataIso = string;
export type DataHoraIso = string;

export interface Sessao {
  autenticado: boolean;
  nome: string | null;
  consultor: boolean;
}

export interface ErroApi {
  mensagem: string;
}

export interface CategoriaConformidade {
  nome: string | null;
  conformes: number | null;
  total: number | null;
}

export interface Certificacao {
  id: number;
  sigla: string | null;
  nome: string;
  escopo: string | null;
  status: string | null;
  cor: string | null;
  conformidade: number | null;
  totalRequisitos: number | null;
  conformes: number | null;
  pendentes: number | null;
  naoConformes: number | null;
  organismo: string | null;
  validoAte: DataIso | null;
  ultimaAuditoria: DataIso | null;
  proximaAuditoria: DataIso | null;
  categorias: CategoriaConformidade[];
}

export interface Referencia {
  norma: string | null;
  versao: string | null;
  codigo: string | null;
}

export interface Requisito {
  id: number;
  codigo: string | null;
  titulo: string | null;
  descricao: string | null;
  tema: string | null;
  prioridade: string | null;
  status: string | null;
  responsavel: string | null;
  periodicidade: string | null;
  prazo: DataIso | null;
  observacao: string | null;
  oQueE: string | null;
  passoAPasso: string | null;
  evidencias: string | null;
  autoverificacao: string | null;
  errosComuns: string | null;
  ondeBuscarAjuda: string | null;
  temGuia: boolean;
  referencias: Referencia[];
}

export interface Apontamento {
  id: number;
  vistoriaId: number;
  vistoriaCodigoDocumento: string | null;
  vistoriaUnidade: string | null;
  vistoriaData: DataIso | null;
  requisitoId: number | null;
  requisitoCodigo: string | null;
  requisitoTitulo: string | null;
  requisitoDescricao: string | null;
  anomalia: string;
  localizacao: string | null;
  grauRisco: string;
  prazo: DataIso | null;
  diasRestantes: number | null;
  responsavelExecucao: string | null;
  situacaoEncontrada: string | null;
  situacaoRequerida: string | null;
  riscoAssociado: string | null;
  imagemEncontradaUrl: string | null;
  imagemRequeridaUrl: string | null;
  status: string;
  dataEncerramento: DataIso | null;
  imagemEvidenciaEncerramentoUrl: string | null;
  observacoesEncerramento: string | null;
  aberto: boolean;
}

export interface Vistoria {
  id: number;
  codigoDocumento: string;
  unidadePropriedade: string;
  setorArea: string | null;
  dataVistoria: DataIso;
  tipo: string;
  tecnicoResponsavel: string | null;
  cargoTecnico: string | null;
  motivo: string | null;
  aprovadoPor: string | null;
  cargoAprovador: string | null;
  usuarioId: string | null;
  usuarioNome: string | null;
  totalApontamentos: number;
  criticos: number;
  altos: number;
  medios: number;
  baixos: number;
  emAberto: number;
  encerrados: number;
  apontamentos: Apontamento[];
}

export interface NovaVistoria {
  unidadePropriedade: string;
  setorArea?: string | null;
  dataVistoria: DataIso;
  tipo: string;
  tecnicoResponsavel?: string | null;
  cargoTecnico?: string | null;
  motivo?: string | null;
  aprovadoPor?: string | null;
  cargoAprovador?: string | null;
}

export interface IndicadorPainel {
  rotulo: string;
  valor: number;
  sub: string;
  cor: string;
  icone: string;
}

export interface Painel {
  indicadores: IndicadorPainel[];
  certificacoes: Certificacao[];
  acoesPrioritarias: Requisito[];
  pendenciasVistoria: Apontamento[];
}

export interface Propriedade {
  id: number;
  nome: string;
  carCodigo: string;
  areaHa: number | null;
  areaReservaLegalHa: number | null;
  areaAppHa: number | null;
  areaVegetacaoNativaHa: number | null;
  municipio: string | null;
  uf: string | null;
  geoJson: string | null;
  geoJsonReservaLegal: string | null;
  geoJsonApp: string | null;
}

export interface NovaPropriedade {
  nome: string;
  carCodigo: string;
  geoJson?: string | null;
}

export interface Alerta {
  id: number;
  codigoAlerta: string | null;
  fonte: string;
  dataDeteccao: DataIso | null;
  areaHa: number | null;
  classeDesmatamento: string | null;
  dentroReservaLegal: boolean | null;
  dentroApp: boolean | null;
  geoJson: string | null;
  imagemAntesUrl: string | null;
  imagemDepoisUrl: string | null;
  status: string;
}

export interface LeituraNdvi {
  id: number;
  dataImagem: DataIso;
  ndviMedio: number | null;
  ndviMinimo: number | null;
  ndviMaximo: number | null;
  percentualAmostrasValidas: number | null;
  amostras: number | null;
  imagemCorRealUrl: string | null;
  imagemMapaCalorUrl: string | null;
  bboxMinLon: number | null;
  bboxMinLat: number | null;
  bboxMaxLon: number | null;
  bboxMaxLat: number | null;
}

export interface RelatorioAmbiental {
  propriedadeId: number;
  propriedadeNome: string;
  carCodigo: string;
  areaHa: number | null;
  areaReservaLegalHa: number | null;
  areaAppHa: number | null;
  areaVegetacaoNativaHa: number | null;
  geoJson: string | null;
  geoJsonReservaLegal: string | null;
  geoJsonApp: string | null;
  totalAlertas: number;
  areaTotalAlertasHa: number;
  ultimoAlerta: DataIso | null;
  situacao: string;
  alertas: Alerta[];
  fontesConsultadas: string[];
  aviso: string;
}

export interface Especie {
  id: number;
  nomePopular: string;
  nomeCientifico: string | null;
  reino: string;
  grupo: string | null;
  statusConservacao: string | null;
  descricao: string | null;
  fotoUrl: string | null;
}

export interface RegistroBio {
  id: number;
  especieId: number;
  especie: string | null;
  nomeCientifico: string | null;
  reino: string | null;
  grupo: string | null;
  statusConservacao: string | null;
  especieFotoUrl: string | null;
  propriedadeId: number | null;
  propriedadeNome: string | null;
  dataHora: DataHoraIso;
  origem: string;
  camera: string | null;
  latitude: number | null;
  longitude: number | null;
  area: string | null;
  quantidade: number | null;
  observacao: string | null;
  fotoUrl: string | null;
  registradoPor: string | null;
  confirmado: boolean;
}

export interface SituacaoCorredores {
  temDados: boolean;
  pasta: string;
  atualizadoEm?: DataHoraIso;
  regioes?: number;
  erro?: string;
}
