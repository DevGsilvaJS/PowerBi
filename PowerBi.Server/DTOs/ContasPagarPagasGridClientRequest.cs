namespace PowerBi.Server.DTOs;

/// <summary>Proxy <c>APIRelatoriosCR/ContasPagarPagasGrid</c> (SavWin).</summary>
public sealed class ContasPagarPagasGridClientRequest
{
    /// <summary>Filtra por loja; vazio = todas do cadastro (FILID como lista separada por vírgula).</summary>
    public string? LojaId { get; set; }

    /// <summary>TODOS (default), ABERTO ou BAIXADO.</summary>
    public string? StatusRecebido { get; set; }

    public string? DuplicataEmissao1 { get; set; }

    public string? DuplicataEmissao2 { get; set; }

    public string? ParVencimento1 { get; set; }

    public string? ParVencimento2 { get; set; }

    public string? RecRecebimento1 { get; set; }

    public string? RecRecebimento2 { get; set; }

    public string? PagamentoVenda1 { get; set; }

    public string? PagamentoVenda2 { get; set; }

    /// <summary>Padrão SavWin <c>1</c>.</summary>
    public string? TipoPeriodo { get; set; }
}
