/** Retorno de `/api/Relatorios/venda-resumo-formas-pagamento`. */
export interface VendaResumoFormaPagamentoItem {
  loja?: string;
  meioPagamento?: string;
  bandeiraCartao?: string;
  formaPagamento?: string;
  nParcelas?: string;
  qtdeUso?: string;
  vendasValor?: string;
  taxaAdmPerc?: string;
  valorSTaxaAdm?: string;
}
