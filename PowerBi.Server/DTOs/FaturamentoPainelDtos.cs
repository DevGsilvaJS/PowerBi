using System.Text.Json.Serialization;

namespace PowerBi.Server.DTOs;

/// <summary>Resposta agregada do painel Faturamento (KPIs, material, grife, formas).</summary>
public class FaturamentoPainelResponse
{
    public Dictionary<string, KpiDadoDto> KpiDados { get; set; } = new();
    public double TotalPagamentoResumo { get; set; }
    public List<FormaPagamentoLinhaDto> FormasPagamento { get; set; } = new();
    public List<VendaMaterialLinhaDto> VendasPorMaterialLinhas { get; set; } = new();
    public List<VendaGrifeSubgrupoDto> VendasPorGrifeSubgrupos { get; set; } = new();
    /// <summary>Vendas por categoria: valor líquido e quantidade de vendas (O.S.) distintas por modalidade.</summary>
    public List<VendaTipoProdutoLinhaDto> VendasPorTipoProdutoLinhas { get; set; } = new();

    /// <summary>
    /// Cards por família de produto na O.S.: soma do líquido alocado por linha (1/2/3) e soma de <c>QUANTIDADETOTAL</c> por linha na família.
    /// </summary>
    public List<FaturamentoFamiliaProdutoCardDto> VendasFamiliaProdutoCards { get; set; } = new();

    /// <summary>
    /// Desconto ponderado por plano (desconto da venda rateado por <c>ValorBruto</c> quando há vários meios no mesmo pedido).
    /// </summary>
    public List<DescontoFormaPagamentoLinhaDto> DescontoPorFormaPagamento { get; set; } = new();

    /// <summary>Total de vendas pendentes de entrega (<c>RetornaVendasPendentesCompletas</c>) no período e lojas filtradas.</summary>
    public int PendentesEntrega { get; set; }
}

/// <summary>Linha de <c>APIVendaFormaPagamentoResumo</c> (espelha JSON SavWin).</summary>
public class VendaFormaPagamentoResumoItemDto
{
    public string? Id { get; set; }
    public string? PlanoPagamento { get; set; }
    public string? BandeiraCartao { get; set; }
    public string? MeioPagamento { get; set; }
    public string? NumeroParcelas { get; set; }
    public string? ValorBruto { get; set; }
    public string? ValorLiquido { get; set; }
    public string? ValorTaxaAntecipacao { get; set; }
    public string? PercentualTaxa { get; set; }
    public string? Loja { get; set; }
    public string? Vendedor { get; set; }
    public string? NumeroPedido { get; set; }

    /// <summary>Data de pagamento da parcela (quando a SavWin envia).</summary>
    [JsonPropertyName("DATAPAGAMENTO")]
    public string? DataPagamento { get; set; }
}

/// <summary>Uma linha agregada por <c>PlanoPagamento</c> com desconto rateado.</summary>
public class DescontoFormaPagamentoLinhaDto
{
    public string PlanoPagamento { get; set; } = string.Empty;
    /// <summary>Soma do campo <c>ValorBruto</c> do resumo SavWin por plano (não é bruto − líquido).</summary>
    public double ValorBruto { get; set; }
    /// <summary>Soma do desconto rateado por linha de forma de pagamento (proporcional ao bruto no pedido).</summary>
    public double ValorDesconto { get; set; }
    /// <summary>
    /// Quantidade de <c>NumeroPedido</c> distintos (agrupamento no resumo SavWin) que usaram aquele plano; linha TOTAL = pedidos distintos no período.
    /// </summary>
    [JsonPropertyName("quantidadeVendas")]
    public int QuantidadeVendas { get; set; }
    /// <summary><c>SUM(ValorDesconto) / SUM(ValorBruto) × 100</c>.</summary>
    public double DescontoPonderadoPercentual { get; set; }
}

/// <summary>Solares / receituários (armações), lentes ou serviços — por linha de produto na O.S.</summary>
public class FaturamentoFamiliaProdutoCardDto
{
    public string Id { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public double Valor { get; set; }
    /// <summary>Soma das quantidades de produto (<c>QUANTIDADETOTAL</c>) nas linhas da família.</summary>
    public int QuantidadeProdutos { get; set; }
}

public class VendaTipoProdutoLinhaDto
{
    public string Label { get; set; } = string.Empty;
    /// <summary>Soma do líquido alocado das linhas da categoria (faturamento na categoria).</summary>
    public double Valor { get; set; }
    /// <summary>Número de vendas (O.S.) distintas classificadas nesta modalidade.</summary>
    public int QuantidadeVendas { get; set; }
    /// <summary>Vendas (loja + código da venda) que entraram nesta categoria.</summary>
    [JsonPropertyName("vendas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public List<VendaPainelIdentificacaoDto> Vendas { get; set; } = new();

    /// <summary>
    /// Chaves internas de agrupamento (<c>loja</c> + separador + <c>codigoDaVenda</c> ou <c>__sem_os__</c>).
    /// Redundante com <see cref="Vendas"/> para garantir transporte em JSON mesmo se objetos aninhados falharem no cliente.
    /// </summary>
    [JsonPropertyName("vendaChavesInternas")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public List<string> VendaChavesInternas { get; set; } = new();
}

/// <summary>Identificação mínima de uma venda no painel (espelha chave de agrupamento loja + O.S.).</summary>
public class VendaPainelIdentificacaoDto
{
    [JsonPropertyName("lojaNome")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? LojaNome { get; set; }

    [JsonPropertyName("codigoDaVenda")]
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? CodigoDaVenda { get; set; }
}

public class KpiDadoDto
{
    public double Valor { get; set; }
    public double Percentual { get; set; }
    public List<double> Bars { get; set; } = new();
}

public class FormaPagamentoLinhaDto
{
    public string MeioPagamento { get; set; } = string.Empty;
    public double Valor { get; set; }
}

public class VendaMaterialLinhaDto
{
    public string Material { get; set; } = string.Empty;
    public double Bruto { get; set; }
    public double Liquido { get; set; }
    public double Quantidade { get; set; }
    public double Percentual { get; set; }
}

public class VendaGrifeSubgrupoDto
{
    public string Titulo { get; set; } = string.Empty;
    public List<VendaGrifeLinhaDto> Linhas { get; set; } = new();
}

public class VendaGrifeLinhaDto
{
    public string Grife { get; set; } = string.Empty;
    public double Bruto { get; set; }
    public double Liquido { get; set; }
    public double Quantidade { get; set; }
    public double Percentual { get; set; }
}
