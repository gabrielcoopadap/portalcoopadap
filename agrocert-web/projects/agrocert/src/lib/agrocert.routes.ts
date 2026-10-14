import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell.component';

export const CERTIFICACAO_ROUTES: Routes = [
  {
    path: '',
    component: ShellComponent,
    data: { modulo: 'certificacao' },
    children: [
      { path: '', pathMatch: 'full', title: 'Gestão de Certificações', loadComponent: () => import('./telas/painel/painel.component').then(m => m.PainelComponent) },
      { path: 'gestao', redirectTo: '', pathMatch: 'full' },
      { path: 'certificacoes', title: 'Certificações Ativas', loadComponent: () => import('./telas/certificacoes/certificacoes-lista.component').then(m => m.CertificacoesListaComponent) },
      { path: 'certificacoes/:id', title: 'Certificação', loadComponent: () => import('./telas/certificacoes/certificacao-detalhe.component').then(m => m.CertificacaoDetalheComponent) },
      { path: 'requisitos', title: 'Requisitos', loadComponent: () => import('./telas/requisitos/requisitos.component').then(m => m.RequisitosComponent) },
      { path: 'vistorias', title: 'Vistorias', loadComponent: () => import('./telas/vistorias/vistorias-lista.component').then(m => m.VistoriasListaComponent) },
      { path: 'vistorias/pendencias', title: 'Pendências de vistoria', loadComponent: () => import('./telas/vistorias/pendencias.component').then(m => m.PendenciasComponent) },
      { path: 'vistorias/:id', title: 'Vistoria', loadComponent: () => import('./telas/vistorias/vistoria-detalhe.component').then(m => m.VistoriaDetalheComponent) },
      { path: 'ambiental', title: 'Ambiental', loadComponent: () => import('./telas/ambiental/propriedades-lista.component').then(m => m.PropriedadesListaComponent) },
      { path: 'ambiental/:id', title: 'Propriedade', loadComponent: () => import('./telas/ambiental/propriedade-detalhe.component').then(m => m.PropriedadeDetalheComponent) },
      { path: 'ambiental/:id/conformidade', title: 'Relatório de conformidade', loadComponent: () => import('./telas/ambiental/conformidade.component').then(m => m.ConformidadeComponent) },
      { path: 'corredores', title: 'Corredores Ecológicos', loadComponent: () => import('./telas/corredores/corredores.component').then(m => m.CorredoresComponent) },
      { path: 'corredores/relatorio/:regiao/:fazenda', title: 'Relatório de corredores', loadComponent: () => import('./telas/corredores/relatorio-corredores.component').then(m => m.RelatorioCorredoresComponent) },
      { path: 'fauna-flora', title: 'Fauna e Flora', loadComponent: () => import('./telas/fauna-flora/fauna-flora-painel.component').then(m => m.FaunaFloraPainelComponent) },
      { path: 'fauna-flora/especies', title: 'Espécies', loadComponent: () => import('./telas/fauna-flora/especies.component').then(m => m.EspeciesComponent) },
      { path: 'fauna-flora/registros', title: 'Registros de ocorrência', loadComponent: () => import('./telas/fauna-flora/registros.component').then(m => m.RegistrosComponent) },
    ],
  },
];

export const CLIMA_ROUTES: Routes = [
  {
    path: '',
    component: ShellComponent,
    data: { modulo: 'clima' },
    children: [
      { path: '', pathMatch: 'full', title: 'Clima e El Niño', loadComponent: () => import('./telas/clima/clima-resumo.component').then(m => m.ClimaResumoComponent) },
      { path: 'resumo', redirectTo: '', pathMatch: 'full' },
      { path: 'previsao', title: 'Previsão detalhada', loadComponent: () => import('./telas/clima/clima-previsao.component').then(m => m.ClimaPrevisaoComponent) },
      { path: 'enso', title: 'El Niño / La Niña', loadComponent: () => import('./telas/clima/clima-enso.component').then(m => m.ClimaEnsoComponent) },
      { path: 'validacao', title: 'Validação do modelo', loadComponent: () => import('./telas/clima/clima-validacao.component').then(m => m.ClimaValidacaoComponent) },
      { path: 'dados', title: 'Base de dados', loadComponent: () => import('./telas/clima/clima-dados.component').then(m => m.ClimaDadosComponent) },
    ],
  },
];
