/**
 * Corpo do proxy (camelCase). O servidor resolve loja via RetornaLista e envia à SavWin:
 * contas a pagar → FILID = id interno; contas a receber → FILID = código da loja.
 * Demais chaves: STATUSRECEBIDO, DUPEMISSAO*, RECRECEBIMENTO*, PAGAMENTOVENDA*, TIPOPERIODO, etc.
 */
export interface ContasPagarPagasGridRequest {
  lojaId?: string | null;
  /** TODOS | ABERTO | BAIXADO */
  statusRecebido?: string | null;
  /**
   * Período de emissão da duplicata (dd/MM/yyyy).
   * Na ContasPagarPagasGrid pode ser null quando o filtro é só por {@link pagamentoVenda1}/2 (ex.: SavWin).
   */
  duplicataEmissao1?: string | null;
  duplicataEmissao2?: string | null;
  parVencimento1?: string | null;
  parVencimento2?: string | null;
  recRecebimento1?: string | null;
  recRecebimento2?: string | null;
  pagamentoVenda1?: string | null;
  pagamentoVenda2?: string | null;
  /** Padrão SavWin "1" */
  tipoPeriodo?: string | null;
}

export interface ContasPagarPagasGridItem {
  LOJA?: string;
  TIPO?: string;
  DUPLICATA?: string;
  PARCELA?: string;
  EMISSAO?: string;
  CODFORNECEDOR?: string;
  NOMEFANTASIA?: string;
  RAZAOSOCIAL?: string;
  RGINSCESTADUAL?: string;
  CPFCNPJ?: string;
  VENCIMENTO?: string;
  VALOR?: string;
  DESCONTORENEG?: string;
  PAGAMENTO?: string;
  VLRRECEBIDO?: string;
  REFERENTE?: string;
  OBSERVACAO?: string;
  NBANCO?: string;
  BANCO?: string;
  CHEQUE?: string;
  CONTRATO?: string;
  STATUS?: string;
  FORMAPAGAMENTO?: string;
  USUARIO?: string;
  GRUPOPLANOCONTAS?: string;
  JUROS?: string;
  MULTA?: string;
  CENTROCUSTO?: string;
  PROTESTO?: string;
  OBSERVACAOFORMAPAGAMEN?: string;
  NOTAFISCAL?: string;
  PLANOCONTAS?: string;
  PCNUMEROIDENTIFICACAO?: string;
  CODIGOCOMPRAFORNECEDOR?: string;
  BOLETORECEBIDO?: string;
}

