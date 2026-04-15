using Microsoft.EntityFrameworkCore;
using PowerBi.Server.Data;
using PowerBi.Server.DTOs;
using PowerBi.Server.Entities;
using PowerBi.Server.Services.Savwin;

namespace PowerBi.Server.Services.Faturamento;

/// <summary>
/// Camada de aplicação: busca dados nas APIs SavWin e aplica regras em <see cref="FaturamentoAgregacao"/>.
/// </summary>
public sealed class FaturamentoPainelService : IFaturamentoPainelService
{
    private readonly ApplicationDbContext _db;
    private readonly ISavwinRelatoriosClient _savwin;
    private readonly ILogger<FaturamentoPainelService> _logger;

    public FaturamentoPainelService(
        ApplicationDbContext db,
        ISavwinRelatoriosClient savwin,
        ILogger<FaturamentoPainelService> logger)
    {
        _db = db;
        _savwin = savwin;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FaturamentoPainelResponse?> ObterPainelAsync(
        int gestaoClienteId,
        ProdutosPorOsClientRequest parametros,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.GestaoClientes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == gestaoClienteId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var formasReq = new VendaResumoFormasPagamentoRequest
        {
            DataPgtoInicio = parametros.DataInicial,
            DataPgtoFim = parametros.DataFinal,
            DataVendaInicio = parametros.DataInicial,
            DataVendaFim = parametros.DataFinal,
            AgrupaFormaPagamento = "S",
            LojaId = parametros.LojaId
        };

        var catalogReq = new EntradasEstoqueGridClientRequest
        {
            LojaId = "0",
            InicioSeq = "1",
            FinalSeq = "99999999"
        };

        var produtosTask = _savwin.FetchProdutosPorOsAsync(entity, parametros, cancellationToken);
        var formasTask = _savwin.FetchVendaResumoFormasPagamentoAsync(entity, formasReq, cancellationToken);
        var resumoFpTask = FetchVendaFormaPagamentoResumoOuVazioAsync(entity, parametros, cancellationToken);
        var pendentesTask = CountVendasPendentesEntregaOuZeroAsync(entity, parametros, cancellationToken);
        await Task.WhenAll(produtosTask, formasTask, resumoFpTask, pendentesTask);

        var produtos = await produtosTask;
        var formas = await formasTask;
        var resumoFp = await resumoFpTask;
        var pendentesEntrega = await pendentesTask;

        IReadOnlyList<ProdutosCadastradosGridItem> catalog;
        try
        {
            catalog = await _savwin.FetchProdutosCadastradosGridAsync(entity, catalogReq, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ProdutosCadastradosGrid indisponível; vendas por categoria usarão fallback Serviços nas armações.");
            catalog = Array.Empty<ProdutosCadastradosGridItem>();
        }

        var codigoMap = FaturamentoAgregacao.BuildCodigoParaCategoriaMap(catalog);
        var painel = FaturamentoAgregacao.Calcular(produtos, formas, codigoMap, resumoFp);
        painel.PendentesEntrega = pendentesEntrega;
        return painel;
    }

    private async Task<int> CountVendasPendentesEntregaOuZeroAsync(
        GestaoCliente entity,
        ProdutosPorOsClientRequest parametros,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _savwin.CountVendasPendentesEntregaAsync(entity, parametros, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RetornaVendasPendentesCompletas indisponível; pendentes de entrega zerados.");
            return 0;
        }
    }

    private async Task<IReadOnlyList<VendaFormaPagamentoResumoItemDto>> FetchVendaFormaPagamentoResumoOuVazioAsync(
        GestaoCliente entity,
        ProdutosPorOsClientRequest parametros,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _savwin.FetchVendaFormaPagamentoResumoAsync(entity, parametros, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "APIVendaFormaPagamentoResumo indisponível; painel sem desconto ponderado por forma.");
            return Array.Empty<VendaFormaPagamentoResumoItemDto>();
        }
    }
}
