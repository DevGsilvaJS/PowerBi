namespace PowerBi.Server.DTOs;

/// <summary>Proxy para <c>APIRelatoriosCR/EntradasEstoqueGrid</c> (SavWin). Body: CODIGOLOJA, INICIOSEQ, FINALSEQ.</summary>
public sealed class EntradasEstoqueGridClientRequest
{
    /// <summary>Código da loja; vazio = <c>0</c> (todas).</summary>
    public string? LojaId { get; set; }

    public string? InicioSeq { get; set; }

    public string? FinalSeq { get; set; }
}
