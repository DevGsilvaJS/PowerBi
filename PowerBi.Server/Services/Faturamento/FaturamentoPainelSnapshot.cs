using PowerBi.Server.DTOs;

namespace PowerBi.Server.Services.Faturamento;

/// <summary>Dados brutos SavWin já carregados na 1ª fase do painel — reutilizados após <c>ProdutosCadastradosGrid</c>.</summary>
internal sealed class FaturamentoPainelSnapshot
{
    public required IReadOnlyList<ProdutoPorOsItem> Produtos { get; init; }

    public required IReadOnlyList<VendaResumoFormaPagamentoItem> Formas { get; init; }

    public required IReadOnlyList<VendaFormaPagamentoResumoItemDto> ResumoFp { get; init; }

    public int PendentesEntrega { get; init; }
}
