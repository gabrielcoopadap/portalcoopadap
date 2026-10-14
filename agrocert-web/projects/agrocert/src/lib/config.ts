import { EnvironmentProviders, InjectionToken, makeEnvironmentProviders } from '@angular/core';

export interface AgroCertConfig {
  apiUrl: string;
  rotaCertificacao: string;
  rotaClima: string;
}

export const AGROCERT_CONFIG = new InjectionToken<AgroCertConfig>('AGROCERT_CONFIG', {
  providedIn: 'root',
  factory: () => ({ apiUrl: '', rotaCertificacao: '/certificacao', rotaClima: '/clima' }),
});

export function provideAgroCert(config: Partial<AgroCertConfig> = {}): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: AGROCERT_CONFIG,
      useValue: {
        apiUrl: (config.apiUrl ?? '').replace(/\/+$/, ''),
        rotaCertificacao: (config.rotaCertificacao ?? '/certificacao').replace(/\/+$/, ''),
        rotaClima: (config.rotaClima ?? '/clima').replace(/\/+$/, ''),
      } satisfies AgroCertConfig,
    },
  ]);
}
