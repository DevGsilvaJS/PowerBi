using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerBi.Server.Entities;

/// <summary>Cache persistido: contas a pagar (baixadas) do comparativo anual — série mensal + formas.</summary>
public class ComparativoFinanceiroSnapshotPagar
{
    public long Id { get; set; }

    public int GestaoClienteId { get; set; }

    [ForeignKey(nameof(GestaoClienteId))]
    public GestaoCliente GestaoCliente { get; set; } = null!;

    public int AnoMenor { get; set; }

    public int AnoMaior { get; set; }

    /// <summary>Parâmetro <c>lojaId</c> da API; vazio = todas as lojas do cadastro.</summary>
    [MaxLength(2048)]
    public string LojaParam { get; set; } = string.Empty;

    /// <summary>JSON: array da série comparativa mensal (12 pontos).</summary>
    public string SerieJson { get; set; } = string.Empty;

    /// <summary>JSON: agregado por forma de pagamento.</summary>
    public string FormasJson { get; set; } = string.Empty;

    public DateTime AtualizadoEmUtc { get; set; }
}
