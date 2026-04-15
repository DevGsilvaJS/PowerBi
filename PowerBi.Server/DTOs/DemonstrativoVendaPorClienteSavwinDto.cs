namespace PowerBi.Server.DTOs;

/// <summary>Linha típica de <c>APIDemonstrativoVendaPorCliente</c> (SavWin).</summary>
public sealed class DemonstrativoVendaPorClienteSavwinDto
{
    public string? LOJA { get; set; }
    public string? OS { get; set; }
    public string? DATA { get; set; }
    public string? DATA_CANCELAMENTO { get; set; }

    /// <summary>Ex.: <c>000028904 - NOME DO CLIENTE</c> — código numérico antes de <c> - </c>.</summary>
    public string? CLIENTE { get; set; }

    public string? CPF_CNPJ { get; set; }
    public string? TOTAL_LIQUIDO { get; set; }
    public string? ENDERECO { get; set; }
    public string? BAIRRO { get; set; }
    public string? CIDADE { get; set; }
    public string? UF { get; set; }
    public string? CEP { get; set; }
    public string? VENDEDOR { get; set; }
    public string? FILID { get; set; }
}
