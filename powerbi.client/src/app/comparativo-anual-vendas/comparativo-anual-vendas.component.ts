import { Component, OnInit } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import {
  LojaOption,
  combinarLojasCadastroComSavwin,
  lojaIdsParaParametroApi
} from '../shared/lojas-filtro';
import {
  agregarVendasDistintasPorAnoMes,
  agregarVendasLiquidoPorAnoMes,
  ComparativoMensalPonto,
  montarSerieComparativoDoisAnos
} from '../relatorios/produto-por-os-vendas-mensal.util';
import { RelatoriosApiService } from '../relatorios/relatorios-api.service';

@Component({
  selector: 'app-comparativo-anual-vendas',
  templateUrl: './comparativo-anual-vendas.component.html',
  styleUrl: './comparativo-anual-vendas.component.css'
})
export class ComparativoAnualVendasComponent implements OnInit {
  /** Anos calendário (ex.: 2024 e 2025); a ordem não importa. */
  ano1: number | null = null;
  ano2: number | null = null;
  lojas: LojaOption[] = [];
  lojaIdsSelecionadas: string[] = [];

  periodoErro = '';
  apiErro = '';

  /** Só após o usuário acionar Pesquisar. */
  jaPesquisou = false;
  carregando = false;

  /** Ano mais antigo e mais novo após ordenar (legenda e %). */
  anoRef = 0;
  anoAnterior = 0;

  serieMensal: ComparativoMensalPonto[] = [];
  maxChart = 1;

  readonly chartW = 920;
  readonly chartH = 360;
  readonly padL = 52;
  readonly padR = 12;
  readonly padT = 28;
  /** Espaço abaixo do eixo para rótulo do mês + variação %. */
  readonly padB = 50;

  constructor(
    private readonly auth: AuthService,
    private readonly relatorios: RelatoriosApiService
  ) {}

  ngOnInit(): void {
    const hoje = new Date();
    const ano = hoje.getFullYear();
    this.ano1 = ano - 1;
    this.ano2 = ano;
    this.relatorios.getLojasSavwin().subscribe((items) => {
      this.lojas = combinarLojasCadastroComSavwin(this.auth.getLojasCadastro(), items);
      this.lojaIdsSelecionadas = [];
    });
  }

  pesquisar(): void {
    this.periodoErro = '';
    this.apiErro = '';
    if (this.ano1 == null || this.ano2 == null) {
      this.periodoErro = 'Informe os dois anos.';
      return;
    }
    const a1 = Number(this.ano1);
    const a2 = Number(this.ano2);
    const minA = 1990;
    const maxA = 2100;

    if (!Number.isFinite(a1) || !Number.isFinite(a2)) {
      this.periodoErro = 'Anos inválidos.';
      return;
    }
    if (!Number.isInteger(a1) || !Number.isInteger(a2)) {
      this.periodoErro = 'Use apenas anos inteiros (ex.: 2024 e 2025).';
      return;
    }
    if (a1 < minA || a1 > maxA || a2 < minA || a2 > maxA) {
      this.periodoErro = `Cada ano deve estar entre ${minA} e ${maxA}.`;
      return;
    }
    if (a1 === a2) {
      this.periodoErro = 'Informe dois anos diferentes para comparar.';
      return;
    }
    if (this.lojas.length > 0 && this.lojaIdsSelecionadas.length === 0) {
      this.periodoErro = 'Selecione ao menos uma loja.';
      return;
    }

    const yMin = Math.min(a1, a2);
    const yMax = Math.max(a1, a2);
    this.anoAnterior = yMin;
    this.anoRef = yMax;

    const dataInicial = `${yMin}-01-01`;
    const dataFinal = `${yMax}-12-31`;

    this.carregando = true;
    const lojaParam = lojaIdsParaParametroApi(this.lojas, this.lojaIdsSelecionadas);
    this.relatorios
      .produtosPorOs({
        dataInicial,
        dataFinal,
        lojaId: lojaParam ?? undefined
      })
      .subscribe({
        next: (rows) => {
          const porMes = agregarVendasLiquidoPorAnoMes(rows ?? []);
          const porMesQtd = agregarVendasDistintasPorAnoMes(rows ?? []);
          this.serieMensal = montarSerieComparativoDoisAnos(porMes, yMin, yMax, porMesQtd);
          this.maxChart = Math.max(
            1,
            ...this.serieMensal.flatMap((p) => [p.anoAnterior, p.anoAtual])
          );
          this.jaPesquisou = true;
          this.carregando = false;
        },
        error: (err) => {
          this.serieMensal = [];
          this.jaPesquisou = true;
          this.carregando = false;
          const msg =
            err?.error?.message ??
            err?.error?.title ??
            err?.message ??
            'Não foi possível carregar os dados.';
          this.apiErro = typeof msg === 'string' ? msg : 'Não foi possível carregar os dados.';
        }
      });
  }

