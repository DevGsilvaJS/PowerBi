namespace PowerBi.Server.DTOs;

/// <summary>Parâmetros vindos da tela (datas em yyyy-MM-dd).</summary>
public class ProdutosPorOsClientRequest
{
    public string DataInicial { get; set; } = string.Empty;
    public string DataFinal { get; set; } = string.Empty;
    /// <summary>Filtro opcional de uma loja; vazio = usar todas as lojas do cadastro.</summary>
    public string? LojaId { get; set; }
}
