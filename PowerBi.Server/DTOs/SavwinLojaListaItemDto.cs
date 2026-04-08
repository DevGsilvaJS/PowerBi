namespace PowerBi.Server.DTOs;

/// <summary>Item de <c>APILojas/RetornaLista</c> (ex.: <c>FILID</c> + <c>FILSEQUENCIAL</c>).</summary>
public sealed class SavwinLojaListaItemDto
{
    /// <summary>Identificador da filial (<c>FILID</c> no JSON) — valor usado em <c>FILID</c> nas demais APIs.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Código de loja (<c>FILSEQUENCIAL</c> no JSON); cruza com o cadastro do cliente — não confundir com <see cref="Id"/>.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Nome / fantasia quando disponível.</summary>
    public string Nome { get; set; } = string.Empty;
}
