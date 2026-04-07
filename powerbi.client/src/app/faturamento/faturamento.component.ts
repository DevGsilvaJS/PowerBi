import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { FaturamentoPainelResponse } from '../relatorios/faturamento-painel.model';
import { RelatoriosApiService } from '../relatorios/relatorios-api.service';
import { LojaOption, lojaIdsParaParametroApi, opcoesLojasDoCadastro } from '../shared/lojas-filtro';

export type FaturamentoViewMode = 'valor' | 'percentual' | 'grafico';

type KpiToggle = 'none' | 'valorPercentual';

interface KpiCard {
  id: string;
  title: string;
  /** Controles do card: nenhum | valor + percentual */
  toggle: KpiToggle;
}

interface KpiDados {
  valor: number;
  /** 0–100 para exibição em % */
  percentual: number;
  bars: number[];
}

/** Linha do painel “Vendas por Material”. */
export interface VendaMaterialLinhaView {
  material: string;
  /** Soma de valorBruto por linha (fallback: valor de linha SavWin). */
  bruto: number;
  /**
   * Líquido: rateio do valorLiquidoTotalVenda da venda pela participação do preço de linha;
   * se não houver líquido de venda, usa o preço de linha.
   */
  liquido: number;
  /** Soma das quantidades das linhas SavWin desse material. */
  quantidade: number;
  /** Participação no total líquido do card (0–100). */
  percentual: number;
  /** Valores animados (0 → alvo), exibidos na tela. */
  exBruto: number;
  exLiquido: number;
  exQuantidade: number;
  exPercentual: number;
}

/** Linha do painel “Vendas por Grife” (por grife dentro de um subgrupo de produto). */
export interface VendaGrifeLinhaView {
  grife: string;
  bruto: number;
  liquido: number;
  quantidade: number;
  /** Participação no total líquido das 3 categorias — prefixos 01, 02 e 03 (0–100). */
  percentual: number;
  exBruto: number;
  exLiquido: number;
  exQuantidade: number;
  exPercentual: number;
}

/** Um dos 3 cards (01 / 02 / 03) dentro de Vendas por Grife. */
export interface VendaGrifeSubgrupoView {
  titulo: string;
  linhas: VendaGrifeLinhaView[];
}

/** Linha do painel de formas: título = texto exato de <code>MEIO_PAGAMENTO</code> na API. */
export interface FormaPagamentoLinhaView {
  meioPagamento: string;
  valorAlvo: number;
  valorExibido: number;
}

/** Linha do card “Vendas por categoria”: descrição (modalidade), valor líquido e quantidade de vendas (O.S.) distintas. */
export interface VendaProdutoLinhaView {
  label: string;
  valorAlvo: number;
  valorExibido: number;
  qtdVendasAlvo: number;
  qtdVendasExibida: number;
}

/** Card de família de produto (1 solar/receituário, 2 lentes, 3 serviços) na O.S. */
export interface VendaFamiliaProdutoCardView {
  id: string;
  titulo: string;
  valorAlvo: number;
  valorExibido: number;
  qtdAlvo: number;
  qtdExibida: number;
}

const VENDAS_POR_PRODUTO_LABELS: readonly string[] = [
  'Solar',
  'Receituário',
  'Solar + Serviço',
  'Receituário + Serviço',
  'Óculos completo',
  'Lente',
  'Lente e serviço',
  'Serviços'
];

function linhasVendasPorProdutoZeradas(): VendaProdutoLinhaView[] {
  return VENDAS_POR_PRODUTO_LABELS.map((label) => ({
    label,
    valorAlvo: 0,
    valorExibido: 0,
    qtdVendasAlvo: 0,
    qtdVendasExibida: 0
  }));
}

/** Maior valor líquido primeiro; empate por descrição (pt-BR). */
function ordenarVendasPorProdutoPorValorDesc(linhas: VendaProdutoLinhaView[]): VendaProdutoLinhaView[] {
  return [...linhas].sort((a, b) => {
    const d = b.valorAlvo - a.valorAlvo;
    if (d !== 0) {
      return d;
    }
    return a.label.localeCompare(b.label, 'pt-BR');
  });
}

