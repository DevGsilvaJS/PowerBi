using System.Text.Json.Serialization;

namespace PowerBi.Server.DTOs;

/// <summary>Linha retornada pela SavWin (APIVendaResumoTodasFormasPagamento).</summary>
public class VendaResumoFormasPagamentoSavwinDto
{
    [JsonPropertyName("LOJA")]
    public string? Loja { get; set; }

    [JsonPropertyName("MEIO_PAGAMENTO")]
    public string? MeioPagamento { get; set; }

    [JsonPropertyName("BANDEIRA_CARTAO")]
    public string? BandeiraCartao { get; set; }

    [JsonPropertyName("FORMA_PAGAMENTO")]
    public string? FormaPagamento { get; set; }

    [JsonPropertyName("N_PARCELAS")]
    public string? NParcelas { get; set; }

    [JsonPropertyName("QTDE_USO")]
    public string? QtdeUso { get; set; }

    [JsonPropertyName("VENDAS_VALOR")]
    public string? VendasValor { get; set; }

    [JsonPropertyName("TAXA_ADM_PERC")]
    public string? TaxaAdmPerc { get; set; }

    [JsonPropertyName("VALOR_S_TAXA_ADM")]
    public string? ValorSTaxaAdm { get; set; }
}
