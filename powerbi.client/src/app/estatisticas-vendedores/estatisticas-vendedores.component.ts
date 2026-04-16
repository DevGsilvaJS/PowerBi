import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { ProdutoPorOsItem } from '../relatorios/produto-por-os.model';
import { RelatoriosApiService } from '../relatorios/relatorios-api.service';
import {
  LojaOption,
  combinarLojasCadastroComSavwin,
  lojaIdsParaParametroApi
} from '../shared/lojas-filtro';

export type BarMetric = 'liquido' | 'descontoPct' | 'ticket' | 'qtd';

export interface LinhaRankingVendedor {
  vendedor: string;
  valorLiquido: number;
  qtdVendasDistintas: number;
  descontoMedioPct: number;
  valorTotalDesconto: number;
  /** Líquido / O.S. distintas. */
  ticketMedio: number;
}

export interface TendenciaVendedor {
  vendedor: string;
  /** Último mês com dado × 100 / penúltimo − 100; <code>null</code> se não houver dois meses. */
  varLiquidoPct: number | null;
  /** Diferença em pontos percentuais (último − penúltimo); mais desconto = valor positivo. */
  varDescontoPp: number | null;
  tendenciaLiquido: 'melhora' | 'piora' | 'estavel' | 'n/a';
  /** Menos % desconto costuma ser melhor para a loja. */
  tendenciaDesconto: 'melhora' | 'piora' | 'estavel' | 'n/a';
  osPenultimo: number;
  osUltimo: number;
  ticketPenultimo: number;
  ticketUltimo: number;
  /** <code>(O.S. último − penúltimo) / penúltimo × 100</code>; <code>null</code> se penúltimo = 0. */
  varQtdPct: number | null;
  /** <code>(ticket último − penúltimo) / penúltimo × 100</code>; <code>null</code> se ticket penúltimo = 0. */
  varTicketPct: number | null;
  tendenciaQtd: 'melhora' | 'piora' | 'estavel' | 'n/a';
  tendenciaTicket: 'melhora' | 'piora' | 'estavel' | 'n/a';
  /** Leitura combinada: a queda/alta de líquido veio mais de O.S. ou de ticket. */
  sinteseDriverLiquido: string;
}

interface LiquidoContexto {
  somaPrecoPorVenda: Map<string, number>;
  liquidoPorVenda: Map<string, number>;
  faturamentoUsaLiquidoPorVenda: boolean;
}

interface AggVendedorRank {
  valorLiquido: number;
  valorTotalDesconto: number;
  vendas: Set<string>;
  pctPorChave: Map<string, number>;
  pesoLiqDescontoPorChave: Map<string, number>;
}

interface AggMesVend {
  liquido: number;
  valorDesconto: number;
  pctPorChave: Map<string, number>;
  pesoLiqPorChave: Map<string, number>;
  vendas: Set<string>;
}

/** Mesma chave de venda que o painel Faturamento (loja + O.S.). */
const SEP_VENDA = '\u001f';

const MESES_CURTOS = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez'];

function pickRaw(p: ProdutoPorOsItem, camel: string, pascal: string): unknown {
  const o = p as Record<string, unknown>;
  return o[camel] ?? o[pascal];
}

function parseDecimalBr(raw: unknown): number {
  if (raw == null || raw === '') {
    return 0;
  }
  if (typeof raw === 'number') {
    return Number.isFinite(raw) ? raw : 0;
  }
  const s = String(raw).trim();
  if (!s) {
    return 0;
  }
  const norm = s.replace(/\./g, '').replace(',', '.');
  const n = Number(norm);
  return Number.isFinite(n) ? n : 0;
}

function chaveAgrupamentoVenda(p: ProdutoPorOsItem): string {
  const loja = (p.lojaNome ?? '').trim() || '—';
  const cv = (p.codigoDaVenda ?? '').trim();
  return cv.length === 0 ? `${loja}${SEP_VENDA}__sem_os__` : `${loja}${SEP_VENDA}${cv}`;
}

function valorLinhaSavWin(p: ProdutoPorOsItem): number {
  const pt = parseDecimalBr(pickRaw(p, 'precoTotalProduto', 'PrecoTotalProduto'));
  if (pt > 0) {
    return pt;
  }
  const vb = parseDecimalBr(pickRaw(p, 'valorBruto', 'ValorBruto'));
  return vb > 0 ? vb : 0;
}

function valorLiquidoLinhaAlocado(
  p: ProdutoPorOsItem,
  ctx: LiquidoContexto
): number {
  const precoLinha = valorLinhaSavWin(p);
  const kAgg = chaveAgrupamentoVenda(p);
  const sumV = ctx.somaPrecoPorVenda.get(kAgg) ?? 0;
  const liqV = ctx.liquidoPorVenda.get(kAgg) ?? 0;
  if (liqV > 0 && sumV > 0) {
    return (precoLinha / sumV) * liqV;
  }
  return ctx.faturamentoUsaLiquidoPorVenda ? 0 : precoLinha;
}

