import { Component, OnInit } from '@angular/core';
import { forkJoin } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import {
  agregarPagasPorMesDataPagamento,
  agregarRecebidasPorMesDataRecebimento
} from '../financeiro/financeiro-comparativo-anual.util';
import {
  agruparFormasContasPagasBaixadas,
  agruparFormasContasRecebidasBaixadas,
  FormaPagamentoComparativoLinha
} from '../financeiro/financeiro-formas-comparativo.util';
import {
  ComparativoMensalPonto,
  montarSerieComparativoDoisAnos
} from '../relatorios/produto-por-os-vendas-mensal.util';
import { RelatoriosApiService } from '../relatorios/relatorios-api.service';
import { ComparativoFinanceiroCacheDto } from '../financeiro/comparativo-financeiro-cache.model';
import { ContasPagarPagasGridRequest } from '../financeiro/contas-pagar-pagas-grid.model';
import {
  LojaOption,
  lojaIdsParaParametroApi,
  mensagemSelecioneApenasUmaLojaSeTodasMarcadas,
  opcoesLojasDoCadastro
} from '../shared/lojas-filtro';

@Component({
  selector: 'app-comparativo-anual-financeiro',
  templateUrl: './comparativo-anual-financeiro.component.html',
  styleUrls: [
    '../comparativo-anual-vendas/comparativo-anual-vendas.component.css',
    './comparativo-anual-financeiro.component.css'
  ]
})
export class ComparativoAnualFinanceiroComponent implements OnInit {
  ano1: number | null = null;
  ano2: number | null = null;
  lojas: LojaOption[] = [];
  lojaIdsSelecionadas: string[] = [];

  periodoErro = '';
  apiErro = '';

  jaPesquisou = false;
  carregando = false;

  anoRef = 0;
  anoAnterior = 0;

  /** Série em R$ (base para variação % mês a mês). */
  seriePagasValor: ComparativoMensalPonto[] = [];
  serieRecebidasValor: ComparativoMensalPonto[] = [];

  /** Formas de pagamento nas linhas baixadas do mesmo período (emissão dos anos comparados). */
  formasPagasBaixadas: FormaPagamentoComparativoLinha[] = [];
  formasRecebidasBaixadas: FormaPagamentoComparativoLinha[] = [];

  cacheAcaoMensagem = '';

  /**
   * DELETE dos snapshots em curso — evita «Pesquisar» antes do banco apagar
   * (senão o GET ainda encontra linhas e parece que o «limpar» não funcionou).
   */
  apagandoDadosServidor = false;

  /** Após limpar dados no servidor, a próxima pesquisa não consulta snapshot (reforço contra corrida HTTP). */
  private ignorarSnapshotServidorNaProximaPesquisa = false;

  /**
   * Incrementado a cada carga de séries para o *ngFor recriar as barras e repetir a animação.
   */
  animacaoBarrasVersao = 0;

  readonly chartW = 920;
  readonly chartH = 360;
  readonly padL = 52;
  readonly padR = 12;
  readonly padT = 28;
  readonly padB = 50;

  constructor(
    private readonly auth: AuthService,
    private readonly relatorios: RelatoriosApiService
  ) {}

  ngOnInit(): void {
    const hoje = new Date();
    const ano = hoje.getFullYear();
    this.ano1 = ano - 1;
    this.ano2 = ano;
    this.montarLojasDoCadastro();
  }

  montarLojasDoCadastro(): void {
    this.lojas = opcoesLojasDoCadastro(this.auth.getLojasCadastro());
    this.lojaIdsSelecionadas = this.lojas.length > 0 ? [this.lojas[0].id] : [];
  }

