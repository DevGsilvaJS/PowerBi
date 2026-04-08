using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PowerBi.Server.Entities;

/// <summary>Cache persistido: contas a receber (recebidas/baixadas) — série mensal + formas.</summary>
public class ComparativoFinanceiroSnapshotReceber
{
    public long Id { get; set; }

    public int GestaoClienteId { get; set; }

    [ForeignKey(nameof(GestaoClienteId))]
    public GestaoCliente GestaoCliente { get; set; } = null!;

    public int AnoMenor { get; set; }

    public int AnoMaior { get; set; }

    [MaxLength(2048)]
    public string LojaParam { get; set; } = string.Empty;

    public string SerieJson { get; set; } = string.Empty;

    public string FormasJson { get; set; } = string.Empty;

    public DateTime AtualizadoEmUtc { get; set; }
}