function nomeVendedorLinha(p: ProdutoPorOsItem): string {
  const a = p.vendedor?.trim();
  if (a) {
    return a;
  }
  const b = p.vendedor2?.trim();
  if (b) {
    return b;
  }
  const c = p.vendedor3?.trim();
  if (c) {
    return c;
  }
  return '— Sem vendedor —';
}

function mediaDescontoPonderadoLiq(
  pctPorChave: Map<string, number>,
  pesoLiqPorChave: Map<string, number>
): number {
  let sumPctPeso = 0;
  let sumPeso = 0;
  for (const [chave, peso] of pesoLiqPorChave) {
    if (peso <= 0) {
      continue;
    }
    const pct = pctPorChave.get(chave) ?? 0;
    sumPctPeso += pct * peso;
    sumPeso += peso;
  }
  if (sumPeso > 0) {
    const media = sumPctPeso / sumPeso;
    return Math.min(100, Math.max(0, media));
  }
  const vals = [...pctPorChave.values()];
  if (vals.length === 0) {
    return 0;
  }
  const mediaSimples = vals.reduce((a, b) => a + b, 0) / vals.length;
  return Math.min(100, Math.max(0, mediaSimples));
}

function buildLiquidoContexto(itens: ProdutoPorOsItem[]): LiquidoContexto {
  const somaPrecoPorVenda = new Map<string, number>();
  const liquidoPorVenda = new Map<string, number>();

  for (const p of itens) {
    const k = chaveAgrupamentoVenda(p);
    const pl = valorLinhaSavWin(p);
    somaPrecoPorVenda.set(k, (somaPrecoPorVenda.get(k) ?? 0) + pl);
    const liq = parseDecimalBr(pickRaw(p, 'valorLiquidoTotalVenda', 'ValorLiquidoTotalVenda'));
    if (liq > 0) {
      liquidoPorVenda.set(k, liq);
    }
  }

  let sumLiquidoCabecalho = 0;
  liquidoPorVenda.forEach((v) => {
    sumLiquidoCabecalho += v;
  });
  const faturamentoUsaLiquidoPorVenda = sumLiquidoCabecalho > 0;

  return { somaPrecoPorVenda, liquidoPorVenda, faturamentoUsaLiquidoPorVenda };
}

/** Retorna <code>YYYY-MM</code> ou <code>null</code>. */
function parseDataVendaMesKey(p: ProdutoPorOsItem): string | null {
  const raw = pickRaw(p, 'dataVenda', 'DataVenda');
  if (raw == null || raw === '') {
    return null;
  }
  const s = String(raw).trim();
  const iso = s.match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (iso) {
    return `${iso[1]}-${iso[2]}`;
  }
  const br = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})/);
  if (br) {
    const m = parseInt(br[2], 10);
    const y = parseInt(br[3], 10);
    if (m >= 1 && m <= 12) {
      return `${y}-${String(m).padStart(2, '0')}`;
    }
  }
  const t = Date.parse(s);
  if (!Number.isNaN(t)) {
    const d = new Date(t);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
  }
  return null;
}

function mesLegenda(ym: string): string {
  const [y, m] = ym.split('-');
  const mi = parseInt(m, 10) - 1;
  if (mi < 0 || mi > 11) {
    return ym;
  }
  return `${MESES_CURTOS[mi]}/${y}`;
}

function novoAggMes(): AggMesVend {
  return {
    liquido: 0,
    valorDesconto: 0,
    pctPorChave: new Map(),
    pesoLiqPorChave: new Map(),
    vendas: new Set()
  };
}

function tendenciaDeVarPct(
  varPct: number | null,
  thr: number
): 'melhora' | 'piora' | 'estavel' | 'n/a' {
  if (varPct === null) {
    return 'n/a';
  }
  if (varPct > thr) {
    return 'melhora';
  }
  if (varPct < -thr) {
    return 'piora';
  }
  return 'estavel';
}

