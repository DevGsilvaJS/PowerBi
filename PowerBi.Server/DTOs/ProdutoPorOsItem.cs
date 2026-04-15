namespace PowerBi.Server.DTOs;

/// <summary>Item enviado ao cliente Angular (camelCase via System.Text.Json padrão).</summary>
public class ProdutoPorOsItem
{
    public string? CodigoDaVenda { get; set; }
    public string? LojaNome { get; set; }
    public string? QuantidadeTotal { get; set; }
    public string? Vendedor { get; set; }
    public string? Vendedor2 { get; set; }
    public string? Vendedor3 { get; set; }
    public string? Medico { get; set; }
    public string? TipoVenda { get; set; }
    public string? TipoIndicacao { get; set; }
    public string? DataVenda { get; set; }
    public string? Cliente { get; set; }
    public string? ClientePagador { get; set; }
    public string? CpfCliente { get; set; }
    public string? CodigoProduto { get; set; }
    public string? FantasiaProduto { get; set; }
    public string? CustoProdutos { get; set; }
    public string? ValorBruto { get; set; }
    public string? DescontoValorProduto { get; set; }
    public string? PrecoTotalProduto { get; set; }
    public string? Usuario { get; set; }
    public string? Grife { get; set; }
    public string? ValorLiquidoTotalVenda { get; set; }
    public string? TaxaAdm { get; set; }
    public string? DescricaoProduto { get; set; }
    public string? LinhaDeProduto { get; set; }
    public string? HoraVenda { get; set; }
    public string? NcmCodigo { get; set; }
    public string? NcmDescricao { get; set; }
    public string? CodigoBarras { get; set; }
    public string? EanProduto { get; set; }
    public string? UpcProduto { get; set; }
    public string? NumeroCupomFiscal { get; set; }
    public string? DataHoraEmissaoCupom { get; set; }
    public string? StatusCupomFiscal { get; set; }
    public string? TipoVendedor { get; set; }
    public string? CliSequencial { get; set; }
    public string? UltimoPagamento { get; set; }
    public string? FormaPagamento { get; set; }
    public string? FabricanteProduto { get; set; }
    public string? VenTotalReceber { get; set; }
    public string? EhDevolucao { get; set; }
    public string? Pontuacao { get; set; }
    public string? DescontoPercentualDaVenda { get; set; }
    public string? IpdTipo { get; set; }
    public string? DataTroca { get; set; }

    /// <summary>Bairro associado à venda (SavWin <c>BAIRRO</c>), quando disponível.</summary>
    public string? Bairro { get; set; }
}
