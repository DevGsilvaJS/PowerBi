import { ProdutoPorOsItem } from './produto-por-os.model';

const SEP = '\u001f';

/** Números no formato BR ou simples (espelha regras do servidor). */
export function parseBr(s?: string | number | null): number {
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

function valorLinhaSavWin(p: ProdutoPorOsItem): number {
  const pt = parseBr(p.precoTotalProduto);
  if (pt > 0) {
    return pt;
  }
  const vb = parseBr(p.valorBruto);
  return vb > 0 ? vb : 0;
}

function chaveAgrupamentoVenda(p: ProdutoPorOsItem): string {
  const loja = (p.lojaNome ?? '').trim() || '—';
  const cv = (p.codigoDaVenda ?? '').trim();
  if (cv) {
    return `${loja}${SEP}${cv}`;
  }
  return `${loja}${SEP}__sem_os__`;
}

function valorLiquidoLinhaAlocado(
  p: ProdutoPorOsItem,
  somaPrecoPorVenda: Map<string, number>,
  liquidoPorVenda: Map<string, number>,
  faturamentoUsaLiquidoPorVenda: boolean
): number {
  const precoLinha = valorLinhaSavWin(p);
  const kAgg = chaveAgrupamentoVenda(p);
  const sumV = somaPrecoPorVenda.get(kAgg) ?? 0;
  const liqV = liquidoPorVenda.get(kAgg);
  if (liqV != null && liqV > 0 && sumV > 0) {
    return (precoLinha / sumV) * liqV;
  }
  if (faturamentoUsaLiquidoPorVenda) {
    return 0;
  }
  return precoLinha;
}

/**
 * Extrai ano e mês (1–12) de <code>dataVenda</code> (ISO, dd/MM/yyyy ou parse nativo).
 */
export function parseDataVendaAnoMes(s?: string | null): { year: number; month: number } | null {
  if (!s?.trim()) {
    return null;
  }
  const t = s.trim();
  const iso = /^(\d{4})-(\d{2})-(\d{2})/.exec(t);
  if (iso) {
    const year = +iso[1];
    const month = +iso[2];
    if (month >= 1 && month <= 12) {
      return { year, month };
    }
  }
  const br = /^(\d{1,2})\/(\d{1,2})\/(\d{4})/.exec(t);
  if (br) {
    const day = +br[1];
    const month = +br[2];
    const year = +br[3];
    if (month >= 1 && month <= 12 && day >= 1 && day <= 31) {
      return { year, month };
    }
  }
  const ms = Date.parse(t);
  if (!Number.isNaN(ms)) {
    const d = new Date(ms);
    return { year: d.getFullYear(), month: d.getMonth() + 1 };
  }
  return null;
}

/**
 * Soma vendas líquidas por linha (mesma regra do painel Faturamento) agrupadas por <code>YYYY-MM</code>
 * conforme <code>dataVenda</code> de cada linha.
 */
export function agregarVendasLiquidoPorAnoMes(rows: ProdutoPorOsItem[]): Map<string, number> {
  const somaPrecoPorVenda = new Map<string, number>();
  const liquidoPorVenda = new Map<string, number>();

  for (const p of rows) {
    const pl = valorLinhaSavWin(p);
    const kAgg = chaveAgrupamentoVenda(p);
    somaPrecoPorVenda.set(kAgg, (somaPrecoPorVenda.get(kAgg) ?? 0) + pl);
    const liq = parseBr(p.valorLiquidoTotalVenda);
    if (liq > 0) {
      liquidoPorVenda.set(kAgg, liq);
    }
  }

  let sumLiquidoCabecalho = 0;
  for (const v of liquidoPorVenda.values()) {
    sumLiquidoCabecalho += v;
  }
  const faturamentoUsaLiquidoPorVenda = sumLiquidoCabecalho > 0;

  const porMes = new Map<string, number>();
  for (const p of rows) {
    const dm = parseDataVendaAnoMes(p.dataVenda ?? null);
    if (!dm) {
      continue;
    }
    const key = `${dm.year}-${String(dm.month).padStart(2, '0')}`;
    const liquidoLinha = valorLiquidoLinhaAlocado(
      p,
      somaPrecoPorVenda,
      liquidoPorVenda,
      faturamentoUsaLiquidoPorVenda
    );
    porMes.set(key, (porMes.get(key) ?? 0) + liquidoLinha);
  }
  return porMes;
}

/**
 * Conta vendas (O.S.) distintas por mês-calendário, mesma chave de agrupamento do faturamento.
 */
export function agregarVendasDistintasPorAnoMes(rows: ProdutoPorOsItem[]): Map<string, number> {
  const porMesChaves = new Map<string, Set<string>>();
  for (const p of rows) {
    const dm = parseDataVendaAnoMes(p.dataVenda ?? null);
    if (!dm) {
      continue;
    }
    const key = `${dm.year}-${String(dm.month).padStart(2, '0')}`;
    const kAgg = chaveAgrupamentoVenda(p);
    let set = porMesChaves.get(key);
    if (!set) {
      set = new Set<string>();
      porMesChaves.set(key, set);
    }
    set.add(kAgg);
  }
  const out = new Map<string, number>();
  for (const [k, set] of porMesChaves) {
    out.set(k, set.size);
  }
  return out;
}

export const MESES_CURTOS_PT = [
  'Jan',
  'Fev',
  'Mar',
  'Abr',
  'Mai',
  'Jun',
  'Jul',
  'Ago',
  'Set',
  'Out',
  'Nov',
  'Dez'
] as const;

export type VariacaoSentidoMensal = 'alta' | 'queda' | 'igual' | 'semBase';

export interface ComparativoMensalPonto {
  mes: number;
  label: string;
  /** Valor líquido no ano base (menor dos dois anos escolhidos). */
  anoAnterior: number;
  /** Valor líquido no ano de comparação (maior dos dois anos). */
  anoAtual: number;
  /** Número de vendas (O.S.) distintas no mês — ano menor (quando informado na montagem da série). */
  qtdVendasAnterior: number;
  /** Número de vendas (O.S.) distintas no mês — ano maior. */
  qtdVendasAtual: number;
  /** Variação % do mês no ano maior vs o mesmo mês no ano menor; `null` sem base. */
  variacaoPct: number | null;
  variacaoSentido: VariacaoSentidoMensal;
}

function calcVariacaoMensal(anoAnterior: number, anoAtual: number): {
  variacaoPct: number | null;
  variacaoSentido: VariacaoSentidoMensal;
} {
  const eps = 1e-9;
  if (anoAnterior > eps) {
    const pct = ((anoAtual - anoAnterior) / anoAnterior) * 100;
    if (pct > eps) {
      return { variacaoPct: pct, variacaoSentido: 'alta' };
    }
    if (pct < -eps) {
      return { variacaoPct: pct, variacaoSentido: 'queda' };
    }
    return { variacaoPct: 0, variacaoSentido: 'igual' };
  }
  if (anoAtual > eps) {
    return { variacaoPct: null, variacaoSentido: 'semBase' };
  }
  return { variacaoPct: null, variacaoSentido: 'igual' };
}

/**
 * Monta os 12 meses comparando explicitamente <code>anoMenor</code> e <code>anoMaior</code> (ordem livre na tela).
 * <code>porMesQtd</code> opcional: contagens de O.S. distintas por <code>YYYY-MM</code> (ex.: vendas por mês).
 */
export function montarSerieComparativoDoisAnos(
  porMes: Map<string, number>,
  anoMenor: number,
  anoMaior: number,
  porMesQtd?: Map<string, number>
): ComparativoMensalPonto[] {
  return MESES_CURTOS_PT.map((label, i) => {
    const m = i + 1;
    const mm = String(m).padStart(2, '0');
    const ant = porMes.get(`${anoMenor}-${mm}`) ?? 0;
    const atual = porMes.get(`${anoMaior}-${mm}`) ?? 0;
    const antQ = porMesQtd?.get(`${anoMenor}-${mm}`) ?? 0;
    const atualQ = porMesQtd?.get(`${anoMaior}-${mm}`) ?? 0;
    const v = calcVariacaoMensal(ant, atual);
    return {
      mes: m,
      label,
      anoAnterior: ant,
      anoAtual: atual,
      qtdVendasAnterior: antQ,
      qtdVendasAtual: atualQ,
      variacaoPct: v.variacaoPct,
      variacaoSentido: v.variacaoSentido
    };
  });
}
