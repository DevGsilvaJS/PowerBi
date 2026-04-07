import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard } from './auth/auth.guard';
import { EstoqueComponent } from './estoque/estoque.component';
import { FaturamentoComponent } from './faturamento/faturamento.component';
import { ComparativoAnualVendasComponent } from './comparativo-anual-vendas/comparativo-anual-vendas.component';
import { ComparativoAnualFinanceiroComponent } from './comparativo-anual-financeiro/comparativo-anual-financeiro.component';
import { EstatisticasVendedoresComponent } from './estatisticas-vendedores/estatisticas-vendedores.component';
import { FinanceiroComponent } from './financeiro/financeiro.component';
import { ResumoGeralComponent } from './resumo-geral/resumo-geral.component';
import { GestaoClientesComponent } from './gestao-clientes/gestao-clientes.component';
import { LoginComponent } from './login/login.component';
import { ShellComponent } from './shell/shell.component';

const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: 'login', component: LoginComponent },
  { path: 'gestaoclientes', component: GestaoClientesComponent },
  {
    path: 'app',
    component: ShellComponent,
    canActivate: [AuthGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'resumo-geral' },
      { path: 'resumo-geral', component: ResumoGeralComponent },
      { path: 'faturamento', component: FaturamentoComponent },
      { path: 'estatisticas-vendedores', component: EstatisticasVendedoresComponent },
      { path: 'comparativo-anual-vendas', component: ComparativoAnualVendasComponent },
      { path: 'comparativo-anual-financeiro', component: ComparativoAnualFinanceiroComponent },
      { path: 'estoque', component: EstoqueComponent },
      { path: 'financeiro', component: FinanceiroComponent },
      { path: 'dashboard', redirectTo: 'resumo-geral', pathMatch: 'full' }
    ]
  },
  { path: '**', redirectTo: 'login' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
