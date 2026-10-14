import { Component, EventEmitter, Input, Output } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { RouterLink } from '@angular/router';
import { RotaPipe } from '../../util/pipes';

export type AbaClima = 'resumo' | 'previsao' | 'enso' | 'validacao' | 'dados';

@Component({
  selector: 'ac-clima-cabecalho',
  standalone: true,
  imports: [NgFor, NgIf, RouterLink, RotaPipe],
  styles: [':host { display: block; }'],
  template: `
    <div class="cm-topo">
      <div>
        <p class="cm-eyebrow">Clima</p>
        <h1 class="cm-titulo"><span class="material-icons" aria-hidden="true">thermostat</span> Clima e El Niño</h1>
        <p class="cm-subtitulo">Previsão mensal com ciclo climático e o efeito do El Niño / La Niña medido nos dados da estação.</p>
      </div>
    </div>
    <nav class="cm-abas cl-abas" aria-label="Seções do módulo Clima">
      <a *ngFor="let a of abas" class="cm-aba" [class.is-ativa]="a.id === aba" [routerLink]="a.rota | acRota:'clima'"
         [attr.aria-current]="a.id === aba ? 'page' : null"><span class="material-icons" aria-hidden="true">{{ a.icone }}</span> {{ a.texto }}</a>
    </nav>
    <div *ngIf="erro" class="cm-alerta cm-alerta--erro" role="alert"><span class="material-icons" aria-hidden="true">error_outline</span><div>{{ erro }}</div></div>
  `,
})
export class ClimaCabecalhoComponent {
  @Input() aba: AbaClima = 'resumo';
  @Input() erro: string | null = null;

  readonly abas: { id: AbaClima; icone: string; texto: string; rota: string }[] = [
    { id: 'resumo', icone: 'home', texto: 'Resumo', rota: '' },
    { id: 'previsao', icone: 'calendar_month', texto: 'Previsão detalhada', rota: 'previsao' },
    { id: 'enso', icone: 'waves', texto: 'El Niño / La Niña', rota: 'enso' },
    { id: 'validacao', icone: 'rule', texto: 'Validação do modelo', rota: 'validacao' },
    { id: 'dados', icone: 'storage', texto: 'Base de dados', rota: 'dados' },
  ];
}

@Component({
  selector: 'ac-clima-indisponivel',
  standalone: true,
  styles: [':host { display: block; }'],
  template: `
    <section class="cm-card cl-card cl-indisponivel">
      <span class="cl-indisponivel__icone"><span class="material-icons" aria-hidden="true">cloud_off</span></span>
      <div>
        <h2>A api-clima não está respondendo</h2>
        <p>O portal tentou falar com <code>{{ endereco }}</code> e não conseguiu. Para testar no computador:</p>
        <ol>
          <li>Abra um terminal na pasta <code>src\\api-clima</code>.</li>
          <li>Na primeira vez: <code>npm install</code> e <code>npx prisma migrate deploy</code> (o arquivo <code>.env</code> precisa ter o <code>DATABASE_URL</code> do PostgreSQL).</li>
          <li>Suba a API: <code>npm run start:dev</code> e espere aparecer que ela está ouvindo na porta 3030.</li>
          <li>Volte aqui e atualize a página.</li>
        </ol>
        <p class="cm-ajuda">Se a API estiver em outro endereço, ajuste <code>Clima:ApiUrl</code> no <code>appsettings.json</code> do portal.</p>
        <button type="button" class="cm-btn cm-btn--primario" (click)="tentarDeNovo.emit()"><span class="material-icons" aria-hidden="true">refresh</span> Tentar de novo</button>
      </div>
    </section>
  `,
})
export class ClimaIndisponivelComponent {
  @Input() endereco = 'http://localhost:3030';
  @Output() tentarDeNovo = new EventEmitter<void>();
}
