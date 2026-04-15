import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { forkJoin } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { ContasPagarPagasGridRequest } from '../financeiro/contas-pagar-pagas-grid.model';
import { RelatoriosApiService } from '../relatorios/relatorios-api.service';
import {
  LojaOption,
  combinarLojasCadastroComSavwin,
  lojaIdsParaParametroApi,
  mensagemSelecioneApenasUmaLojaSeTodasMarcadas
} from '../shared/lojas-filtro';
import {
  PontoResumoMensalKpi,
  SecaoNarrativaResumo,
  agregarPagasPorMesDataPagamentoIntervalo,
  agregarReceberPorMesDataEmissaoIntervalo,
  agregarRecebidasPorMesDataRecebimentoIntervalo,
  enumerarMesesEntreIso,
  gerarInsightsResumoKpi,
  gerarNarrativaResumoGrafico,
  montarSerieResumoPorIntervalo,
  somaCampoPontos,
  ymdMinMaxFromIsoRange
} from './resumo-geral-faturamento.util';

@Component({
  selector: 'app-resumo-geral',
  templateUrl: './resumo-geral.component.html',
  styleUrls: [
    './resumo-geral.component.css',
    '../comparativo-anual-vendas/comparativo-anual-vendas.component.css'
  ]
})
export class ResumoGeralComponent implements OnInit, OnDestroy {
  lojas: LojaOption[] = [];
  lojaIdsSelecionadas: string[] = [];

  /** Formato yyyy-MM-dd (inputs type=date), alinhado ao módulo Faturamento. */
  dataInicial = '';
  dataFinal = '';

  periodoErro = '';
  apiErro = '';

  carregandoSerieSavWin = false;
  jaPesquisou = false;

  /** Exibição do período (rótulos dd/mm/aaaa). */
  periodoLabel = '';

  pontos: PontoResumoMensalKpi[] = [];
  insights: { texto: string; tipo: 'ok' | 'alerta' | 'info' }[] = [];
  /** Leitura em texto gerada a partir dos pontos do gráfico. */
  narrativaResumo: SecaoNarrativaResumo[] = [];

  /** Valores animados exibidos nos cards (contagem). */
  displayKpi = { faturado: 0, recebido: 0, pagas: 0, resultado: 0 };
  private alvosKpi = { faturado: 0, recebido: 0, pagas: 0, resultado: 0 };
  private kpiAnimFrame = 0;

  /** Incrementado para re-disparar animações CSS do gráfico. */
  chartAnimTick = 0;

  /** ViewBox amplo para o SVG escalar limpo em telas largas. */
  readonly chartW = 1100;
  readonly chartH = 380;
  readonly padL = 58;
  readonly padR = 24;
  readonly padT = 28;
  readonly padB = 44;

