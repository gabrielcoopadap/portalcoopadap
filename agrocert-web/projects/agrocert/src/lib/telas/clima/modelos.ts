export type FaseEnso = 'NINO' | 'NINA' | 'NEUTRO' | string;

export interface PrevisaoAno {
  anoAlvo: number;
  anoReferencia: number;
  ciclo: number;
  cicloDisponivel: boolean;
  pesoCiclo: number;
  nivelIntervalo: number;
  metodo: string;
  resumo: string;
  ensoAno: EnsoAno;
  anosAnalogos: AnoAnalogo[];
  meses: PrevisaoMes[];
  avisos: string[];
  margem: string | null;
}

export interface EnsoAno { oniMedio: number; fasePredominante: FaseEnso; descricao: string; }

export interface AnoAnalogo { ano: number; distancia: number; oniMedio: number; descricao: string; }

export interface PrevisaoMes {
  mes: number;
  ano: number;
  enso: EnsoMes;
  chuva: Faixa;
  maxima: Faixa;
  minima: Faixa;
  tempAmbiente: Faixa;
  umidadeRelativa: Faixa;
  riscos: Risco[];
  margem: string | null;
}

export interface EnsoMes {
  oni: number;
  incerteza: number;
  fase: FaseEnso;
  intensidade: string;
  descricao: string;
  origem: string;
  probElNino: number;
  probLaNina: number;
}

export interface Faixa {
  min: number;
  valor: number;
  max: number;
  normal: number;
  efeitoEnso: number;
  efeitoCiclo: number | null;
  anomalia: number;
  anomaliaPct: number | null;
  probAbaixoNormal: number;
  probDentroNormal: number;
  probAcimaNormal: number;
  observado: number | null;
}

export interface Risco { tipo: string; nivel: string; probabilidade: number; mensagem: string; }

export interface SituacaoEnso {
  ultimoMes: { ano: number; mes: number };
  oni: number;
  fase: FaseEnso;
  intensidade: string;
  episodio: FaseEnso;
  tendencia_3_meses: number;
  resumo: string;
  ultimos_12_meses: OniObservado[];
  proximos_12_meses: OniProjetado[];
  observacao: string | null;
}

export interface OniObservado { ano: number; mes: number; oni: number | null; fase: FaseEnso | null; }

export interface OniProjetado { ano: number; mes: number; oni: number; incerteza: number; fase: FaseEnso; descricao: string; }

export interface ImpactoEnso { periodo: string; comoLer: string; linhas: ImpactoLinha[]; }

export interface ImpactoLinha {
  mes: number;
  variavel: string;
  nome: string;
  normal: number;
  tendenciaPorDecada: number;
  elNinoModerado: number;
  elNinoForte: number;
  laNinaModerada: number;
  elNinoModeradoPct: number | null;
  laNinaModeradaPct: number | null;
  t_el_nino: number;
  t_la_nina: number;
  evidenciaElNino: string;
  evidenciaLaNina: string;
  anosElNino: number;
  anosLaNina: number;
  n: number;
  r2: number;
}

export interface EnsoTela { situacao: SituacaoEnso; impacto: ImpactoEnso; }

export type NivelChuva = 'Menos' | 'Normal' | 'Mais';
export type NivelCalor = 'MaisFrio' | 'Normal' | 'MaisQuente';

export interface AlertaResumoApi { alto?: boolean; texto?: string; item1?: boolean; item2?: string; }

export interface AlertaResumo { alto: boolean; texto: string; }

export interface MesResumo {
  mes: number;
  ano: number;
  chuva: NivelChuva;
  chuvaMm: number;
  chuvaMin: number;
  chuvaMax: number;
  chuvaNormalMm: number;
  calor: NivelCalor;
  maxima: number;
  maximaNormal: number;
  minima: number;
  icone: string;
  frase: string;
  alertas: AlertaResumoApi[];
  jaMedido: boolean;
}

export interface ResumoClimaTela {
  situacao: SituacaoEnso;
  impacto: ImpactoEnso | null;
  meses: MesResumo[];
  titulo: string;
  explicacao: string;
  efeitoNaRegiao: string | null;
  iconeSituacao: string;
  classeSituacao: string;
}

export interface Validacao {
  metodo: string;
  antecedenciaOniMeses: number;
  anos: string;
  comoLer: string;
  porVariavel: Record<string, ResumoValidacao>;
  porMes: Record<string, ValidacaoMes>;
  porFase: Record<string, ResumoValidacao>;
}

export interface ResumoValidacao {
  meses: number;
  erroMedioClimatologia: number;
  erroMedioMetodoAntigo: number;
  erroMedioEnsoObservado: number;
  erroMedioEnsoPrevisto: number;
  ganhoVsClimatologiaPct: number;
  ganhoPrevistoVsClimatologiaPct: number;
  ganhoVsMetodoAntigoPct: number;
  cobertura_intervalo_80: number;
}

export interface ValidacaoMes { erroClimatologia: number; erroEnso: number; ganhoPct: number; }

export interface Qualidade {
  registros: number;
  periodo: string;
  totalProblemas: number;
  porTipo: Record<string, number>;
  problemas: ProblemaDado[];
}

export interface ProblemaDado { tipo: string; ano: number; mes: number; id: number | null; detalhe: string; }

export interface CorrecaoEnso {
  simulacao: boolean;
  registrosCorrigidos: number;
  registrosACorrigir: number;
  alteracoes: { id: number | null; ano: number; mes: number; detalhe: string }[];
}

export interface ParametrosPrevisao { ano: number; oni: string | null; ciclo: number; pesoCiclo: number; nivel: number; }
