import { Requisito } from '../../api/modelos';

const COLLATOR = new Intl.Collator('pt-BR');

function distintosOrdenados(valores: (string | null | undefined)[]): string[] {
  const unicos = Array.from(new Set(valores.filter((v): v is string => typeof v === 'string' && v.length > 0)));
  return unicos.sort(COLLATOR.compare);
}

const vazio = (s: string | null | undefined): boolean => s === null || s === undefined || s === '';
const embranco = (s: string | null | undefined): boolean => vazio(s) || s!.trim() === '';

export function normasPresentes(requisitos: Requisito[]): string[] {
  return distintosOrdenados(requisitos.flatMap(r => r.referencias ?? []).map(x => x.norma));
}

export function temasPresentes(requisitos: Requisito[]): string[] {
  return distintosOrdenados(requisitos.map(r => r.tema));
}

export function filtrar(
  requisitos: Requisito[],
  busca: string | null | undefined,
  tema: string | null | undefined,
  status: string | null | undefined,
  norma: string | null | undefined,
  prioridade: string | null | undefined = null,
): Requisito[] {
  const termo = embranco(busca) ? null : busca!.trim().toLocaleLowerCase('pt-BR');
  return requisitos.filter(r => {
    const refs = r.referencias ?? [];
    if (termo !== null) {
      // Interpolação do C#: null vira "".
      const alvo = `${r.titulo ?? ''} ${r.descricao ?? ''} ${r.tema ?? ''} ${refs.map(x => x.codigo ?? '').join(' ')}`;
      if (!alvo.toLocaleLowerCase('pt-BR').includes(termo)) return false;
    }
    if (!vazio(tema) && r.tema !== tema) return false;
    if (!vazio(status) && r.status !== status) return false;
    if (!vazio(norma) && !refs.some(x => x.norma === norma)) return false;
    if (!vazio(prioridade) && r.prioridade !== prioridade) return false;
    return true;
  });
}
