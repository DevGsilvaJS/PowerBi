/** Espelha o retorno do proxy `/api/Relatorios/produtos-por-os` (camelCase, ex.: valorLiquidoTotalVenda). */
export interface ProdutoPorOsItem {
  /** Número da O.S.; único em conjunto com a loja (repete entre lojas). */
  codigoDaVenda?: string;
  lojaNome?: string;
  quantidadeTotal?: string;
  vendedor?: string;
  vendedor2?: string;
  vendedor3?: string;
  medico?: string;
  tipoVenda?: string;
  tipoIndicacao?: string;
  dataVenda?: string;
  cliente?: string;
  clientePagador?: string;
  cpfCliente?: string;
  codigoProduto?: string;
  fantasiaProduto?: string;
  custoProdutos?: string;
  valorBruto?: string;
  descontoValorProduto?: string;
  precoTotalProduto?: string;
  usuario?: string;
  grife?: string;
  valorLiquidoTotalVenda?: string;
  taxaAdm?: string;
  descricaoProduto?: string;
  linhaDeProduto?: string;
  horaVenda?: string;
  ncmCodigo?: string;
  ncmDescricao?: string;
  codigoBarras?: string;
  eanProduto?: string;
  upcProduto?: string;
  numeroCupomFiscal?: string;
  dataHoraEmissaoCupom?: string;
  statusCupomFiscal?: string;
  tipoVendedor?: string | null;
  cliSequencial?: string;
  ultimoPagamento?: string;
  /** Quando a API SavWin enviar (ex.: FORMAPAGAMENTO). */
  formaPagamento?: string;
  fabricanteProduto?: string;
  venTotalReceber?: string;
  ehDevolucao?: string;
  pontuacao?: string;
  descontoPercentualDaVenda?: string;
  ipdTipo?: string;
  dataTroca?: string;
}