/** Explica se a mudança de líquido veio mais de volume (O.S.) ou de ticket médio. */
function sinteseDriverLiquidoMensal(
  varLiquidoPct: number | null,
  varQtdPct: number | null,
  varTicketPct: number | null,
  os0: number,
  os1: number
): string {
  const thrL = 0.5;
  const thrC = 0.5;

  if (varLiquidoPct === null) {
    return 'Sem líquido no penúltimo mês para calcular variação.';
  }
  if (Math.abs(varLiquidoPct) <= thrL) {
    return 'Líquido estável entre os dois meses.';
  }

  if (os0 === 0 && os1 > 0) {
    return 'Não havia O.S. no penúltimo mês; o líquido atual veio só do último mês.';
  }

  const caiu = varLiquidoPct < -thrL;
  const subiu = varLiquidoPct > thrL;

  const qOk = varQtdPct !== null;
  const tOk = varTicketPct !== null;

  if (caiu) {
    if (qOk && tOk) {
      const aq = Math.abs(varQtdPct);
      const at = Math.abs(varTicketPct);
      const qNeg = varQtdPct < -thrC;
      const tNeg = varTicketPct < -thrC;
      if (qNeg && tNeg) {
        if (aq >= at * 1.15) {
          return 'Queda de líquido puxada sobretudo por menos O.S.';
        }
        if (at >= aq * 1.15) {
          return 'Queda de líquido puxada sobretudo por ticket médio menor.';
        }
        return 'Queda por menos O.S. e ticket menor ao mesmo tempo.';
      }
      if (qNeg && !tNeg) {
        return 'Queda de líquido alinhada principalmente a menos O.S.';
      }
      if (!qNeg && tNeg) {
        return 'Queda de líquido alinhada principalmente a ticket médio menor.';
      }
      return 'Queda de líquido com O.S. e ticket pouco alterados (efeitos mistos).';
    }
    if (qOk && varQtdPct < -thrC) {
      return 'Queda de líquido coerente com menos O.S.';
    }
    if (tOk && varTicketPct < -thrC) {
      return 'Queda de líquido coerente com ticket médio menor.';
    }
    return 'Queda de líquido; use as colunas O.S. e ticket para detalhar.';
  }

  if (subiu) {
    if (qOk && tOk) {
      const aq = Math.abs(varQtdPct);
      const at = Math.abs(varTicketPct);
      const qPos = varQtdPct > thrC;
      const tPos = varTicketPct > thrC;
      if (qPos && tPos) {
        if (aq >= at * 1.15) {
          return 'Alta de líquido puxada sobretudo por mais O.S.';
        }
        if (at >= aq * 1.15) {
          return 'Alta de líquido puxada sobretudo por ticket médio maior.';
        }
        return 'Alta com mais O.S. e ticket maior em conjunto.';
      }
      if (qPos && !tPos) {
        return 'Alta de líquido alinhada principalmente a mais O.S.';
      }
      if (!qPos && tPos) {
        return 'Alta de líquido alinhada principalmente a ticket médio maior.';
      }
      return 'Alta de líquido com O.S. e ticket pouco alterados (efeitos mistos).';
    }
    if (qOk && varQtdPct > thrC) {
      return 'Alta de líquido coerente com mais O.S.';
    }
    if (tOk && varTicketPct > thrC) {
      return 'Alta de líquido coerente com ticket médio maior.';
    }
    return 'Alta de líquido; use as colunas O.S. e ticket para detalhar.';
  }

  return '—';
}

@Component({
  selector: 'app-estatisticas-vendedores',
  templateUrl: './estatisticas-vendedores.component.html',
  styleUrl: './estatisticas-vendedores.component.css'
})
export class EstatisticasVendedoresComponent implements OnInit, OnDestroy {
  /** Painel com definições das métricas do relatório. */
  modalMetricasAberto = false;

  dataInicial = '';
  dataFinal = '';
  lojas: LojaOption[] = [];
  lojaIdsSelecionadas: string[] = [];

  periodoErro = '';
  apiErro = '';
  carregando = false;
  jaPesquisou = false;

  linhasRanking: LinhaRankingVendedor[] = [];

  /** Meses com dados (YYYY-MM), ordenados. */
  mesesOrdenados: string[] = [];
  /** Top vendedores (nome) para linhas da evolução. */
  vendedoresEvolucao: string[] = [];
  tendencias: TendenciaVendedor[] = [];

  private celulaMes = new Map<string, Map<string, AggMesVend>>();
  maxEvolLiquido = 0;
  maxEvolDesconto = 0;

  readonly barCharts: { id: string; title: string; metric: BarMetric }[] = [
    { id: 'liq', title: 'Total líquido por vendedor', metric: 'liquido' },
    { id: 'desc', title: '% desconto médio ponderado por vendedor', metric: 'descontoPct' },
    { id: 'ticket', title: 'Ticket médio por vendedor (líquido / O.S.)', metric: 'ticket' },
    { id: 'qtd', title: 'Quantidade de vendas por vendedor (O.S. distintas)', metric: 'qtd' }
  ];

  /** Cards no topo: mesmas métricas dos gráficos, valor do vendedor em rotação. */
  readonly metricCards: { id: string; label: string; metric: BarMetric }[] = [
    { id: 'liq', label: 'Total líquido', metric: 'liquido' },
    { id: 'desc', label: 'Desconto médio ponderado', metric: 'descontoPct' },
    { id: 'ticket', label: 'Ticket médio', metric: 'ticket' },
    { id: 'qtd', label: 'Quantidade de vendas', metric: 'qtd' }
  ];

  /** Índice em <code>linhasRanking</code> do vendedor exibido nos cards. */
  indiceCardRotativo = 0;
  /** Intervalo da rotação automática (ms). */
  readonly intervaloRotacaoCardsMs = 5000;

  private rotacaoCardsTimer: ReturnType<typeof setInterval> | null = null;

  /** Valores exibidos nos cards (animados entre vendedores). */
  kpiExLiquido = 0;
  kpiExDescontoPct = 0;
  kpiExTicket = 0;
  kpiExQtd = 0;

  private animKpiToken = 0;
  private static readonly ANIM_KPI_MS = 1200;

  readonly chartW = 840;
  readonly chartPadT = 8;
  readonly chartPadB = 26;
  readonly chartLabelW = 168;
  readonly chartPadR = 8;
  readonly chartValorW = 120;
  readonly chartRowH = 30;
  readonly chartRowGap = 3;
  readonly tickFracs = [0, 0.25, 0.5, 0.75, 1] as const;

