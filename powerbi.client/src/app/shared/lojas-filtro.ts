/** Opção de loja nos filtros (somente lojas reais do cadastro). */
export interface LojaOption {
  id: string;
  nome: string;
  /** Código de loja (FILSEQUENTIAL) para ordenar a lista (0001, 0002…), não o FILID. */
  codigoOrdem?: string;
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
 * Código de loja para rótulos (ex.: <code>1</code> → <code>0001</code>).
 * Só alinha zeros quando o código é inteiramente numérico; caso contrário devolve o texto trimado.
 */
export function formatarCodigoLojaExibicao(codigo: string): string {
  const t = codigo.trim();
  if (t.length === 0) {
    return '';
  }
  if (/^\d+$/.test(t)) {
    const w = Math.max(4, t.length);
    return t.padStart(w, '0');
  }
  return t;
}

function rotuloLojaCodigoENome(codigo: string, nomeFantasia: string): string {
  const codFmt = formatarCodigoLojaExibicao(codigo);
  const nome = nomeFantasia.trim();
  if (!codFmt) {
    return nome || 'Loja';
  }
  return nome.length > 0 ? `${codFmt} - ${nome}` : codFmt;
}

/** Ordenação por código de loja (numérico quando só dígitos; senão <code>localeCompare</code> com <code>numeric</code>). */
export function compararCodigoLojaOrdem(a: string, b: string): number {
  const ta = a.trim();
  const tb = b.trim();
  if (/^\d+$/.test(ta) && /^\d+$/.test(tb)) {
    return parseInt(ta, 10) - parseInt(tb, 10);
  }
  return ta.localeCompare(tb, 'pt-BR', { numeric: true, sensitivity: 'base' });
}

/**
 * Cruza o cadastro do login com a lista SavWin só por <code>codigo</code> (FILSEQUENTIAL).
 * Não cruza por <code>id</code> (FILID): o mesmo número pode ser FILID de uma filial e código de outra,
 * o que duplicava opções no filtro (ex.: FRANQUEADORA com FILID=1 e NILO com código=1).
 * O <code>id</code> da opção continua sendo o FILID (<code>idApi || cod</code>) para as chamadas à API.
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
    if (!bateCodigo) {
      continue;
    }
    const filialParaApi = idApi || cod;
    if (visto.has(filialParaApi)) {
      continue;
    }
    visto.add(filialParaApi);
    const nome = rotuloLojaCodigoENome(cod, item.nome?.trim() || '');
    out.push({ id: filialParaApi, nome, codigoOrdem: cod });
  }
  if (out.length === 0) {
    return fallback;
  }
  out.sort((a, b) =>
    compararCodigoLojaOrdem(a.codigoOrdem ?? a.id, b.codigoOrdem ?? b.id)
  );
  return out;
}

/** Monta opções a partir da string do cadastro (ex.: <code>1,2,3</code>). */
export function opcoesLojasDoCadastro(cadastroCsv: string): LojaOption[] {
  const ids = cadastroCsv
    .split(',')
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
  ids.sort((x, y) => compararCodigoLojaOrdem(x, y));
  return ids.map((id) => ({
    id,
    nome: rotuloLojaCodigoENome(id, 'Loja'),
    codigoOrdem: id
  }));
}

/**
 * FILIDs separados por vírgula, ordenados pelo <strong>código</strong> da loja (não pela ordem do clique).
 * <code>null</code> = nenhuma loja válida selecionada.
 */
function lojaIdsOrdenadosPorCodigo(lojas: LojaOption[], idsSelecionados: Set<string>): string | null {
  const chosen = lojas.filter((l) => idsSelecionados.has(l.id));
  if (chosen.length === 0) {
    return null;
  }
  chosen.sort((a, b) =>
    compararCodigoLojaOrdem(a.codigoOrdem ?? a.id, b.codigoOrdem ?? b.id)
  );
  return chosen.map((l) => l.id).join(',');
}

/**
 * Parâmetro <code>lojaId</code> / LOJAS na API: string com FILIDs separados por vírgula (todas ou subconjunto),
 * sempre na ordem do código de loja. <code>null</code> se não houver seleção válida.
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
  const setSel = new Set(sel.filter((id) => setAll.has(id)));
  if (setSel.size === 0) {
    return null;
  }
  return lojaIdsOrdenadosPorCodigo(lojas, setSel);
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

/**
 * Lista de IDs para chamadas que fazem uma requisição por loja (ex.: estoque).
 * Quando <strong>todas</strong> as lojas estão marcadas, devolve <code>null</code> (uma chamada com <code>lojaId</code> vazio no servidor).
 */
export function lojaIdsParaListaChamadasIndividuais(
  lojas: LojaOption[],
  selecionadas: string[]
): string[] | null {
  if (lojas.length === 0) {
    return null;
  }
  const setAll = new Set(lojas.map((l) => l.id));
  const sel = [...new Set(selecionadas.map((s) => s.trim()).filter((s) => s.length > 0))];
  if (sel.length === 0) {
    return null;
  }
  const setSel = new Set(sel.filter((id) => setAll.has(id)));
  if (setSel.size === 0) {
    return null;
  }
  const todasMarcadas = setSel.size === setAll.size && [...setAll].every((id) => setSel.has(id));
  if (todasMarcadas) {
    return null;
  }
  const param = lojaIdsParaParametroApi(lojas, selecionadas);
  if (param === null) {
    return null;
  }
  return param.split(',').filter(Boolean);
}
