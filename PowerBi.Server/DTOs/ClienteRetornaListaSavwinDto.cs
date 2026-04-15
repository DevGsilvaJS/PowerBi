namespace PowerBi.Server.DTOs;

/// <summary>Item de <c>POST APIClientes/RetornaLista</c> (corpo vazio).</summary>
public sealed class ClienteRetornaListaSavwinDto
{
    public string? CLIID { get; set; }

    /// <summary>Chave sequencial do cliente (cruzar com o código extraído de <see cref="DemonstrativoVendaPorClienteSavwinDto.CLIENTE"/>).</summary>
    public string? CLISEQUENCIAL { get; set; }

    public string? PESNOME { get; set; }
    public string? PESSOBRENOME { get; set; }
    public string? ENDBAIRRO { get; set; }
    public string? CIDNOME { get; set; }
    public string? CPF { get; set; }
}