  /** Mesma largura lógica dos gráficos de barras; altura maior para equilibrar com o bloco acima. */
  readonly evoChartW = 840;
  readonly evoChartH = 300;
  readonly evoPadL = 52;
  readonly evoPadR = 20;
  readonly evoPadT = 16;
  readonly evoPadB = 42;

  readonly coresLinha = ['#6366f1', '#22d3ee', '#a78bfa', '#34d399', '#fb923c', '#f472b6'];

  private static readonly EVOLUCAO_TOP = 6;

  constructor(
    private readonly auth: AuthService,
    private readonly relatorios: RelatoriosApiService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const { inicio, fim } = this.primeiroUltimoDiaMesCorrente();
    this.dataInicial = this.toInputDate(inicio);
    this.dataFinal = this.toInputDate(fim);
    this.relatorios.getLojasSavwin().subscribe((items) => {
      this.lojas = combinarLojasCadastroComSavwin(this.auth.getLojasCadastro(), items);
      this.lojaIdsSelecionadas = [];
      this.pesquisar();
    });
  }

  ngOnDestroy(): void {
    this.pararRotacaoCards();
    this.cancelarAnimacaoKpi();
  }

  /** Vendedor atualmente destacado nos quatro cards (mesmos dados dos gráficos). */
  linhaRotativaAtual(): LinhaRankingVendedor | null {
    const n = this.linhasRanking.length;
    if (n === 0) {
      return null;
    }
    const i = ((this.indiceCardRotativo % n) + n) % n;
    return this.linhasRanking[i] ?? null;
  }

  private pararRotacaoCards(): void {
    if (this.rotacaoCardsTimer != null) {
      clearInterval(this.rotacaoCardsTimer);
      this.rotacaoCardsTimer = null;
    }
  }

  private cancelarAnimacaoKpi(): void {
    this.animKpiToken++;
  }

  private resetKpiExibido(): void {
    this.kpiExLiquido = 0;
    this.kpiExDescontoPct = 0;
    this.kpiExTicket = 0;
    this.kpiExQtd = 0;
  }

  /**
   * Interpola dos valores atuais na tela até o vendedor alvo (ease-out, como em Faturamento).
   * Se o alvo for menor que o exibido, a contagem desce; se for maior, sobe.
   */
  private animarKpisParaAlvo(alvo: LinhaRankingVendedor): void {
    const token = ++this.animKpiToken;
    const de = {
      liquido: this.kpiExLiquido,
      descontoPct: this.kpiExDescontoPct,
      ticket: this.kpiExTicket,
      qtd: this.kpiExQtd
    };
    const para = {
      liquido: alvo.valorLiquido,
      descontoPct: alvo.descontoMedioPct,
      ticket: alvo.ticketMedio,
      qtd: alvo.qtdVendasDistintas
    };

    const duracaoMs = EstatisticasVendedoresComponent.ANIM_KPI_MS;
    const t0 = performance.now();
    const easeOutCubic = (t: number) => 1 - (1 - t) ** 3;

    const tick = (now: number) => {
      if (token !== this.animKpiToken) {
        return;
      }
      const u = Math.min(1, (now - t0) / duracaoMs);
      const e = easeOutCubic(u);
      const mix = (a: number, b: number) => a + (b - a) * e;

      if (u >= 1) {
        this.kpiExLiquido = para.liquido;
        this.kpiExTicket = para.ticket;
        this.kpiExDescontoPct = para.descontoPct;
        this.kpiExQtd = para.qtd;
      } else {
        const liq = mix(de.liquido, para.liquido);
        const tk = mix(de.ticket, para.ticket);
        const desc = mix(de.descontoPct, para.descontoPct);
        const qMix = mix(de.qtd, para.qtd);

        const refL = Math.max(Math.abs(de.liquido), Math.abs(para.liquido), 1);
        const refT = Math.max(Math.abs(de.ticket), Math.abs(para.ticket), 1);
        this.kpiExLiquido = arredondarContagemAnimadaBidirecional(liq, refL);
        this.kpiExTicket = arredondarContagemAnimadaBidirecional(tk, refT);
        this.kpiExDescontoPct = Math.max(0, Math.min(100, Math.round(desc * 10) / 10));
        this.kpiExQtd = Math.round(qMix);
      }

      this.cdr.markForCheck();
      if (u < 1) {
        requestAnimationFrame(tick);
      }
    };
    requestAnimationFrame(tick);
  }

  private iniciarRotacaoCards(): void {
    this.pararRotacaoCards();
    const n = this.linhasRanking.length;
    if (n === 0) {
      return;
    }
    this.indiceCardRotativo = Math.min(this.indiceCardRotativo, n - 1);
    if (n <= 1) {
      return;
    }
    this.rotacaoCardsTimer = setInterval(() => {
      const len = this.linhasRanking.length;
      if (len <= 1) {
        return;
      }
      this.indiceCardRotativo = (this.indiceCardRotativo + 1) % len;
      const linha = this.linhasRanking[this.indiceCardRotativo];
      if (linha) {
        this.animarKpisParaAlvo(linha);
      }
    }, this.intervaloRotacaoCardsMs);
  }