export const COLUNAS_CONTAS_PAGAR_PAGAS: ReadonlyArray<{
  key: keyof ContasPagarPagasGridItem;
  titulo: string;
  dica: string;
}> = [
  { key: 'LOJA', titulo: 'Loja', dica: 'Loja da duplicata' },
  { key: 'TIPO', titulo: 'Tipo', dica: 'Título em ABERTO ou BAIXADO' },
  { key: 'DUPLICATA', titulo: 'Duplicata', dica: 'Número da duplicata' },
  { key: 'PARCELA', titulo: 'Parcela', dica: 'Nº da parcela' },
  { key: 'EMISSAO', titulo: 'Emissão', dica: 'Data de emissão' },
  { key: 'CODFORNECEDOR', titulo: 'Cod. forn.', dica: 'Código do fornecedor' },
  { key: 'NOMEFANTASIA', titulo: 'Fantasia', dica: 'Fantasia do fornecedor' },
  { key: 'RAZAOSOCIAL', titulo: 'Razão social', dica: 'Razão do fornecedor' },
  { key: 'RGINSCESTADUAL', titulo: 'IE', dica: 'Documento estadual do fornecedor' },
  { key: 'CPFCNPJ', titulo: 'CPF/CNPJ', dica: 'Documento federal do fornecedor' },
  { key: 'VENCIMENTO', titulo: 'Vencimento', dica: 'Vencimento da parcela' },
  { key: 'VALOR', titulo: 'Valor', dica: 'Valor da parcela' },
  { key: 'DESCONTORENEG', titulo: 'Desc. reneg.', dica: 'Desconto em renegociação' },
  { key: 'PAGAMENTO', titulo: 'Pagamento', dica: 'Data do pagamento' },
  { key: 'VLRRECEBIDO', titulo: 'Vlr recebido', dica: 'Valor recebido' },
  { key: 'REFERENTE', titulo: 'Referente', dica: 'Campo livre na parcela' },
  { key: 'OBSERVACAO', titulo: 'Obs. cab.', dica: 'Observação do cabeçalho da duplicata' },
  { key: 'NBANCO', titulo: 'Nº banco', dica: 'Nº do banco na parcela' },
  { key: 'BANCO', titulo: 'Banco', dica: 'Nome do banco na parcela' },
  { key: 'CHEQUE', titulo: 'Cheque', dica: 'Nº do cheque na parcela' },
  { key: 'CONTRATO', titulo: 'Contrato', dica: 'Nº do contrato da parcela' },
  { key: 'STATUS', titulo: 'Status', dica: 'Em ABERTO, BAIXADA, CANCELADA' },
  { key: 'FORMAPAGAMENTO', titulo: 'Forma pgto', dica: 'Forma de pagamento da parcela' },
  { key: 'USUARIO', titulo: 'Usuário', dica: 'Usuário da duplicata' },
  { key: 'GRUPOPLANOCONTAS', titulo: 'Grupo plano', dica: 'Plano de contas pai' },
  { key: 'JUROS', titulo: 'Juros', dica: 'Juros (manual no contas a pagar)' },
  { key: 'MULTA', titulo: 'Multa', dica: 'Multa (manual no contas a pagar)' },
  { key: 'CENTROCUSTO', titulo: 'Centro custo', dica: 'Centro de custo da parcela' },
  { key: 'PROTESTO', titulo: 'Protesto', dica: '0 Normal, 1 Pelo Banco, 2 Pela Empresa' },
  { key: 'OBSERVACAOFORMAPAGAMEN', titulo: 'Obs. forma pgto', dica: 'Observação da parcela (forma pagamento)' },
  { key: 'NOTAFISCAL', titulo: 'NF', dica: 'Nota fiscal' },
  { key: 'PLANOCONTAS', titulo: 'Plano contas', dica: 'Plano de contas da parcela' },
  { key: 'PCNUMEROIDENTIFICACAO', titulo: 'PC identif.', dica: 'Campo livre na parcela' },
  { key: 'CODIGOCOMPRAFORNECEDOR', titulo: 'Cod. compra', dica: 'Campo livre na parcela' },
  { key: 'BOLETORECEBIDO', titulo: 'Boleto rec.', dica: 'SIM ou NAO' }
];

export function normalizarLinhaContasPagarPagas(row: Record<string, unknown>): ContasPagarPagasGridItem {
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(row)) {
    if (k == null) {
      continue;
    }
    out[k.toUpperCase()] = v == null || v === '' ? '' : String(v);
  }
  return out as ContasPagarPagasGridItem;
}

/**
 * A SavWin pode devolver o array direto ou um objeto com a grade em uma propriedade (ex.: <code>d</code>, <code>Data</code>).
 */
function extrairArrayGradeContasPagar(raw: unknown): unknown[] {
  if (Array.isArray(raw)) {
    return raw;
  }
  if (!raw || typeof raw !== 'object') {
    return [];
  }
  const o = raw as Record<string, unknown>;
  const keys = Object.keys(o);
  const prioridade = /^(data|d|resultado|registros?|rows|items|lista)$/i;
  const kPref = keys.find((k) => prioridade.test(k.trim()) && Array.isArray(o[k]));
  if (kPref) {
    return o[kPref] as unknown[];
  }
  for (const k of keys) {
    const v = o[k];
    if (Array.isArray(v) && v.length > 0 && v[0] != null && typeof v[0] === 'object') {
      return v;
    }
  }
  for (const k of keys) {
    const v = o[k];
    if (Array.isArray(v)) {
      return v;
    }
  }
  return [raw];
}

export function parseRespostaContasPagarPagasGrid(raw: unknown): ContasPagarPagasGridItem[] {
  const linhas = extrairArrayGradeContasPagar(raw);
  return linhas.map((r) =>
    r && typeof r === 'object' ? normalizarLinhaContasPagarPagas(r as Record<string, unknown>) : {}
  ) as ContasPagarPagasGridItem[];
}
