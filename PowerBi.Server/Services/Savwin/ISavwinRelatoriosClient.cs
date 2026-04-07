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

    /// <summary>POST EntradasEstoqueGrid — retorno JSON bruto da SavWin.</summary>
    Task<string> FetchEntradasEstoqueGridRawJsonAsync(
        GestaoCliente cliente,
        EntradasEstoqueGridClientRequest request,
        CancellationToken cancellationToken = default);

    Task<string> FetchContasPagarPagasGridRawJsonAsync(
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>POST ContasReceberRecebidasGrid — mesmo corpo que ContasPagarPagasGrid.</summary>
    Task<string> FetchContasReceberRecebidasGridRawJsonAsync(
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>POST ProdutosCadastradosGrid — body: CODIGOLOJA, INICIOSEQ, FINALSEQ (como EntradasEstoqueGrid).</summary>
    Task<IReadOnlyList<ProdutosCadastradosGridItem>> FetchProdutosCadastradosGridAsync(
        GestaoCliente cliente,
        EntradasEstoqueGridClientRequest request,
        CancellationToken cancellationToken = default);
}
