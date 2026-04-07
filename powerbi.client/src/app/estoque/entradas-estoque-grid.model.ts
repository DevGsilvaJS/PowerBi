/** Corpo enviado ao proxy (camelCase); SavWin recebe só CODIGOLOJA, INICIOSEQ, FINALSEQ. */
export interface EntradasEstoqueGridRequest {
  lojaId?: string | null;
  inicioSeq?: string | null;
  finalSeq?: string | null;
}

/**
 * Linha retornada por <code>POST /api/Relatorios/entradas-estoque-grid</code> (SavWin EntradasEstoqueGrid).
 * Chaves normalizadas em MAIÚSCULAS após o parse (a API pode enviar outro casing).
 */
export interface EntradasEstoqueGridItem {
  /** ID interno do sistema para o produto */
  MATID?: string;
  /** Sequencial do produto */
  CODIGO?: string;
  /** Sequencial e fantasia do produto */
  PRODUTO?: string;
  /** Descrição do produto */
  DESCRICAO?: string;
  /** Tipo de produto no cadastro de produtos */
  TIPOPRODUTO?: string;
  /** Grife cadastrada no produto */
  GRIFE?: string;
  /** Linha cadastrada no produto */
  LINHA?: string;
  /** Modelo (grife cadastrada no produto — nomenclatura SavWin) */
  MODELO?: string;
  /** Cor numérica cadastrada no produto */
  CORNUMERICA?: string;
  /** Cor cadastrada no produto */
  COR?: string;
  /** Tamanho cadastrado no produto */
  TAMANHO?: string;
  /** Sub-linha 1 cadastrada no produto */
  SUBLINHA1?: string;
  /** Sub-linha 2 cadastrada no produto */
  SUBLINHA2?: string;
  /** Data do cadastro do produto */
  DATACADASTRO?: string;
  /** “PAI” se for lente pai (apenas lentes) */
  PAI?: string;
  /** “ITEM NAO VENDIDO” / “ITEM VENDIDO” conforme cadastro */
  ITEMVENDIDO?: string;
  /** “FORA DE LINHA” / “EM LINHA” conforme cadastro */
  EMLINHA?: string;
  /** Fabricante (cadastro de lentes) */
  FABRICANTE?: string;
  /** Descrição do NCM cadastrado no produto */
  NCM?: string;
  /** Família (cadastro de lentes) */
  FAMILIA?: string;
  /** Markup da primeira movimentação do produto */
  MARKUP?: string;
  /** Preço de custo cadastrado no produto */
  PRECODECUSTO?: string;
  /** Preço de venda cadastrado no produto */
  PRECODEVENDA?: string;
  /** Pontuação (cashback) */
  PONTUACAO?: string;
  /** “SIM” / “NAO” — imagem cadastrada */
  IMAGEM?: string;
  /** “SIM” / “NAO” — marcado como brinde */
  BRINDE?: string;
  /** Unidade de medida para compra */
  UNIDADECOMPRA?: string;
  /** “SIM” / “NAO” — aceita estoque negativo */
  ESTOQUENEGATIVO?: string;
  /** “SIM” / “NÃO” — loja virtual */
  VENDAWEB?: string;
  /** Fornecedor na entrada (pode diferir do cadastro do produto) */
  FORNECEDOR?: string;
  /** Tipo quando produto é “OUTROS” */
  TIPOOUTROS?: string;
  /** “PAI” / “FILHO” — lentes */
  FAMILIA_FILIACAO?: string;
  /** Ponte (armação) */
  PONTE?: string;
  /** Haste (armação) */
  HASTE?: string;
}

