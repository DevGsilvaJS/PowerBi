import { HTTP_INTERCEPTORS, HttpClientModule } from '@angular/common/http';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BrowserModule } from '@angular/platform-browser';

import { AuthInterceptor } from './auth/auth.interceptor';
import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
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
import { LojasMultiDropdownComponent } from './shared/lojas-multi-dropdown/lojas-multi-dropdown.component';

@NgModule({
  declarations: [
    AppComponent,
    LoginComponent,
    ShellComponent,
    ResumoGeralComponent,
    FaturamentoComponent,
    EstatisticasVendedoresComponent,
    EstoqueComponent,
    FinanceiroComponent,
    ComparativoAnualVendasComponent,
    ComparativoAnualFinanceiroComponent,
    GestaoClientesComponent,
    LojasMultiDropdownComponent
  ],
  imports: [
    BrowserModule,
    HttpClientModule,
    FormsModule,
    AppRoutingModule
  ],
  providers: [{ provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true }],
  bootstrap: [AppComponent]
})
export class AppModule { }
