namespace PowerBi.Server.DTOs;

/// <summary>Converte o DTO da SavWin (maiúsculas) para o DTO da API (serialização camelCase para o Angular).</summary>
public static class ProdutoPorOsSavwinMapping
{
    public static List<ProdutoPorOsItem> ToApiItems(List<ProdutoPorOsSavwinDto>? list) =>
        list?.Select(ToApiItem).ToList() ?? new List<ProdutoPorOsItem>();

    public static ProdutoPorOsItem ToApiItem(ProdutoPorOsSavwinDto s)
    {
        var d = new ProdutoPorOsItem();
        foreach (var p in typeof(ProdutoPorOsSavwinDto).GetProperties())
        {
            typeof(ProdutoPorOsItem).GetProperty(p.Name)?.SetValue(d, p.GetValue(s));
        }

        return d;
    }
}
