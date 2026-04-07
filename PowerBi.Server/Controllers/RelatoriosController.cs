using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PowerBi.Server.Data;
using PowerBi.Server.DTOs;
using PowerBi.Server.Entities;
using PowerBi.Server.Services.Faturamento;
using PowerBi.Server.Services.Savwin;

namespace PowerBi.Server.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class RelatoriosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISavwinRelatoriosClient _savwin;
    private readonly IFaturamentoPainelService _faturamentoPainel;
    private readonly ILogger<RelatoriosController> _logger;

    public RelatoriosController(
        ApplicationDbContext db,
        ISavwinRelatoriosClient savwin,
        IFaturamentoPainelService faturamentoPainel,
        ILogger<RelatoriosController> logger)
    {
        _db = db;
        _savwin = savwin;
        _faturamentoPainel = faturamentoPainel;
        _logger = logger;
    }

    /// <summary>Proxy para ProdutosPorOS: usa ChaveWs (Bearer) e Identificador (header) do usuário logado.</summary>
    [HttpPost("produtos-por-os")]
    [ProducesResponseType(typeof(IEnumerable<ProdutoPorOsItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IEnumerable<ProdutoPorOsItem>>> ProdutosPorOs(
        [FromBody] ProdutosPorOsClientRequest request,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolverClienteAsync(cancellationToken);
        if (resolved.Unauthorized)
        {
            return Unauthorized();
        }

        try
        {
            var list = await _savwin.FetchProdutosPorOsAsync(resolved.Entity!, request, cancellationToken);
            return Ok(list);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ProdutosPorOs falhou");
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    /// <summary>Proxy para APIVendaResumoTodasFormasPagamento (datas de pagamento + loja).</summary>
    [HttpPost("venda-resumo-formas-pagamento")]
    [ProducesResponseType(typeof(IEnumerable<VendaResumoFormaPagamentoItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IEnumerable<VendaResumoFormaPagamentoItem>>> VendaResumoFormasPagamento(
        [FromBody] VendaResumoFormasPagamentoRequest request,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolverClienteAsync(cancellationToken);
        if (resolved.Unauthorized)
        {
            return Unauthorized();
        }

        try
        {
            var list = await _savwin.FetchVendaResumoFormasPagamentoAsync(resolved.Entity!, request, cancellationToken);
            return Ok(list);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "VendaResumoFormasPagamento falhou");
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    /// <summary>
    /// Painel Faturamento: busca SavWin, agrega KPIs/material/grife/formas no servidor.
    /// </summary>
    [HttpPost("faturamento-painel")]
    [ProducesResponseType(typeof(FaturamentoPainelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<FaturamentoPainelResponse>> FaturamentoPainel(
        [FromBody] ProdutosPorOsClientRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetGestaoClienteId();
        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _faturamentoPainel.ObterPainelAsync(userId.Value, request, cancellationToken);
            if (result is null)
            {
                return Unauthorized();
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "FaturamentoPainel falhou");
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    /// <summary>Proxy SavWin <c>EntradasEstoqueGrid</c> (body: CODIGOLOJA, INICIOSEQ, FINALSEQ).</summary>
    [HttpPost("entradas-estoque-grid")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> EntradasEstoqueGrid(
        [FromBody] EntradasEstoqueGridClientRequest request,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolverClienteAsync(cancellationToken);
        if (resolved.Unauthorized)
        {
            return Unauthorized();
        }

        try
        {
            var raw = await _savwin.FetchEntradasEstoqueGridRawJsonAsync(resolved.Entity!, request, cancellationToken);
            return Content(raw, "application/json");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "EntradasEstoqueGrid falhou");
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    /// <summary>Proxy SavWin <c>ContasPagarPagasGrid</c> (contas a pagar / pagas).</summary>
    [HttpPost("contas-pagar-pagas-grid")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ContasPagarPagasGrid(
        [FromBody] ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolverClienteAsync(cancellationToken);
        if (resolved.Unauthorized)
        {
            return Unauthorized();
        }

        try
        {
            var raw = await _savwin.FetchContasPagarPagasGridRawJsonAsync(resolved.Entity!, request, cancellationToken);
            return Content(raw, "application/json");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ContasPagarPagasGrid falhou");
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    /// <summary>Proxy SavWin <c>ContasReceberRecebidasGrid</c> (contas a receber / recebidas).</summary>
    [HttpPost("contas-receber-recebidas-grid")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ContasReceberRecebidasGrid(
        [FromBody] ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolverClienteAsync(cancellationToken);
        if (resolved.Unauthorized)
        {
            return Unauthorized();
        }

        try
        {
            var raw = await _savwin.FetchContasReceberRecebidasGridRawJsonAsync(resolved.Entity!, request, cancellationToken);
            return Content(raw, "application/json");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "ContasReceberRecebidasGrid falhou");
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    private int? GetGestaoClienteId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(sub, out var id) ? id : null;
    }

    private async Task<(bool Unauthorized, GestaoCliente? Entity)> ResolverClienteAsync(CancellationToken cancellationToken)
    {
        var userId = GetGestaoClienteId();
        if (userId is null)
        {
            return (true, null);
        }

        var entity = await _db.GestaoClientes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId.Value, cancellationToken);
        return entity is null ? (true, null) : (false, entity);
    }
}
