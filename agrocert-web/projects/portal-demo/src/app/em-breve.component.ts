import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'demo-em-breve',
  standalone: true,
  template: `
    <div class="eb-fundo">
      <div class="eb-card">
        <h1>Estamos preparando uma novidade!</h1>
        <p>A funcionalidade "{{ titulo }}" será adicionada em breve ao portal.</p>
        <p>Com ela, você poderá {{ descricao }}</p>
        <small>Agradecemos a sua paciência. Fique de olho para as próximas atualizações!</small>
      </div>
    </div>
  `,
  styles: [`
    .eb-fundo { min-height: calc(100vh - 64px); display: flex; align-items: center; justify-content: center; padding: 24px; }
    .eb-card { max-width: 580px; width: 100%; background: #fff; border-radius: 12px; box-shadow: 0 2px 12px rgba(0, 0, 0, .08);
      padding: 34px 44px 30px; text-align: center; color: #333; }
    h1 { margin: 0 0 14px; color: #1fa04a; font-family: "Redonda Bold", "Nunito", Roboto, sans-serif; font-size: 25px; font-weight: 700; }
    p { margin: 0 0 12px; font-size: 16px; line-height: 1.6; }
    small { display: block; margin-top: 18px; color: #777; font-size: 13px; }
  `],
})
export class EmBreveComponent {
  private readonly dados = inject(ActivatedRoute).snapshot.data;
  readonly titulo: string = this.dados['funcionalidade'] ?? this.dados['title'] ?? '';
  readonly descricao: string = this.dados['descricao'] ?? 'usar novos recursos no portal.';
}
