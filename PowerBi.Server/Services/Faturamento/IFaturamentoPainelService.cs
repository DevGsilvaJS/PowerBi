using PowerBi.Server.DTOs;

namespace PowerBi.Server.Services.Faturamento;

/// <summary>Orquestra SavWin + agregação do painel Faturamento.</summary>
public interface IFaturamentoPainelService
{
    /// <returns><c>null</c> se o cliente de gestão não existir.</returns>
    Task<FaturamentoPainelResponse?> ObterPainelAsync(
        int gestaoClienteId,
        ProdutosPorOsClientRequest parametros,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 2ª fase: aplica <c>ProdutosCadastradosGrid</c> sobre o snapshot da 1ª fase (mesmo período/lojas).
    /// Se o snapshot expirou, refaz a carga completa em uma única rodada.
    /// </summary>
    Task<FaturamentoPainelResponse?> CompletarCategoriasPainelAsync(
        int gestaoClienteId,
        ProdutosPorOsClientRequest parametros,
        CancellationToken cancellationToken = default);
}
