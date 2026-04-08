import { ContasPagarPagasGridItem } from '../financeiro/contas-pagar-pagas-grid.model';
import { ContasReceberRecebidasGridItem } from '../financeiro/contas-receber-recebidas-grid.model';
import {
  valorLinhaContaPagaBaixada,
  valorLinhaContaRecebidaBaixada
} from '../financeiro/financeiro-comparativo-anual.util';
import { MESES_CURTOS_PT, parseBr, parseDataVendaAnoMes } from '../relatorios/produto-por-os-vendas-mensal.util';

export interface PontoResumoMensalKpi {
  mes: number;
  label: string;
  /** Faturamento: soma do valor das parcelas a receber com emissão no mês. */
  faturado: number;
  /** Recebimento: valores recebidos no mês (data de recebimento — cache comparativo). */
  recebido: number;
  /** Contas pagas: pagamentos no mês (data de pagamento — cache comparativo). */
  pagas: number;
  /** Resultado do mês = recebido − pagas. */
  resultado: number;
}

function dataEmissaoDuplicataReceber(r: ContasReceberRecebidasGridItem): string | undefined {
  const o = r as Record<string, string | undefined>;
  const v =
    (r.EMISSAO?.trim?.() ? r.EMISSAO : undefined) ||
    o['EMISSAO']?.trim() ||
    o['DUPEMISSAO']?.trim();
  return v || undefined;
}

function dataRecebimentoReceber(r: ContasReceberRecebidasGridItem): string | undefined {
  const v =
    r.RECEBIMENTO?.trim() ||
    r.REC_RECEBIMENTO?.trim() ||
    r.RECRECEBIMENTO?.trim();
  return v || undefined;
}

/** Converte data em formato SavWin (dd/MM/yyyy, ISO ou parseável) para número YYYYMMDD para comparação inclusiva. */
export function parseSavWinParaYmd(s?: string | null): number | null {
  if (!s?.trim()) {
    return null;
  }
  const t = s.trim();
  const br = /^(\d{1,2})\/(\d{1,2})\/(\d{4})/.exec(t);
  if (br) {
    const day = +br[1];
    const month = +br[2];
    const year = +br[3];
    if (month >= 1 && month <= 12 && day >= 1 && day <= 31) {
      return year * 10000 + month * 100 + day;
    }
  }
  const iso = /^(\d{4})-(\d{2})-(\d{2})/.exec(t);
  if (iso) {
    return +iso[1] * 10000 + +iso[2] * 100 + +iso[3];
  }
  const ms = Date.parse(t);
  if (!Number.isNaN(ms)) {
    const d = new Date(ms);
    return d.getFullYear() * 10000 + (d.getMonth() + 1) * 100 + d.getDate();
  }
  return null;
}

/** Data inicial/final do filtro (inputs type=date, yyyy-MM-dd). */
export function ymdMinMaxFromIsoRange(dataIniIso: string, dataFimIso: string): { min: number; max: number } {
  return { min: ymdFromIsoInput(dataIniIso), max: ymdFromIsoInput(dataFimIso) };
}

function ymdFromIsoInput(iso: string): number {
  const p = /^(\d{4})-(\d{2})-(\d{2})$/.exec(iso.trim());
  if (!p) {
    return NaN;
  }
  return +p[1] * 10000 + +p[2] * 100 + +p[3];
}

/** Meses-calendário entre as datas (inclusive), com rótulo tipo Jan/25. */
export function enumerarMesesEntreIso(dataIniIso: string, dataFimIso: string): { key: string; label: string }[] {
  const pIni = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dataIniIso.trim());
  const pFim = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dataFimIso.trim());
  if (!pIni || !pFim) {
    return [];
  }
  let y = +pIni[1];
  let m = +pIni[2];
  const yEnd = +pFim[1];
  const mEnd = +pFim[2];
  const out: { key: string; label: string }[] = [];
  while (y < yEnd || (y === yEnd && m <= mEnd)) {
    const key = `${y}-${String(m).padStart(2, '0')}`;
    const label = `${MESES_CURTOS_PT[m - 1]}/${String(y).slice(-2)}`;
    out.push({ key, label });
    m++;
    if (m > 12) {
      m = 1;
      y++;
    }
  }
  return out;
}

