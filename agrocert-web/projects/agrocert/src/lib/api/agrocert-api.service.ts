import { HttpClient, HttpErrorResponse, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { AGROCERT_CONFIG } from '../config';
import {
  Alerta, Apontamento, Certificacao, Especie, LeituraNdvi, NovaPropriedade, NovaVistoria, Painel, Propriedade,
  RegistroBio, RelatorioAmbiental, Requisito, Sessao, SituacaoCorredores, Vistoria,
} from './modelos';

export type Parametros = Record<string, string | number | boolean | null | undefined>;

@Injectable({ providedIn: 'root' })
export class AgroCertApi {
  private readonly http = inject(HttpClient);
  private readonly config = inject(AGROCERT_CONFIG);

  url(caminho: string): string {
    return `${this.config.apiUrl}/${caminho.replace(/^\/+/, '')}`;
  }

  arquivo(url: string | null | undefined): string | null {
    if (!url) return null;
    if (/^(https?:|data:|blob:)/i.test(url)) return url;
    return this.url(url);
  }

  get<T>(caminho: string, params?: Parametros): Observable<T> {
    return this.http.get<T>(this.url(caminho), { params: this.params(params) });
  }

  post<T>(caminho: string, corpo: unknown, params?: Parametros): Observable<T> {
    return this.http.post<T>(this.url(caminho), corpo, { params: this.params(params) });
  }

  put<T>(caminho: string, corpo: unknown): Observable<T> {
    return this.http.put<T>(this.url(caminho), corpo);
  }

  delete(caminho: string): Observable<void> {
    return this.http.delete<void>(this.url(caminho));
  }

  blob(caminho: string, params?: Parametros): Observable<HttpResponse<Blob>> {
    return this.http.get(this.url(caminho), { params: this.params(params), responseType: 'blob', observe: 'response' });
  }

  baixar(caminho: string, padrao: string, params?: Parametros): Observable<void> {
    return this.blob(caminho, params).pipe(map(r => salvarBlob(r.body!, nomeDoArquivo(r) ?? padrao)));
  }

  abrirPdf(caminho: string, params?: Parametros): Observable<void> {
    return this.blob(caminho, params).pipe(map(r => {
      const url = URL.createObjectURL(r.body!);
      window.open(url, '_blank');
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    }));
  }

  sessao(): Observable<Sessao> { return this.get<Sessao>('api/sessao'); }
  painel(): Observable<Painel> { return this.get<Painel>('api/painel'); }

  certificacoes(): Observable<Certificacao[]> { return this.get('api/certificacoes'); }
  certificacao(id: number): Observable<Certificacao> { return this.get(`api/certificacoes/${id}`); }
  criarCertificacao(c: Partial<Certificacao>): Observable<Certificacao> { return this.post('api/certificacoes', c); }
  atualizarCertificacao(id: number, c: Partial<Certificacao>): Observable<Certificacao> { return this.put(`api/certificacoes/${id}`, c); }
  excluirCertificacao(id: number): Observable<void> { return this.delete(`api/certificacoes/${id}`); }

  requisitos(): Observable<Requisito[]> { return this.get('api/requisitos'); }
  criarRequisito(r: Partial<Requisito>): Observable<Requisito> { return this.post('api/requisitos', r); }
  atualizarRequisito(id: number, r: Partial<Requisito>): Observable<Requisito> { return this.put(`api/requisitos/${id}`, r); }
  excluirRequisito(id: number): Observable<void> { return this.delete(`api/requisitos/${id}`); }
  baixarListaMestra(certificacao?: string | null): Observable<void> {
    return this.baixar('api/requisitos/lista-mestra.xlsx', 'lista-mestra.xlsx', { certificacao });
  }

  vistorias(): Observable<Vistoria[]> { return this.get('api/vistorias'); }
  vistoria(id: number): Observable<Vistoria> { return this.get(`api/vistorias/${id}`); }
  pendencias(): Observable<Apontamento[]> { return this.get('api/vistorias/pendencias'); }
  criarVistoria(v: NovaVistoria): Observable<Vistoria> { return this.post('api/vistorias', v); }
  excluirVistoria(id: number): Observable<void> { return this.delete(`api/vistorias/${id}`); }
  adicionarApontamento(vistoriaId: number, form: FormData): Observable<Apontamento> {
    return this.post(`api/vistorias/${vistoriaId}/apontamentos`, form);
  }
  encerrarApontamento(id: number, form: FormData): Observable<Apontamento> { return this.put(`api/vistorias/apontamentos/${id}/encerrar`, form); }
  reabrirApontamento(id: number): Observable<void> { return this.put(`api/vistorias/apontamentos/${id}/reabrir`, {}); }
  excluirApontamento(id: number): Observable<void> { return this.delete(`api/vistorias/apontamentos/${id}`); }
  abrirRelatorioVistoria(id: number): Observable<void> { return this.abrirPdf(`api/vistorias/${id}/relatorio/pdf`); }

  propriedades(): Observable<Propriedade[]> { return this.get('api/ambiental/propriedades'); }
  propriedade(id: number): Observable<Propriedade> { return this.get(`api/ambiental/propriedades/${id}`); }
  cadastrarPropriedade(p: NovaPropriedade): Observable<Propriedade> { return this.post('api/ambiental/propriedades', p); }
  excluirPropriedade(id: number): Observable<void> { return this.delete(`api/ambiental/propriedades/${id}`); }
  sincronizarAlertas(id: number): Observable<Alerta[]> { return this.post(`api/ambiental/propriedades/${id}/sincronizar-alertas`, {}); }
  ndvi(id: number): Observable<LeituraNdvi[]> { return this.get(`api/ambiental/propriedades/${id}/ndvi`); }
  sincronizarNdvi(id: number): Observable<LeituraNdvi[]> { return this.post(`api/ambiental/propriedades/${id}/sincronizar-ndvi`, {}); }
  fichaAmbiental(id: number): Observable<RelatorioAmbiental> { return this.get(`api/ambiental/propriedades/${id}/relatorio`); }
  abrirFichaPdf(id: number): Observable<void> { return this.abrirPdf(`api/ambiental/propriedades/${id}/relatorio.pdf`); }
  conformidade<T>(id: number): Observable<T> { return this.get(`api/ambiental/propriedades/${id}/conformidade`); }
  caminhoSatelite(id: number, tipo: 'cor' | 'ndvi', ano: number, caixa: number[]): string {
    const c = caixa.map(v => v.toFixed(6)).join(',');
    return `api/ambiental/propriedades/${id}/conformidade/satelite?tipo=${tipo}&ano=${ano}&caixa=${c}`;
  }

  painelFaunaFlora<T>(meses: number): Observable<T> { return this.get('api/fauna-flora/painel', { meses }); }
  especies(): Observable<Especie[]> { return this.get('api/fauna-flora/especies'); }
  criarEspecie(form: FormData): Observable<Especie> { return this.post('api/fauna-flora/especies', form); }
  atualizarEspecie(id: number, form: FormData): Observable<Especie> { return this.put(`api/fauna-flora/especies/${id}`, form); }
  excluirEspecie(id: number): Observable<void> { return this.delete(`api/fauna-flora/especies/${id}`); }
  registros(): Observable<RegistroBio[]> { return this.get('api/fauna-flora/registros'); }
  criarRegistro(form: FormData): Observable<RegistroBio> { return this.post('api/fauna-flora/registros', form); }
  excluirRegistro(id: number): Observable<void> { return this.delete(`api/fauna-flora/registros/${id}`); }

  situacaoCorredores(): Observable<SituacaoCorredores> { return this.get('api/corredores'); }
  corredores<T>(): Observable<T> { return this.get('api/corredores/arquivo/corredores.json'); }
  caminhoArquivoCorredores(nome: string): string { return `api/corredores/arquivo/${nome}`; }

  clima<T>(rota: 'resumo' | 'previsao' | 'enso' | 'validacao' | 'dados', params?: Parametros): Observable<T> {
    return this.get(`api/clima/${rota}`, params);
  }
  corrigirEnso<T>(aplicar: boolean): Observable<T> { return this.post('api/clima/dados/corrigir-enso', {}, { aplicar }); }

  private params(p?: Parametros): HttpParams | undefined {
    if (!p) return undefined;
    let hp = new HttpParams();
    for (const [k, v] of Object.entries(p)) {
      if (v === null || v === undefined || v === '') continue;
      hp = hp.set(k, String(v));
    }
    return hp;
  }
}

export function mensagemDeErro(e: unknown): string {
  if (e instanceof HttpErrorResponse) {
    const corpo = e.error;
    if (corpo && typeof corpo === 'object') {
      if (typeof corpo.mensagem === 'string') return corpo.mensagem;
      if (corpo.errors && typeof corpo.errors === 'object') {
        const msgs = Object.values(corpo.errors as Record<string, string[]>).flat();
        if (msgs.length) return msgs.join(' ');
      }
      if (typeof corpo.title === 'string') return corpo.title;
    }
    if (typeof corpo === 'string' && corpo.trim() && !corpo.trim().startsWith('<')) return corpo;
    if (e.status === 0) return 'Não foi possível falar com o servidor do AgroCert. Verifique a conexão.';
    if (e.status === 401) return 'Sua sessão expirou. Entre de novo no portal.';
    if (e.status === 403) return 'Você não tem permissão para esta ação.';
    if (e.status === 404) return 'Não encontrado.';
    return `Erro ${e.status} ao falar com o servidor.`;
  }
  return e instanceof Error ? e.message : String(e);
}

function nomeDoArquivo(r: HttpResponse<Blob>): string | null {
  const cd = r.headers.get('Content-Disposition');
  if (!cd) return null;
  const utf = /filename\*=UTF-8''([^;]+)/i.exec(cd);
  if (utf) return decodeURIComponent(utf[1]);
  const simples = /filename="?([^";]+)"?/i.exec(cd);
  return simples ? simples[1] : null;
}

function salvarBlob(blob: Blob, nome: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = nome;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}
