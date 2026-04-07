namespace PowerBi.Server.DTOs;

/// <summary>Item enviado ao Angular (camelCase).</summary>
public class VendaResumoFormaPagamentoItem
{
    public string? Loja { get; set; }
    public string? MeioPagamento { get; set; }
    public string? BandeiraCartao { get; set; }
    public string? FormaPagamento { get; set; }
    public string? NParcelas { get; set; }
    public string? QtdeUso { get; set; }
    public string? VendasValor { get; set; }
    public string? TaxaAdmPerc { get; set; }
    public string? ValorSTaxaAdm { get; set; }
}
