import { Injectable } from '@angular/core';

export interface Recomendacao {
  plantio: string;
  produto: string;
  unidade: string;
  dose: number | null;
  areaHa: number | null;
  volumeTotal: number | null;
  dataAplicacao: string | null;
  observacao: string;
}

export interface Arquivo {
  nome: string;
  tamanho: number;
  url: string;
}

export interface Visita {
  id: number;
  numero: number;
  tipo: string | null;
  cultura: string;
  cooperado: string;
  propriedade: string;
  data: string;
  tecnico: string;
  descricao: string;
  detalhes: string;
  recomendacoes: Recomendacao[];
  arquivos: Arquivo[];
  criadoPor: string;
  criadoEm: string;
  alteradoPor: string | null;
  alteradoEm: string | null;
}

export const TIPOS_VISITA = ['Visita técnica', 'Acompanhamento de safra', 'Recomendação de insumos', 'Coleta de amostra', 'Vistoria de certificação'];
export const CULTURAS = ['Abacate', 'Alho', 'Batata', 'Beterraba', 'Café', 'Cebola', 'Cenoura', 'Feijão', 'Milho', 'Soja', 'Sorgo', 'Trigo'];
export const UNIDADES = ['kg/ha', 'L/ha', 'g/ha', 'mL/ha', 't/ha', 'sc/ha'];

export const COOPERADOS: { nome: string; propriedades: string[] }[] = [
  { nome: 'Antônio Carlos Ferreira', propriedades: ['Fazenda Boa Vista', 'Sítio Água Limpa'] },
  { nome: 'Beatriz Lima Moreira', propriedades: ['Fazenda Santa Helena'] },
  { nome: 'Carlos Eduardo Nunes', propriedades: ['Fazenda Três Barras', 'Fazenda Pontal'] },
  { nome: 'Daniela Rocha Teixeira', propriedades: ['Sítio São José'] },
  { nome: 'Eduardo Martins Prado', propriedades: ['Fazenda Cachoeira', 'Fazenda Recanto'] },
  { nome: 'Fernanda Alves Costa', propriedades: ['Fazenda Monte Alegre'] },
  { nome: 'Gustavo Henrique Rezende', propriedades: ['Fazenda Serra Azul', 'Sítio Paraíso'] },
  { nome: 'Helena Cristina Vieira', propriedades: ['Fazenda Bela Vista'] },
];
export const TECNICOS = ['Ana Paula Ribeiro', 'Bruno César Andrade', 'Juliana Matos Freitas', 'Marcelo Duarte Lopes', 'Renato Silveira Campos'];

@Injectable({ providedIn: 'root' })
export class VisitasService {
  private lista: Visita[] = gerarExemplos();

  todas(): Visita[] {
    return this.lista;
  }

  obter(id: number): Visita | undefined {
    return this.lista.find(v => v.id === id);
  }

  salvar(v: Omit<Visita, 'id' | 'numero' | 'criadoPor' | 'criadoEm' | 'alteradoPor' | 'alteradoEm'> & { id?: number }, usuario: string): Visita {
    const agora = new Date().toISOString();
    if (v.id) {
      const atual = this.obter(v.id)!;
      Object.assign(atual, v, { alteradoPor: usuario, alteradoEm: agora });
      return atual;
    }
    const id = Math.max(0, ...this.lista.map(x => x.id)) + 1;
    const numero = Math.max(0, ...this.lista.map(x => x.numero)) + 1;
    const nova: Visita = { ...v, id, numero, criadoPor: usuario, criadoEm: agora, alteradoPor: null, alteradoEm: null } as Visita;
    this.lista = [nova, ...this.lista];
    return nova;
  }

  ultimasRecomendacoes(cooperado: string, propriedade: string, ignorarId?: number): Recomendacao[] {
    return this.lista
      .filter(v => v.cooperado === cooperado && v.propriedade === propriedade && v.id !== ignorarId)
      .sort((a, b) => b.data.localeCompare(a.data))
      .flatMap(v => v.recomendacoes)
      .slice(0, 10);
  }
}

function gerarExemplos(): Visita[] {
  let s = 20261007;
  const rnd = () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296);
  const pega = <T>(l: T[]) => l[Math.floor(rnd() * l.length)];
  const produtos: [string, string][] = [['Fertilizante NPK 20-05-20', 'kg/ha'], ['Calcário dolomítico', 't/ha'], ['Fungicida (azoxistrobina)', 'L/ha'],
    ['Inseticida biológico', 'L/ha'], ['Adubo foliar com boro', 'L/ha'], ['Gesso agrícola', 't/ha']];
  const descricoes = ['Acompanhamento do desenvolvimento da lavoura', 'Avaliação de pragas e doenças', 'Recomendação de adubação de cobertura',
    'Coleta de solo para análise', 'Visita de rotina pós-plantio', 'Avaliação de estande e uniformidade', 'Planejamento da próxima safra'];
  const hoje = new Date();
  const lista: Visita[] = [];
  for (let i = 0; i < 64; i++) {
    const c = pega(COOPERADOS);
    const tec = pega(TECNICOS);
    const dias = Math.floor(i * 1.3 + rnd() * 2);
    const criado = new Date(hoje.getTime() - dias * 86400000 - Math.floor(rnd() * 9 * 3600000));
    const alterado = rnd() < 0.8 ? new Date(criado.getTime() + Math.floor(rnd() * 20 * 3600000)) : null;
    const area = Math.round((5 + rnd() * 60) * 10) / 10;
    const recs: Recomendacao[] = rnd() < 0.6 ? [0, 1].slice(0, 1 + Math.floor(rnd() * 2)).map(() => {
      const [produto, unidade] = pega(produtos);
      const dose = Math.round((0.5 + rnd() * 3) * 10) / 10;
      return { plantio: `Talhão ${1 + Math.floor(rnd() * 12)}`, produto, unidade, dose, areaHa: area, volumeTotal: Math.round(dose * area * 10) / 10,
        dataAplicacao: iso(new Date(criado.getTime() + 5 * 86400000)), observacao: '' };
    }) : [];
    lista.push({
      id: 64 - i, numero: 3280 - i, tipo: pega(TIPOS_VISITA), cultura: pega(CULTURAS), cooperado: c.nome, propriedade: pega(c.propriedades),
      data: iso(criado), tecnico: tec, descricao: pega(descricoes),
      detalhes: '<p>Lavoura em bom estado geral. Sem sinais relevantes de pragas.</p>',
      recomendacoes: recs, arquivos: [],
      criadoPor: tec.toUpperCase(), criadoEm: criado.toISOString(),
      alteradoPor: alterado ? tec.toUpperCase() : null, alteradoEm: alterado ? alterado.toISOString() : null,
    });
  }
  return lista;
}

export function iso(d: Date): string {
  const z = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${z(d.getMonth() + 1)}-${z(d.getDate())}`;
}

export function dataBr(v: string | null | undefined): string {
  if (!v) return '-';
  const [a, m, d] = v.slice(0, 10).split('-');
  return `${d}/${m}/${a}`;
}

export function dataHoraBr(v: string | null | undefined): string {
  if (!v) return '-';
  const d = new Date(v);
  const z = (n: number) => String(n).padStart(2, '0');
  return `${z(d.getDate())}/${z(d.getMonth() + 1)}/${d.getFullYear()} ${z(d.getHours())}:${z(d.getMinutes())}:${z(d.getSeconds())}`;
}
