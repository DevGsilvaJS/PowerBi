import { Component, OnInit } from '@angular/core';
import { forkJoin } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { RelatoriosApiService } from '../relatorios/relatorios-api.service';
import { LojaOption, lojaIdsParaParametroApi, opcoesLojasDoCadastro } from '../shared/lojas-filtro';
import { ContasPagarPagasGridItem, ContasPagarPagasGridRequest } from './contas-pagar-pagas-grid.model';
import { ContasReceberRecebidasGridItem } from './contas-receber-recebidas-grid.model';

export interface DespesaPorPlano {
  plano: string;
  valor: number;
}

export interface ReceberPorFormaPagamento {
  forma: string;
  valor: number;
  /** Parte percentual do total do bloco (0–100). */
  percentual: number;
}

@Component({
  selector: 'app-financeiro',
  templateUrl: './financeiro.component.html',
  styleUrl: './financeiro.component.css'
})
export class FinanceiroComponent implements OnInit {
  lojas: LojaOption[] = [];
  lojaIdsSelecionadas: string[] = [];

  /** Período de emissão da duplicata (DUPEMISSAO1 / DUPEMISSAO2) */
  duplicataEmissao1 = '';
  duplicataEmissao2 = '';

  periodoErro = '';
  apiErro = '';

  carregando = false;
  linhasAberto: ContasPagarPagasGridItem[] = [];
  linhasBaixado: ContasPagarPagasGridItem[] = [];
  linhasReceberAberto: ContasReceberRecebidasGridItem[] = [];
  linhasReceberBaixado: ContasReceberRecebidasGridItem[] = [];

  constructor(
    private readonly auth: AuthService,
    private readonly relatorios: RelatoriosApiService
  ) {}

  ngOnInit(): void {
    const hoje = new Date();
    const inicio = new Date(hoje.getFullYear(), hoje.getMonth(), 1);
    this.duplicataEmissao1 = this.formatarDdMmYyyy(inicio);
    this.duplicataEmissao2 = this.formatarDdMmYyyy(hoje);
    this.montarLojasDoCadastro();
  }

  private formatarDdMmYyyy(d: Date): string {
    const dd = String(d.getDate()).padStart(2, '0');
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    const yyyy = d.getFullYear();
    return `${dd}/${mm}/${yyyy}`;
  }

  montarLojasDoCadastro(): void {
    this.lojas = opcoesLojasDoCadastro(this.auth.getLojasCadastro());
    this.lojaIdsSelecionadas = this.lojas.map((l) => l.id);
  }

  pesquisar(): void {
    this.periodoErro = '';
    this.apiErro = '';
    const reData = /^\d{2}\/\d{2}\/\d{4}$/;
    const d1 = this.duplicataEmissao1?.trim() ?? '';
    const d2 = this.duplicataEmissao2?.trim() ?? '';
    if (!reData.test(d1) || !reData.test(d2)) {
      this.periodoErro = 'Informe emissão inicial e final no formato dd/mm/aaaa.';
      return;
    }
    const [dia1, mes1, ano1] = d1.split('/').map((x) => parseInt(x, 10));
    const [dia2, mes2, ano2] = d2.split('/').map((x) => parseInt(x, 10));
    if (new Date(ano2, mes2 - 1, dia2).getTime() < new Date(ano1, mes1 - 1, dia1).getTime()) {
      this.periodoErro = 'A data final não pode ser anterior à inicial.';
      return;
    }
    if (this.lojas.length > 0 && this.lojaIdsSelecionadas.length === 0) {
      this.periodoErro = 'Selecione ao menos uma loja.';
      return;
    }

    const lojaParam = lojaIdsParaParametroApi(this.lojas, this.lojaIdsSelecionadas);
    const base: Omit<ContasPagarPagasGridRequest, 'statusRecebido'> = {
      lojaId: lojaParam,
      duplicataEmissao1: d1,
      duplicataEmissao2: d2,
      tipoPeriodo: '1'
    };

    this.carregando = true;
    this.linhasAberto = [];
    this.linhasBaixado = [];
    this.linhasReceberAberto = [];
    this.linhasReceberBaixado = [];

    forkJoin({
      pagarAberto: this.relatorios.contasPagarPagasGrid({ ...base, statusRecebido: 'ABERTO' }),
      pagarBaixado: this.relatorios.contasPagarPagasGrid({ ...base, statusRecebido: 'BAIXADO' }),
      receberAberto: this.relatorios.contasReceberRecebidasGrid({ ...base, statusRecebido: 'ABERTO' }),
      receberBaixado: this.relatorios.contasReceberRecebidasGrid({ ...base, statusRecebido: 'BAIXADO' })
    }).subscribe({
      next: ({ pagarAberto, pagarBaixado, receberAberto, receberBaixado }) => {
        this.linhasAberto = pagarAberto ?? [];
        this.linhasBaixado = pagarBaixado ?? [];
        this.linhasReceberAberto = receberAberto ?? [];
        this.linhasReceberBaixado = receberBaixado ?? [];
        this.carregando = false;
      },
      error: (err) => {
        this.linhasAberto = [];
        this.linhasBaixado = [];
        this.linhasReceberAberto = [];
        this.linhasReceberBaixado = [];
        this.carregando = false;
        const msg =
          err?.error?.message ??
          (typeof err?.error === 'string' ? err.error : null) ??
          err?.message ??
          'Não foi possível carregar as contas.';
        this.apiErro = typeof msg === 'string' ? msg : 'Não foi possível carregar as contas.';
      }
    });
  }

