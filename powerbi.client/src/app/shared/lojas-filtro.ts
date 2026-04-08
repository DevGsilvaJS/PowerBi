/** Opção de loja nos filtros (somente lojas reais do cadastro). */
export interface LojaOption {
  id: string;
  nome: string;
}

/** Item de <code>APILojas/RetornaLista</code> — <code>id</code> = FILID; <code>codigo</code> = FILSEQUENTIAL (código no cadastro). */
export interface SavwinLojaItem {
  id: string;
  codigo: string;
  nome: string;
}

/** Normaliza código numérico para cruzar cadastro com a lista da API (zeros à esquerda). */
function normCodigoLoja(s: string): string {
  const t = s.trim();
  if (/^\d+$/.test(t)) {
    return String(parseInt(t, 10));
  }
  return t.toUpperCase();
}

/**
 * Cruza o cadastro do login com a lista SavWin (por <code>codigo</code> ou pelo próprio <code>id</code>);
 * o <code>id</code> de cada opção é o identificador interno da filial (<b>FILID</b> nas APIs de contas), não o código.
 */
export function combinarLojasCadastroComSavwin(
  cadastroCsv: string,
  itensSavwin: SavwinLojaItem[]
): LojaOption[] {
  const fallback = opcoesLojasDoCadastro(cadastroCsv);
  if (!itensSavwin?.length) {
    return fallback;
  }
  const codigosCad = new Set(
    cadastroCsv
      .split(',')
      .map((x) => x.trim())
      .filter((x) => x.length > 0)
      .map(normCodigoLoja)
  );
  if (codigosCad.size === 0) {
    return fallback;
  }
  const visto = new Set<string>();
  const out: LojaOption[] = [];
  for (const item of itensSavwin) {
    const idApi = (item?.id ?? '').trim();
    const cod = (item?.codigo ?? '').trim();
    if (!idApi && !cod) {
      continue;
    }
    const bateCodigo = cod.length > 0 && codigosCad.has(normCodigoLoja(cod));
    const bateIdCadastro = idApi.length > 0 && codigosCad.has(normCodigoLoja(idApi));
    if (!bateCodigo && !bateIdCadastro) {
      continue;
    }
    const filialParaApi = idApi || cod;
    if (visto.has(filialParaApi)) {
      continue;
    }
    visto.add(filialParaApi);
    const nome = item.nome?.trim() || `Loja ${cod || idApi}`;
    out.push({ id: filialParaApi, nome });
  }
  if (out.length === 0) {
    return fallback;
  }
  out.sort((a, b) => {
    const na = parseInt(a.id, 10);
    const nb = parseInt(b.id, 10);
    if (!Number.isNaN(na) && !Number.isNaN(nb)) {
      return na - nb;
    }
    return a.id.localeCompare(b.id, 'pt-BR');
  });
  return out;
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
