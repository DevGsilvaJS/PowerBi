/** Opção de loja nos filtros (somente lojas reais do cadastro). */
export interface LojaOption {
  id: string;
  nome: string;
}

/** Monta opções a partir da string do cadastro (ex.: <code>1,2,3</code>). */
export function opcoesLojasDoCadastro(cadastroCsv: string): LojaOption[] {
  const ids = cadastroCsv
    .split(',')
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
  return ids.map((id) => ({ id, nome: `Loja ${id}` }));
}

/**
 * Parâmetro <code>lojaId</code> / FILID / LOJAS: <code>null</code> = todas as lojas do cliente na API;
 * string com IDs separados por vírgula = subconjunto.
 */
export function lojaIdsParaParametroApi(lojas: LojaOption[], selecionadas: string[]): string | null {
  if (lojas.length === 0) {
    return null;
  }
  const setAll = new Set(lojas.map((l) => l.id));
  const sel = [...new Set(selecionadas.map((s) => s.trim()).filter((s) => s.length > 0))];
  if (sel.length === 0) {
    return null;
  }
  if (sel.length === setAll.size && sel.every((id) => setAll.has(id))) {
    return null;
  }
  return sel.sort().join(',');
}

/**
 * Com mais de uma loja no cadastro, se o usuário marcar <strong>todas</strong>,
 * o parâmetro vira “todas na API”. Use para exigir escolha explícita de uma loja.
 */
export function mensagemSelecioneApenasUmaLojaSeTodasMarcadas(
  lojas: LojaOption[],
  selecionadas: string[]
): string | null {
  if (lojas.length <= 1) {
    return null;
  }
  const setAll = new Set(lojas.map((l) => l.id));
  const sel = [...new Set(selecionadas.map((s) => s.trim()).filter((s) => s.length > 0))];
  if (sel.length === 0) {
    return null;
  }
  const todasMarcadas = sel.length === setAll.size && sel.every((id) => setAll.has(id));
  if (!todasMarcadas) {
    return null;
  }
  return 'Selecione apenas uma loja (não use a opção "Todas as lojas").';
}

/** Lista de IDs para chamadas que aceitam só uma loja por requisição (ex.: estoque). */
export function lojaIdsParaListaChamadasIndividuais(
  lojas: LojaOption[],
  selecionadas: string[]
): string[] | null {
  if (lojas.length === 0) {
    return null;
  }
  const param = lojaIdsParaParametroApi(lojas, selecionadas);
  if (param === null) {
    return null;
  }
  return param.split(',').filter(Boolean);
}