  private toInputDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  /** Primeiro e último dia do mês civil atual (hora local). */
  private primeiroUltimoDiaMesCorrente(): { inicio: Date; fim: Date } {
    const agora = new Date();
    const y = agora.getFullYear();
    const mes = agora.getMonth();
    const inicio = new Date(y, mes, 1);
    const fim = new Date(y, mes + 1, 0);
    return { inicio, fim };
  }

  abrirModalMetricas(): void {
    this.modalMetricasAberto = true;
  }

  fecharModalMetricas(): void {
    this.modalMetricasAberto = false;
  }

  chartHBar(): number {
    const n = this.linhasRanking.length;
    if (n === 0) {
      return 120;
    }
    return this.chartPadT + n * (this.chartRowH + this.chartRowGap) - this.chartRowGap + this.chartPadB;
  }

  barPlotW(): number {
    return this.chartW - this.chartLabelW - this.chartValorW - this.chartPadR;
  }

  barValue(r: LinhaRankingVendedor, m: BarMetric): number {
    switch (m) {
      case 'liquido':
        return r.valorLiquido;
      case 'descontoPct':
        return r.descontoMedioPct;
      case 'ticket':
        return r.ticketMedio;
      case 'qtd':
        return r.qtdVendasDistintas;
      default:
        return 0;
    }
  }

  maxBar(m: BarMetric): number {
    if (!this.linhasRanking.length) {
      return 0;
    }
    return Math.max(...this.linhasRanking.map((r) => this.barValue(r, m)), 1e-9);
  }

  barWidth(r: LinhaRankingVendedor, metric: BarMetric): number {
    const max = this.maxBar(metric);
    const v = this.barValue(r, metric);
    if (max <= 0 || v <= 0) {
      return 0;
    }
    return (v / max) * this.barPlotW();
  }

  tickX(frac: number): number {
    return this.chartLabelW + frac * this.barPlotW();
  }

  tickValorBar(frac: number, metric: BarMetric): string {
    const v = this.maxBar(metric) * frac;
    if (metric === 'descontoPct') {
      return this.formatPercentualDesconto(v);
    }
    if (metric === 'qtd') {
      return this.formatInteiro(Math.round(v));
    }
    return this.formatMoedaCurta(v);
  }

  valorLabelX(): number {
    return this.chartW - this.chartPadR;
  }

  formatBarEndLabel(r: LinhaRankingVendedor, metric: BarMetric): string {
    switch (metric) {
      case 'liquido':
        return this.formatMoeda(r.valorLiquido);
      case 'descontoPct':
        return this.formatPercentualDesconto(r.descontoMedioPct);
      case 'ticket':
        return this.formatMoeda(r.ticketMedio);
      case 'qtd':
        return this.formatInteiro(r.qtdVendasDistintas);
      default:
        return '';
    }
  }

  /** Formata os valores animados dos cards rotativos. */
  formatKpiExibido(metric: BarMetric): string {
    switch (metric) {
      case 'liquido':
        return this.formatMoeda(this.kpiExLiquido);
      case 'descontoPct':
        return this.formatPercentualDesconto(this.kpiExDescontoPct);
      case 'ticket':
        return this.formatMoeda(this.kpiExTicket);
      case 'qtd':
        return this.formatInteiro(this.kpiExQtd);
      default:
        return '';
    }
  }

  rowTop(i: number): number {
    return this.chartPadT + i * (this.chartRowH + this.chartRowGap);
  }

  truncarNome(nome: string, max = 30): string {
    const s = nome.trim();
    if (s.length <= max) {
      return s;
    }
    return `${s.slice(0, max - 1)}…`;
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

    const lojaId = lojaIdsParaParametroApi(this.lojas, this.lojaIdsSelecionadas) ?? undefined;
    this.carregando = true;
    this.pararRotacaoCards();
    this.cancelarAnimacaoKpi();
    this.resetKpiExibido();
    this.linhasRanking = [];
    this.indiceCardRotativo = 0;
    this.resetEvolucao();
    this.jaPesquisou = true;

    this.relatorios
      .produtosPorOs({
        dataInicial: this.dataInicial,
        dataFinal: this.dataFinal,
        lojaId: lojaId ?? undefined
      })
      .subscribe({
        next: (itens) => {
          const list = itens ?? [];
          this.linhasRanking = this.agregarRanking(list);
          this.montarEvolucao(list);
          this.indiceCardRotativo = 0;
          this.resetKpiExibido();
          this.iniciarRotacaoCards();
          const primeiro = this.linhasRanking[0];
          if (primeiro) {
            this.animarKpisParaAlvo(primeiro);
          }
          this.carregando = false;
        },
        error: (err) => {
          this.carregando = false;
          this.apiErro =
            err?.error?.message ?? err?.message ?? 'Não foi possível carregar os dados. Tente novamente.';
        }
      });
  }

