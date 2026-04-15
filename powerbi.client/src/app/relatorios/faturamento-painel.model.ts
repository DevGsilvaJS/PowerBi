/** Resposta de <code>POST /api/Relatorios/faturamento-painel</code> (espelha DTOs do servidor). */
export interface FaturamentoPainelResponse {
  kpiDados: Record<string, KpiDadoDto>;
  totalPagamentoResumo: number;
  formasPagamento: FormaPagamentoLinhaDto[];
  vendasPorMaterialLinhas: VendaMaterialLinhaDto[];
  vendasPorGrifeSubgrupos: VendaGrifeSubgrupoDto[];
  /** Vendas por categoria (líquido por modalidade; composição de prefixos + cadastro). */
  vendasPorTipoProdutoLinhas?: VendaTipoProdutoLinhaDto[];
  /** Solares / receituários / lentes / serviços — líquido por linha + O.S. distintas com a família. */
  vendasFamiliaProdutoCards?: FaturamentoFamiliaProdutoCardDto[];
  /** Desconto médio por forma de pagamento (APIVendaFormaPagamentoResumo, por parcela). */
  descontoPorFormaPagamento?: DescontoFormaPagamentoLinhaDto[];
  /** Vendas pendentes de entrega (RetornaVendasPendentesCompletas) no período e lojas. */
  pendentesEntrega?: number;
}

export interface DescontoFormaPagamentoLinhaDto {
  planoPagamento: string;
  /** Soma do campo ValorBruto da SavWin por plano. */
  valorBruto: number;
  valorDesconto: number;
  /** Pedidos distintos por `NumeroPedido` (agrupamento do resumo) naquele plano. */
  quantidadeVendas: number;
  descontoPonderadoPercentual: number;
}

export interface FaturamentoFamiliaProdutoCardDto {
  id: string;
  titulo: string;
  valor: number;
  /** Soma de QUANTIDADETOTAL por linha na família. */
  quantidadeProdutos: number;
}

export interface VendaPainelIdentificacaoDto {
  lojaNome?: string | null;
  codigoDaVenda?: string | null;
}

export interface VendaTipoProdutoLinhaDto {
  label: string;
  valor: number;
  /** Número de vendas (O.S.) distintas na categoria. */
  quantidadeVendas: number;
  /** Loja + código da venda (O.S.) classificados nesta modalidade. */
  vendas?: VendaPainelIdentificacaoDto[];
  /** Chaves internas loja + separador + O.S. (fallback se `vendas` não vier no JSON). */
  vendaChavesInternas?: string[];
}

export interface KpiDadoDto {
  valor: number;
  percentual: number;
  bars: number[];
}

export interface FormaPagamentoLinhaDto {
  meioPagamento: string;
  valor: number;
}

export interface VendaMaterialLinhaDto {
  material: string;
  bruto: number;
  liquido: number;
  quantidade: number;
  percentual: number;
}

export interface VendaGrifeSubgrupoDto {
  titulo: string;
  linhas: VendaGrifeLinhaDto[];
}

export interface VendaGrifeLinhaDto {
  grife: string;
  bruto: number;
  liquido: number;
  quantidade: number;
  percentual: number;
}