/** Data do pagamento na grade SavWin de contas a pagar (nomes de coluna variam entre clientes). */
export function dataPagamentoContaPagar(r: ContasPagarPagasGridItem): string | undefined {
  const o = r as Record<string, string | undefined>;
  const ordem = [
    'PAGAMENTO',
    'DATAPAGAMENTO',
    'DATA_PAGAMENTO',
    'DATA_PAGTO',
    'DTPAGAMENTO',
    'DTBAIXA',
    'DATABAIXA',
    'DATA_BAIXA',
    'DATA_DE_PAGAMENTO',
    'DATAPGT',
    'DTPGT',
    'DATAHORABAIXA',
    'DATA_PGTO'
  ];
  for (const k of ordem) {
    const t = o[k]?.trim();
    if (t && parseSavWinParaYmd(t) != null) {
      return t;
    }
  }
  for (const [k, val] of Object.entries(o)) {
    const t = typeof val === 'string' ? val.trim() : '';
    if (!t) {
      continue;
    }
    const ku = k.toUpperCase();
    if (!/(PAG|BAIX|PGTO|DTP|DAT.*PAG|PAG.*DAT)/i.test(ku)) {
      continue;
    }
    if (parseSavWinParaYmd(t) != null) {
      return t;
    }
  }
  return undefined;
}

export function agregarPagasPorMesDataPagamentoIntervalo(
  rows: ContasPagarPagasGridItem[],
  ymdMin: number,
  ymdMax: number
): Map<string, number> {
  const map = new Map<string, number>();
  for (const r of rows) {
    const campoPag = dataPagamentoContaPagar(r);
    const ymd = parseSavWinParaYmd(campoPag ?? null);
    if (ymd == null || ymd < ymdMin || ymd > ymdMax) {
      continue;
    }
    const dm = parseDataVendaAnoMes(campoPag ?? null);
    if (!dm) {
      continue;
    }
    const key = `${dm.year}-${String(dm.month).padStart(2, '0')}`;
    map.set(key, (map.get(key) ?? 0) + valorLinhaContaPagaBaixada(r));
  }
  return map;
}

export function agregarRecebidasPorMesDataRecebimentoIntervalo(
  rows: ContasReceberRecebidasGridItem[],
  ymdMin: number,
  ymdMax: number
): Map<string, number> {
  const map = new Map<string, number>();
  for (const r of rows) {
    const campo = dataRecebimentoReceber(r);
    const ymd = parseSavWinParaYmd(campo ?? null);
    if (ymd == null || ymd < ymdMin || ymd > ymdMax) {
      continue;
    }
    const dm = parseDataVendaAnoMes(campo ?? null);
    if (!dm) {
      continue;
    }
    const key = `${dm.year}-${String(dm.month).padStart(2, '0')}`;
    map.set(key, (map.get(key) ?? 0) + valorLinhaContaRecebidaBaixada(r));
  }
  return map;
}

export function agregarReceberPorMesDataEmissaoIntervalo(
  rows: ContasReceberRecebidasGridItem[],
  ymdMin: number,
  ymdMax: number
): Map<string, number> {
  const map = new Map<string, number>();
  for (const r of rows) {
    const campo = dataEmissaoDuplicataReceber(r);
    const ymd = parseSavWinParaYmd(campo ?? null);
    if (ymd == null || ymd < ymdMin || ymd > ymdMax) {
      continue;
    }
    const dm = parseDataVendaAnoMes(campo ?? null);
    if (!dm) {
      continue;
    }
    const key = `${dm.year}-${String(dm.month).padStart(2, '0')}`;
    map.set(key, (map.get(key) ?? 0) + parseBr(r.VALOR));
  }
  return map;
}

/**
 * Alinha faturamento (emissão), recebidos, pagas e resultado por mês no intervalo.
 */
export function montarSerieResumoPorIntervalo(
  porMesEmissao: Map<string, number>,
  porMesRec: Map<string, number>,
  porMesPag: Map<string, number>,
  meses: { key: string; label: string }[]
): PontoResumoMensalKpi[] {
  return meses.map((meta, idx) => {
    const faturado = porMesEmissao.get(meta.key) ?? 0;
    const recebido = porMesRec.get(meta.key) ?? 0;
    const pagas = porMesPag.get(meta.key) ?? 0;
    const resultado = recebido - pagas;
    return { mes: idx + 1, label: meta.label, faturado, recebido, pagas, resultado };
  });
}

