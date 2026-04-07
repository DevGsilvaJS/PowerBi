using System.Text.Json.Serialization;

namespace PowerBi.Server.DTOs;

/// <summary>Linha mínima de <c>APIRelatoriosCR/ProdutosCadastradosGrid</c> (join com produtos por O.S.).</summary>
public sealed class ProdutosCadastradosGridItem
{
    [JsonPropertyName("MATID")]
    public string? MatId { get; set; }

    [JsonPropertyName("CODIGO")]
    public string? Codigo { get; set; }

    [JsonPropertyName("TIPOPRODUTO")]
    public string? TipoProduto { get; set; }
}
