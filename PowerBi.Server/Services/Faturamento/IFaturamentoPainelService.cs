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
}