  pesquisar(): void {
    this.periodoErro = '';
    this.apiErro = '';
    if (this.ano1 == null || this.ano2 == null) {
      this.periodoErro = 'Informe os dois anos.';
      return;
    }
    const a1 = Number(this.ano1);
    const a2 = Number(this.ano2);
    const minA = 1990;
    const maxA = 2100;

    if (!Number.isFinite(a1) || !Number.isFinite(a2)) {
      this.periodoErro = 'Anos inválidos.';
      return;
    }
    if (!Number.isInteger(a1) || !Number.isInteger(a2)) {
      this.periodoErro = 'Use apenas anos inteiros (ex.: 2024 e 2025).';
      return;
    }
    if (a1 < minA || a1 > maxA || a2 < minA || a2 > maxA) {
      this.periodoErro = `Cada ano deve estar entre ${minA} e ${maxA}.`;
      return;
    }
    if (a1 === a2) {
      this.periodoErro = 'Informe dois anos diferentes para comparar.';
      return;
    }
    if (this.lojas.length > 0 && this.lojaIdsSelecionadas.length === 0) {
      this.periodoErro = 'Selecione ao menos uma loja.';
      return;
    }
    const msgUmaLoja = mensagemSelecioneApenasUmaLojaSeTodasMarcadas(
      this.lojas,
      this.lojaIdsSelecionadas
    );
    if (msgUmaLoja) {
      this.periodoErro = msgUmaLoja;
      return;
    }

    const yMin = Math.min(a1, a2);
    const yMax = Math.max(a1, a2);
    this.anoAnterior = yMin;
    this.anoRef = yMax;

    const d1 = `01/01/${yMin}`;
    const d2 = `31/12/${yMax}`;

    const lojaParam = lojaIdsParaParametroApi(this.lojas, this.lojaIdsSelecionadas);
    const base: Omit<ContasPagarPagasGridRequest, 'statusRecebido'> = {
      lojaId: lojaParam,
      duplicataEmissao1: d1,
      duplicataEmissao2: d2,
      parVencimento1: null,
      parVencimento2: null,
      recRecebimento1: d1,
      recRecebimento2: d2,
      pagamentoVenda1: null,
      pagamentoVenda2: null,
      tipoPeriodo: '1'
    };

    this.carregando = true;
    this.apiErro = '';
    this.cacheAcaoMensagem = '';
    this.formasPagasBaixadas = [];
    this.formasRecebidasBaixadas = [];

    const diagId = `cmp-fin-${Date.now()}`;
    const t0 = performance.now();
    console.info(`[ComparativoAnualFinanceiro][${diagId}] Início`, {
      anos: `${yMin}–${yMax}`,
      loja: lojaParam ?? '(todas)'
    });

    const irDiretoSavWin = this.ignorarSnapshotServidorNaProximaPesquisa;
    if (irDiretoSavWin) {
      this.ignorarSnapshotServidorNaProximaPesquisa = false;
      console.info(`[ComparativoAnualFinanceiro][${diagId}] Ignorando snapshot (após limpar dados no servidor)`);
      this.carregarComparativoDaSavWin(diagId, t0, yMin, yMax, base, lojaParam);
      return;
    }

    this.relatorios.getComparativoFinanceiroCache(yMin, yMax, lojaParam ?? undefined).subscribe({
      next: (cached) => {
        if (cached) {
          this.aplicarSnapshotDoServidor(cached, yMin, yMax);
          this.animacaoBarrasVersao++;
          this.jaPesquisou = true;
          this.carregando = false;
          console.info(`[ComparativoAnualFinanceiro][${diagId}] Snapshot no servidor (hit)`);
          return;
        }

        this.carregarComparativoDaSavWin(diagId, t0, yMin, yMax, base, lojaParam);
      },
      error: (err) => {
        this.carregando = false;
        this.jaPesquisou = true;
        this.apiErro = this.mensagemErroHttp(err, 'Não foi possível verificar dados salvos no servidor.');
      }
    });
  }

  private carregarComparativoDaSavWin(
    diagId: string,
    t0: number,
    yMin: number,
    yMax: number,
    base: Omit<ContasPagarPagasGridRequest, 'statusRecebido'>,
    lojaParam: string | null
  ): void {
    forkJoin({
      pagas: this.relatorios.contasPagarPagasGrid({ ...base, statusRecebido: 'BAIXADO' }),
      recebidas: this.relatorios.contasReceberRecebidasGrid({ ...base, statusRecebido: 'BAIXADO' })
    }).subscribe({
      next: ({ pagas, recebidas }) => {
        const tAfterHttp = performance.now();
        const np = (pagas ?? []).length;
        const nr = (recebidas ?? []).length;
        console.info(
          `[ComparativoAnualFinanceiro][${diagId}] Contas BAIXADAS (2× paralelo) até JSON no browser: ${Math.round(tAfterHttp - t0)}ms · linhas pagas=${np}, recebidas=${nr}`
        );

        const tAgg0 = performance.now();
        const porMesPagas = agregarPagasPorMesDataPagamento(pagas ?? [], yMin, yMax);
        const porMesRec = agregarRecebidasPorMesDataRecebimento(recebidas ?? [], yMin, yMax);
        this.seriePagasValor = montarSerieComparativoDoisAnos(porMesPagas, yMin, yMax);
        this.serieRecebidasValor = montarSerieComparativoDoisAnos(porMesRec, yMin, yMax);
        this.formasPagasBaixadas = agruparFormasContasPagasBaixadas(pagas ?? []);
        this.formasRecebidasBaixadas = agruparFormasContasRecebidasBaixadas(recebidas ?? []);
        this.animacaoBarrasVersao++;
        const tAgg1 = performance.now();
        console.info(
          `[ComparativoAnualFinanceiro][${diagId}] Agregação + séries: ${Math.round(tAgg1 - tAgg0)}ms · total desde clique: ${Math.round(tAgg1 - t0)}ms`
        );

        this.relatorios
          .putComparativoFinanceiroCache({
            anoMenor: yMin,
            anoMaior: yMax,
            lojaId: lojaParam,
            seriePagas: this.seriePagasValor,
            serieRecebidas: this.serieRecebidasValor,
            formasPagas: this.formasPagasBaixadas,
            formasRecebidas: this.formasRecebidasBaixadas
          })
          .subscribe({
            next: () => {
              this.jaPesquisou = true;
              this.carregando = false;
            },
            error: (putErr) => {
              console.warn(`[ComparativoAnualFinanceiro][${diagId}] PUT snapshot falhou`, putErr);
              this.jaPesquisou = true;
              this.carregando = false;
              this.apiErro = this.mensagemErroHttp(
                putErr,
                'Os gráficos foram carregados, mas não foi possível gravar no PostgreSQL.'
              );
            }
          });
      },
      error: (err) => {
        console.warn(
          `[ComparativoAnualFinanceiro][${diagId}] Erro após ${Math.round(performance.now() - t0)}ms`,
          err
        );
        this.seriePagasValor = [];
        this.serieRecebidasValor = [];
        this.formasPagasBaixadas = [];
        this.formasRecebidasBaixadas = [];
        this.jaPesquisou = true;
        this.carregando = false;
        this.apiErro = this.mensagemErroHttp(err, 'Não foi possível carregar os dados.');
      }
    });
  }

