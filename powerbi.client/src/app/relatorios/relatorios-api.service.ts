import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, of, throwError } from 'rxjs';
import { map } from 'rxjs/operators';
import { ComparativoFinanceiroCacheDto } from '../financeiro/comparativo-financeiro-cache.model';
import {
  ContasPagarPagasGridItem,
  ContasPagarPagasGridRequest,
  parseRespostaContasPagarPagasGrid
} from '../financeiro/contas-pagar-pagas-grid.model';
import {
  ContasReceberRecebidasGridItem,
  ContasReceberRecebidasGridRequest,
  parseRespostaContasReceberRecebidasGrid
} from '../financeiro/contas-receber-recebidas-grid.model';
import {
  EntradasEstoqueGridItem,
  EntradasEstoqueGridRequest,
  parseRespostaEntradasEstoqueGrid
} from '../estoque/entradas-estoque-grid.model';
import { environment } from '../../environments/environment';
import { FaturamentoPainelResponse } from './faturamento-painel.model';
import { ProdutoPorOsItem } from './produto-por-os.model';
import { VendaResumoFormaPagamentoItem } from './venda-resumo-forma-pagamento.model';

export interface ProdutosPorOsRequest {
  dataInicial: string;
  dataFinal: string;
  /** Opcional: uma loja; omitir ou vazio = todas (string do cadastro). */
  lojaId?: string;
}

export interface VendaResumoFormasPagamentoRequest {
  dataPgtoInicio: string;
  dataPgtoFim: string;
  dataVendaInicio?: string | null;
  dataVendaFim?: string | null;
  agrupaFormaPagamento?: string | null;
  lojaId?: string;
}

@Injectable({
  providedIn: 'root'
})
export class RelatoriosApiService {
  private readonly base = `${environment.apiBaseUrl.replace(/\/$/, '')}/api/Relatorios`;
  private readonly url = `${this.base}/produtos-por-os`;
  private readonly urlFormasPagamento = `${this.base}/venda-resumo-formas-pagamento`;
  private readonly urlFaturamentoPainel = `${this.base}/faturamento-painel`;
  private readonly urlEntradasEstoqueGrid = `${this.base}/entradas-estoque-grid`;
  private readonly urlContasPagarPagasGrid = `${this.base}/contas-pagar-pagas-grid`;
  private readonly urlContasReceberRecebidasGrid = `${this.base}/contas-receber-recebidas-grid`;
  private readonly urlComparativoFinanceiroCache = `${this.base}/comparativo-financeiro-cache`;

  constructor(private readonly http: HttpClient) {}

  /** KPIs, material, grife e formas já agregados no servidor. */
  faturamentoPainel(body: ProdutosPorOsRequest): Observable<FaturamentoPainelResponse> {
    return this.http.post<FaturamentoPainelResponse>(this.urlFaturamentoPainel, {
      dataInicial: body.dataInicial,
      dataFinal: body.dataFinal,
      lojaId: body.lojaId?.trim() || null
    });
  }

  produtosPorOs(body: ProdutosPorOsRequest): Observable<ProdutoPorOsItem[]> {
    return this.http.post<ProdutoPorOsItem[]>(this.url, {
      dataInicial: body.dataInicial,
      dataFinal: body.dataFinal,
      lojaId: body.lojaId?.trim() || null
    });
  }

  vendaResumoFormasPagamento(
    body: VendaResumoFormasPagamentoRequest
  ): Observable<VendaResumoFormaPagamentoItem[]> {
    return this.http.post<VendaResumoFormaPagamentoItem[]>(this.urlFormasPagamento, {
      dataPgtoInicio: body.dataPgtoInicio,
      dataPgtoFim: body.dataPgtoFim,
      dataVendaInicio: body.dataVendaInicio ?? null,
      dataVendaFim: body.dataVendaFim ?? null,
      agrupaFormaPagamento: body.agrupaFormaPagamento ?? 'S',
      lojaId: body.lojaId?.trim() || null
    });
  }

