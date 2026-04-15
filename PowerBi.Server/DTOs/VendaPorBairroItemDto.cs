namespace PowerBi.Server.DTOs;

/// <summary>Agregado de vendas por bairro para Estatísticas cliente (mapa + gráfico).</summary>
public sealed class VendaPorBairroItemDto
{
    public string Bairro { get; set; } = "";

    /// <summary>Cidade quando conhecida (cadastro de cliente ou linha do demonstrativo).</summary>
    public string? Cidade { get; set; }

    public double ValorLiquido { get; set; }

    /// <summary>Quantidade de O.S. distintas (LOJA + OS) no bairro.</summary>
    public int QtdVendasDistintas { get; set; }

    /// <summary>Reservado para mapa; opcional.</summary>
    public double? Lat { get; set; }

    public double? Lon { get; set; }
}