  limparDadosSalvosComparativo(): void {
    this.cacheAcaoMensagem = '';
    if (
      !confirm(
        'Apagar no PostgreSQL todos os comparativos financeiros salvos deste usuário? A próxima pesquisa buscará de novo na SavWin e gravará de volta após carregar.'
      )
    ) {
      return;
    }
    this.apagandoDadosServidor = true;
    this.apiErro = '';
    this.relatorios.deleteComparativoFinanceiroCache().subscribe({
      next: () => {
        this.apagandoDadosServidor = false;
        this.ignorarSnapshotServidorNaProximaPesquisa = true;
        this.seriePagasValor = [];
        this.serieRecebidasValor = [];
        this.formasPagasBaixadas = [];
        this.formasRecebidasBaixadas = [];
        this.jaPesquisou = false;
        this.cacheAcaoMensagem =
          'Dados apagados no servidor. Clique em Pesquisar para buscar na SavWin e repovoar o PostgreSQL.';
      },
      error: (err) => {
        this.apagandoDadosServidor = false;
        this.apiErro = this.mensagemErroHttp(err, 'Não foi possível apagar os dados no servidor.');
      }
    });
  }

  private aplicarSnapshotDoServidor(c: ComparativoFinanceiroCacheDto, yMin: number, yMax: number): void {
    if (c.anoMenor !== yMin || c.anoMaior !== yMax) {
      this.seriePagasValor = [];
      this.serieRecebidasValor = [];
      this.formasPagasBaixadas = [];
      this.formasRecebidasBaixadas = [];
      return;
    }
    this.seriePagasValor = Array.isArray(c.seriePagas) ? c.seriePagas : [];
    this.serieRecebidasValor = Array.isArray(c.serieRecebidas) ? c.serieRecebidas : [];
    this.formasPagasBaixadas = Array.isArray(c.formasPagas) ? c.formasPagas : [];
    this.formasRecebidasBaixadas = Array.isArray(c.formasRecebidas) ? c.formasRecebidas : [];
  }

  private mensagemErroHttp(err: unknown, fallback: string): string {
    const e = err as { error?: unknown; message?: string };
    if (typeof e?.error === 'string' && e.error.trim()) {
      return e.error.trim();
    }
    const o = e?.error as { message?: string; title?: string } | undefined;
    if (typeof o?.message === 'string' && o.message.trim()) {
      return o.message.trim();
    }
    if (typeof o?.title === 'string' && o.title.trim()) {
      return o.title.trim();
    }
    if (typeof e?.message === 'string' && e.message.trim()) {
      return e.message.trim();
    }
    return fallback;
  }

  get maxChartPagas(): number {
    return this.maxChart(this.seriePagasValor);
  }

  get maxChartRecebidas(): number {
    return this.maxChart(this.serieRecebidasValor);
  }

  maxChart(serieValor: ComparativoMensalPonto[]): number {
    return Math.max(1, ...serieValor.flatMap((p) => [p.anoAnterior, p.anoAtual]));
  }

