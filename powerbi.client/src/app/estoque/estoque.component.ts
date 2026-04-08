import { Component, OnInit } from '@angular/core';
import { forkJoin } from 'rxjs';
import { map } from 'rxjs/operators';
import { AuthService } from '../auth/auth.service';
import {
  LojaOption,
  combinarLojasCadastroComSavwin,
  lojaIdsParaListaChamadasIndividuais
} from '../shared/lojas-filtro';
import { COLUNAS_ENTRADA_ESTOQUE, EntradasEstoqueGridItem } from './entradas-estoque-grid.model';
import { RelatoriosApiService } from '../relatorios/relatorios-api.service';

@Component({
  selector: 'app-estoque',
  templateUrl: './estoque.component.html',
  styleUrl: './estoque.component.css'
})
export class EstoqueComponent implements OnInit {
  readonly colunasGrid = COLUNAS_ENTRADA_ESTOQUE;

  dataInicial = '';
  dataFinal = '';
  lojas: LojaOption[] = [];
  lojaIdsSelecionadas: string[] = [];

  periodoErro = '';
  apiErro = '';

  carregando = false;
  linhas: EntradasEstoqueGridItem[] = [];

  constructor(
    private readonly auth: AuthService,
    private readonly relatorios: RelatoriosApiService
  ) {}

  ngOnInit(): void {
    const hoje = new Date();
    const dd = String(hoje.getDate()).padStart(2, '0');
    const mm = String(hoje.getMonth() + 1).padStart(2, '0');
    const yyyy = hoje.getFullYear();
    const s = `${dd}/${mm}/${yyyy}`;
    this.dataInicial = s;
    this.dataFinal = s;
    this.relatorios.getLojasSavwin().subscribe((items) => {
      this.lojas = combinarLojasCadastroComSavwin(this.auth.getLojasCadastro(), items);
      this.lojaIdsSelecionadas = this.lojas.map((l) => l.id);
    });
  }

  pesquisar(): void {
    this.periodoErro = '';
    this.apiErro = '';
    /** Datas permanecem na tela para outros blocos; o grid SavWin não as utiliza. */
    const reData = /^\d{2}\/\d{2}\/\d{4}$/;
    const di = this.dataInicial?.trim() ?? '';
    const df = this.dataFinal?.trim() ?? '';
    if ((di && !reData.test(di)) || (df && !reData.test(df))) {
      this.periodoErro = 'Se informar datas, use o formato dd/mm/aaaa.';
      return;
    }
    if (di && df && reData.test(di) && reData.test(df)) {
      const [d1, m1, y1] = di.split('/').map((x) => parseInt(x, 10));
      const [d2, m2, y2] = df.split('/').map((x) => parseInt(x, 10));
      if (new Date(y2, m2 - 1, d2).getTime() < new Date(y1, m1 - 1, d1).getTime()) {
        this.periodoErro = 'A data final não pode ser anterior à data inicial.';
        return;
      }
    }

    if (this.lojas.length > 0 && this.lojaIdsSelecionadas.length === 0) {
      this.periodoErro = 'Selecione ao menos uma loja.';
      return;
    }

    const lista = lojaIdsParaListaChamadasIndividuais(this.lojas, this.lojaIdsSelecionadas);
    const reqs =
      lista === null
        ? [this.relatorios.entradasEstoqueGrid({ lojaId: null })]
        : lista.map((id) => this.relatorios.entradasEstoqueGrid({ lojaId: id }));

    this.carregando = true;
    this.linhas = [];
    forkJoin(reqs)
      .pipe(map((chunks) => chunks.flat()))
      .subscribe({
        next: (rows) => {
          this.linhas = rows ?? [];
          this.carregando = false;
        },
        error: (err) => {
          this.linhas = [];
          this.carregando = false;
          const msg =
            err?.error?.message ??
            (typeof err?.error === 'string' ? err.error : null) ??
            err?.message ??
            'Não foi possível carregar o estoque.';
          this.apiErro = typeof msg === 'string' ? msg : 'Não foi possível carregar o estoque.';
        }
      });
  }

  valorCelula(row: EntradasEstoqueGridItem, key: keyof EntradasEstoqueGridItem): string {
    const v = row[key];
    return v == null || v === '' ? '—' : String(v);
  }
}
