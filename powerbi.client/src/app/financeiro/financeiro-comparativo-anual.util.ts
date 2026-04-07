import { ContasPagarPagasGridItem } from './contas-pagar-pagas-grid.model';
import { ContasReceberRecebidasGridItem } from './contas-receber-recebidas-grid.model';
import { parseBr, parseDataVendaAnoMes } from '../relatorios/produto-por-os-vendas-mensal.util';

export function valorLinhaContaPagaBaixada(r: ContasPagarPagasGridItem): number {
  const rec = parseBr(r.VLRRECEBIDO);
  return rec > 0 ? rec : parseBr(r.VALOR);
}

export function valorLinhaContaRecebidaBaixada(r: ContasReceberRecebidasGridItem): number {
  const rec = Math.max(parseBr(r.VALOR_RECEBIDO), parseBr(r.VLRRECEBIDO));
  return rec > 0 ? rec : parseBr(r.VALOR);
}

function dataRecebimentoReceber(r: ContasReceberRecebidasGridItem): string | undefined {
  const v =
    r.RECEBIMENTO?.trim() ||
    r.REC_RECEBIMENTO?.trim() ||
    r.RECRECEBIMENTO?.trim();
  return v || undefined;
}

/**
 * Soma valores por <code>YYYY-MM</code> usando a data de pagamento da parcela (contas pagas).
 * Considera apenas linhas cujo ano está em <code>anoA</code> ou <code>anoB</code>.
 */
export function agregarPagasPorMesDataPagamento(
  rows: ContasPagarPagasGridItem[],
  anoA: number,
  anoB: number
): Map<string, number> {
  const map = new Map<string, number>();
  for (const r of rows) {
    const dm = parseDataVendaAnoMes(r.PAGAMENTO ?? null);
    if (!dm || (dm.year !== anoA && dm.year !== anoB)) {
      continue;
    }
    const key = `${dm.year}-${String(dm.month).padStart(2, '0')}`;
    map.set(key, (map.get(key) ?? 0) + valorLinhaContaPagaBaixada(r));
  }
  return map;
}

/**
 * Soma valores por <code>YYYY-MM</code> usando a data de recebimento (contas recebidas baixadas).
 */
export function agregarRecebidasPorMesDataRecebimento(
  rows: ContasReceberRecebidasGridItem[],
  anoA: number,
  anoB: number
): Map<string, number> {
  const map = new Map<string, number>();
  for (const r of rows) {
    const dm = parseDataVendaAnoMes(dataRecebimentoReceber(r) ?? null);
    if (!dm || (dm.year !== anoA && dm.year !== anoB)) {
      continue;
    }
    const key = `${dm.year}-${String(dm.month).padStart(2, '0')}`;
    map.set(key, (map.get(key) ?? 0) + valorLinhaContaRecebidaBaixada(r));
  }
  return map;
}