/** Ordem das colunas + rótulo curto + descrição (tooltip, glossário SavWin). */
export const COLUNAS_ENTRADA_ESTOQUE: ReadonlyArray<{
  key: keyof EntradasEstoqueGridItem;
  titulo: string;
  dica: string;
}> = [
  { key: 'MATID', titulo: 'MAT ID', dica: 'ID interno do sistema para o produto' },
  { key: 'CODIGO', titulo: 'Código', dica: 'Sequencial do produto' },
  { key: 'PRODUTO', titulo: 'Produto', dica: 'Sequencial e fantasia do produto' },
  { key: 'DESCRICAO', titulo: 'Descrição', dica: 'Descrição do produto' },
  { key: 'TIPOPRODUTO', titulo: 'Tipo', dica: 'Tipo de produto no cadastro de produtos' },
  { key: 'GRIFE', titulo: 'Grife', dica: 'Grife cadastrada no produto' },
  { key: 'LINHA', titulo: 'Linha', dica: 'Linha cadastrada no produto' },
  { key: 'MODELO', titulo: 'Modelo', dica: 'Grife cadastrada no produto (campo MODELO na API)' },
  { key: 'CORNUMERICA', titulo: 'Cor num.', dica: 'Cor numérica cadastrada no produto' },
  { key: 'COR', titulo: 'Cor', dica: 'Cor cadastrada no produto' },
  { key: 'TAMANHO', titulo: 'Tam.', dica: 'Tamanho cadastrado no produto' },
  { key: 'SUBLINHA1', titulo: 'Sub-linha 1', dica: 'Sub-linha 1 cadastrada no produto' },
  { key: 'SUBLINHA2', titulo: 'Sub-linha 2', dica: 'Sub-linha 2 cadastrada no produto' },
  { key: 'DATACADASTRO', titulo: 'Dt. cadastro', dica: 'Data do cadastro do produto' },
  { key: 'PAI', titulo: 'Pai', dica: '“PAI” se for lente pai (apenas lentes)' },
  { key: 'ITEMVENDIDO', titulo: 'Item vendido', dica: '“ITEM NAO VENDIDO” / “ITEM VENDIDO”' },
  { key: 'EMLINHA', titulo: 'Em linha', dica: '“FORA DE LINHA” / “EM LINHA”' },
  { key: 'FABRICANTE', titulo: 'Fabricante', dica: 'Fabricante no cadastro de lentes' },
  { key: 'NCM', titulo: 'NCM', dica: 'Descrição do NCM cadastrado no produto' },
  { key: 'FAMILIA', titulo: 'Família', dica: 'Família no cadastro de lentes' },
  { key: 'MARKUP', titulo: 'Markup', dica: 'Markup da primeira movimentação do produto' },
  { key: 'PRECODECUSTO', titulo: 'P. custo', dica: 'Preço de custo cadastrado no produto' },
  { key: 'PRECODEVENDA', titulo: 'P. venda', dica: 'Preço de venda cadastrado no produto' },
  { key: 'PONTUACAO', titulo: 'Pontuação', dica: 'Pontuação (cashback)' },
  { key: 'IMAGEM', titulo: 'Imagem', dica: '“SIM” / “NAO” — imagem cadastrada' },
  { key: 'BRINDE', titulo: 'Brinde', dica: '“SIM” / “NAO” — marcado como brinde' },
  { key: 'UNIDADECOMPRA', titulo: 'Un. compra', dica: 'Unidade de medida para compra' },
  { key: 'ESTOQUENEGATIVO', titulo: 'Est. neg.', dica: '“SIM” / “NAO” — aceita estoque negativo' },
  { key: 'VENDAWEB', titulo: 'Venda web', dica: '“SIM” / “NÃO” — uso em loja virtual' },
  { key: 'FORNECEDOR', titulo: 'Fornecedor', dica: 'Fornecedor na entrada (pode diferir do cadastro)' },
  { key: 'TIPOOUTROS', titulo: 'Tipo outros', dica: 'Tipo quando o produto é “OUTROS”' },
  { key: 'FAMILIA_FILIACAO', titulo: 'Fam. filiação', dica: '“PAI” / “FILHO” (lentes)' },
  { key: 'PONTE', titulo: 'Ponte', dica: 'Ponte em armação' },
  { key: 'HASTE', titulo: 'Haste', dica: 'Haste em armação' }
];

export function normalizarLinhaEntradaEstoque(row: Record<string, unknown>): EntradasEstoqueGridItem {
  const out: Record<string, string> = {};
  for (const [k, v] of Object.entries(row)) {
    if (k == null) {
      continue;
    }
    const key = k.toUpperCase();
    out[key] = v == null || v === '' ? '' : String(v);
  }
  return out as EntradasEstoqueGridItem;
}

export function parseRespostaEntradasEstoqueGrid(raw: unknown): EntradasEstoqueGridItem[] {
  if (Array.isArray(raw)) {
    return raw.map((r) =>
      r && typeof r === 'object' ? normalizarLinhaEntradaEstoque(r as Record<string, unknown>) : {}
    ) as EntradasEstoqueGridItem[];
  }
  if (raw && typeof raw === 'object') {
    return [normalizarLinhaEntradaEstoque(raw as Record<string, unknown>)];
  }
  return [];
}