export function somaCampoPontos(
  pontos: PontoResumoMensalKpi[],
  campo: 'faturado' | 'recebido' | 'pagas' | 'resultado'
): number {
  let t = 0;
  for (const p of pontos) {
    t += p[campo];
  }
  return t;
}

export interface InsightResumoFaturamento {
  texto: string;
  tipo: 'ok' | 'alerta' | 'info';
}

export function gerarInsightsResumoKpi(pontos: PontoResumoMensalKpi[]): InsightResumoFaturamento[] {
  const out: InsightResumoFaturamento[] = [];
  if (pontos.length < 3) {
    return out;
  }

  const valid = pontos.filter((p) => p.faturado > 0 || p.recebido > 0 || p.pagas > 0);
  if (valid.length === 0) {
    return out;
  }

  const last3 = pontos.slice(-3);
  const dFat = last3[2].faturado - last3[0].faturado;
  const dRes = last3[2].resultado - last3[0].resultado;

  if (dFat > 1 && dRes < -1) {
    out.push({
      tipo: 'alerta',
      texto:
        'Faturamento em alta nos últimos meses, mas o resultado (recebido − contas pagas) caiu — revise despesas, prazos de recebimento ou concentração de pagamentos.'
    });
  }

  const resRecentes = last3.map((p) => p.resultado);
  const resTodosPositivos = resRecentes.every((r) => r >= -1e-6);
  const crescente =
    resRecentes[2] > resRecentes[0] + 1 &&
    resRecentes[1] >= resRecentes[0] - 1e-6 &&
    resRecentes[2] >= resRecentes[1] - 1e-6;
  if (resTodosPositivos && crescente) {
    out.push({
      tipo: 'ok',
      texto: 'Resultado positivo e em tendência de alta — leitura favorável para o caixa no período recente.'
    });
  }

  const dRec = last3[2].recebido - last3[0].recebido;
  if (dFat > 1 && dRec < -1) {
    out.push({
      tipo: 'alerta',
      texto:
        'Faturado (emissão) subiu e os recebimentos caíram — possível inadimplência ou mudança de prazo na carteira.'
    });
  }

  const totalFat = pontos.reduce((s, p) => s + p.faturado, 0);
  const totalRec = pontos.reduce((s, p) => s + p.recebido, 0);
  const escala = Math.max(1, totalFat, totalRec, ...pontos.flatMap((p) => [p.faturado, p.recebido, p.pagas]));
  const gaps = pontos.map((p) => p.faturado - p.recebido);
  const media = gaps.reduce((a, b) => a + b, 0) / gaps.length;
  const variancia =
    gaps.reduce((s, g) => s + (g - media) * (g - media), 0) / Math.max(1, gaps.length);
  const desvio = Math.sqrt(variancia);
  if (desvio < escala * 0.05 && Math.abs(media) > escala * 0.02) {
    out.push({
      tipo: 'info',
      texto:
        'A diferença entre faturamento e recebimento permanece estável — descompasso de prazo relativamente constante.'
    });
  }

  const gapTotal = totalFat - totalRec;
  if (Math.abs(gapTotal) < escala * 0.12) {
    out.push({
      tipo: 'ok',
      texto: 'Faturamento e recebimentos totais próximos no período — reconhecimento alinhado à entrada de caixa.'
    });
  }

  return out;
}

/** Seções de texto geradas a partir dos pontos do gráfico (leitura dinâmica). */
export interface SecaoNarrativaResumo {
  titulo: string;
  paragrafos: string[];
}

function escalaMaxPontos(pontos: PontoResumoMensalKpi[]): number {
  let m = 1;
  for (const p of pontos) {
    m = Math.max(m, Math.abs(p.faturado), Math.abs(p.recebido), Math.abs(p.pagas), Math.abs(p.resultado));
  }
  return m;
}

/**
 * Gera leitura detalhada com base nos valores do gráfico (totais, transições, gaps, picos).
 */
