import { OnDestroy, Pipe, PipeTransform, inject } from '@angular/core';
import { Observable, catchError, map, of, shareReplay } from 'rxjs';
import { AgroCertApi } from '../api/agrocert-api.service';

@Pipe({ name: 'acImagemApi', standalone: true })
export class ImagemApiPipe implements PipeTransform, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private caminho: string | null = null;
  private resultado$: Observable<string | null> = of(null);
  private urls: string[] = [];

  transform(caminho: string | null | undefined): Observable<string | null> {
    if (!caminho) return of(null);
    if (caminho !== this.caminho) {
      this.caminho = caminho;
      this.resultado$ = this.api.blob(caminho).pipe(
        map(r => {
          const url = URL.createObjectURL(r.body!);
          this.urls.push(url);
          return url;
        }),
        catchError(() => of(null)),
        shareReplay(1),
      );
    }
    return this.resultado$;
  }

  ngOnDestroy(): void {
    this.urls.forEach(u => URL.revokeObjectURL(u));
  }
}

export function urlImagemApi(api: AgroCertApi, caminho: string): Observable<string | null> {
  return api.blob(caminho).pipe(map(r => URL.createObjectURL(r.body!)), catchError(() => of(null)));
}