  /** Agregação por <code>PLANOCONTAS</code> (fallback <code>GRUPOPLANOCONTAS</code>). */
  get despesasPorPlanoAberto(): DespesaPorPlano[] {
    return this.agruparPorPlano(this.linhasAberto, (r) => this.parseBr(r.VALOR));
  }

  get despesasPorPlanoBaixado(): DespesaPorPlano[] {
    return this.agruparPorPlano(this.linhasBaixado, (r) => this.valorLinhaBaixado(r));
  }

  get receberPorFormaAberto(): ReceberPorFormaPagamento[] {
    return this.agruparPorFormaPagamento(this.linhasReceberAberto, (r) => this.parseBr(r.VALOR));
  }

  get receberPorFormaBaixado(): ReceberPorFormaPagamento[] {
    return this.agruparPorFormaPagamento(this.linhasReceberBaixado, (r) => this.valorLinhaRecebida(r));
  }

  /** Soma <code>VALOR</code> das parcelas em aberto. */
  get totalValorAberto(): number {
    return this.somarCampoValor(this.linhasAberto);
  }

  /** Soma recebido; se linha sem <code>VLRRECEBIDO</code>, usa <code>VALOR</code>. */
  get totalValorBaixado(): number {
    let t = 0;
    for (const r of this.linhasBaixado) {
      const rec = this.parseBr(r.VLRRECEBIDO);
      t += rec > 0 ? rec : this.parseBr(r.VALOR);
    }
    return t;
  }

  get totalReceberAberto(): number {
    return this.somarValorParcelaReceber(this.linhasReceberAberto);
  }

  /** Soma <code>VALOR_RECEBIDO</code> / <code>VLRRECEBIDO</code>; senão <code>VALOR</code>. */
  get totalReceberBaixado(): number {
    let t = 0;
    for (const r of this.linhasReceberBaixado) {
      t += this.valorLinhaRecebida(r);
    }
    return t;
  }

  formatMoeda(v: number): string {
    if (this.carregando) {
      return '…';
    }
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL'
    }).format(v);
  }

  formatPercentual(pct: number): string {
    if (this.carregando) {
      return '…';
    }
    return `${pct.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
  }

  private agruparPorPlano(
    rows: ContasPagarPagasGridItem[],
    valorDe: (r: ContasPagarPagasGridItem) => number
  ): DespesaPorPlano[] {
    const map = new Map<string, number>();
    for (const r of rows) {
      const k = this.chavePlanoContas(r);
      map.set(k, (map.get(k) ?? 0) + valorDe(r));
    }
    return [...map.entries()]
      .map(([plano, valor]) => ({ plano, valor }))
      .sort(
        (a, b) =>
          b.valor - a.valor || a.plano.localeCompare(b.plano, 'pt-BR', { sensitivity: 'base' })
      );
  }

  private chavePlanoContas(r: ContasPagarPagasGridItem): string {
    const p = r.PLANOCONTAS?.trim();
    if (p) {
      return p;
    }
    const g = r.GRUPOPLANOCONTAS?.trim();
    if (g) {
      return g;
    }
    return '—';
  }

  private valorLinhaBaixado(r: ContasPagarPagasGridItem): number {
    const rec = this.parseBr(r.VLRRECEBIDO);
    return rec > 0 ? rec : this.parseBr(r.VALOR);
  }

  private agruparPorFormaPagamento(
    rows: ContasReceberRecebidasGridItem[],
    valorDe: (r: ContasReceberRecebidasGridItem) => number
  ): ReceberPorFormaPagamento[] {
    const map = new Map<string, number>();
    for (const r of rows) {
      const k = this.chaveFormaPagamento(r);
      map.set(k, (map.get(k) ?? 0) + valorDe(r));
    }
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

  /** Agrupa textos "CARTAO>…" (ex.: bandeira/parcela) sob o rótulo "CARTAO". */
  private chaveFormaPagamento(r: ContasReceberRecebidasGridItem): string {
    const f = r.FORMA_PAGAMENTO?.trim() || r.FORMAPAGAMENTO?.trim();
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

  private somarCampoValor(rows: ContasPagarPagasGridItem[]): number {
    let t = 0;
    for (const r of rows) {
      t += this.parseBr(r.VALOR);
    }
    return t;
  }

  private somarValorParcelaReceber(rows: ContasReceberRecebidasGridItem[]): number {
    let t = 0;
    for (const r of rows) {
      t += this.parseBr(r.VALOR);
    }
    return t;
  }

  private valorLinhaRecebida(r: ContasReceberRecebidasGridItem): number {
    const rec = Math.max(this.parseBr(r.VALOR_RECEBIDO), this.parseBr(r.VLRRECEBIDO));
    return rec > 0 ? rec : this.parseBr(r.VALOR);
  }

  private parseBr(s?: string | null): number {
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
}
