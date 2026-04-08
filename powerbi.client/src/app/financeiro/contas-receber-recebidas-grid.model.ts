/** Mesmo corpo do proxy que contas a pagar (loja, STATUSRECEBIDO, DUPEMISSAO*, TIPOPERIODO). */
export type ContasReceberRecebidasGridRequest = import('./contas-pagar-pagas-grid.model').ContasPagarPagasGridRequest;

/**
 * Colunas documentadas pela SavWin (chaves após normalização para maiúsculas;
 * a API pode enviar com ou sem underscore).
 */
export interface ContasReceberRecebidasGridItem {
  VALOR?: string;
  /** Documentação SavWin */
  VALOR_RECEBIDO?: string;
  /** Variante possível no JSON */
  VLRRECEBIDO?: string;
  PLANO_CONTAS?: string;
  PLANOCONTAS?: string;
  GRUPO_PLANO_CONTAS?: string;
  GRUPOPLANOCONTAS?: string;
  FORMA_PAGAMENTO?: string;
  FORMAPAGAMENTO?: string;
  /** Data de vencimento da parcela (quando a SavWin envia). */
  VENCIMENTO?: string;
  PAR_VENCIMENTO?: string;
  RECEBIMENTO?: string;
  REC_RECEBIMENTO?: string;
  RECRECEBIMENTO?: string;
  [key: string]: string | undefined;
}

export function normalizarLinhaContasReceberRecebidas(row: Record<string, unknown>): ContasReceberRecebidasGridItem {
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(row)) {
    if (k == null) {
      continue;
    }
    out[k.toUpperCase()] = v == null || v === '' ? '' : String(v);
  }
  return out as ContasReceberRecebidasGridItem;
}

export function parseRespostaContasReceberRecebidasGrid(raw: unknown): ContasReceberRecebidasGridItem[] {
  if (Array.isArray(raw)) {
    return raw.map((r) =>
      r && typeof r === 'object'
        ? normalizarLinhaContasReceberRecebidas(r as Record<string, unknown>)
        : {}
    ) as ContasReceberRecebidasGridItem[];
  }
  if (raw && typeof raw === 'object') {
    return [normalizarLinhaContasReceberRecebidas(raw as Record<string, unknown>)];
  }
  return [];
}