  constructor(
    private readonly relatorios: RelatoriosApiService,
    private readonly auth: AuthService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const hoje = new Date();
    const ano = hoje.getFullYear();
    this.dataInicial = `${ano}-01-01`;
    this.dataFinal = this.toInputDate(hoje);
    this.relatorios.getLojasSavwin().subscribe((items) => {
      this.lojas = combinarLojasCadastroComSavwin(this.auth.getLojasCadastro(), items);
      this.lojaIdsSelecionadas = this.lojas.length > 0 ? [this.lojas[0].id] : [];
      this.pesquisar();
    });
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.kpiAnimFrame);
  }

  private toInputDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  /** Converte yyyy-MM-dd para dd/MM/yyyy (corpo das grids SavWin). */
  private isoParaBr(iso: string): string {
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso.trim());
    if (!m) {
      return '';
    }
    return `${m[3]}/${m[2]}/${m[1]}`;
  }

  get carregando(): boolean {
    return this.carregandoSerieSavWin;
  }

  pesquisar(): void {
    this.periodoErro = '';
    this.apiErro = '';

    if (!this.dataInicial?.trim() || !this.dataFinal?.trim()) {
      this.periodoErro = 'Informe a data inicial e a data final.';
      return;
    }
    const tIni = Date.parse(this.dataInicial + 'T12:00:00');
    const tFim = Date.parse(this.dataFinal + 'T12:00:00');
    if (Number.isNaN(tIni) || Number.isNaN(tFim)) {
      this.periodoErro = 'Datas inválidas.';
      return;
    }
    if (tFim < tIni) {
      this.periodoErro = 'A data final não pode ser anterior à data inicial.';
      return;
    }
    if (this.lojas.length > 0 && this.lojaIdsSelecionadas.length === 0) {
      this.periodoErro = 'Selecione ao menos uma loja.';
      return;
    }
    const msgUmaLoja = mensagemSelecioneApenasUmaLojaSeTodasMarcadas(this.lojas, this.lojaIdsSelecionadas);
    if (msgUmaLoja) {
      this.periodoErro = msgUmaLoja;
      return;
    }

    const lojaParam = lojaIdsParaParametroApi(this.lojas, this.lojaIdsSelecionadas);
    const d1 = this.isoParaBr(this.dataInicial);
    const d2 = this.isoParaBr(this.dataFinal);
    if (!d1 || !d2) {
      this.periodoErro = 'Formato de data inválido.';
      return;
    }

    const { min: ymdMin, max: ymdMax } = ymdMinMaxFromIsoRange(this.dataInicial, this.dataFinal);
    if (!Number.isFinite(ymdMin) || !Number.isFinite(ymdMax)) {
      this.periodoErro = 'Não foi possível interpretar o intervalo de datas.';
      return;
    }

    const mesesMeta = enumerarMesesEntreIso(this.dataInicial, this.dataFinal);
    if (mesesMeta.length === 0) {
      this.periodoErro = 'Intervalo de datas inválido.';
      return;
    }

    this.periodoLabel = `${this.formatarDataExibicao(this.dataInicial)} a ${this.formatarDataExibicao(this.dataFinal)}`;

    this.jaPesquisou = true;
    this.pontos = [];
    this.insights = [];
    this.narrativaResumo = [];
    this.resetDisplayKpi();

    /**
     * Contas a receber: emissão + rec/recebimento no período (comparativo).
     * Contas a pagar (SavWin): DUPEMISSAO null + PAGAMENTOVENDA1/2 = período pesquisado; STATUSRECEBIDO TODOS;
     * Loja(s) no corpo; o servidor resolve via RetornaLista (FILID na SavWin: id interno em pagar, código em receber). Recorte por datas no cliente.
     */
    const baseReceber: Omit<ContasPagarPagasGridRequest, 'statusRecebido'> = {
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
    const basePagar: Omit<ContasPagarPagasGridRequest, 'statusRecebido'> = {
      lojaId: lojaParam,
      duplicataEmissao1: null,
      duplicataEmissao2: null,
      parVencimento1: null,
      parVencimento2: null,
      recRecebimento1: null,
      recRecebimento2: null,
      pagamentoVenda1: d1,
      pagamentoVenda2: d2,
      tipoPeriodo: '1'
    };

    this.carregandoSerieSavWin = true;
    forkJoin({
      pagas: this.relatorios.contasPagarPagasGrid({ ...basePagar, statusRecebido: 'TODOS' }),
      aberto: this.relatorios.contasReceberRecebidasGrid({ ...baseReceber, statusRecebido: 'ABERTO' }),
      baixado: this.relatorios.contasReceberRecebidasGrid({ ...baseReceber, statusRecebido: 'BAIXADO' })
    }).subscribe({
      next: ({ pagas, aberto, baixado }) => {
        const porMesPag = agregarPagasPorMesDataPagamentoIntervalo(pagas ?? [], ymdMin, ymdMax);
        const porMesRec = agregarRecebidasPorMesDataRecebimentoIntervalo(baixado ?? [], ymdMin, ymdMax);
        const linhasFat = [...(aberto ?? []), ...(baixado ?? [])];
        const porMesFat = agregarReceberPorMesDataEmissaoIntervalo(linhasFat, ymdMin, ymdMax);
        this.pontos = montarSerieResumoPorIntervalo(porMesFat, porMesRec, porMesPag, mesesMeta);
        this.insights = gerarInsightsResumoKpi(this.pontos);
        this.narrativaResumo = gerarNarrativaResumoGrafico(this.pontos, (v) => this.formatMoeda(v));
        this.apiErro = '';
        this.carregandoSerieSavWin = false;
        this.chartAnimTick++;
        this.definirAlvosEAnimarKpis();
        this.cdr.markForCheck();
      },
      error: (err) => {
        this.carregandoSerieSavWin = false;
        this.apiErro = this.mensagemErro(err, 'Não foi possível carregar os dados na SavWin.');
        this.cdr.markForCheck();
      }
    });
  }

  private formatarDataExibicao(iso: string): string {
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso.trim());
    if (!m) {
      return iso;
    }
    return `${m[3]}/${m[2]}/${m[1]}`;
  }

  private resetDisplayKpi(): void {
    cancelAnimationFrame(this.kpiAnimFrame);
    this.displayKpi = { faturado: 0, recebido: 0, pagas: 0, resultado: 0 };
  }

  private definirAlvosEAnimarKpis(): void {
    this.alvosKpi = {
      faturado: somaCampoPontos(this.pontos, 'faturado'),
      recebido: somaCampoPontos(this.pontos, 'recebido'),
      pagas: somaCampoPontos(this.pontos, 'pagas'),
      resultado: somaCampoPontos(this.pontos, 'resultado')
    };
    cancelAnimationFrame(this.kpiAnimFrame);
    const t0 = performance.now();
    const dur = 880;
    const ease = (t: number) => 1 - (1 - t) * (1 - t);

    const tick = (now: number) => {
      const u = Math.min(1, (now - t0) / dur);
      const e = ease(u);
      const a = this.alvosKpi;
      this.displayKpi = {
        faturado: a.faturado * e,
        recebido: a.recebido * e,
        pagas: a.pagas * e,
        resultado: a.resultado * e
      };
      this.cdr.markForCheck();
      if (u < 1) {
        this.kpiAnimFrame = requestAnimationFrame(tick);
      } else {
        this.displayKpi = { ...a };
        this.cdr.markForCheck();
      }
    };
    this.kpiAnimFrame = requestAnimationFrame(tick);
  }

  get temDadosGrafico(): boolean {
    return this.pontos.some((p) => p.faturado > 0 || p.recebido > 0 || p.pagas !== 0 || p.resultado !== 0);
  }

  get totalFaturado(): number {
    return somaCampoPontos(this.pontos, 'faturado');
  }

  get totalRecebido(): number {
    return somaCampoPontos(this.pontos, 'recebido');
  }

  get totalPagas(): number {
    return somaCampoPontos(this.pontos, 'pagas');
  }

  get totalResultado(): number {
    return somaCampoPontos(this.pontos, 'resultado');
  }

  get minChartY(): number {
    if (this.pontos.length === 0) {
      return 0;
    }
    const vals = this.pontos.flatMap((p) => [p.faturado, p.recebido, p.pagas, p.resultado]);
    const m = Math.min(...vals);
    return Math.min(0, m);
  }

  get maxChartY(): number {
    if (this.pontos.length === 0) {
      return 1;
    }
    const vals = this.pontos.flatMap((p) => [p.faturado, p.recebido, p.pagas, p.resultado]);
    return Math.max(1, ...vals);
  }

  get innerW(): number {
    return this.chartW - this.padL - this.padR;
  }

  get innerH(): number {
    return this.chartH - this.padT - this.padB;
  }

  get chartRangeY(): number {
    const r = this.maxChartY - this.minChartY;
    return r > 0 ? r : 1;
  }

  xMes(i: number): number {
    const n = Math.max(1, this.pontos.length);
    return this.padL + (i / (n - 1 || 1)) * this.innerW;
  }

  yValor(v: number): number {
    const min = this.minChartY;
    const range = this.chartRangeY;
    return this.padT + this.innerH * (1 - (v - min) / range);
  }

  polylinePontos(campo: 'faturado' | 'recebido' | 'pagas' | 'resultado'): string {
    if (this.pontos.length === 0) {
      return '';
    }
    return this.pontos
      .map((p, i) => `${this.xMes(i).toFixed(2)},${this.yValor(p[campo]).toFixed(2)}`)
      .join(' ');
  }

  tickYs(): number[] {
    const n = 4;
    const ticks: number[] = [];
    for (let i = 0; i <= n; i++) {
      ticks.push(this.padT + (this.innerH * i) / n);
    }
    return ticks;
  }

  tickValor(i: number): number {
    return this.maxChartY - (this.chartRangeY * i) / 4;
  }

  get yLinhaZero(): number | null {
    const min = this.minChartY;
    const max = this.maxChartY;
    if (min >= 0 || max <= 0) {
      return null;
    }
    return this.yValor(0);
  }

  formatMoeda(v: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v);
  }

  formatMoedaCurta(v: number): string {
    const abs = Math.abs(v);
    if (abs >= 1_000_000) {
      return `${(v / 1_000_000).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}M`;
    }
    if (abs >= 1_000) {
      return `${(v / 1_000).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}k`;
    }
    return this.formatMoeda(v);
  }

  private mensagemErro(err: unknown, fallback: string): string {
    const e = err as { error?: unknown; message?: string; status?: number };
    if (typeof e?.error === 'string' && e.error.trim()) {
      return e.error.trim();
    }
    const o = e?.error as { message?: string } | undefined;
    if (typeof o?.message === 'string' && o.message.trim()) {
      return o.message.trim();
    }
    if (typeof e?.message === 'string' && e.message.trim()) {
      return e.message.trim();
    }
    return fallback;
  }
}