  private resetEvolucao(): void {
    this.mesesOrdenados = [];
    this.vendedoresEvolucao = [];
    this.tendencias = [];
    this.celulaMes.clear();
    this.maxEvolLiquido = 0;
    this.maxEvolDesconto = 0;
  }

  private agregarRanking(itens: ProdutoPorOsItem[]): LinhaRankingVendedor[] {
    const ctx = buildLiquidoContexto(itens);

    const porVendedor = new Map<string, AggVendedorRank>();

    for (const p of itens) {
      const v = nomeVendedorLinha(p);
      const liquidoLinha = valorLiquidoLinhaAlocado(p, ctx);
      const loja = (p.lojaNome ?? '').trim();
      const os = (p.codigoDaVenda ?? '').trim();
      const chaveVenda = `${loja}\u0000${os}`;
      let agg = porVendedor.get(v);
      if (!agg) {
        agg = {
          valorLiquido: 0,
          valorTotalDesconto: 0,
          vendas: new Set<string>(),
          pctPorChave: new Map<string, number>(),
          pesoLiqDescontoPorChave: new Map<string, number>()
        };
        porVendedor.set(v, agg);
      }
      agg.valorLiquido += liquidoLinha;
      agg.valorTotalDesconto += parseDecimalBr(
        pickRaw(p, 'descontoValorProduto', 'DescontoValorProduto')
      );
      if (os.length > 0) {
        agg.vendas.add(chaveVenda);
      }

      const chaveAgg = chaveAgrupamentoVenda(p);
      const pct = parseDecimalBr(
        pickRaw(p, 'descontoPercentualDaVenda', 'DescontoPercentualDaVenda')
      );
      agg.pctPorChave.set(chaveAgg, pct);
      agg.pesoLiqDescontoPorChave.set(
        chaveAgg,
        (agg.pesoLiqDescontoPorChave.get(chaveAgg) ?? 0) + liquidoLinha
      );
    }

    const linhas: LinhaRankingVendedor[] = [...porVendedor.entries()].map(([vendedor, a]) => {
      const qtd = a.vendas.size;
      const ticketMedio = qtd > 0 ? a.valorLiquido / qtd : 0;
      return {
        vendedor,
        valorLiquido: a.valorLiquido,
        qtdVendasDistintas: qtd,
        descontoMedioPct: mediaDescontoPonderadoLiq(a.pctPorChave, a.pesoLiqDescontoPorChave),
        valorTotalDesconto: a.valorTotalDesconto,
        ticketMedio
      };
    });

    linhas.sort((x, y) => {
      const d = y.valorLiquido - x.valorLiquido;
      if (d !== 0) {
        return d;
      }
      return x.vendedor.localeCompare(y.vendedor, 'pt-BR');
    });
    return linhas;
  }

  private montarEvolucao(itens: ProdutoPorOsItem[]): void {
    this.resetEvolucao();
    const ctx = buildLiquidoContexto(itens);
    const porMes = new Map<string, Map<string, AggMesVend>>();

    for (const p of itens) {
      const mes = parseDataVendaMesKey(p);
      if (!mes) {
        continue;
      }
      const v = nomeVendedorLinha(p);
      const liquidoLinha = valorLiquidoLinhaAlocado(p, ctx);
      const loja = (p.lojaNome ?? '').trim();
      const os = (p.codigoDaVenda ?? '').trim();
      const chaveVenda = `${loja}\u0000${os}`;

      let mapV = porMes.get(mes);
      if (!mapV) {
        mapV = new Map();
        porMes.set(mes, mapV);
      }
      let cell = mapV.get(v);
      if (!cell) {
        cell = novoAggMes();
        mapV.set(v, cell);
      }
      cell.liquido += liquidoLinha;
      cell.valorDesconto += parseDecimalBr(
        pickRaw(p, 'descontoValorProduto', 'DescontoValorProduto')
      );
      if (os.length > 0) {
        cell.vendas.add(chaveVenda);
      }
      const chaveAgg = chaveAgrupamentoVenda(p);
      const pct = parseDecimalBr(
        pickRaw(p, 'descontoPercentualDaVenda', 'DescontoPercentualDaVenda')
      );
      cell.pctPorChave.set(chaveAgg, pct);
      cell.pesoLiqPorChave.set(
        chaveAgg,
        (cell.pesoLiqPorChave.get(chaveAgg) ?? 0) + liquidoLinha
      );
    }

    this.mesesOrdenados = [...porMes.keys()].sort();
    this.celulaMes = porMes;

    const top = this.linhasRanking
      .slice(0, EstatisticasVendedoresComponent.EVOLUCAO_TOP)
      .map((r) => r.vendedor);
    this.vendedoresEvolucao = top;

    for (const mes of this.mesesOrdenados) {
      const m = porMes.get(mes);
      if (!m) {
        continue;
      }
      for (const cell of m.values()) {
        const d = mediaDescontoPonderadoLiq(cell.pctPorChave, cell.pesoLiqPorChave);
        this.maxEvolDesconto = Math.max(this.maxEvolDesconto, d);
        this.maxEvolLiquido = Math.max(this.maxEvolLiquido, cell.liquido);
      }
    }

    this.montarTendencias();
  }

