import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AgroCertApi, mensagemDeErro } from '../../api/agrocert-api.service';
import { CategoriaConformidade, Certificacao } from '../../api/modelos';
import { AvisosService } from '../../compartilhado/avisos';
import { PilulaComponent } from '../../compartilhado/pilula.component';
import { EstadoComponent } from '../../compartilhado/estado.component';
import { RotaPipe } from '../../util/pipes';
import * as F from '../../util/formato';
import * as R from '../../util/rotulos';
import { FormCertificacaoComponent } from './form-certificacao.component';

function arredondarPar(v: number): number {
  const piso = Math.floor(v);
  const dif = v - piso;
  if (dif > 0.5) return piso + 1;
  if (dif < 0.5) return piso;
  return piso % 2 === 0 ? piso : piso + 1;
}

@Component({
  selector: 'ac-certificacao-detalhe',
  standalone: true,
  imports: [NgIf, NgFor, RouterLink, PilulaComponent, EstadoComponent, RotaPipe, FormCertificacaoComponent],
  providers: [RotaPipe],
  templateUrl: './certificacao-detalhe.component.html',
})
export class CertificacaoDetalheComponent implements OnInit, OnDestroy {
  private readonly api = inject(AgroCertApi);
  private readonly avisos = inject(AvisosService);
  private readonly rotaAtiva = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly rota = inject(RotaPipe);
  private subParam?: Subscription;
  private subDados?: Subscription;

  readonly F = F;
  readonly R = R;
  id = 0;
  c: Certificacao | null = null;
  erro: string | null = null;
  editando = false;
  excluindo = false;

  ngOnInit(): void {
    this.subParam = this.rotaAtiva.paramMap.subscribe(p => {
      this.id = Number(p.get('id'));
      this.editando = false;
      this.carregar();
    });
  }

  ngOnDestroy(): void {
    this.subParam?.unsubscribe();
    this.subDados?.unsubscribe();
  }

  carregar(): void {
    this.erro = null;
    this.c = null;
    this.subDados?.unsubscribe();
    this.subDados = this.api.certificacao(this.id).subscribe({
      next: c => (this.c = c),
      error: e => (this.erro = mensagemDeErro(e)),
    });
  }

  pct(cat: CategoriaConformidade): number {
    const total = cat.total ?? 0;
    return total === 0 ? 0 : arredondarPar(((cat.conformes ?? 0) * 100) / total);
  }

  min100(v: number): number { return Math.min(100, v); }

  normaDosRequisitos(c: Certificacao): string {
    return c.nome === 'CAFE Practices' ? 'C.A.F.E. Practices' : c.nome;
  }

  salva(): void {
    this.editando = false;
    this.carregar();
  }

  excluir(c: Certificacao): void {
    if (!window.confirm('Excluir esta certificação?')) return;
    this.excluindo = true;
    this.api.excluirCertificacao(c.id).subscribe({
      next: () => {
        this.excluindo = false;
        this.avisos.ok('Certificação excluída.');
        this.router.navigateByUrl(this.rota.transform('certificacoes'));
      },
      error: e => {
        this.excluindo = false;
        this.avisos.erro(e);
      },
    });
  }
}
