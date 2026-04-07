using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PowerBi.Server.Data;
using PowerBi.Server.DTOs;
using PowerBi.Server.Entities;

namespace PowerBi.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GestaoClientesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public GestaoClientesController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Lista todos os cadastros de gestão de clientes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<GestaoClienteResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GestaoClienteResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var list = await _db.GestaoClientes
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Select(x => ToResponse(x))
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    /// <summary>Obtém um cadastro por id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GestaoClienteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GestaoClienteResponseDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.GestaoClientes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(entity));
    }

    /// <summary>Cria um novo cadastro.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(GestaoClienteResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GestaoClienteResponseDto>> Create(
        [FromBody] GestaoClienteCreateUpdateDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var entity = new GestaoCliente
        {
            Usuario = dto.Usuario.Trim(),
            Senha = dto.Senha,
            ChaveWs = dto.ChaveWs.Trim(),
            Identificador = dto.Identificador.Trim(),
            Lojas = dto.Lojas?.Trim() ?? string.Empty,
            CriadoEm = DateTime.UtcNow
        };

        _db.GestaoClientes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var response = ToResponse(entity);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, response);
    }

    /// <summary>Atualiza um cadastro existente.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(GestaoClienteResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GestaoClienteResponseDto>> Update(
        int id,
        [FromBody] GestaoClienteUpdateDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var entity = await _db.GestaoClientes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        entity.Usuario = dto.Usuario.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Senha))
        {
            entity.Senha = dto.Senha;
        }

        entity.ChaveWs = dto.ChaveWs.Trim();
        entity.Identificador = dto.Identificador.Trim();
        entity.Lojas = dto.Lojas?.Trim() ?? string.Empty;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(entity));
    }

    /// <summary>Remove um cadastro.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var entity = await _db.GestaoClientes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        _db.GestaoClientes.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static GestaoClienteResponseDto ToResponse(GestaoCliente x) => new()
    {
        Id = x.Id,
        Usuario = x.Usuario,
        ChaveWs = x.ChaveWs,
        Identificador = x.Identificador,
        Lojas = x.Lojas,
        CriadoEm = x.CriadoEm
    };
}
