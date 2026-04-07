namespace PowerBi.Server.DTOs;

public static class VendaResumoFormasPagamentoMapping
{
    public static List<VendaResumoFormaPagamentoItem> ToApiItems(List<VendaResumoFormasPagamentoSavwinDto>? list) =>
        list?.Select(ToApiItem).ToList() ?? new List<VendaResumoFormaPagamentoItem>();

    public static VendaResumoFormaPagamentoItem ToApiItem(VendaResumoFormasPagamentoSavwinDto s)
    {
        var d = new VendaResumoFormaPagamentoItem();
        foreach (var p in typeof(VendaResumoFormasPagamentoSavwinDto).GetProperties())
        {
            typeof(VendaResumoFormaPagamentoItem).GetProperty(p.Name)?.SetValue(d, p.GetValue(s));
        }

        return d;
    }
}
