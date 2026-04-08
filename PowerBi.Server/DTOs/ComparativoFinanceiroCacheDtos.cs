using System.Text.Json;

namespace PowerBi.Server.DTOs;

public sealed class ComparativoFinanceiroCachePutRequest
{
    public int AnoMenor { get; set; }

    public int AnoMaior { get; set; }

    public string? LojaId { get; set; }

    public JsonElement SeriePagas { get; set; }

    public JsonElement SerieRecebidas { get; set; }

    public JsonElement FormasPagas { get; set; }

    public JsonElement FormasRecebidas { get; set; }
}
