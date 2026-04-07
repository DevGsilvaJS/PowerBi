using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PowerBi.Server.Data;
using PowerBi.Server.DTOs;

namespace PowerBi.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Login usando cadastro de gestão de clientes (usuário/senha no banco).</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Usuario) || string.IsNullOrWhiteSpace(request.Senha))
        {
            return Unauthorized();
        }

        var usuario = request.Usuario.Trim();
        var entity = await _db.GestaoClientes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Usuario == usuario, cancellationToken);

        if (entity is null || entity.Senha != request.Senha)
        {
            return Unauthorized();
        }

        var token = CreateJwtToken(entity.Id, entity.Usuario);
        return Ok(new LoginResponse
        {
            AccessToken = token,
            Usuario = entity.Usuario,
            Lojas = entity.Lojas ?? string.Empty
        });
    }

    private string CreateJwtToken(int gestaoClienteId, string usuario)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, gestaoClienteId.ToString()),
            new Claim(ClaimTypes.Name, usuario)
        };
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
