using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PowerBi.Server.DTOs;
using PowerBi.Server.Entities;
using Microsoft.Extensions.Logging;

namespace PowerBi.Server.Services.Savwin;

/// <summary>Cliente HTTP para APIs SavWin de relatórios (proxy técnico).</summary>
public sealed class SavwinRelatoriosClient : ISavwinRelatoriosClient
{
    private const string SavwinProdutosPorOsUrl = "https://api.savwinweb.com.br/api/APIRelatoriosCR/ProdutosPorOS";
    private const string SavwinVendaResumoFormasPagamentoUrl =
        "http://cliapi.savwinweb.com.br/api/APIRelatoriosCR/APIVendaResumoTodasFormasPagamento";

    private const string SavwinEntradasEstoqueGridUrl =
        "https://api.savwinweb.com.br/api/APIRelatoriosCR/EntradasEstoqueGrid";

    private const string SavwinContasPagarPagasGridUrl =
        "https://api.savwinweb.com.br/api/APIRelatoriosCR/ContasPagarPagasGrid";

    private const string SavwinContasReceberRecebidasGridUrl =
        "https://api.savwinweb.com.br/api/APIRelatoriosCR/ContasReceberRecebidasGrid";

    private const string SavwinProdutosCadastradosGridUrl =
        "https://api.savwinweb.com.br/api/APIRelatoriosCR/ProdutosCadastradosGrid";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SavwinRelatoriosClient> _logger;

