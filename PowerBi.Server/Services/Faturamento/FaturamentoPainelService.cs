using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PowerBi.Server.Data;
using PowerBi.Server.DTOs;
using PowerBi.Server.Entities;
using PowerBi.Server.Services.Savwin;

namespace PowerBi.Server.Services.Faturamento;

/// <summary>
/// Camada de aplicação: busca dados nas APIs SavWin e aplica regras em <see cref="FaturamentoAgregacao"/>.
/// O painel dispara <c>ProdutosPorOS</c>, formas, resumo, pendentes e <c>ProdutosCadastradosGrid</c> em paralelo.
/// </summary>
public sealed class FaturamentoPainelService : IFaturamentoPainelService
{
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromMinutes(10);

    /// <summary>Body SavWin <c>ProdutosCadastradosGrid</c> (CODIGOLOJA + faixa de sequência).</summary>
    private static readonly EntradasEstoqueGridClientRequest CatalogoProdutosCadastradosRequest = new()
    {
        LojaId = "1",
        InicioSeq = "1",
        FinalSeq = "19999999"
    };

    private readonly ApplicationDbContext _db;
    private readonly ISavwinRelatoriosClient _savwin;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FaturamentoPainelService> _logger;

    public FaturamentoPainelService(
        ApplicationDbContext db,
        ISavwinRelatoriosClient savwin,
        IMemoryCache cache,
        ILogger<FaturamentoPainelService> logger)
    {
        _db = db;
        _savwin = savwin;
        _cache = cache;
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

        var produtosTask = _savwin.FetchProdutosPorOsAsync(entity, parametros, cancellationToken);
        var formasTask = _savwin.FetchVendaResumoFormasPagamentoAsync(entity, formasReq, cancellationToken);
        var resumoFpTask = FetchVendaFormaPagamentoResumoOuVazioAsync(entity, parametros, cancellationToken);
        var pendentesTask = CountVendasPendentesEntregaOuZeroAsync(entity, parametros, cancellationToken);
        var catalogTask = FetchCatalogoProdutosParaPainelOuVazioAsync(entity, cancellationToken);

        await Task.WhenAll(produtosTask, formasTask, resumoFpTask, pendentesTask, catalogTask).ConfigureAwait(false);

        var produtos = await produtosTask.ConfigureAwait(false);
        var formas = await formasTask.ConfigureAwait(false);
        var resumoFp = await resumoFpTask.ConfigureAwait(false);
        var pendentesEntrega = await pendentesTask.ConfigureAwait(false);
        var catalog = await catalogTask.ConfigureAwait(false);

        var snapshot = new FaturamentoPainelSnapshot
        {
            Produtos = produtos,
            Formas = formas,
            ResumoFp = resumoFp,
            PendentesEntrega = pendentesEntrega
        };

        var cacheKey = BuildSnapshotCacheKey(gestaoClienteId, parametros);
        _cache.Set(cacheKey, snapshot, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = SnapshotTtl });

        var codigoMap = FaturamentoAgregacao.BuildCodigoParaCategoriaMap(catalog);
        var painel = FaturamentoAgregacao.Calcular(produtos, formas, codigoMap, resumoFp);
        painel.PendentesEntrega = pendentesEntrega;
        painel.CategoriasRefinamentoPendente = false;
        return painel;
    }

    /// <inheritdoc />
    public async Task<FaturamentoPainelResponse?> CompletarCategoriasPainelAsync(
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

        var cacheKey = BuildSnapshotCacheKey(gestaoClienteId, parametros);
        if (!_cache.TryGetValue(cacheKey, out FaturamentoPainelSnapshot? snapshot) || snapshot is null)
        {
            _logger.LogWarning(
                "Cache do painel faturamento ausente para refinamento de categorias (chave {Key}). Refazendo carga completa.",
                cacheKey);
            return await ObterPainelCompletoComCatalogoAsync(entity, parametros, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<ProdutosCadastradosGridItem> catalog;
        try
        {
            catalog = await _savwin
                .FetchProdutosCadastradosGridAsync(entity, CatalogoProdutosCadastradosRequest, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ProdutosCadastradosGrid indisponível no refinamento; mantendo fallback de categorias.");
            catalog = Array.Empty<ProdutosCadastradosGridItem>();
        }

        var codigoMap = FaturamentoAgregacao.BuildCodigoParaCategoriaMap(catalog);
        var painel = FaturamentoAgregacao.Calcular(snapshot.Produtos, snapshot.Formas, codigoMap, snapshot.ResumoFp);
        painel.PendentesEntrega = snapshot.PendentesEntrega;
        painel.CategoriasRefinamentoPendente = false;
        return painel;
    }

    /// <summary>Comportamento monolítico (sem cache): usado se o snapshot expirou antes do refinamento.</summary>
    private async Task<FaturamentoPainelResponse> ObterPainelCompletoComCatalogoAsync(
        GestaoCliente entity,
        ProdutosPorOsClientRequest parametros,
        CancellationToken cancellationToken)
    {
        var formasReq = new VendaResumoFormasPagamentoRequest
        {
            DataPgtoInicio = parametros.DataInicial,
            DataPgtoFim = parametros.DataFinal,
            DataVendaInicio = parametros.DataInicial,
            DataVendaFim = parametros.DataFinal,
            AgrupaFormaPagamento = "S",
            LojaId = parametros.LojaId
        };

        var produtosTask = _savwin.FetchProdutosPorOsAsync(entity, parametros, cancellationToken);
        var formasTask = _savwin.FetchVendaResumoFormasPagamentoAsync(entity, formasReq, cancellationToken);
        var resumoFpTask = FetchVendaFormaPagamentoResumoOuVazioAsync(entity, parametros, cancellationToken);
        var pendentesTask = CountVendasPendentesEntregaOuZeroAsync(entity, parametros, cancellationToken);
        var catalogTask = FetchCatalogoProdutosParaPainelOuVazioAsync(entity, cancellationToken);

        await Task.WhenAll(produtosTask, formasTask, resumoFpTask, pendentesTask, catalogTask).ConfigureAwait(false);

        var produtos = await produtosTask.ConfigureAwait(false);
        var formas = await formasTask.ConfigureAwait(false);
        var resumoFp = await resumoFpTask.ConfigureAwait(false);
        var pendentesEntrega = await pendentesTask.ConfigureAwait(false);
        var catalog = await catalogTask.ConfigureAwait(false);

        var codigoMap = FaturamentoAgregacao.BuildCodigoParaCategoriaMap(catalog);
        var painel = FaturamentoAgregacao.Calcular(produtos, formas, codigoMap, resumoFp);
        painel.PendentesEntrega = pendentesEntrega;
        painel.CategoriasRefinamentoPendente = false;
        return painel;
    }

    private async Task<IReadOnlyList<ProdutosCadastradosGridItem>> FetchCatalogoProdutosParaPainelOuVazioAsync(
        GestaoCliente entity,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _savwin
                .FetchProdutosCadastradosGridAsync(entity, CatalogoProdutosCadastradosRequest, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ProdutosCadastradosGrid indisponível no painel; categorias em fallback.");
            return Array.Empty<ProdutosCadastradosGridItem>();
        }
    }

    private static string BuildSnapshotCacheKey(int gestaoClienteId, ProdutosPorOsClientRequest p) =>
        $"faturamento_painel_snap:{gestaoClienteId}:{p.DataInicial?.Trim() ?? ""}:{p.DataFinal?.Trim() ?? ""}:{p.LojaId?.Trim() ?? ""}";

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
