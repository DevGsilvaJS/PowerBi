using PowerBi.Server.DTOs;

namespace PowerBi.Server.Services.Faturamento;

/// <summary>Orquestra SavWin + agregação do painel Faturamento.</summary>
public interface IFaturamentoPainelService
{
    /// <summary>
    /// Carrega vendas, formas, resumo, pendentes e catálogo (<c>ProdutosCadastradosGrid</c>) em paralelo e devolve o painel já com categorias refinadas.
    /// </summary>
    /// <returns><c>null</c> se o cliente de gestão não existir.</returns>
    Task<FaturamentoPainelResponse?> ObterPainelAsync(
        int gestaoClienteId,
        ProdutosPorOsClientRequest parametros,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opcional: reaplica o catálogo sobre o snapshot em memória (mesmo período/lojas) quando o front ainda chama esta rota.
    /// Se o snapshot expirou, refaz a carga completa (paralela, como <see cref="ObterPainelAsync"/>).
    /// </summary>
    Task<FaturamentoPainelResponse?> CompletarCategoriasPainelAsync(
        int gestaoClienteId,
        ProdutosPorOsClientRequest parametros,
        CancellationToken cancellationToken = default);
}