function cardsFamiliaProdutoZerados(): VendaFamiliaProdutoCardView[] {
  return [
    {
      id: 'solares',
      titulo: 'Solares Vendidos',
      valorAlvo: 0,
      valorExibido: 0,
      qtdAlvo: 0,
      qtdExibida: 0
    },
    {
      id: 'receituarios',
      titulo: 'Receituários Vendidos',
      valorAlvo: 0,
      valorExibido: 0,
      qtdAlvo: 0,
      qtdExibida: 0
    },
    {
      id: 'lentes',
      titulo: 'Lentes Vendidas',
      valorAlvo: 0,
      valorExibido: 0,
      qtdAlvo: 0,
      qtdExibida: 0
    },
    {
      id: 'servicos',
      titulo: 'Serviços Vendidos',
      valorAlvo: 0,
      valorExibido: 0,
      qtdAlvo: 0,
      qtdExibida: 0
    }
  ];
}

@Component({
  selector: 'app-faturamento',
  templateUrl: './faturamento.component.html',
  styleUrl: './faturamento.component.css'
})
export class FaturamentoComponent implements OnInit, OnDestroy {
  dataInicial = '';
  dataFinal = '';
  lojas: LojaOption[] = [];
  lojaIdsSelecionadas: string[] = [];

  periodoErro = '';

  /** Inicia em carregamento para o 1.º paint alinhar com KPIs / demais painéis até o painel responder. */
  produtosCarregando = true;

  /** Agregados para os cards (SavWin / Produtos por OS). */
  kpiDados: Record<string, KpiDados> = {};

  /** Total a receber (valor final após animação / fallback produtos). */
  totalPagamentoResumo = 0;

  /** Total exibido durante animação de contagem. */
  totalPagamentoExibido = 0;

  /** Uma linha por <code>MEIO_PAGAMENTO</code> distinto (nome vindo da API). */
  formasPagamentoLinhas: FormaPagamentoLinhaView[] = [];

  /** Card “Vendas por categoria”: uma linha por modalidade (valores da API). */
  vendasPorProdutoLinhas: VendaProdutoLinhaView[] = linhasVendasPorProdutoZeradas();

  /** Card “Vendas por Material”: agregado por <code>linhaDeProduto</code>. */
  vendasPorMaterialLinhas: VendaMaterialLinhaView[] = [];

  /** Card “Vendas por Grife”: por prefixo de código (01/02/03) e grife. */
  vendasPorGrifeSubgrupos: VendaGrifeSubgrupoView[] = [];

  /** Quatro cards: solares / receituários / lentes / serviços (líquido por linha + O.S. com a família). */
  cardsFamiliaProduto: VendaFamiliaProdutoCardView[] = cardsFamiliaProdutoZerados();

  private animFormasToken = 0;

  /** Valores animados dos KPIs (0 → <code>kpiDados</code>), mesmo padrão das formas de pagamento. */
  kpiValorExibido: Record<string, number> = {};
  kpiPercentualExibido: Record<string, number> = {};

  readonly kpiCards: KpiCard[] = [
    { id: 'vendasBruto', title: 'Vendas (Bruto)', toggle: 'none' },
    { id: 'vendas', title: 'Vendas (Líquido)', toggle: 'none' },
    { id: 'cmv', title: 'CMV', toggle: 'none' },
    { id: 'descVendedor', title: 'Desconto', toggle: 'valorPercentual' },
    { id: 'ticketMedio', title: 'Ticket médio', toggle: 'none' },
    { id: 'vendasProdutos', title: 'Quantidade', toggle: 'none' }
  ];

  modes: Record<string, FaturamentoViewMode> = Object.fromEntries(
    this.kpiCards.map((k) => [k.id, 'valor' as FaturamentoViewMode])
  ) as Record<string, FaturamentoViewMode>;

