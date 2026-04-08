namespace PowerBi.Server.DTOs;

/// <summary>Proxy <c>APIRelatoriosCR/ContasPagarPagasGrid</c> (SavWin).</summary>
/// <remarks>
/// Contrato típico (JSON enviado à SavWin): <c>FILID</c>, <c>STATUSRECEBIDO</c>,
/// <c>DUPEMISSAO1/2</c> e demais filtros opcionais nulos, <c>PAGAMENTOVENDA1/2</c> em dd/MM/yyyy quando o período for por pagamento,
/// <c>TIPOPERIODO</c> (ex.: <c>1</c>).
/// </remarks>
public sealed class ContasPagarPagasGridClientRequest
{
    /// <summary>
    /// Código(s) ou id(s) de loja (vírgula); vazio = cadastro do cliente.
    /// O servidor chama <c>RetornaLista</c> e envia à SavWin o <b>Id</b> interno da filial em <c>FILID</c>.
    /// </summary>
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
