using PowerBi.Server.DTOs;
using PowerBi.Server.Services.Faturamento;

namespace PowerBi.Server.Tests;

public class FaturamentoAgregacaoDescontoPlanoTests
{
    [Fact]
    public void Desconto_por_plano_preenche_quantidade_vendas_com_pedidos_distintos()
    {
        var resumo = new List<VendaFormaPagamentoResumoItemDto>
        {
            new()
            {
                Loja = "1",
                NumeroPedido = "12345",
                PlanoPagamento = "PIX",
                ValorBruto = "100,00",
                ValorLiquido = "95,00"
            },
            new()
            {
                Loja = "1",
                NumeroPedido = "12345",
                PlanoPagamento = "DINHEIRO",
                ValorBruto = "50,00",
                ValorLiquido = "48,00"
            },
            new()
            {
                Loja = "1",
                NumeroPedido = "999",
                PlanoPagamento = "PIX",
                ValorBruto = "200,00",
                ValorLiquido = "200,00"
            }
        };

        var painel = FaturamentoAgregacao.Calcular(
            Array.Empty<ProdutoPorOsItem>(),
            Array.Empty<VendaResumoFormaPagamentoItem>(),
            null,
            resumo);

        var linhas = painel.DescontoPorFormaPagamento;
        Assert.NotEmpty(linhas);

        var pix = linhas.First(l => l.PlanoPagamento == "PIX");
        Assert.Equal(2, pix.QuantidadeVendas);

        var dinheiro = linhas.First(l => l.PlanoPagamento == "DINHEIRO");
        Assert.Equal(1, dinheiro.QuantidadeVendas);

        var total = linhas.First(l => l.PlanoPagamento == "TOTAL");
        Assert.Equal(2, total.QuantidadeVendas);
    }

    [Fact]
    public void Desconto_venda_usa_preco_total_produto_menos_liquido_total_cruzando_loja_e_os()
    {
        var produtos = new List<ProdutoPorOsItem>
        {
            new()
            {
                LojaNome = "1",
                CodigoDaVenda = "200",
                PrecoTotalProduto = "60,00",
                ValorLiquidoTotalVenda = "90,00"
            },
            new()
            {
                LojaNome = "1",
                CodigoDaVenda = "200",
                PrecoTotalProduto = "40,00",
                ValorLiquidoTotalVenda = "90,00"
            }
        };

        var resumo = new List<VendaFormaPagamentoResumoItemDto>
        {
            new()
            {
                Loja = "1",
                NumeroPedido = "200",
                PlanoPagamento = "PIX",
                ValorBruto = "100,00",
                ValorLiquido = "100,00"
            }
        };

        var painel = FaturamentoAgregacao.Calcular(
            produtos,
            Array.Empty<VendaResumoFormaPagamentoItem>(),
            null,
            resumo);

        var pix = painel.DescontoPorFormaPagamento.First(l => l.PlanoPagamento == "PIX");
        Assert.Equal(10, pix.ValorDesconto, 2);
    }

    [Fact]
    public void Loja_resumo_apenas_digitos_bate_com_loja_nome_completo_nos_produtos()
    {
        var produtos = new List<ProdutoPorOsItem>
        {
            new()
            {
                LojaNome = "0001 - OTICA GAZETTA - MATRIZ",
                CodigoDaVenda = "76245",
                PrecoTotalProduto = "60,00",
                ValorLiquidoTotalVenda = "90,00"
            },
            new()
            {
                LojaNome = "0001 - OTICA GAZETTA - MATRIZ",
                CodigoDaVenda = "76245",
                PrecoTotalProduto = "40,00",
                ValorLiquidoTotalVenda = "0"
            }
        };

        var resumo = new List<VendaFormaPagamentoResumoItemDto>
        {
            new()
            {
                Loja = "1",
                NumeroPedido = "76245",
                PlanoPagamento = "PIX",
                ValorBruto = "100,00",
                ValorLiquido = "100,00"
            }
        };

        var painel = FaturamentoAgregacao.Calcular(
            produtos,
            Array.Empty<VendaResumoFormaPagamentoItem>(),
            null,
            resumo);

        var pix = painel.DescontoPorFormaPagamento.First(l => l.PlanoPagamento == "PIX");
        Assert.Equal(10, pix.ValorDesconto, 2);
    }

    [Fact]
    public void Venda_76245_bruto_menos_liquido_total_revela_desconto_quando_soma_preco_igual_liquido()
    {
        var produtos = new List<ProdutoPorOsItem>
        {
            new()
            {
                LojaNome = "0001 - OTICA GAZETTA - MATRIZ",
                CodigoDaVenda = "76245",
                ValorBruto = "339",
                PrecoTotalProduto = "126,8",
                ValorLiquidoTotalVenda = "415"
            },
            new()
            {
                LojaNome = "0001 - OTICA GAZETTA - MATRIZ",
                CodigoDaVenda = "76245",
                ValorBruto = "299,9",
                PrecoTotalProduto = "288,2",
                ValorLiquidoTotalVenda = "0"
            }
        };

        var resumo = new List<VendaFormaPagamentoResumoItemDto>
        {
            new()
            {
                Loja = "1",
                NumeroPedido = "76245",
                PlanoPagamento = "PIX",
                ValorBruto = "415,00",
                ValorLiquido = "415,00"
            }
        };

        var painel = FaturamentoAgregacao.Calcular(
            produtos,
            Array.Empty<VendaResumoFormaPagamentoItem>(),
            null,
            resumo);

        var pix = painel.DescontoPorFormaPagamento.First(l => l.PlanoPagamento == "PIX");
        Assert.Equal(223.9, pix.ValorDesconto, 1);
    }
}
