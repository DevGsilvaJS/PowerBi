using Microsoft.EntityFrameworkCore;
using PowerBi.Server.Data;
using PowerBi.Server.DTOs;
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
        await Task.WhenAll(produtosTask, formasTask);

        var produtos = await produtosTask;
        var formas = await formasTask;

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
        return FaturamentoAgregacao.Calcular(produtos, formas, codigoMap);
    }
}
