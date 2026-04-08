import { ContasPagarPagasGridItem } from './contas-pagar-pagas-grid.model';
import { ContasReceberRecebidasGridItem } from './contas-receber-recebidas-grid.model';

export interface FormaPagamentoComparativoLinha {
  forma: string;
  valor: number;
  percentual: number;
}

function parseBr(s?: string | null): number {
  if (s == null || (typeof s === 'string' && !s.trim())) {
    return 0;
  }
  let t = String(s).trim();
  if (t.includes(',') && t.includes('.')) {
    t = t.replace(/\./g, '').replace(',', '.');
  } else if (t.includes(',')) {
    t = t.replace(',', '.');
  }
  const n = parseFloat(t);
  return Number.isFinite(n) ? n : 0;
}

/** Mesma regra do resumo financeiro: agrupa textos "CARTAO>…" sob o rótulo "CARTAO". */
function chaveFormaTexto(fRaw?: string | null): string {
  const f = fRaw?.trim();
  if (!f) {
    return '—';
  }
  const i = f.indexOf('>');
  if (i === -1) {
    return f;
  }
  const head = f.slice(0, i).trim();
  return head || f;
}

function valorLinhaPagarBaixada(r: ContasPagarPagasGridItem): number {
  const rec = parseBr(r.VLRRECEBIDO);
  return rec > 0 ? rec : parseBr(r.VALOR);
}

function valorLinhaReceberBaixada(r: ContasReceberRecebidasGridItem): number {
  const rec = Math.max(parseBr(r.VALOR_RECEBIDO), parseBr(r.VLRRECEBIDO));
  return rec > 0 ? rec : parseBr(r.VALOR);
}

function ordenarFormas(map: Map<string, number>): FormaPagamentoComparativoLinha[] {
  let total = 0;
  for (const v of map.values()) {
    total += v;
  }
  return [...map.entries()]
    .map(([forma, valor]) => ({
      forma,
      valor,
      percentual: total > 0 ? (valor / total) * 100 : 0
    }))
    .sort(
      (a, b) =>
        b.valor - a.valor || a.forma.localeCompare(b.forma, 'pt-BR', { sensitivity: 'base' })
    );
}

/** Contas a pagar baixadas no grid atual, agrupadas por <code>FORMAPAGAMENTO</code>. */
export function agruparFormasContasPagasBaixadas(
  rows: ContasPagarPagasGridItem[]
): FormaPagamentoComparativoLinha[] {
  const map = new Map<string, number>();
  for (const r of rows) {
    const k = chaveFormaTexto(r.FORMAPAGAMENTO);
    map.set(k, (map.get(k) ?? 0) + valorLinhaPagarBaixada(r));
  }
  return ordenarFormas(map);
}

/** Contas a receber baixadas no grid atual, agrupadas por forma de pagamento. */
export function agruparFormasContasRecebidasBaixadas(
  rows: ContasReceberRecebidasGridItem[]
): FormaPagamentoComparativoLinha[] {
  const map = new Map<string, number>();
  for (const r of rows) {
    const k = chaveFormaTexto(r.FORMA_PAGAMENTO || r.FORMAPAGAMENTO);
    map.set(k, (map.get(k) ?? 0) + valorLinhaReceberBaixada(r));
  }
  return ordenarFormas(map);
}
