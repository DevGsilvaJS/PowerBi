using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PowerBi.Server.DTOs;
using PowerBi.Server.Entities;
using Microsoft.Extensions.Caching.Memory;
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
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SavwinRelatoriosClient> _logger;

    public SavwinRelatoriosClient(
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<SavwinRelatoriosClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _memoryCache = memoryCache;
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
        var swHttp = Stopwatch.StartNew();
        try
        {
            response = await client.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            swHttp.Stop();
            _logger.LogError(ex, "Falha ao chamar Savwin ProdutosPorOS (HTTP {HttpMs}ms)", swHttp.ElapsedMilliseconds);
            throw new InvalidOperationException("Falha ao contatar a API externa.", ex);
        }

        swHttp.Stop();
        var swRead = Stopwatch.StartNew();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        swRead.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Savwin ProdutosPorOS HTTP {Status} após HTTP {HttpMs}ms, leitura corpo {ReadMs}ms: {Body}",
                response.StatusCode,
                swHttp.ElapsedMilliseconds,
                swRead.ElapsedMilliseconds,
                body);
            throw new InvalidOperationException($"Savwin HTTP {(int)response.StatusCode}");
        }

        try
        {
            var swParse = Stopwatch.StartNew();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            IReadOnlyList<ProdutoPorOsItem> result;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = JsonSerializer.Deserialize<List<ProdutoPorOsSavwinDto>>(body, options);
                if (list is null)
                {
                    result = Array.Empty<ProdutoPorOsItem>();
                }
                else
                {
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

                    result = ProdutoPorOsSavwinMapping.ToApiItems(list);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                var one = JsonSerializer.Deserialize<ProdutoPorOsSavwinDto>(body, options);
                if (one is null)
                {
                    result = Array.Empty<ProdutoPorOsItem>();
                }
                else
                {
                    AplicarCodigoDaVendaDoJsonNaLinha(one, root);
                    result = new[] { ProdutoPorOsSavwinMapping.ToApiItem(one) };
                }
            }
            else
            {
                result = Array.Empty<ProdutoPorOsItem>();
            }

            swParse.Stop();
            _logger.LogInformation(
                "Savwin ProdutosPorOS [externo]: HTTP {HttpMs}ms, leitura corpo {ReadMs}ms ({BodyBytes} bytes), parse/mapeamento {ParseMs}ms, itens {Count}",
                swHttp.ElapsedMilliseconds,
                swRead.ElapsedMilliseconds,
                body.Length,
                swParse.ElapsedMilliseconds,
                result.Count);

            return result;
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
        FetchContasTitulosGridCachedAsync(
            "contas_pagar",
            SavwinContasPagarPagasGridUrl,
            "ContasPagarPagasGrid",
            cliente,
            request,
            cancellationToken);

    /// <inheritdoc />
    public Task<string> FetchContasReceberRecebidasGridRawJsonAsync(
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken = default) =>
        FetchContasTitulosGridCachedAsync(
            "contas_receber",
            SavwinContasReceberRecebidasGridUrl,
            "ContasReceberRecebidasGrid",
            cliente,
            request,
            cancellationToken);

    /// <summary>
    /// Cache em memória (10 min) por cliente + hash do payload enviado à SavWin — separado entre pagar e receber.
    /// </summary>
    private async Task<string> FetchContasTitulosGridCachedAsync(
        string cacheKeyPrefix,
        string savwinUrl,
        string logName,
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken)
    {
        var payloadJson = BuildContasTitulosGridPayloadJson(cliente, request);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        var cacheKey = $"{cacheKeyPrefix}:{cliente.Id}:{hash}";

        return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await PostContasTitulosGridAsync(savwinUrl, logName, cliente, request, cancellationToken);
        }) ?? throw new InvalidOperationException("Cache retornou resultado inesperado.");
    }

    private static string BuildContasTitulosGridPayloadJson(
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request)
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

        return JsonSerializer.Serialize(payload);
    }

    private async Task<string> PostContasTitulosGridAsync(
        string savwinUrl,
        string logName,
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken)
    {
        var json = BuildContasTitulosGridPayloadJson(cliente, request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, savwinUrl);
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
        httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var http = _httpClientFactory.CreateClient();
        // Contas em grade podem demorar; 502 no cliente costuma ser timeout ou corpo grande.
        http.Timeout = TimeSpan.FromMinutes(5);

        HttpResponseMessage response;
        var swHttp = Stopwatch.StartNew();
        try
        {
            response = await http.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            swHttp.Stop();
            _logger.LogError(ex, "Falha ao chamar Savwin {LogName} (HTTP {HttpMs}ms)", logName, swHttp.ElapsedMilliseconds);
            throw new InvalidOperationException("Falha ao contatar a API externa.", ex);
        }

        swHttp.Stop();
        var swRead = Stopwatch.StartNew();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        swRead.Stop();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Savwin {LogName} HTTP {Status} após HTTP {HttpMs}ms, leitura corpo {ReadMs}ms: {Body}",
                logName,
                response.StatusCode,
                swHttp.ElapsedMilliseconds,
                swRead.ElapsedMilliseconds,
                body);
            var detalhe = body.Length > 500 ? body[..500] + "…" : body;
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detalhe)
                    ? $"SavWin retornou HTTP {(int)response.StatusCode}."
                    : $"SavWin HTTP {(int)response.StatusCode}: {detalhe}");
        }

        _logger.LogInformation(
            "Savwin {LogName} OK: HTTP {HttpMs}ms, leitura corpo {ReadMs}ms, {Bytes} bytes",
            logName,
            swHttp.ElapsedMilliseconds,
            swRead.ElapsedMilliseconds,
            body.Length);

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
