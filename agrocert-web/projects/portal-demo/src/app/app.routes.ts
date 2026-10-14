import { Routes } from '@angular/router';
import { InicioComponent } from './inicio.component';
import { EmBreveComponent } from './em-breve.component';
import { RelatoriosComponent } from './relatorios.component';
import { VisitasListaComponent } from './visitas/visitas-lista.component';
import { VisitaFormComponent } from './visitas/visita-form.component';
import { TODOS_EM_BREVE } from './menu-portal';

export const routes: Routes = [
  { path: '', component: InicioComponent, title: 'Portal Coopadap' },
  { path: 'certificacao', loadChildren: () => import('agrocert').then(m => m.CERTIFICACAO_ROUTES) },
  { path: 'clima', loadChildren: () => import('agrocert').then(m => m.CLIMA_ROUTES) },
  { path: 'portal/visita-tecnica', component: VisitasListaComponent, title: 'Visitas Técnicas' },
  { path: 'portal/visita-tecnica/nova', component: VisitaFormComponent, title: 'Cadastrar visita' },
  { path: 'portal/visita-tecnica/:id', component: VisitaFormComponent, title: 'Visita técnica' },
  { path: 'portal/relatorios', component: RelatoriosComponent, title: 'Relatórios' },
  ...TODOS_EM_BREVE.map(i => ({
    path: i.caminho, component: EmBreveComponent, title: i.texto,
    data: { funcionalidade: i.funcionalidade, descricao: i.descricao },
  })),
  { path: '**', redirectTo: '' },
];
