namespace PowerBi.Server.DTOs;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    /// <summary>Lista de lojas do cadastro (ex.: 1,2,3).</summary>
    public string Lojas { get; set; } = string.Empty;
}