  get innerW(): number {
    return this.chartW - this.padL - this.padR;
  }

  get innerH(): number {
    return this.chartH - this.padT - this.padB;
  }

  barHeight(valor: number): number {
    if (this.maxChart <= 0) {
      return 0;
    }
    return (valor / this.maxChart) * this.innerH;
  }

  slotX(i: number): number {
    const slot = this.innerW / 12;
    return this.padL + i * slot;
  }

  barPairOffset(i: number): { a: number; b: number; slot: number; barW: number } {
    const slot = this.innerW / 12;
    const barW = Math.max(10, slot * 0.34);
    const gap = Math.max(2, slot * 0.08);
    const x0 = this.padL + i * slot + (slot - (2 * barW + gap)) / 2;
    return { a: x0, b: x0 + barW + gap, slot, barW };
  }

  barY(valor: number): number {
    return this.chartH - this.padB - this.barHeight(valor);
  }

  tickYs(): number[] {
    const n = 4;
    const ticks: number[] = [];
    for (let i = 0; i <= n; i++) {
      ticks.push(this.padT + (this.innerH * i) / n);
    }
    return ticks;
  }

  tickValor(i: number): number {
    const n = 4;
    return (this.maxChart * (n - i)) / n;
  }

  formatMoeda(v: number): string {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
      maximumFractionDigits: v >= 100000 ? 0 : 2
    }).format(v);
  }

  formatMoedaCurta(v: number): string {
    if (v >= 1_000_000) {
      return `${(v / 1_000_000).toFixed(v >= 10_000_000 ? 0 : 1)} M`;
    }
    if (v >= 1000) {
      return `${(v / 1000).toFixed(v >= 10000 ? 0 : 1)} k`;
    }
    return this.formatMoeda(v);
  }

  formatInteiro(v: number): string {
    return new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 0 }).format(Math.round(v));
  }

  /** Texto do % vs mesmo mês do ano anterior. */
  textoVariacao(p: ComparativoMensalPonto): string {
    if (p.variacaoPct != null) {
      const sign = p.variacaoPct > 0 ? '+' : '';
      return `${sign}${p.variacaoPct.toFixed(1)}%`;
    }
    if (p.variacaoSentido === 'semBase') {
      return '—';
    }
    return '0%';
  }

  classeVariacaoSvg(p: ComparativoMensalPonto): string {
    switch (p.variacaoSentido) {
      case 'alta':
      case 'semBase':
        return 'chart-variacao chart-variacao--alta';
      case 'queda':
        return 'chart-variacao chart-variacao--queda';
      default:
        return 'chart-variacao chart-variacao--neutra';
    }
  }

  /** Classe de cor na tabela de detalhe (mesma lógica do % no SVG). */
  classeVariacaoLinha(p: ComparativoMensalPonto): string {
    switch (p.variacaoSentido) {
      case 'alta':
      case 'semBase':
        return 'chart-detalhe-var--alta';
      case 'queda':
        return 'chart-detalhe-var--queda';
      default:
        return 'chart-detalhe-var--neutra';
    }
  }

  tituloVariacao(p: ComparativoMensalPonto): string {
    if (p.variacaoPct != null) {
      return `Variação ${this.anoRef} vs ${this.anoAnterior}: ${this.textoVariacao(p)}`;
    }
    if (p.variacaoSentido === 'semBase') {
      return `Sem vendas em ${this.anoAnterior} para comparar; ${this.anoRef}: ${this.formatMoeda(p.anoAtual)}`;
    }
    return 'Sem variação (zerado nos dois anos)';
  }
}
