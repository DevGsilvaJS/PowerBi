using PowerBi.Server.DTOs;
using PowerBi.Server.Entities;

namespace PowerBi.Server.Services.Savwin;

/// <summary>
/// Acesso HTTP às APIs SavWin de relatórios (sem regra de negócio de faturamento).
/// </summary>
public interface ISavwinRelatoriosClient
{
    Task<IReadOnlyList<ProdutoPorOsItem>> FetchProdutosPorOsAsync(
        GestaoCliente cliente,
        ProdutosPorOsClientRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VendaResumoFormaPagamentoItem>> FetchVendaResumoFormasPagamentoAsync(
        GestaoCliente cliente,
        VendaResumoFormasPagamentoRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>POST APIVendaFormaPagamentoResumo — vendas por parcela/meio (datas de venda yyyy-MM-dd).</summary>
    Task<IReadOnlyList<VendaFormaPagamentoResumoItemDto>> FetchVendaFormaPagamentoResumoAsync(
        GestaoCliente cliente,
        ProdutosPorOsClientRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>POST EntradasEstoqueGrid — retorno JSON bruto da SavWin.</summary>
    Task<string> FetchEntradasEstoqueGridRawJsonAsync(
        GestaoCliente cliente,
        EntradasEstoqueGridClientRequest request,
        CancellationToken cancellationToken = default);

    Task<string> FetchContasPagarPagasGridRawJsonAsync(
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>POST ContasReceberRecebidasGrid — mesmo contrato que o proxy de contas a pagar; <c>FILID</c> enviado à SavWin é o <b>código</b> da loja.</summary>
    Task<string> FetchContasReceberRecebidasGridRawJsonAsync(
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>POST ProdutosCadastradosGrid — body: CODIGOLOJA, INICIOSEQ, FINALSEQ (como EntradasEstoqueGrid).</summary>
    Task<IReadOnlyList<ProdutosCadastradosGridItem>> FetchProdutosCadastradosGridAsync(
        GestaoCliente cliente,
        EntradasEstoqueGridClientRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>POST APILojas/RetornaLista (corpo vazio) — <c>Id</c> para grid de contas a <b>pagar</b>; <c>Codigo</c> (FILSEQUENTIAL) para <c>FILID</c> na grid de contas a <b>receber</b>.</summary>
    Task<IReadOnlyList<SavwinLojaListaItemDto>> FetchListaLojasAsync(
        GestaoCliente cliente,
        CancellationToken cancellationToken = default);

    /// <summary>POST APIDemonstrativoVendaPorCliente — <c>DATAINICIO</c>/<c>DATAFIM</c> em dd/MM/yyyy e <c>LOJA</c>.</summary>
    Task<IReadOnlyList<DemonstrativoVendaPorClienteSavwinDto>> FetchDemonstrativoVendaPorClienteAsync(
        GestaoCliente cliente,
        ProdutosPorOsClientRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>POST APIClientes/RetornaLista — corpo vazio (<c>{{}}</c>).</summary>
    Task<IReadOnlyList<ClienteRetornaListaSavwinDto>> FetchClientesRetornaListaAsync(
        GestaoCliente cliente,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST APIDados/RetornaVendasPendentesCompletas — soma contagens por <c>CODIGOLOJA</c> no intervalo (dd/MM/yyyy).
    /// </summary>
    Task<int> CountVendasPendentesEntregaAsync(
        GestaoCliente cliente,
        ProdutosPorOsClientRequest request,
        CancellationToken cancellationToken = default);
}