  private getLiquidoMesVendedor(mes: string, vendedor: string): number {
    return this.celulaMes.get(mes)?.get(vendedor)?.liquido ?? 0;
  }

  private getQtdOsMes(mes: string, vendedor: string): number {
    return this.celulaMes.get(mes)?.get(vendedor)?.vendas.size ?? 0;
  }

  private getTicketMesVendedor(mes: string, vendedor: string): number {
    const q = this.getQtdOsMes(mes, vendedor);
    const l = this.getLiquidoMesVendedor(mes, vendedor);
    return q > 0 ? l / q : 0;
  }

  private getDescontoMesVendedor(mes: string, vendedor: string): number {
    const cell = this.celulaMes.get(mes)?.get(vendedor);
    if (!cell) {
      return 0;
    }
    return mediaDescontoPonderadoLiq(cell.pctPorChave, cell.pesoLiqPorChave);
  }

  private montarTendencias(): void {
    this.tendencias = [];
    if (this.mesesOrdenados.length < 2) {
      for (const v of this.vendedoresEvolucao) {
        this.tendencias.push({
          vendedor: v,
          varLiquidoPct: null,
          varDescontoPp: null,
          tendenciaLiquido: 'n/a',
          tendenciaDesconto: 'n/a',
          osPenultimo: 0,
          osUltimo: 0,
          ticketPenultimo: 0,
          ticketUltimo: 0,
          varQtdPct: null,
          varTicketPct: null,
          tendenciaQtd: 'n/a',
          tendenciaTicket: 'n/a',
          sinteseDriverLiquido: '—'
        });
      }
      return;
    }

    const m0 = this.mesesOrdenados[this.mesesOrdenados.length - 2]!;
    const m1 = this.mesesOrdenados[this.mesesOrdenados.length - 1]!;

    const thrL = 0.5;
    const thrD = 0.2;
    const thrQT = 0.5;

    for (const v of this.vendedoresEvolucao) {
      const l0 = this.getLiquidoMesVendedor(m0, v);
      const l1 = this.getLiquidoMesVendedor(m1, v);
      const d0 = this.getDescontoMesVendedor(m0, v);
      const d1 = this.getDescontoMesVendedor(m1, v);
      const q0 = this.getQtdOsMes(m0, v);
      const q1 = this.getQtdOsMes(m1, v);
      const t0 = this.getTicketMesVendedor(m0, v);
      const t1 = this.getTicketMesVendedor(m1, v);

      let varLiquidoPct: number | null = null;
      if (l0 > 0) {
        varLiquidoPct = ((l1 - l0) / l0) * 100;
      } else if (l1 > 0) {
        varLiquidoPct = 100;
      }

      const varDescontoPp = d1 - d0;

      let varQtdPct: number | null = null;
      if (q0 > 0) {
        varQtdPct = ((q1 - q0) / q0) * 100;
      }

      let varTicketPct: number | null = null;
      if (t0 > 0) {
        varTicketPct = ((t1 - t0) / t0) * 100;
      }

      let tendenciaLiquido: TendenciaVendedor['tendenciaLiquido'] = 'estavel';
      if (varLiquidoPct === null) {
        tendenciaLiquido = 'n/a';
      } else if (varLiquidoPct > thrL) {
        tendenciaLiquido = 'melhora';
      } else if (varLiquidoPct < -thrL) {
        tendenciaLiquido = 'piora';
      }

      let tendenciaDesconto: TendenciaVendedor['tendenciaDesconto'] = 'estavel';
      if (varDescontoPp < -thrD) {
        tendenciaDesconto = 'melhora';
      } else if (varDescontoPp > thrD) {
        tendenciaDesconto = 'piora';
      }

      const tendenciaQtd = tendenciaDeVarPct(varQtdPct, thrQT);
      const tendenciaTicket = tendenciaDeVarPct(varTicketPct, thrQT);

      const sinteseDriverLiquido = sinteseDriverLiquidoMensal(
        varLiquidoPct,
        varQtdPct,
        varTicketPct,
        q0,
        q1
      );

      this.tendencias.push({
        vendedor: v,
        varLiquidoPct,
        varDescontoPp,
        tendenciaLiquido,
        tendenciaDesconto,
        osPenultimo: q0,
        osUltimo: q1,
        ticketPenultimo: t0,
        ticketUltimo: t1,
        varQtdPct,
        varTicketPct,
        tendenciaQtd,
        tendenciaTicket,
        sinteseDriverLiquido
      });
    }
  }

  mesLegendaPub(ym: string): string {
    return mesLegenda(ym);
  }

  evoInnerW(): number {
    return this.evoChartW - this.evoPadL - this.evoPadR;
  }

  evoInnerH(): number {
    return this.evoChartH - this.evoPadT - this.evoPadB;
  }

  evoX(i: number): number {
    const n = this.mesesOrdenados.length;
    if (n <= 0) {
      return this.evoPadL;
    }
    if (n === 1) {
      return this.evoPadL + this.evoInnerW() / 2;
    }
    return this.evoPadL + (i / (n - 1)) * this.evoInnerW();
  }

