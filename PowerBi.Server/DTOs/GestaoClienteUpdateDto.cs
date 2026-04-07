using System.ComponentModel.DataAnnotations;

namespace PowerBi.Server.DTOs;

/// <summary>Atualização: senha em branco ou ausente mantém a senha atual no banco.</summary>
public class GestaoClienteUpdateDto
{
    [Required(ErrorMessage = "Usuário é obrigatório.")]
    [MaxLength(256)]
    public string Usuario { get; set; } = string.Empty;

    /// <summary>Nova senha; se vazia, não altera a senha armazenada.</summary>
    [MaxLength(512)]
    public string? Senha { get; set; }

    [Required(ErrorMessage = "ChaveWs é obrigatória.")]
    [MaxLength(512)]
    public string ChaveWs { get; set; } = string.Empty;

    [Required(ErrorMessage = "Identificador é obrigatório.")]
    [MaxLength(256)]
    public string Identificador { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string Lojas { get; set; } = string.Empty;
}