    public SavwinRelatoriosClient(IHttpClientFactory httpClientFactory, ILogger<SavwinRelatoriosClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProdutoPorOsItem>> FetchProdutosPorOsAsync(
        GestaoCliente cliente,
        ProdutosPorOsClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var dataInicial = SavwinRelatorioParametros.ToSavwinDate(request.DataInicial);
        var dataFinal = SavwinRelatorioParametros.ToSavwinDate(request.DataFinal);
        var lojas = SavwinRelatorioParametros.BuildLojasParam(cliente, request.LojaId);

        var payload = new Dictionary<string, string>
        {
            ["DATAINICIAL"] = dataInicial,
            ["DATAFINAL"] = dataFinal,
            ["LOJAS"] = lojas
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SavwinProdutosPorOsUrl);
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
        httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar Savwin ProdutosPorOS");
            throw new InvalidOperationException("Falha ao contatar a API externa.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Savwin HTTP {Status}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Savwin HTTP {(int)response.StatusCode}");
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = JsonSerializer.Deserialize<List<ProdutoPorOsSavwinDto>>(body, options);
                if (list is null)
                {
                    return Array.Empty<ProdutoPorOsItem>();
                }

                var i = 0;
                foreach (var row in root.EnumerateArray())
                {
                    if (i >= list.Count)
                    {
                        break;
                    }

                    AplicarCodigoDaVendaDoJsonNaLinha(list[i], row);
                    i++;
                }

                return ProdutoPorOsSavwinMapping.ToApiItems(list);
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                var one = JsonSerializer.Deserialize<ProdutoPorOsSavwinDto>(body, options);
                if (one is null)
                {
                    return Array.Empty<ProdutoPorOsItem>();
                }

                AplicarCodigoDaVendaDoJsonNaLinha(one, root);
                return new[] { ProdutoPorOsSavwinMapping.ToApiItem(one) };
            }

            return Array.Empty<ProdutoPorOsItem>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON inválido da Savwin: {Body}", body);
            throw new InvalidOperationException("Resposta inválida da API externa.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VendaResumoFormaPagamentoItem>> FetchVendaResumoFormasPagamentoAsync(
        GestaoCliente cliente,
        VendaResumoFormasPagamentoRequest request,
        CancellationToken cancellationToken = default)
    {
        var dataPgtoIni = SavwinRelatorioParametros.ToSavwinDate(request.DataPgtoInicio);
        var dataPgtoFim = SavwinRelatorioParametros.ToSavwinDate(request.DataPgtoFim);
        var loja = SavwinRelatorioParametros.LojaParametroUnico(cliente, request.LojaId);
        var agrupa = string.IsNullOrWhiteSpace(request.AgrupaFormaPagamento)
            ? "S"
            : request.AgrupaFormaPagamento.Trim();

        var dataVendaIni = string.IsNullOrWhiteSpace(request.DataVendaInicio)
            ? dataPgtoIni
            : SavwinRelatorioParametros.ToSavwinDate(request.DataVendaInicio!);
        var dataVendaFim = string.IsNullOrWhiteSpace(request.DataVendaFim)
            ? dataPgtoFim
            : SavwinRelatorioParametros.ToSavwinDate(request.DataVendaFim!);

        var payload = new Dictionary<string, object?>
        {
            ["DATAPGTOINICIO"] = dataPgtoIni,
            ["DATAPGTOFIM"] = dataPgtoFim,
            ["DATAVENDAINICIO"] = dataVendaIni,
            ["DATAVENDAFIM"] = dataVendaFim,
            ["AGRUPAFORMAPAGAMENTO"] = agrupa,
            ["LOJA"] = loja
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SavwinVendaResumoFormasPagamentoUrl);
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
        httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar Savwin APIVendaResumoTodasFormasPagamento");
            throw new InvalidOperationException("Falha ao contatar a API externa.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Savwin formas pagamento HTTP {Status}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Savwin HTTP {(int)response.StatusCode}");
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = JsonSerializer.Deserialize<List<VendaResumoFormasPagamentoSavwinDto>>(body, options);
                return VendaResumoFormasPagamentoMapping.ToApiItems(list);
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                var one = JsonSerializer.Deserialize<VendaResumoFormasPagamentoSavwinDto>(body, options);
                return one is null
                    ? Array.Empty<VendaResumoFormaPagamentoItem>()
                    : new[] { VendaResumoFormasPagamentoMapping.ToApiItem(one) };
            }

            return Array.Empty<VendaResumoFormaPagamentoItem>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON inválido Savwin formas pagamento: {Body}", body);
            throw new InvalidOperationException("Resposta inválida da API externa.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<string> FetchEntradasEstoqueGridRawJsonAsync(
        GestaoCliente cliente,
        EntradasEstoqueGridClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var codigoLoja = string.IsNullOrWhiteSpace(request.LojaId) ? "0" : request.LojaId.Trim();
        var inicioSeq = string.IsNullOrWhiteSpace(request.InicioSeq) ? "1" : request.InicioSeq.Trim();
        var finalSeq = string.IsNullOrWhiteSpace(request.FinalSeq) ? "99999999" : request.FinalSeq.Trim();

        var payload = new Dictionary<string, string>
        {
            ["CODIGOLOJA"] = codigoLoja,
            ["INICIOSEQ"] = inicioSeq,
            ["FINALSEQ"] = finalSeq
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SavwinEntradasEstoqueGridUrl);
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
        httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(2);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar Savwin EntradasEstoqueGrid");
            throw new InvalidOperationException("Falha ao contatar a API externa.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Savwin EntradasEstoqueGrid HTTP {Status}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Savwin HTTP {(int)response.StatusCode}");
        }

        return body;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProdutosCadastradosGridItem>> FetchProdutosCadastradosGridAsync(
        GestaoCliente cliente,
        EntradasEstoqueGridClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var codigoLoja = string.IsNullOrWhiteSpace(request.LojaId) ? "0" : request.LojaId.Trim();
        var inicioSeq = string.IsNullOrWhiteSpace(request.InicioSeq) ? "1" : request.InicioSeq.Trim();
        var finalSeq = string.IsNullOrWhiteSpace(request.FinalSeq) ? "99999999" : request.FinalSeq.Trim();

        var payload = new Dictionary<string, string>
        {
            ["CODIGOLOJA"] = codigoLoja,
            ["INICIOSEQ"] = inicioSeq,
            ["FINALSEQ"] = finalSeq
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SavwinProdutosCadastradosGridUrl);
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
        httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(3);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar Savwin ProdutosCadastradosGrid");
            throw new InvalidOperationException("Falha ao contatar a API externa.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Savwin ProdutosCadastradosGrid HTTP {Status}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"Savwin HTTP {(int)response.StatusCode}");
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = JsonSerializer.Deserialize<List<ProdutosCadastradosGridItem>>(body, options);
                return list ?? (IReadOnlyList<ProdutosCadastradosGridItem>)Array.Empty<ProdutosCadastradosGridItem>();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                var one = JsonSerializer.Deserialize<ProdutosCadastradosGridItem>(body, options);
                return one is null
                    ? Array.Empty<ProdutosCadastradosGridItem>()
                    : new[] { one };
            }

            return Array.Empty<ProdutosCadastradosGridItem>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON inválido Savwin ProdutosCadastradosGrid: {Body}", body);
            throw new InvalidOperationException("Resposta inválida da API externa.", ex);
        }
    }

    /// <inheritdoc />
    public Task<string> FetchContasPagarPagasGridRawJsonAsync(
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken = default) =>
        PostContasTitulosGridAsync(SavwinContasPagarPagasGridUrl, "ContasPagarPagasGrid", cliente, request, cancellationToken);

    /// <inheritdoc />
    public Task<string> FetchContasReceberRecebidasGridRawJsonAsync(
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken = default) =>
        PostContasTitulosGridAsync(SavwinContasReceberRecebidasGridUrl, "ContasReceberRecebidasGrid", cliente, request, cancellationToken);

