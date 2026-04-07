namespace PowerBi.Server.DTOs;

/// <summary>Resposta sem expor a senha.</summary>
public class GestaoClienteResponseDto
{
    public int Id { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string ChaveWs { get; set; } = string.Empty;
    public string Identificador { get; set; } = string.Empty;
    public string Lojas { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}
