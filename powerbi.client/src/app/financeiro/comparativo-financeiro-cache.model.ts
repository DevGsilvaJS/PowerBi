import { ComparativoMensalPonto } from '../relatorios/produto-por-os-vendas-mensal.util';
import { FormaPagamentoComparativoLinha } from './financeiro-formas-comparativo.util';

/** Resposta de GET <code>/api/Relatorios/comparativo-financeiro-cache</code>. */
export interface ComparativoFinanceiroCacheDto {
  anoMenor: number;
  anoMaior: number;
  lojaId: string | null;
  seriePagas: ComparativoMensalPonto[];
  serieRecebidas: ComparativoMensalPonto[];
  formasPagas: FormaPagamentoComparativoLinha[];
  formasRecebidas: FormaPagamentoComparativoLinha[];
  ultimaConsultaUtc: string | null;
}
