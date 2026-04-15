/** Resposta de <code>POST /api/Relatorios/vendas-por-bairro</code> (camelCase). */
export interface VendaPorBairroItem {
  bairro: string;
  cidade?: string | null;
  valorLiquido: number;
  qtdVendasDistintas: number;
  lat?: number | null;
  lon?: number | null;
}
