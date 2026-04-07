using System.Text.Json.Serialization;

namespace PowerBi.Server.DTOs;

/// <summary>Formato JSON retornado pela SavWin (chaves em maiúsculas). Usado só na desserialização.</summary>
public class ProdutoPorOsSavwinDto
{
    [JsonPropertyName("CODIGODAVENDA")]
    [JsonConverter(typeof(SavwinStringOrNumberConverter))]
    public string? CodigoDaVenda { get; set; }

    [JsonPropertyName("LOJANOME")]
    public string? LojaNome { get; set; }

    [JsonPropertyName("QUANTIDADETOTAL")]
    public string? QuantidadeTotal { get; set; }

    [JsonPropertyName("VENDEDOR")]
    public string? Vendedor { get; set; }

    [JsonPropertyName("VENDEDOR2")]
    public string? Vendedor2 { get; set; }

    [JsonPropertyName("VENDEDOR3")]
    public string? Vendedor3 { get; set; }

    [JsonPropertyName("MEDICO")]
    public string? Medico { get; set; }

    [JsonPropertyName("TIPOVENDA")]
    public string? TipoVenda { get; set; }

    [JsonPropertyName("TIPOINDICACAO")]
    public string? TipoIndicacao { get; set; }

    [JsonPropertyName("DATAVENDA")]
    public string? DataVenda { get; set; }

    [JsonPropertyName("CLIENTE")]
    public string? Cliente { get; set; }

    [JsonPropertyName("CLIENTEPAGADOR")]
    public string? ClientePagador { get; set; }

    [JsonPropertyName("CPFCLIENTE")]
    public string? CpfCliente { get; set; }

    [JsonPropertyName("CODIGOPRODUTO")]
    public string? CodigoProduto { get; set; }

    [JsonPropertyName("FANTASIAPRODUTO")]
    public string? FantasiaProduto { get; set; }

    [JsonPropertyName("CUSTOPRODUTOS")]
    public string? CustoProdutos { get; set; }

    [JsonPropertyName("VALORBRUTO")]
    public string? ValorBruto { get; set; }

    [JsonPropertyName("DESCONTOVALORPRODUTO")]
    public string? DescontoValorProduto { get; set; }

    [JsonPropertyName("PRECOTOTALPRODUTO")]
    public string? PrecoTotalProduto { get; set; }

    [JsonPropertyName("USUARIO")]
    public string? Usuario { get; set; }

    [JsonPropertyName("GRIFE")]
    public string? Grife { get; set; }

    [JsonPropertyName("VALORLIQUIDOTOTALVENDA")]
    public string? ValorLiquidoTotalVenda { get; set; }

    [JsonPropertyName("TAXAADM")]
    public string? TaxaAdm { get; set; }

    [JsonPropertyName("DESCRICAOPRODUTO")]
    public string? DescricaoProduto { get; set; }

    [JsonPropertyName("LINHADEPRODUTO")]
    public string? LinhaDeProduto { get; set; }

    [JsonPropertyName("HORAVENDA")]
    public string? HoraVenda { get; set; }

    [JsonPropertyName("NCMCODIGO")]
    public string? NcmCodigo { get; set; }

    [JsonPropertyName("NCMDESCRICAO")]
    public string? NcmDescricao { get; set; }

    [JsonPropertyName("CODIGOBARRAS")]
    public string? CodigoBarras { get; set; }

    [JsonPropertyName("EANPRODUTO")]
    public string? EanProduto { get; set; }

    [JsonPropertyName("UPCPRODUTO")]
    public string? UpcProduto { get; set; }

    [JsonPropertyName("NUMEROCUPOMFISCAL")]
    public string? NumeroCupomFiscal { get; set; }

    [JsonPropertyName("DATAHORAEMISSAOCUPOM")]
    public string? DataHoraEmissaoCupom { get; set; }

    [JsonPropertyName("STATUSCUPOMFISCAL")]
    public string? StatusCupomFiscal { get; set; }

    [JsonPropertyName("TIPOVENDEDOR")]
    public string? TipoVendedor { get; set; }

    [JsonPropertyName("CLISEQUENCIAL")]
    public string? CliSequencial { get; set; }

    [JsonPropertyName("ULTIMOPAGAMENTO")]
    public string? UltimoPagamento { get; set; }

    /// <summary>Quando a SavWin enviar (ex.: texto da forma de pagamento).</summary>
    [JsonPropertyName("FORMAPAGAMENTO")]
    public string? FormaPagamento { get; set; }

    [JsonPropertyName("FABRICANTEPRODUTO")]
    public string? FabricanteProduto { get; set; }

    [JsonPropertyName("VENTOTALRECEBER")]
    public string? VenTotalReceber { get; set; }

    [JsonPropertyName("EHDEVOLUCAO")]
    public string? EhDevolucao { get; set; }

    [JsonPropertyName("PONTUACAO")]
    public string? Pontuacao { get; set; }

    [JsonPropertyName("DESCONTOPERCENTUALDAVENDA")]
    public string? DescontoPercentualDaVenda { get; set; }

    [JsonPropertyName("IPDTIPO")]
    public string? IpdTipo { get; set; }

    [JsonPropertyName("DATATROCA")]
    public string? DataTroca { get; set; }
}
