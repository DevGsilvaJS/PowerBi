namespace PowerBi.Server.DTOs;

/// <summary>Parâmetros para APIVendaResumoTodasFormasPagamento (datas em yyyy-MM-dd na tela).</summary>
public class VendaResumoFormasPagamentoRequest
{
    public string DataPgtoInicio { get; set; } = string.Empty;
    public string DataPgtoFim { get; set; } = string.Empty;
    public string? DataVendaInicio { get; set; }
    public string? DataVendaFim { get; set; }
    /// <summary>Padrão SavWin: N ou S (padrão no proxy: S).</summary>
    public string? AgrupaFormaPagamento { get; set; }
    public string? LojaId { get; set; }
}