  trackByMesPagas(_index: number, p: ComparativoMensalPonto): string {
    return `${this.animacaoBarrasVersao}-p-${p.mes}-${p.label}`;
  }

  trackByMesRecebidas(_index: number, p: ComparativoMensalPonto): string {
    return `${this.animacaoBarrasVersao}-r-${p.mes}-${p.label}`;
  }

  get innerW(): number {
    return this.chartW - this.padL - this.padR;
  }

  get innerH(): number {
    return this.chartH - this.padT - this.padB;
  }

  barHeight(valor: number, max: number): number {
    if (max <= 0) {
      return 0;
    }
    return (valor / max) * this.innerH;
  }

  slotX(i: number): number {
    const slot = this.innerW / 12;
    return this.padL + i * slot;
  }

  barPairOffset(i: number): { a: number; b: number; slot: number; barW: number } {
    const slot = this.innerW / 12;
    const barW = Math.max(10, slot * 0.34);
    const gap = Math.max(2, slot * 0.08);
    const x0 = this.padL + i * slot + (slot - (2 * barW + gap)) / 2;
    return { a: x0, b: x0 + barW + gap, slot, barW };
  }

  barY(valor: number, max: number): number {
    return this.chartH - this.padB - this.barHeight(valor, max);
  }

  tickYs(): number[] {
    const n = 4;
    const ticks: number[] = [];
    for (let i = 0; i <= n; i++) {
      ticks.push(this.padT + (this.innerH * i) / n);
    }
    return ticks;
  }

  tickValor(i: number, max: number): number {
    const n = 4;
    return (max * (n - i)) / n;
  }

  formatMoeda(v: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
      maximumFractionDigits: v >= 100000 ? 0 : 2
    }).format(v);
  }

  formatPercentual(p: number): string {
    return `${p.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
  }

  formatMoedaCurta(v: number): string {
    if (v >= 1_000_000) {
      return `${(v / 1_000_000).toFixed(v >= 10_000_000 ? 0 : 1)} M`;
    }
    if (v >= 1000) {
      return `${(v / 1000).toFixed(v >= 10000 ? 0 : 1)} k`;
    }
    return this.formatMoeda(v);
  }

  textoVariacao(p: ComparativoMensalPonto): string {
    if (p.variacaoPct != null) {
      const sign = p.variacaoPct > 0 ? '+' : '';
      return `${sign}${p.variacaoPct.toFixed(1)}%`;
    }
    if (p.variacaoSentido === 'semBase') {
      return '—';
    }
    return '0%';
  }

  classeVariacaoSvg(p: ComparativoMensalPonto): string {
    switch (p.variacaoSentido) {
      case 'alta':
      case 'semBase':
        return 'chart-variacao chart-variacao--alta';
      case 'queda':
        return 'chart-variacao chart-variacao--queda';
      default:
        return 'chart-variacao chart-variacao--neutra';
    }
  }

  tituloVariacao(p: ComparativoMensalPonto): string {
    if (p.variacaoPct != null) {
      return `Variação ${this.anoRef} vs ${this.anoAnterior}: ${this.textoVariacao(p)}`;
    }
    if (p.variacaoSentido === 'semBase') {
      return `Sem base em ${this.anoAnterior}; ${this.anoRef}: ${this.formatMoeda(p.anoAtual)}`;
    }
    return 'Sem variação (zerado nos dois anos)';
  }

  tituloBarraPagasAnt(p: ComparativoMensalPonto): string {
    return `${p.label} ${this.anoAnterior}: ${this.formatMoeda(this.valorOriginal(p, 'ant', this.seriePagasValor))}`;
  }

  tituloBarraPagasAtu(p: ComparativoMensalPonto): string {
    return `${p.label} ${this.anoRef}: ${this.formatMoeda(this.valorOriginal(p, 'atu', this.seriePagasValor))} · ${this.tituloVariacao(p)}`;
  }

  tituloBarraRecAnt(p: ComparativoMensalPonto): string {
    return `${p.label} ${this.anoAnterior}: ${this.formatMoeda(this.valorOriginal(p, 'ant', this.serieRecebidasValor))}`;
  }

  tituloBarraRecAtu(p: ComparativoMensalPonto): string {
    return `${p.label} ${this.anoRef}: ${this.formatMoeda(this.valorOriginal(p, 'atu', this.serieRecebidasValor))} · ${this.tituloVariacao(p)}`;
  }

  private valorOriginal(
    p: ComparativoMensalPonto,
    qual: 'ant' | 'atu',
    serieFonte: ComparativoMensalPonto[]
  ): number {
    const idx = p.mes - 1;
    const src = serieFonte[idx];
    return qual === 'ant' ? src.anoAnterior : src.anoAtual;
  }
}