export function gerarNarrativaResumoGrafico(
  pontos: PontoResumoMensalKpi[],
  formatMoeda: (v: number) => string
): SecaoNarrativaResumo[] {
  if (pontos.length === 0) {
    return [];
  }

  const escala = escalaMaxPontos(pontos);
  const thr = Math.max(escala * 0.035, 10);
  const epsZero = Math.max(escala * 0.002, 1);

  const out: SecaoNarrativaResumo[] = [];

  const tFat = somaCampoPontos(pontos, 'faturado');
  const tRec = somaCampoPontos(pontos, 'recebido');
  const tPag = somaCampoPontos(pontos, 'pagas');
  const tRes = somaCampoPontos(pontos, 'resultado');

  const pano: string[] = [];
  pano.push(
    `Somando todos os meses exibidos, o faturamento (por emissão) totaliza ${formatMoeda(tFat)}, os recebimentos ${formatMoeda(tRec)}, as contas pagas ${formatMoeda(tPag)} e o resultado acumulado (recebido − pagas) soma ${formatMoeda(tRes)}.`
  );
  if (tRes > thr) {
    pano.push(
      `No agregado do período, entrou mais dinheiro do que saiu em pagamentos — leitura de fluxo de caixa positivo neste recorte.`
    );
  } else if (tRes < -thr) {
    pano.push(
      `No agregado do período, os pagamentos superaram os recebimentos — fluxo de caixa negativo neste recorte.`
    );
  } else {
    pano.push(`No agregado, recebimentos e pagamentos ficam próximos do equilíbrio.`);
  }
  out.push({ titulo: 'Panorama do período', paragrafos: pano });

  if (pontos.length >= 2) {
    type Trans = { score: number; texto: string };
    const transicoes: Trans[] = [];
    for (let i = 1; i < pontos.length; i++) {
      const a = pontos[i - 1];
      const b = pontos[i];
      const df = b.faturado - a.faturado;
      const dr = b.recebido - a.recebido;
      const dp = b.pagas - a.pagas;
      const ds = b.resultado - a.resultado;
      const score = Math.max(Math.abs(df), Math.abs(dr), Math.abs(dp), Math.abs(ds));
      if (score < thr) {
        continue;
      }
      const partes: string[] = [];
      if (Math.abs(df) >= thr) {
        partes.push(
          df < 0
            ? `o faturamento caiu de ${formatMoeda(a.faturado)} (${a.label}) para ${formatMoeda(b.faturado)} (${b.label})`
            : `o faturamento subiu de ${formatMoeda(a.faturado)} (${a.label}) para ${formatMoeda(b.faturado)} (${b.label})`
        );
      }
      if (Math.abs(dr) >= thr) {
        partes.push(
          dr < 0
            ? `os recebimentos recuaram de ${formatMoeda(a.recebido)} para ${formatMoeda(b.recebido)}`
            : `os recebimentos avançaram de ${formatMoeda(a.recebido)} para ${formatMoeda(b.recebido)}`
        );
      }
      if (Math.abs(dp) >= thr) {
        partes.push(
          dp < 0
            ? `as contas pagas caíram de ${formatMoeda(a.pagas)} para ${formatMoeda(b.pagas)}`
            : `as contas pagas subiram de ${formatMoeda(a.pagas)} para ${formatMoeda(b.pagas)}`
        );
      }
      if (Math.abs(ds) >= thr) {
        partes.push(
          ds < 0
            ? `o resultado (recebido − pagas) piorou de ${formatMoeda(a.resultado)} para ${formatMoeda(b.resultado)}`
            : `o resultado melhorou de ${formatMoeda(a.resultado)} para ${formatMoeda(b.resultado)}`
        );
      }
      if (partes.length > 0) {
        transicoes.push({
          score,
          texto: `Entre ${a.label} e ${b.label}, ${partes.join('; ')}.`
        });
      }
    }
    transicoes.sort((x, y) => y.score - x.score);
    const limite = pontos.length > 14 ? 6 : 10;
    const escolhidas = transicoes.slice(0, limite);
    if (escolhidas.length > 0) {
      out.push({
        titulo: 'O que mudou de um mês para o outro',
        paragrafos: escolhidas.map((t) => t.texto)
      });
    }
  }

  let iMaxFat = 0;
  let iMinFat = 0;
  let iMaxRec = 0;
  for (let i = 0; i < pontos.length; i++) {
    if (pontos[i].faturado > pontos[iMaxFat].faturado) {
      iMaxFat = i;
    }
    if (pontos[i].faturado < pontos[iMinFat].faturado) {
      iMinFat = i;
    }
    if (pontos[i].recebido > pontos[iMaxRec].recebido) {
      iMaxRec = i;
    }
  }

  const picos: string[] = [];
  if (pontos[iMaxFat].faturado >= thr) {
    picos.push(
      `O pico de faturamento ocorre em ${pontos[iMaxFat].label} (${formatMoeda(pontos[iMaxFat].faturado)}).`
    );
  }
  if (pontos[iMinFat].faturado < pontos[iMaxFat].faturado - thr && pontos.length > 1) {
    picos.push(
      `O menor faturamento mensal está em ${pontos[iMinFat].label} (${formatMoeda(pontos[iMinFat].faturado)}).`
    );
  }
  if (pontos[iMaxRec].recebido >= thr) {
    picos.push(
      `O maior recebimento mensal está em ${pontos[iMaxRec].label} (${formatMoeda(pontos[iMaxRec].recebido)}).`
    );
  }
  if (picos.length > 0) {
    out.push({ titulo: 'Picos e vales nas séries', paragrafos: picos });
  }

  let idxMaxGap = 0;
  let maxGap = -Infinity;
  let idxMinGap = 0;
  let minGap = Infinity;
  for (let i = 0; i < pontos.length; i++) {
    const g = pontos[i].recebido - pontos[i].faturado;
    if (g > maxGap) {
      maxGap = g;
      idxMaxGap = i;
    }
    if (g < minGap) {
      minGap = g;
      idxMinGap = i;
    }
  }

  const gapPar: string[] = [];
  if (maxGap > thr) {
    gapPar.push(
      `Em ${pontos[idxMaxGap].label}, o recebimento supera o faturamento em ${formatMoeda(maxGap)} no mesmo mês-calendário. Isso costuma indicar entrada de parcelas, atrasados ou títulos emitidos em meses anteriores — o caixa daquele mês não corresponde só às vendas do próprio mês.`
    );
  }
  if (minGap < -thr) {
    gapPar.push(
      `Em ${pontos[idxMinGap].label}, o faturamento fica ${formatMoeda(-minGap)} acima do recebido — receita reconhecida com entrada de caixa ainda menor naquele mês (carteira a receber ou concentração de emissão).`
    );
  }
  if (gapPar.length > 0) {
    out.push({ titulo: 'Faturamento × recebimento (mesmo mês)', paragrafos: gapPar });
  }

  const neg = pontos.filter((p) => p.resultado < -thr);
  const pos = pontos.filter((p) => p.resultado > thr);
  if (neg.length > 0 || pos.length > 0) {
    const resumo: string[] = [];
    if (pos.length > 0) {
      resumo.push(
        `${pos.length} mês(es) com resultado claramente positivo (${pos.map((p) => p.label).join(', ')}).`
      );
    }
    if (neg.length > 0) {
      resumo.push(
        `${neg.length} mês(es) com resultado negativo (${neg.map((p) => p.label).join(', ')}).`
      );
    }
    out.push({ titulo: 'Resultado mês a mês (recebido − pagas)', paragrafos: resumo });
  }

  let k = pontos.length - 1;
  while (k >= 0 && pontos[k].faturado < epsZero && pontos[k].pagas < epsZero) {
    k--;
  }
  const primeiroSuffixZero = k + 1;
  if (primeiroSuffixZero < pontos.length && primeiroSuffixZero > 0) {
    const mesesZ = pontos.slice(primeiroSuffixZero);
    const temRec = mesesZ.some((p) => p.recebido > thr || p.resultado > thr);
    if (temRec) {
      out.push({
        titulo: 'Meses com faturamento e pagamentos zerados',
        paragrafos: [
          `A partir de ${pontos[primeiroSuffixZero].label}, faturamento e contas pagas aparecem zerados ou muito baixos, enquanto ainda há recebimento ou resultado nesses meses — típico de recorte que inclui datas futuras, falta de lançamento ou entradas de caixa sem nova emissão ou pagamento no período.`
        ]
      });
    }
  }

  return out;
}