  constructor(
    private readonly auth: AuthService,
    private readonly relatorios: RelatoriosApiService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnDestroy(): void {
    this.animFormasToken++;
  }

  ngOnInit(): void {
    const hoje = new Date();
    this.dataInicial = this.toInputDate(hoje);
    this.dataFinal = this.toInputDate(hoje);
    this.montarLojasDoCadastro();
    this.carregarDadosKpi();
  }

  montarLojasDoCadastro(): void {
    this.lojas = opcoesLojasDoCadastro(this.auth.getLojasCadastro());
    this.lojaIdsSelecionadas = this.lojas.map((l) => l.id);
  }

  pesquisar(): void {
    this.periodoErro = '';
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
    this.carregarDadosKpi();
  }

  private carregarDadosKpi(): void {
    this.produtosCarregando = true;
    this.cdr.markForCheck();
    this.animFormasToken++;
    this.formasPagamentoLinhas = [];
    this.vendasPorProdutoLinhas = linhasVendasPorProdutoZeradas();
    this.vendasPorMaterialLinhas = [];
    this.vendasPorGrifeSubgrupos = [];
    this.cardsFamiliaProduto = cardsFamiliaProdutoZerados();
    this.totalPagamentoExibido = 0;
    const lojaParam = lojaIdsParaParametroApi(this.lojas, this.lojaIdsSelecionadas);
    const req = {
      dataInicial: this.dataInicial,
      dataFinal: this.dataFinal,
      lojaId: lojaParam ?? undefined
    };

    this.relatorios.faturamentoPainel(req).subscribe({
      next: (resp: FaturamentoPainelResponse) => {
        this.aplicarRespostaPainel(resp);
        for (const k of this.kpiCards) {
          this.kpiValorExibido[k.id] = 0;
          this.kpiPercentualExibido[k.id] = 0;
        }
        const linhasAgg = (resp.formasPagamento ?? []).map((f) => ({
          meioPagamento: f.meioPagamento,
          valor: f.valor
        }));
        this.produtosCarregando = false;
        this.iniciarAnimacaoValoresTela(linhasAgg, this.totalPagamentoResumo);
      },
      error: () => {
        this.aplicarRespostaPainel({
          kpiDados: {},
          totalPagamentoResumo: 0,
          formasPagamento: [],
          vendasPorMaterialLinhas: [],
          vendasPorGrifeSubgrupos: [],
          vendasPorTipoProdutoLinhas: [],
          vendasFamiliaProdutoCards: []
        });
        this.kpiValorExibido = {};
        this.kpiPercentualExibido = {};
        this.formasPagamentoLinhas = [];
        this.vendasPorProdutoLinhas = linhasVendasPorProdutoZeradas();
        this.cardsFamiliaProduto = cardsFamiliaProdutoZerados();
        this.totalPagamentoResumo = 0;
        this.totalPagamentoExibido = 0;
        this.produtosCarregando = false;
        this.cdr.markForCheck();
      }
    });
  }

  /** Mapeia DTO do servidor para o estado da tela (inclui campos <code>ex*</code> para animação). */
  private aplicarRespostaPainel(resp: FaturamentoPainelResponse): void {
    const z: KpiDados = { valor: 0, percentual: 0, bars: Array(7).fill(0) };
    this.kpiDados = {};
    for (const k of this.kpiCards) {
      const d = resp.kpiDados?.[k.id];
      this.kpiDados[k.id] = d
        ? { valor: d.valor, percentual: d.percentual, bars: d.bars?.length ? [...d.bars] : [...z.bars] }
        : { ...z, bars: [...z.bars] };
    }
    this.totalPagamentoResumo = resp.totalPagamentoResumo ?? 0;
    this.vendasPorMaterialLinhas = (resp.vendasPorMaterialLinhas ?? []).map((l) => ({
      material: l.material,
      bruto: l.bruto,
      liquido: l.liquido,
      quantidade: l.quantidade,
      percentual: l.percentual,
      exBruto: 0,
      exLiquido: 0,
      exQuantidade: 0,
      exPercentual: 0
    }));
    this.vendasPorGrifeSubgrupos = (resp.vendasPorGrifeSubgrupos ?? []).map((sg) => ({
      titulo: sg.titulo,
      linhas: (sg.linhas ?? []).map((l) => ({
        grife: l.grife,
        bruto: l.bruto,
        liquido: l.liquido,
        quantidade: l.quantidade,
        percentual: l.percentual,
        exBruto: 0,
        exLiquido: 0,
        exQuantidade: 0,
        exPercentual: 0
      }))
    }));
    this.vendasPorProdutoLinhas = this.mapearVendasPorProdutoDoPainel(resp);
    this.cardsFamiliaProduto = this.mapearCardsFamiliaProduto(resp);
  }

  private mapearCardsFamiliaProduto(resp: FaturamentoPainelResponse): VendaFamiliaProdutoCardView[] {
    const base = cardsFamiliaProdutoZerados();
    const anyResp = resp as unknown as Record<string, unknown>;
    const raw = anyResp['vendasFamiliaProdutoCards'] ?? anyResp['VendasFamiliaProdutoCards'];
    if (!Array.isArray(raw) || raw.length === 0) {
      return base;
    }
    const porId = new Map<string, { titulo: string; valor: number; qtd: number }>();
    for (const item of raw) {
      const o = item as Record<string, unknown>;
      const id = String(o['id'] ?? o['Id'] ?? '').trim();
      if (!id) {
        continue;
      }
      const rawQ = Number(
        o['quantidadeProdutos'] ??
          o['QuantidadeProdutos'] ??
          o['quantidadeVendas'] ??
          o['QuantidadeVendas'] ??
          0
      );
      const qtd = Number.isFinite(rawQ) ? Math.max(0, Math.round(rawQ)) : 0;
      porId.set(id, {
        titulo: String(o['titulo'] ?? o['Titulo'] ?? ''),
        valor: Number(o['valor'] ?? o['Valor'] ?? 0),
        qtd
      });
    }
    return base.map((b) => {
      const m = porId.get(b.id);
      if (!m) {
        return { ...b };
      }
      const valor = Number.isFinite(m.valor) ? m.valor : 0;
      return {
        ...b,
        titulo: m.titulo.trim() || b.titulo,
        valorAlvo: valor,
        valorExibido: 0,
        qtdAlvo: m.qtd,
        qtdExibida: 0
      };
    });
  }

  /** Lê <code>vendasPorTipoProdutoLinhas</code> (camelCase ou PascalCase) e monta linhas para animação. */
  private mapearVendasPorProdutoDoPainel(resp: FaturamentoPainelResponse): VendaProdutoLinhaView[] {
    const anyResp = resp as unknown as Record<string, unknown>;
    const raw =
      anyResp['vendasPorTipoProdutoLinhas'] ??
      anyResp['VendasPorTipoProdutoLinhas'];
    if (!Array.isArray(raw) || raw.length === 0) {
      return ordenarVendasPorProdutoPorValorDesc(linhasVendasPorProdutoZeradas());
    }

    const porLabel = new Map<string, { valor: number; qtd: number }>();

    for (const item of raw) {
      const o = item as Record<string, unknown>;
      const label = String(o['label'] ?? o['Label'] ?? '').trim();
      if (!label) {
        continue;
      }
      const valor = Number(o['valor'] ?? o['Valor'] ?? 0);
      const qtdV = Number(o['quantidadeVendas'] ?? o['QuantidadeVendas'] ?? 0);
      const v = Number.isFinite(valor) ? valor : 0;
      const q = Number.isFinite(qtdV) ? Math.max(0, Math.floor(qtdV)) : 0;
      const prev = porLabel.get(label);
      if (prev) {
        porLabel.set(label, { valor: prev.valor + v, qtd: prev.qtd + q });
      } else {
        porLabel.set(label, { valor: v, qtd: q });
      }
    }

    const montadas = VENDAS_POR_PRODUTO_LABELS.map((label) => {
      const row = porLabel.get(label);
      if (!row) {
        return {
          label,
          valorAlvo: 0,
          valorExibido: 0,
          qtdVendasAlvo: 0,
          qtdVendasExibida: 0
        };
      }
      return {
        label,
        valorAlvo: row.valor,
        valorExibido: 0,
        qtdVendasAlvo: row.qtd,
        qtdVendasExibida: 0
      };
    });
    return ordenarVendasPorProdutoPorValorDesc(montadas);
  }

  /**
   * Mesma animação ease-out das formas de pagamento: KPIs, totais do painel de pagamento,
   * linhas de material e grife (valores monetários, quantidades e %).
   */
  private iniciarAnimacaoValoresTela(
    linhasFormas: Array<{ meioPagamento: string; valor: number }>,
    totalAlvo: number
  ): void {
    const token = this.animFormasToken;
    this.formasPagamentoLinhas = linhasFormas.map((l) => ({
      meioPagamento: l.meioPagamento,
      valorAlvo: l.valor,
      valorExibido: 0
    }));
    this.vendasPorProdutoLinhas = this.vendasPorProdutoLinhas.map((row) => ({
      ...row,
      valorExibido: 0,
      qtdVendasExibida: 0
    }));
    this.cardsFamiliaProduto = this.cardsFamiliaProduto.map((c) => ({
      ...c,
      valorExibido: 0,
      qtdExibida: 0
    }));
    this.totalPagamentoExibido = 0;
    this.cdr.markForCheck();

    const duracaoMs = 1200;
    const inicio = performance.now();
    const easeOutCubic = (t: number) => 1 - (1 - t) ** 3;

    const tick = (now: number) => {
      if (token !== this.animFormasToken) {
        return;
      }
      const u = Math.min(1, (now - inicio) / duracaoMs);
      const e = easeOutCubic(u);

      for (const row of this.formasPagamentoLinhas) {
        const interp = row.valorAlvo * e;
        row.valorExibido =
          u >= 1 ? row.valorAlvo : arredondarContagemAnimada(interp, row.valorAlvo);
      }
      for (const row of this.vendasPorProdutoLinhas) {
        const interp = row.valorAlvo * e;
        row.valorExibido =
          u >= 1 ? row.valorAlvo : arredondarContagemAnimada(interp, row.valorAlvo);
        const interpQtd = row.qtdVendasAlvo * e;
        row.qtdVendasExibida =
          u >= 1
            ? row.qtdVendasAlvo
            : row.qtdVendasAlvo <= 0
              ? 0
              : Math.round(arredondarContagemAnimada(interpQtd, row.qtdVendasAlvo));
      }
      for (const c of this.cardsFamiliaProduto) {
        const interpV = c.valorAlvo * e;
        c.valorExibido = u >= 1 ? c.valorAlvo : arredondarContagemAnimada(interpV, c.valorAlvo);
        const interpQtd = c.qtdAlvo * e;
        c.qtdExibida =
          u >= 1
            ? c.qtdAlvo
            : c.qtdAlvo <= 0
              ? 0
              : Math.round(arredondarContagemAnimada(interpQtd, c.qtdAlvo));
      }
      this.totalPagamentoExibido = u >= 1 ? totalAlvo : arredondarContagemAnimada(totalAlvo * e, totalAlvo);

      for (const k of this.kpiCards) {
        const d = this.kpiDados[k.id];
        if (!d) {
          continue;
        }
        this.kpiValorExibido[k.id] =
          u >= 1 ? d.valor : arredondarContagemAnimada(d.valor * e, d.valor);
        this.kpiPercentualExibido[k.id] =
          u >= 1 ? d.percentual : interpolarPercentualAnimado(d.percentual * e, d.percentual);
      }

      for (const l of this.vendasPorMaterialLinhas) {
        l.exBruto = u >= 1 ? l.bruto : arredondarContagemAnimada(l.bruto * e, l.bruto);
        l.exLiquido = u >= 1 ? l.liquido : arredondarContagemAnimada(l.liquido * e, l.liquido);
        l.exQuantidade =
          u >= 1 ? l.quantidade : arredondarContagemAnimada(l.quantidade * e, l.quantidade);
        l.exPercentual =
          u >= 1 ? l.percentual : interpolarPercentualAnimado(l.percentual * e, l.percentual);
      }

      for (const sg of this.vendasPorGrifeSubgrupos) {
        for (const l of sg.linhas) {
          l.exBruto = u >= 1 ? l.bruto : arredondarContagemAnimada(l.bruto * e, l.bruto);
          l.exLiquido = u >= 1 ? l.liquido : arredondarContagemAnimada(l.liquido * e, l.liquido);
          l.exQuantidade =
            u >= 1 ? l.quantidade : arredondarContagemAnimada(l.quantidade * e, l.quantidade);
          l.exPercentual =
            u >= 1 ? l.percentual : interpolarPercentualAnimado(l.percentual * e, l.percentual);
        }
      }

      this.cdr.markForCheck();

      if (u < 1) {
        requestAnimationFrame(tick);
      }
    };
    requestAnimationFrame(tick);
  }

  private toInputDate(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }

  setMode(id: string, mode: FaturamentoViewMode): void {
    this.modes = { ...this.modes, [id]: mode };
  }

  getMode(id: string): FaturamentoViewMode {
    return this.modes[id] ?? 'valor';
  }

  /** Modo efetivo para o switch (cards só Valor/Percentual ignoram <code>grafico</code>). */
  getModeForCard(k: KpiCard): FaturamentoViewMode {
    const m = this.getMode(k.id);
    if (k.toggle === 'valorPercentual' && m === 'grafico') {
      return 'valor';
    }
    return m;
  }

  formatValor(id: string): string {
    if (this.produtosCarregando) {
      return '…';
    }
    const v =
      id in this.kpiValorExibido ? this.kpiValorExibido[id]! : (this.kpiDados[id]?.valor ?? 0);
    if (id === 'vendasProdutos') {
      return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 }).format(v);
    }
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(v);
  }

  formatPercentual(id: string): string {
    if (this.produtosCarregando) {
      return '…';
    }
    const p =
      id in this.kpiPercentualExibido
        ? this.kpiPercentualExibido[id]!
        : (this.kpiDados[id]?.percentual ?? 0);
    return new Intl.NumberFormat('pt-BR', {
      style: 'percent',
      minimumFractionDigits: 1,
      maximumFractionDigits: 1
    }).format(p / 100);
  }

  formatMoeda(v: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(v);
  }

  formatValorFormaLinha(f: FormaPagamentoLinhaView): string {
    if (this.produtosCarregando) {
      return '…';
    }
    return this.formatMoeda(f.valorExibido);
  }

  formatValorVendaProdutoLinha(v: VendaProdutoLinhaView): string {
    if (this.produtosCarregando) {
      return '…';
    }
    return this.formatMoeda(v.valorExibido);
  }

  /** Quantidade de vendas (O.S.) distintas na categoria — inteiro, não unidades de produto. */
  formatQtdVendasProdutoLinha(v: VendaProdutoLinhaView): string {
    if (this.produtosCarregando) {
      return '…';
    }
    return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 }).format(v.qtdVendasExibida);
  }

  formatValorFamiliaProduto(c: VendaFamiliaProdutoCardView): string {
    if (this.produtosCarregando) {
      return '…';
    }
    return this.formatMoeda(c.valorExibido);
  }

  formatQtdFamiliaProduto(c: VendaFamiliaProdutoCardView): string {
    if (this.produtosCarregando) {
      return '…';
    }
    return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 }).format(c.qtdExibida);
  }

  formatTotalPagamentoResumo(): string {
    if (this.produtosCarregando) {
      return '…';
    }
    return this.formatMoeda(this.totalPagamentoExibido);
  }

  formatQuantidadeMaterial(q: number): string {
    if (this.produtosCarregando) {
      return '…';
    }
    return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 2 }).format(q);
  }

  /** `percentual` no modelo é 0–100. */
  formatPercentualMaterial(p: number): string {
    if (this.produtosCarregando) {
      return '…';
    }
    return new Intl.NumberFormat('pt-BR', {
      style: 'percent',
      minimumFractionDigits: 1,
      maximumFractionDigits: 1
    }).format(p / 100);
  }

  /** Há pelo menos uma linha em algum dos 3 cards (01/02/03). */
  temVendasGrifeQualquer(): boolean {
    return this.vendasPorGrifeSubgrupos.some((s) => s.linhas.length > 0);
  }

  kpiLabelPercentual(id: string): string {
    const labels: Record<string, string> = {
      vendasBruto: 'Margem sobre bruto (vs custo)',
      vendas: 'Margem sobre faturamento líquido',
      cmv: 'CMV sobre faturamento líquido',
      descVendedor: 'Percentual médio na venda',
      ticketMedio: 'Ticket médio / maior ticket',
      vendasProdutos: 'Diversidade (produtos / linhas)'
    };
    return labels[id] ?? 'Percentual';
  }
}

/** Interpolação do % (0–100) na animação, com uma casa decimal. */
function interpolarPercentualAnimado(interpolado: number, alvo: number): number {
  if (alvo <= 0) {
    return 0;
  }
  return Math.min(alvo, Math.round(interpolado * 10) / 10);
}

/** Arredonda o valor intermediário da animação para parecer contagem 0,1,2… em escala adequada. */
function arredondarContagemAnimada(interpolado: number, alvo: number): number {
  if (alvo <= 0) {
    return 0;
  }
  if (alvo < 100) {
    return Math.round(interpolado * 100) / 100;
  }
  if (alvo < 10000) {
    return Math.round(interpolado);
  }
  const step = Math.max(1, Math.round(alvo / 500));
  return Math.min(alvo, Math.round(interpolado / step) * step);
}
