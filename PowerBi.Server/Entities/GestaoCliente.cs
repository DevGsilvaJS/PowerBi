using System.ComponentModel.DataAnnotations;

namespace PowerBi.Server.Entities;

/// <summary>
/// Configuração / acesso de integração (gestão de clientes).
/// </summary>
public class GestaoCliente
{
    public int Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string Usuario { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string Senha { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string ChaveWs { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string Identificador { get; set; } = string.Empty;

    /// <summary>IDs de lojas separados por vírgula (ex.: 1,2,3,4,5).</summary>
    [MaxLength(2048)]
    public string Lojas { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }
}