    private async Task<string> PostContasTitulosGridAsync(
        string savwinUrl,
        string logName,
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken)
    {
        var filid = SavwinRelatorioParametros.BuildLojasParam(cliente, request.LojaId);
        var status = NormalizeStatusRecebido(request.StatusRecebido);
        var tipoPeriodo = string.IsNullOrWhiteSpace(request.TipoPeriodo) ? "1" : request.TipoPeriodo.Trim();

        var du1 = SavwinRelatorioParametros.ToSavwinDate(request.DuplicataEmissao1 ?? string.Empty);
        var du2 = SavwinRelatorioParametros.ToSavwinDate(request.DuplicataEmissao2 ?? string.Empty);

        var payload = new Dictionary<string, object?>
        {
            ["FILID"] = filid,
            ["STATUSRECEBIDO"] = status,
            ["DUPEMISSAO1"] = string.IsNullOrWhiteSpace(du1) ? null : du1,
            ["DUPEMISSAO2"] = string.IsNullOrWhiteSpace(du2) ? null : du2,
            ["PARVENCIMENTO1"] = SavwinRelatorioParametros.ToSavwinDateOrNull(request.ParVencimento1),
            ["PARVENCIMENTO2"] = SavwinRelatorioParametros.ToSavwinDateOrNull(request.ParVencimento2),
            ["RECRECEBIMENTO1"] = SavwinRelatorioParametros.ToSavwinDateOrNull(request.RecRecebimento1),
            ["RECRECEBIMENTO2"] = SavwinRelatorioParametros.ToSavwinDateOrNull(request.RecRecebimento2),
            ["PAGAMENTOVENDA1"] = SavwinRelatorioParametros.ToSavwinDateOrNull(request.PagamentoVenda1),
            ["PAGAMENTOVENDA2"] = SavwinRelatorioParametros.ToSavwinDateOrNull(request.PagamentoVenda2),
            ["TIPOPERIODO"] = tipoPeriodo
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, savwinUrl);
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
        httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(2);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar Savwin {LogName}", logName);
            throw new InvalidOperationException("Falha ao contatar a API externa.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Savwin {LogName} HTTP {Status}: {Body}", logName, response.StatusCode, body);
            throw new InvalidOperationException($"Savwin HTTP {(int)response.StatusCode}");
        }

        return body;
    }

    /// <summary>
    /// Garante <see cref="ProdutoPorOsSavwinDto.CodigoDaVenda"/> a partir do objeto bruto: a SavWin pode usar
    /// chave/casing diferente ou número onde o desserializador padrão não preenche a string.
    /// </summary>
    private static void AplicarCodigoDaVendaDoJsonNaLinha(ProdutoPorOsSavwinDto linha, JsonElement objetoLinha)
    {
        var doJson = ExtrairCodigoDaVendaDoObjetoJson(objetoLinha);
        if (string.IsNullOrWhiteSpace(doJson))
        {
            return;
        }

        linha.CodigoDaVenda = doJson.Trim();
    }

    /// <summary>Localiza propriedade equivalente a CODIGODAVENDA (vários formatos de nome) e retorna texto.</summary>
    private static string? ExtrairCodigoDaVendaDoObjetoJson(JsonElement obj)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var prop in obj.EnumerateObject())
        {
            if (IsNomePropriedadeCodigoDaVenda(prop.Name))
            {
                return JsonValorParaStringCodigo(prop.Value);
            }
        }

        return null;
    }

    /// <summary>Cobre CODIGODAVENDA, CodigoDaVenda, CODIGO_DA_VENDA, etc.</summary>
    private static bool IsNomePropriedadeCodigoDaVenda(string name)
    {
        var n = new string(name.Trim().Where(c => !char.IsWhiteSpace(c) && c != '_').ToArray())
            .ToUpperInvariant();
        return n == "CODIGODAVENDA";
    }

    private static string? JsonValorParaStringCodigo(JsonElement v)
    {
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            _ => null
        };
    }

    private static string NormalizeStatusRecebido(string? value)
    {
        var u = (value ?? string.Empty).Trim().ToUpperInvariant();
        return u is "ABERTO" or "BAIXADO" or "TODOS" ? u : "TODOS";
    }
}

/// <summary>Parâmetros de data/loja alinhados ao controller antigo.</summary>
internal static class SavwinRelatorioParametros
{
    public static string LojaParametroUnico(GestaoCliente entity, string? lojaId)
    {
        if (!string.IsNullOrWhiteSpace(lojaId))
        {
            return lojaId.Trim();
        }

        var first = entity.Lojas?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return first ?? string.Empty;
    }

    public static string BuildLojasParam(GestaoCliente entity, string? lojaId)
    {
        if (!string.IsNullOrWhiteSpace(lojaId))
        {
            return lojaId.Trim();
        }

        return entity.Lojas?.Trim() ?? string.Empty;
    }

    public static string ToSavwinDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var v = value.Trim();
        if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        if (DateTime.TryParseExact(v, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
        {
            return d2.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        return v;
    }

    public static string? ToSavwinDateOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var s = ToSavwinDate(value);
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
