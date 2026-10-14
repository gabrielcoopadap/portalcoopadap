import { Pipe, PipeTransform, inject } from '@angular/core';
import { AGROCERT_CONFIG } from '../config';
import { AgroCertApi } from '../api/agrocert-api.service';
import * as F from './formato';

@Pipe({ name: 'acData', standalone: true })
export class DataPipe implements PipeTransform {
  transform(v: string | Date | null | undefined): string { return F.data(v); }
}

@Pipe({ name: 'acDataHora', standalone: true })
export class DataHoraPipe implements PipeTransform {
  transform(v: string | Date | null | undefined): string { return F.dataHora(v); }
}

@Pipe({ name: 'acNumero', standalone: true })
export class NumeroPipe implements PipeTransform {
  transform(v: number | null | undefined, casas = 2): string { return F.numero(v, casas); }
}

@Pipe({ name: 'acRota', standalone: true })
export class RotaPipe implements PipeTransform {
  private readonly config = inject(AGROCERT_CONFIG);
  transform(caminho: string | number | (string | number)[] = '', modulo: 'certificacao' | 'clima' = 'certificacao'): string {
    const base = modulo === 'clima' ? this.config.rotaClima : this.config.rotaCertificacao;
    const partes = (Array.isArray(caminho) ? caminho : [caminho]).map(String).filter(p => p !== '');
    return [base, ...partes].join('/') || '/';
  }
}

@Pipe({ name: 'acArquivo', standalone: true })
export class ArquivoPipe implements PipeTransform {
  private readonly api = inject(AgroCertApi);
  transform(url: string | null | undefined): string | null { return this.api.arquivo(url); }
}
