using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
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

        var sw = Stopwatch.StartNew();
        try
        {
            var list = await _savwin.FetchProdutosPorOsAsync(resolved.Entity!, request, cancellationToken);
            sw.Stop();
            _logger.LogInformation(
                "Relatorios ProdutosPorOs [backend API]: {ElapsedMs}ms total no servidor, {Count} itens, período {DataInicial}–{DataFinal}",
                sw.ElapsedMilliseconds,
                list.Count,
                request.DataInicial,
                request.DataFinal);
            return Ok(list);
        }
        catch (InvalidOperationException ex)
        {
            sw.Stop();
            _logger.LogWarning(
                ex,
                "ProdutosPorOs falhou após {ElapsedMs}ms",
                sw.ElapsedMilliseconds);
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

        var sw = Stopwatch.StartNew();
        try
        {
            var raw = await _savwin.FetchContasPagarPagasGridRawJsonAsync(resolved.Entity!, request, cancellationToken);
            sw.Stop();
            _logger.LogInformation(
                "ContasPagarPagasGrid OK em {Ms} ms · {Bytes} bytes",
                sw.ElapsedMilliseconds,
                raw.Length);
            return Content(raw, "application/json");
        }
        catch (InvalidOperationException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "ContasPagarPagasGrid falhou após {Ms} ms", sw.ElapsedMilliseconds);
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

        var sw = Stopwatch.StartNew();
        try
        {
            var raw = await _savwin.FetchContasReceberRecebidasGridRawJsonAsync(resolved.Entity!, request, cancellationToken);
            sw.Stop();
            _logger.LogInformation(
                "ContasReceberRecebidasGrid OK em {Ms} ms · {Bytes} bytes",
                sw.ElapsedMilliseconds,
                raw.Length);
            return Content(raw, "application/json");
        }
        catch (InvalidOperationException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "ContasReceberRecebidasGrid falhou após {Ms} ms", sw.ElapsedMilliseconds);
            return StatusCode(StatusCodes.Status502BadGateway, ex.Message);
        }
    }

    /// <summary>
    /// Lê snapshot persistido do comparativo financeiro (contas pagas + recebidas) para o par de anos e lojas.
    /// </summary>
    [HttpGet("comparativo-financeiro-cache")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComparativoFinanceiroCache(
        [FromQuery] int anoMenor,
        [FromQuery] int anoMaior,
        [FromQuery] string? lojaId,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolverClienteAsync(cancellationToken);
        if (resolved.Unauthorized)
        {
            return Unauthorized();
        }

        var cid = resolved.Entity!.Id;
        var lojaNorm = NormalizeLojaParam(lojaId);
        var a0 = Math.Min(anoMenor, anoMaior);
        var a1 = Math.Max(anoMenor, anoMaior);

        var pagar = await _db.ComparativoFinanceiroSnapshotsPagar.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.GestaoClienteId == cid && x.AnoMenor == a0 && x.AnoMaior == a1 && x.LojaParam == lojaNorm,
                cancellationToken);
        var receber = await _db.ComparativoFinanceiroSnapshotsReceber.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.GestaoClienteId == cid && x.AnoMenor == a0 && x.AnoMaior == a1 && x.LojaParam == lojaNorm,
                cancellationToken);

        if (pagar is null || receber is null)
        {
            return NotFound();
        }

        var meta = await _db.GestaoClientes.AsNoTracking()
            .Where(x => x.Id == cid)
            .Select(x => x.ComparativoFinanceiroUltimaConsultaUtc)
            .FirstAsync(cancellationToken);

        Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate");
        Response.Headers.Append("Pragma", "no-cache");

        return Ok(new
        {
            anoMenor = a0,
            anoMaior = a1,
            lojaId = string.IsNullOrEmpty(lojaNorm) ? null : lojaNorm,
            seriePagas = JsonSerializer.Deserialize<object>(pagar.SerieJson),
            serieRecebidas = JsonSerializer.Deserialize<object>(receber.SerieJson),
            formasPagas = JsonSerializer.Deserialize<object>(pagar.FormasJson),
            formasRecebidas = JsonSerializer.Deserialize<object>(receber.FormasJson),
            ultimaConsultaUtc = meta
        });
    }

    /// <summary>Grava snapshot do comparativo (após agregação no cliente ou servidor) e atualiza data da última consulta.</summary>
    [HttpPut("comparativo-financeiro-cache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PutComparativoFinanceiroCache(
        [FromBody] ComparativoFinanceiroCachePutRequest? body,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolverClienteAsync(cancellationToken);
        if (resolved.Unauthorized)
        {
            return Unauthorized();
        }

        if (body is null)
        {
            return BadRequest("Corpo obrigatório.");
        }

        var a0 = Math.Min(body.AnoMenor, body.AnoMaior);
        var a1 = Math.Max(body.AnoMenor, body.AnoMaior);
        var lojaNorm = NormalizeLojaParam(body.LojaId);
        var cid = resolved.Entity!.Id;
        var utc = DateTime.UtcNow;

        var serieP = body.SeriePagas.GetRawText();
        var serieR = body.SerieRecebidas.GetRawText();
        var formP = body.FormasPagas.GetRawText();
        var formR = body.FormasRecebidas.GetRawText();

        if (string.IsNullOrWhiteSpace(serieP) || string.IsNullOrWhiteSpace(serieR))
        {
            return BadRequest("seriePagas e serieRecebidas são obrigatórias.");
        }

        var pagar = await _db.ComparativoFinanceiroSnapshotsPagar
            .FirstOrDefaultAsync(
                x => x.GestaoClienteId == cid && x.AnoMenor == a0 && x.AnoMaior == a1 && x.LojaParam == lojaNorm,
                cancellationToken);
        if (pagar is null)
        {
            pagar = new ComparativoFinanceiroSnapshotPagar
            {
                GestaoClienteId = cid,
                AnoMenor = a0,
                AnoMaior = a1,
                LojaParam = lojaNorm
            };
            _db.ComparativoFinanceiroSnapshotsPagar.Add(pagar);
        }

        pagar.SerieJson = serieP;
        pagar.FormasJson = string.IsNullOrWhiteSpace(formP) ? "[]" : formP;
        pagar.AtualizadoEmUtc = utc;

        var receber = await _db.ComparativoFinanceiroSnapshotsReceber
            .FirstOrDefaultAsync(
                x => x.GestaoClienteId == cid && x.AnoMenor == a0 && x.AnoMaior == a1 && x.LojaParam == lojaNorm,
                cancellationToken);
        if (receber is null)
        {
            receber = new ComparativoFinanceiroSnapshotReceber
            {
                GestaoClienteId = cid,
                AnoMenor = a0,
                AnoMaior = a1,
                LojaParam = lojaNorm
            };
            _db.ComparativoFinanceiroSnapshotsReceber.Add(receber);
        }

        receber.SerieJson = serieR;
        receber.FormasJson = string.IsNullOrWhiteSpace(formR) ? "[]" : formR;
        receber.AtualizadoEmUtc = utc;

        var tracked = await _db.GestaoClientes.FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
        if (tracked is not null)
        {
            tracked.ComparativoFinanceiroUltimaConsultaUtc = utc;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Remove todos os snapshots do comparativo deste usuário e zera a data da última consulta.</summary>
    [HttpDelete("comparativo-financeiro-cache")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteComparativoFinanceiroCache(CancellationToken cancellationToken)
    {
        var userId = GetGestaoClienteId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var cid = userId.Value;
        await _db.ComparativoFinanceiroSnapshotsPagar.Where(x => x.GestaoClienteId == cid)
            .ExecuteDeleteAsync(cancellationToken);
        await _db.ComparativoFinanceiroSnapshotsReceber.Where(x => x.GestaoClienteId == cid)
            .ExecuteDeleteAsync(cancellationToken);

        var tracked = await _db.GestaoClientes.FirstOrDefaultAsync(x => x.Id == cid, cancellationToken);
        if (tracked is not null)
        {
            tracked.ComparativoFinanceiroUltimaConsultaUtc = null;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    private static string NormalizeLojaParam(string? lojaId) => lojaId?.Trim() ?? string.Empty;

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
