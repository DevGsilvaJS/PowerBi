using System.Globalization;
using PowerBi.Server.DTOs;
using PowerBi.Server.Services.Faturamento;

namespace PowerBi.Server.Services.EstatisticasCliente;

/// <summary>
/// Cruza demonstrativo de vendas com cadastro de clientes e agrega por bairro (maior liquidez primeiro).
/// </summary>
public static class VendasPorBairroAgregacao
{
    private const char SepBairroCidade = '\u001f';

    public static List<VendaPorBairroItemDto> Calcular(
        IReadOnlyList<DemonstrativoVendaPorClienteSavwinDto> vendas,
        IReadOnlyList<ClienteRetornaListaSavwinDto> clientes)
    {
        if (vendas.Count == 0)
        {
            return new List<VendaPorBairroItemDto>();
        }

        var lookup = new ClientePorCodigoLookup(clientes);
        var grupos = new Dictionary<string, GrupoAgg>(StringComparer.OrdinalIgnoreCase);

        foreach (var v in vendas)
        {
            var liq = (double)FaturamentoAgregacao.ParseBr(v.TOTAL_LIQUIDO);
            var (bairro, cidade) = ResolverBairroECidade(v, lookup);
            if (string.IsNullOrWhiteSpace(bairro))
            {
                bairro = "Não informado";
            }

            var cidadeNorm = string.IsNullOrWhiteSpace(cidade) ? null : cidade.Trim();
            var chave = string.IsNullOrWhiteSpace(cidadeNorm)
                ? bairro.Trim()
                : bairro.Trim() + SepBairroCidade + cidadeNorm;

            if (!grupos.TryGetValue(chave, out var g))
            {
                g = new GrupoAgg(bairro.Trim(), cidadeNorm);
                grupos[chave] = g;
            }

            g.ValorLiquido += liq;
            var os = (v.OS ?? "").Trim();
            var loja = (v.LOJA ?? "").Trim();
            if (os.Length > 0)
            {
                g.VendasOs.Add($"{loja}|{os}");
            }
            else
            {
                g.VendasOs.Add($"{loja}|__sem_os__|{v.CLIENTE ?? ""}");
            }
        }

        return grupos.Values
            .Select(x => new VendaPorBairroItemDto
            {
                Bairro = x.Bairro,
                Cidade = x.Cidade,
                ValorLiquido = x.ValorLiquido,
                QtdVendasDistintas = x.VendasOs.Count,
                Lat = null,
                Lon = null
            })
            .OrderByDescending(x => x.ValorLiquido)
            .ThenBy(x => x.Bairro, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), false))
            .ToList();
    }

    private sealed class GrupoAgg
    {
        public GrupoAgg(string bairro, string? cidade)
        {
            Bairro = bairro;
            Cidade = cidade;
        }

        public string Bairro { get; }
        public string? Cidade { get; }
        public double ValorLiquido { get; set; }
        public HashSet<string> VendasOs { get; } = new(StringComparer.Ordinal);
    }

    private static (string? Bairro, string? Cidade) ResolverBairroECidade(
        DemonstrativoVendaPorClienteSavwinDto v,
        ClientePorCodigoLookup lookup)
    {
        if (!string.IsNullOrWhiteSpace(v.BAIRRO))
        {
            return (v.BAIRRO.Trim(), string.IsNullOrWhiteSpace(v.CIDADE) ? null : v.CIDADE.Trim());
        }

        var cod = ExtrairCodigoCliente(v.CLIENTE);
        if (cod is null)
        {
            return (null, null);
        }

        var cli = lookup.Resolve(cod);
        if (cli is null)
        {
            return (null, null);
        }

        var b = string.IsNullOrWhiteSpace(cli.ENDBAIRRO) ? null : cli.ENDBAIRRO.Trim();
        var c = string.IsNullOrWhiteSpace(cli.CIDNOME) ? null : cli.CIDNOME.Trim();
        return (b, c);
    }

    /// <summary>Parte numérica antes de <c> - </c> (ex.: <c>000028904</c> → chave <c>28904</c> e variantes).</summary>
    public static string? ExtrairCodigoCliente(string? cliente)
    {
        if (string.IsNullOrWhiteSpace(cliente))
        {
            return null;
        }

        var t = cliente.Trim();
        var sep = t.IndexOf(" - ", StringComparison.Ordinal);
        if (sep > 0)
        {
            var left = t[..sep].Trim();
            return SomenteDigitos(left);
        }

        return SomenteDigitos(t);
    }

    private static string? SomenteDigitos(string s)
    {
        var chars = s.Where(char.IsDigit).ToArray();
        if (chars.Length == 0)
        {
            return null;
        }

        return new string(chars).TrimStart('0').Length == 0 ? "0" : new string(chars).TrimStart('0');
    }

    private sealed class ClientePorCodigoLookup
    {
        private readonly Dictionary<string, ClienteRetornaListaSavwinDto> _map = new(StringComparer.Ordinal);

        public ClientePorCodigoLookup(IReadOnlyList<ClienteRetornaListaSavwinDto> clientes)
        {
            foreach (var c in clientes)
            {
                AdicionarChave(c.CLISEQUENCIAL, c);
                AdicionarChave(c.CLIID, c);
            }
        }

        private void AdicionarChave(string? raw, ClienteRetornaListaSavwinDto c)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return;
            }

            var t = raw.Trim();
            _map[t] = c;
            var norm = SomenteDigitosNorm(t);
            if (!string.IsNullOrEmpty(norm))
            {
                _map[norm] = c;
            }
        }

        private static string SomenteDigitosNorm(string s)
        {
            var chars = s.Where(char.IsDigit).ToArray();
            if (chars.Length == 0)
            {
                return string.Empty;
            }

            var d = new string(chars).TrimStart('0');
            return d.Length == 0 ? "0" : d;
        }

        public ClienteRetornaListaSavwinDto? Resolve(string codigoVenda)
        {
            if (_map.TryGetValue(codigoVenda, out var c))
            {
                return c;
            }

            var n = SomenteDigitosNorm(codigoVenda);
            return string.IsNullOrEmpty(n) ? null : _map.GetValueOrDefault(n);
        }
    }
}