  evoYLiquido(valor: number): number {
    const h = this.evoInnerH();
    const max = this.maxEvolLiquido > 0 ? this.maxEvolLiquido : 1;
    return this.evoPadT + h - (valor / max) * h;
  }

  evoYDesconto(pct: number): number {
    const h = this.evoInnerH();
    const max = this.maxEvolDesconto > 0 ? this.maxEvolDesconto : 100;
    return this.evoPadT + h - (pct / max) * h;
  }

  polylineLiquido(vendedor: string): string {
    if (this.mesesOrdenados.length === 0) {
      return '';
    }
    return this.mesesOrdenados
      .map((mes, i) => {
        const x = this.evoX(i);
        const y = this.evoYLiquido(this.getLiquidoMesVendedor(mes, vendedor));
        return `${x},${y}`;
      })
      .join(' ');
  }

  polylineDesconto(vendedor: string): string {
    if (this.mesesOrdenados.length === 0) {
      return '';
    }
    return this.mesesOrdenados
      .map((mes, i) => {
        const x = this.evoX(i);
        const y = this.evoYDesconto(this.getDescontoMesVendedor(mes, vendedor));
        return `${x},${y}`;
      })
      .join(' ');
  }

  corLinhaVi(i: number): string {
    return this.coresLinha[i % this.coresLinha.length]!;
  }

  textoTendenciaLiquido(t: TendenciaVendedor): string {
    if (t.varLiquidoPct === null) {
      return '—';
    }
    const s = `${t.varLiquidoPct >= 0 ? '+' : ''}${t.varLiquidoPct.toFixed(1)}%`;
    if (t.tendenciaLiquido === 'melhora') {
      return `${s} (melhora)`;
    }
    if (t.tendenciaLiquido === 'piora') {
      return `${s} (piora)`;
    }
    return `${s} (estável)`;
  }

  textoTendenciaDesconto(t: TendenciaVendedor): string {
    if (this.mesesOrdenados.length < 2) {
      return '—';
    }
    const s = `${t.varDescontoPp !== null && t.varDescontoPp >= 0 ? '+' : ''}${t.varDescontoPp?.toFixed(1) ?? '0'} p.p.`;
    if (t.tendenciaDesconto === 'melhora') {
      return `${s} (menos desconto — melhora)`;
    }
    if (t.tendenciaDesconto === 'piora') {
      return `${s} (mais desconto — piora)`;
    }
    return `${s} (estável)`;
  }

  textoTendenciaQtdOs(t: TendenciaVendedor): string {
    if (this.mesesOrdenados.length < 2) {
      return '—';
    }
    if (t.osPenultimo === 0 && t.osUltimo > 0) {
      return `${this.formatInteiro(t.osUltimo)} O.S. (sem base mês anterior)`;
    }
    if (t.varQtdPct === null) {
      return `${this.formatInteiro(t.osPenultimo)} → ${this.formatInteiro(t.osUltimo)} O.S.`;
    }
    const s = `${t.varQtdPct >= 0 ? '+' : ''}${t.varQtdPct.toFixed(1)}%`;
    if (t.tendenciaQtd === 'melhora') {
      return `${s} (mais O.S.)`;
    }
    if (t.tendenciaQtd === 'piora') {
      return `${s} (menos O.S.)`;
    }
    return `${s} (estável)`;
  }

  textoTendenciaTicketMes(t: TendenciaVendedor): string {
    if (this.mesesOrdenados.length < 2) {
      return '—';
    }
    if (t.ticketPenultimo <= 0 && t.ticketUltimo > 0) {
      return `${this.formatMoeda(t.ticketUltimo)} (sem ticket mês anterior)`;
    }
    if (t.varTicketPct === null) {
      return `${this.formatMoeda(t.ticketPenultimo)} → ${this.formatMoeda(t.ticketUltimo)}`;
    }
    const s = `${t.varTicketPct >= 0 ? '+' : ''}${t.varTicketPct.toFixed(1)}%`;
    if (t.tendenciaTicket === 'melhora') {
      return `${s} (ticket maior)`;
    }
    if (t.tendenciaTicket === 'piora') {
      return `${s} (ticket menor)`;
    }
    return `${s} (estável)`;
  }

  formatMoeda(n: number): string {
    return n.toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
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

  formatInteiro(n: number): string {
    return n.toLocaleString('pt-BR', { maximumFractionDigits: 0 });
  }

  formatPercentualDesconto(p: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'percent',
      minimumFractionDigits: 1,
      maximumFractionDigits: 1
    }).format(p / 100);
  }
}

/**
 * Passos visíveis ao interpolar valores monetários (sobe ou desce).
 * <code>refMag</code> = maior magnitude entre origem e destino para escolher centavos / inteiros / degraus.
 */
function arredondarContagemAnimadaBidirecional(interpolado: number, refMag: number): number {
  const r = Math.max(Math.abs(refMag), 1e-9);
  if (r < 100) {
    return Math.round(interpolado * 100) / 100;
  }
  if (r < 10000) {
    return Math.round(interpolado);
  }
  const step = Math.max(1, Math.round(r / 500));
  return Math.round(interpolado / step) * step;
}