  /** Grid Entradas de estoque (SavWin), linhas com campos documentados em <code>entradas-estoque-grid.model</code>. */
  entradasEstoqueGrid(body: EntradasEstoqueGridRequest): Observable<EntradasEstoqueGridItem[]> {
    return this.http.post<unknown>(this.urlEntradasEstoqueGrid, {
      lojaId: body.lojaId?.trim() || null,
      inicioSeq: body.inicioSeq?.trim() || null,
      finalSeq: body.finalSeq?.trim() || null
    }).pipe(map((raw) => parseRespostaEntradasEstoqueGrid(raw)));
  }

  contasPagarPagasGrid(body: ContasPagarPagasGridRequest): Observable<ContasPagarPagasGridItem[]> {
    return this.http.post<unknown>(this.urlContasPagarPagasGrid, {
      lojaId: body.lojaId?.trim() || null,
      statusRecebido: body.statusRecebido?.trim() || null,
      duplicataEmissao1: body.duplicataEmissao1.trim(),
      duplicataEmissao2: body.duplicataEmissao2.trim(),
      parVencimento1: body.parVencimento1?.trim() || null,
      parVencimento2: body.parVencimento2?.trim() || null,
      recRecebimento1: body.recRecebimento1?.trim() || null,
      recRecebimento2: body.recRecebimento2?.trim() || null,
      pagamentoVenda1: body.pagamentoVenda1?.trim() || null,
      pagamentoVenda2: body.pagamentoVenda2?.trim() || null,
      tipoPeriodo: body.tipoPeriodo?.trim() || '1'
    }).pipe(map((raw) => parseRespostaContasPagarPagasGrid(raw)));
  }

  contasReceberRecebidasGrid(body: ContasReceberRecebidasGridRequest): Observable<ContasReceberRecebidasGridItem[]> {
    return this.http.post<unknown>(this.urlContasReceberRecebidasGrid, {
      lojaId: body.lojaId?.trim() || null,
      statusRecebido: body.statusRecebido?.trim() || null,
      duplicataEmissao1: body.duplicataEmissao1.trim(),
      duplicataEmissao2: body.duplicataEmissao2.trim(),
      parVencimento1: body.parVencimento1?.trim() || null,
      parVencimento2: body.parVencimento2?.trim() || null,
      recRecebimento1: body.recRecebimento1?.trim() || null,
      recRecebimento2: body.recRecebimento2?.trim() || null,
      pagamentoVenda1: body.pagamentoVenda1?.trim() || null,
      pagamentoVenda2: body.pagamentoVenda2?.trim() || null,
      tipoPeriodo: body.tipoPeriodo?.trim() || '1'
    }).pipe(map((raw) => parseRespostaContasReceberRecebidasGrid(raw)));
  }

  /**
   * Snapshot persistido do comparativo financeiro; <code>null</code> se não houver (404).
   */
  getComparativoFinanceiroCache(
    anoMenor: number,
    anoMaior: number,
    lojaId?: string | null
  ): Observable<ComparativoFinanceiroCacheDto | null> {
    let params = new HttpParams()
      .set('anoMenor', String(anoMenor))
      .set('anoMaior', String(anoMaior));
    const loja = lojaId?.trim();
    if (loja) {
      params = params.set('lojaId', loja);
    }
    const headers = new HttpHeaders({
      'Cache-Control': 'no-cache',
      Pragma: 'no-cache'
    });
    return this.http
      .get<ComparativoFinanceiroCacheDto>(this.urlComparativoFinanceiroCache, { params, headers })
      .pipe(
        catchError((err: HttpErrorResponse) =>
          err.status === 404 ? of(null) : throwError(() => err)
        )
      );
  }

  putComparativoFinanceiroCache(body: {
    anoMenor: number;
    anoMaior: number;
    lojaId: string | null;
    seriePagas: unknown;
    serieRecebidas: unknown;
    formasPagas: unknown;
    formasRecebidas: unknown;
  }): Observable<void> {
    return this.http.put<void>(this.urlComparativoFinanceiroCache, {
      anoMenor: body.anoMenor,
      anoMaior: body.anoMaior,
      lojaId: body.lojaId?.trim() || null,
      seriePagas: body.seriePagas,
      serieRecebidas: body.serieRecebidas,
      formasPagas: body.formasPagas,
      formasRecebidas: body.formasRecebidas
    });
  }

  deleteComparativoFinanceiroCache(): Observable<void> {
    return this.http.delete<void>(this.urlComparativoFinanceiroCache);
  }
}
