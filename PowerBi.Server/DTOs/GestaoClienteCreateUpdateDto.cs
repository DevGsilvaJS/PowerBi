using System.ComponentModel.DataAnnotations;

namespace PowerBi.Server.DTOs;

public class GestaoClienteCreateUpdateDto
{
    [Required(ErrorMessage = "Usuário é obrigatório.")]
    [MaxLength(256)]
    public string Usuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "Senha é obrigatória.")]
    [MaxLength(512)]
    public string Senha { get; set; } = string.Empty;

    [Required(ErrorMessage = "ChaveWs é obrigatória.")]
    [MaxLength(512)]
    public string ChaveWs { get; set; } = string.Empty;

    [Required(ErrorMessage = "Identificador é obrigatório.")]
    [MaxLength(256)]
    public string Identificador { get; set; } = string.Empty;

    /// <summary>IDs de lojas separados por vírgula (ex.: 1,2,3,4,5).</summary>
    [MaxLength(2048)]
    public string Lojas { get; set; } = string.Empty;
}
