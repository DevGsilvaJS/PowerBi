using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
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

    private const string SavwinVendaFormaPagamentoResumoUrl =
        "https://cliapi.savwinweb.com.br/api/APIRelatoriosCR/APIVendaFormaPagamentoResumo";

    private const string SavwinEntradasEstoqueGridUrl =
        "https://api.savwinweb.com.br/api/APIRelatoriosCR/EntradasEstoqueGrid";

    private const string SavwinContasPagarPagasGridUrl =
        "https://api.savwinweb.com.br/api/APIRelatoriosCR/ContasPagarPagasGrid";

    private const string SavwinContasReceberRecebidasGridUrl =
        "https://api.savwinweb.com.br/api/APIRelatoriosCR/ContasReceberRecebidasGrid";

    private const string SavwinProdutosCadastradosGridUrl =
        "https://api.savwinweb.com.br/api/APIRelatoriosCR/ProdutosCadastradosGrid";

    private const string SavwinListaLojasUrl = "https://api.savwinweb.com.br/api/APILojas/RetornaLista";

    private const string SavwinDemonstrativoVendaPorClienteUrl =
        "http://cliapi.savwinweb.com.br/api/APIRelatoriosCR/APIDemonstrativoVendaPorCliente";

    private const string SavwinClientesRetornaListaUrl = "https://api.savwinweb.com.br/api/APIClientes/RetornaLista";

    private const string SavwinVendasPendentesCompletasUrl =
        "http://cliapi.savwinweb.com.br/api/APIDados/RetornaVendasPendentesCompletas";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SavwinRelatoriosClient> _logger;

    /// <summary>Como montar <c>FILID</c> no JSON enviado às grids de títulos (pagar vs receber diferem na SavWin).</summary>
    private enum ContasTitulosFilidResolucao
    {
        /// <summary>FILID = token do request/cadastro (<see cref="SavwinRelatorioParametros.BuildLojasParam"/>).</summary>
        ParametroDireto,
        /// <summary>FILID = Id interno de <c>RetornaLista</c> — <c>ContasPagarPagasGrid</c>.</summary>
        RetornaListaIdInterno,
        /// <summary>FILID = código de loja (FILSEQUENTIAL) — <c>ContasReceberRecebidasGrid</c>.</summary>
        RetornaListaCodigoLoja
    }

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
    public async Task<IReadOnlyList<VendaFormaPagamentoResumoItemDto>> FetchVendaFormaPagamentoResumoAsync(
        GestaoCliente cliente,
        ProdutosPorOsClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var dataIni = (request.DataInicial ?? string.Empty).Trim();
        var dataFim = (request.DataFinal ?? string.Empty).Trim();
        var lojas = SavwinRelatorioParametros.BuildLojasParam(cliente, request.LojaId);

        var payload = new Dictionary<string, string>
        {
            ["DATAINICIAL"] = dataIni,
            ["DATAFINAL"] = dataFim,
            ["LOJAS"] = lojas
        };

        var json = JsonSerializer.Serialize(payload);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SavwinVendaFormaPagamentoResumoUrl);
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
        httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(2);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar Savwin APIVendaFormaPagamentoResumo");
            throw new InvalidOperationException("Falha ao contatar a API externa.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("SavWin APIVendaFormaPagamentoResumo HTTP {Status}: {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"SavWin HTTP {(int)response.StatusCode}");
        }

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var list = JsonSerializer.Deserialize<List<VendaFormaPagamentoResumoItemDto>>(body, options);
                return list ?? new List<VendaFormaPagamentoResumoItemDto>();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                var one = JsonSerializer.Deserialize<VendaFormaPagamentoResumoItemDto>(body, options);
                return one is null
                    ? Array.Empty<VendaFormaPagamentoResumoItemDto>()
                    : new[] { one };
            }

            return Array.Empty<VendaFormaPagamentoResumoItemDto>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON inválido SavWin APIVendaFormaPagamentoResumo: {Body}", body);
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
            // Evita bufferizar o corpo inteiro em SendAsync (respostas muito grandes → OOM).
            response = await http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar Savwin ProdutosCadastradosGrid");
            throw new InvalidOperationException("Falha ao contatar a API externa.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errSnippet = await ReadHttpContentSnippetAsync(response.Content, 4096, cancellationToken);
                _logger.LogWarning("Savwin ProdutosCadastradosGrid HTTP {Status}: {Body}", response.StatusCode, errSnippet);
                throw new InvalidOperationException($"Savwin HTTP {(int)response.StatusCode}");
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                return await DeserializeProdutosCadastradosGridFromStreamAsync(stream, cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON inválido Savwin ProdutosCadastradosGrid (stream)");
                throw new InvalidOperationException("Resposta inválida da API externa.", ex);
            }
        }
    }

    /// <summary>
    /// Desserializa resposta JSON (array ou um objeto) sem <see cref="ReadAsStringAsync"/> nem
    /// <see cref="JsonDocument.Parse(string)"/> no corpo inteiro; arrays usam
    /// <see cref="JsonSerializer.DeserializeAsyncEnumerable{TValue}"/> para reduzir pico de memória.
    /// </summary>
    private static async Task<IReadOnlyList<ProdutosCadastradosGridItem>> DeserializeProdutosCadastradosGridFromStreamAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var first = await ReadFirstNonWhitespaceByteAsync(stream, cancellationToken);
        if (first is null)
        {
            return Array.Empty<ProdutosCadastradosGridItem>();
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        await using var replay = new PrependByteStream(first.Value, stream);

        if (first.Value == (byte)'[')
        {
            var list = new List<ProdutosCadastradosGridItem>(capacity: 8192);
            await foreach (
                var item in JsonSerializer.DeserializeAsyncEnumerable<ProdutosCadastradosGridItem>(
                    replay,
                    options,
                    cancellationToken))
            {
                if (item is not null)
                {
                    list.Add(item);
                }
            }

            return list;
        }

        if (first.Value == (byte)'{')
        {
            var one = await JsonSerializer.DeserializeAsync<ProdutosCadastradosGridItem>(replay, options, cancellationToken);
            return one is null
                ? Array.Empty<ProdutosCadastradosGridItem>()
                : new[] { one };
        }

        throw new JsonException($"Token JSON inicial inesperado (0x{first.Value:X2}).");
    }

    private static async Task<byte?> ReadFirstNonWhitespaceByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        while (true)
        {
            var n = await stream.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                return null;
            }

            var b = buffer[0];
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return b;
        }
    }

    private static async Task<string> ReadHttpContentSnippetAsync(
        HttpContent content,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        await using var s = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var ms = new MemoryStream();
        var buffer = new byte[4096];
        var total = 0;
        while (total < maxBytes)
        {
            var toRead = Math.Min(buffer.Length, maxBytes - total);
            var n = await s.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
            if (n == 0)
            {
                break;
            }

            await ms.WriteAsync(buffer.AsMemory(0, n), cancellationToken).ConfigureAwait(false);
            total += n;
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sem cache em memória: a grade de contas a pagar usa o mesmo contrato que a de receber, mas a SavWin responde
    /// com volumes e formatos diferentes. Cache de 10 min por hash de payload fazia repetir por muito tempo resposta vazia
    /// ou antiga após mudança de filtros no app (loja/período).
    /// </remarks>
    public Task<string> FetchContasPagarPagasGridRawJsonAsync(
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken = default) =>
        PostContasTitulosGridAsync(
            SavwinContasPagarPagasGridUrl,
            "ContasPagarPagasGrid",
            cliente,
            request,
            cancellationToken,
            ContasTitulosFilidResolucao.RetornaListaIdInterno);

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
        // Contas a receber: SavWin espera em FILID o código de loja (FILSEQUENTIAL), não o Id interno — ver ContasTitulosFilidResolucao.
        var payloadJson = await BuildContasTitulosGridPayloadJsonAsync(cliente, request, cancellationToken, ContasTitulosFilidResolucao.RetornaListaCodigoLoja).ConfigureAwait(false);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
        var cacheKey = $"{cacheKeyPrefix}:{cliente.Id}:{hash}";

        return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await PostContasTitulosGridComJsonAsync(savwinUrl, logName, cliente, payloadJson, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false) ?? throw new InvalidOperationException("Cache retornou resultado inesperado.");
    }

    private async Task<string> BuildContasTitulosGridPayloadJsonAsync(
        GestaoCliente cliente,
        ContasPagarPagasGridClientRequest request,
        CancellationToken cancellationToken,
        ContasTitulosFilidResolucao resolucaoFilid)
    {
        string filid;
        switch (resolucaoFilid)
        {
            case ContasTitulosFilidResolucao.ParametroDireto:
                filid = SavwinRelatorioParametros.BuildLojasParam(cliente, request.LojaId);
                break;
            case ContasTitulosFilidResolucao.RetornaListaIdInterno:
                // Contas a pagar: RetornaLista → Id interno em FILID (não o FILSEQUENTIAL).
                filid = await ObterFilidDeRetornaListaAsync(cliente, request.LojaId, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("SavWin ContasTitulosGrid: RetornaLista → FILID (Id) = {Filid}", filid);
                break;
            case ContasTitulosFilidResolucao.RetornaListaCodigoLoja:
                // Contas a receber: RetornaLista → código de loja (FILSEQUENTIAL) em FILID.
                filid = await ObterCodigoDeRetornaListaAsync(cliente, request.LojaId, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("SavWin ContasTitulosGrid: RetornaLista → FILID (Código) = {Filid}", filid);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(resolucaoFilid), resolucaoFilid, null);
        }
        var status = NormalizeStatusRecebido(request.StatusRecebido);
        var tipoPeriodo = string.IsNullOrWhiteSpace(request.TipoPeriodo) ? "1" : request.TipoPeriodo.Trim();

        var du1 = SavwinRelatorioParametros.ToSavwinDate(request.DuplicataEmissao1 ?? string.Empty);
        var du2 = SavwinRelatorioParametros.ToSavwinDate(request.DuplicataEmissao2 ?? string.Empty);

        // Mesmas chaves que a SavWin documenta (ex.: FILID + STATUSRECEBIDO + DUPEMISSAO nulos + PAGAMENTOVENDA1/2 + TIPOPERIODO).
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
        CancellationToken cancellationToken,
        ContasTitulosFilidResolucao resolucaoFilid)
    {
        var json = await BuildContasTitulosGridPayloadJsonAsync(cliente, request, cancellationToken, resolucaoFilid).ConfigureAwait(false);
        return await PostContasTitulosGridComJsonAsync(savwinUrl, logName, cliente, json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> PostContasTitulosGridComJsonAsync(
        string savwinUrl,
        string logName,
        GestaoCliente cliente,
        string json,
        CancellationToken cancellationToken)
    {

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

    /// <inheritdoc />
    public async Task<IReadOnlyList<SavwinLojaListaItemDto>> FetchListaLojasAsync(
        GestaoCliente cliente,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SavwinListaLojasUrl);
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
        httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
        httpRequest.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(1);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar SavWin RetornaLista");
            throw new InvalidOperationException("Falha ao contatar a API de lojas SavWin.", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var detalhe = body.Length > 500 ? body[..500] + "…" : body;
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detalhe)
                    ? $"SavWin RetornaLista HTTP {(int)response.StatusCode}."
                    : $"SavWin RetornaLista HTTP {(int)response.StatusCode}: {detalhe}");
        }

        var list = ParseListaLojasSavwinJson(body);
        _logger.LogInformation("SavWin RetornaLista: {Count} lojas, {Bytes} bytes", list.Count, body.Length);
        return list;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DemonstrativoVendaPorClienteSavwinDto>> FetchDemonstrativoVendaPorClienteAsync(
        GestaoCliente cliente,
        ProdutosPorOsClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var dataInicio = SavwinRelatorioParametros.ToSavwinDate(request.DataInicial);
        var dataFim = SavwinRelatorioParametros.ToSavwinDate(request.DataFinal);
        var lojas = ExpandTokensLoja(cliente, request.LojaId);
        if (lojas.Count == 0)
        {
            return Array.Empty<DemonstrativoVendaPorClienteSavwinDto>();
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var acumulado = new List<DemonstrativoVendaPorClienteSavwinDto>();
        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(3);

        foreach (var loja in lojas)
        {
            var payload = new Dictionary<string, string>
            {
                ["DATAINICIO"] = dataInicio,
                ["DATAFIM"] = dataFim,
                ["LOJA"] = loja
            };

            var json = JsonSerializer.Serialize(payload);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SavwinDemonstrativoVendaPorClienteUrl);
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
            httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao chamar SavWin APIDemonstrativoVendaPorCliente (LOJA={Loja})", loja);
                throw new InvalidOperationException("Falha ao contatar a API externa (Demonstrativo por cliente).", ex);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var detalhe = body.Length > 500 ? body[..500] + "…" : body;
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(detalhe)
                        ? $"SavWin DemonstrativoVendaPorCliente HTTP {(int)response.StatusCode}."
                        : $"SavWin DemonstrativoVendaPorCliente HTTP {(int)response.StatusCode}: {detalhe}");
            }

            acumulado.AddRange(DeserializeDemonstrativoVendaPorCliente(body, options));
        }

        _logger.LogInformation(
            "SavWin APIDemonstrativoVendaPorCliente: {Count} linha(s) total, {Lojas} loja(s)",
            acumulado.Count,
            lojas.Count);

        return acumulado;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClienteRetornaListaSavwinDto>> FetchClientesRetornaListaAsync(
        GestaoCliente cliente,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SavwinClientesRetornaListaUrl);
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
        httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
        // Mesmo padrão de POST APILojas/RetornaLista: corpo vazio (JSON content-type).
        httpRequest.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(3);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao chamar SavWin APIClientes/RetornaLista");
            throw new InvalidOperationException("Falha ao contatar a API externa (cadastro de clientes).", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var detalhe = body.Length > 500 ? body[..500] + "…" : body;
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detalhe)
                    ? $"SavWin APIClientes/RetornaLista HTTP {(int)response.StatusCode}."
                    : $"SavWin APIClientes/RetornaLista HTTP {(int)response.StatusCode}: {detalhe}");
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var list = DeserializeClientesRetornaLista(body, options);
        _logger.LogInformation("SavWin APIClientes/RetornaLista: {Count} cliente(s), {Bytes} bytes", list.Count, body.Length);
        return list;
    }

    /// <inheritdoc />
    public async Task<int> CountVendasPendentesEntregaAsync(
        GestaoCliente cliente,
        ProdutosPorOsClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var dataIni = SavwinRelatorioParametros.ToSavwinDate(request.DataInicial);
        var dataFim = SavwinRelatorioParametros.ToSavwinDate(request.DataFinal);
        var lojas = ExpandTokensLoja(cliente, request.LojaId);
        if (lojas.Count == 0)
        {
            return 0;
        }

        IReadOnlyList<SavwinLojaListaItemDto> lista;
        try
        {
            lista = await ObterListaLojasSavwinComCacheAsync(cliente, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RetornaLista indisponível ao resolver CODIGOLOJA para pendentes; usando token do filtro.");
            lista = Array.Empty<SavwinLojaListaItemDto>();
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromMinutes(2);
        var total = 0;

        foreach (var token in lojas)
        {
            var codigoLoja = ResolverCodigoLojaParaApi(token, lista);
            var payload = new VendasPendentesCompletasSavwinPayload
            {
                CODIGOLOJA = codigoLoja,
                DATAINICIAL = dataIni,
                DATAFINAL = dataFim,
                CPFCLIENTEPAGADOR = null,
                CODIGOVENDA = null
            };

            var json = JsonSerializer.Serialize(payload, options);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, SavwinVendasPendentesCompletasUrl);
            httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cliente.ChaveWs}");
            httpRequest.Headers.TryAddWithoutValidation("Identificador", cliente.Identificador);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
            httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha HTTP RetornaVendasPendentesCompletas (CODIGOLOJA={Codigo})", codigoLoja);
                throw new InvalidOperationException("Falha ao contatar a API externa (pendentes de entrega).", ex);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var detalhe = body.Length > 500 ? body[..500] + "…" : body;
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(detalhe)
                        ? $"SavWin RetornaVendasPendentesCompletas HTTP {(int)response.StatusCode}."
                        : $"SavWin RetornaVendasPendentesCompletas HTTP {(int)response.StatusCode}: {detalhe}");
            }

            total += CountJsonArrayElementsRoot(body);
        }

        _logger.LogInformation(
            "SavWin RetornaVendasPendentesCompletas: total {Count} linha(s), {Lojas} loja(s)",
            total,
            lojas.Count);

        return total;
    }

    private static string ResolverCodigoLojaParaApi(string token, IReadOnlyList<SavwinLojaListaItemDto> lista)
    {
        if (TryMatchItemListaLoja(token, lista, out var item) && item is not null)
        {
            var cod = item.Codigo?.Trim();
            if (!string.IsNullOrEmpty(cod))
            {
                return cod;
            }

            var id = item.Id?.Trim();
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }
        }

        return token.Trim();
    }

    private static int CountJsonArrayElementsRoot(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                return root.GetArrayLength();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in root.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Array)
                    {
                        return p.Value.GetArrayLength();
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("JSON inválido em RetornaVendasPendentesCompletas.", ex);
        }

        return 0;
    }

    private sealed class VendasPendentesCompletasSavwinPayload
    {
        [JsonPropertyName("CODIGOLOJA")]
        public string? CODIGOLOJA { get; set; }

        [JsonPropertyName("DATAINICIAL")]
        public string? DATAINICIAL { get; set; }

        [JsonPropertyName("DATAFINAL")]
        public string? DATAFINAL { get; set; }

        [JsonPropertyName("CPFCLIENTEPAGADOR")]
        public string? CPFCLIENTEPAGADOR { get; set; }

        [JsonPropertyName("CODIGOVENDA")]
        public string? CODIGOVENDA { get; set; }
    }

    private static List<string> ExpandTokensLoja(GestaoCliente cliente, string? lojaId)
    {
        var bruto = SavwinRelatorioParametros.BuildLojasParam(cliente, lojaId);
        if (string.IsNullOrWhiteSpace(bruto))
        {
            return new List<string>();
        }

        return bruto
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<DemonstrativoVendaPorClienteSavwinDto> DeserializeDemonstrativoVendaPorCliente(
        string body,
        JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new List<DemonstrativoVendaPorClienteSavwinDto>();
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<DemonstrativoVendaPorClienteSavwinDto>>(body, options)
                   ?? new List<DemonstrativoVendaPorClienteSavwinDto>();
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            var one = JsonSerializer.Deserialize<DemonstrativoVendaPorClienteSavwinDto>(body, options);
            return one is null
                ? new List<DemonstrativoVendaPorClienteSavwinDto>()
                : new List<DemonstrativoVendaPorClienteSavwinDto> { one };
        }

        return new List<DemonstrativoVendaPorClienteSavwinDto>();
    }

    private static List<ClienteRetornaListaSavwinDto> DeserializeClientesRetornaLista(
        string body,
        JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new List<ClienteRetornaListaSavwinDto>();
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<ClienteRetornaListaSavwinDto>>(body, options)
                   ?? new List<ClienteRetornaListaSavwinDto>();
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            var one = JsonSerializer.Deserialize<ClienteRetornaListaSavwinDto>(body, options);
            return one is null
                ? new List<ClienteRetornaListaSavwinDto>()
                : new List<ClienteRetornaListaSavwinDto> { one };
        }

        return new List<ClienteRetornaListaSavwinDto>();
    }

    private static List<SavwinLojaListaItemDto> ParseListaLojasSavwinJson(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var list = new List<SavwinLojaListaItemDto>();
        foreach (var el in EnumerateArrayElements(root))
        {
            if (el.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // RetornaLista (SavWin): FILID = id interno; FILSEQUENCIAL = código de loja (cadastro do cliente).
            var id = GetSavwinStringProp(
                el,
                "FILID",
                "ID",
                "Id",
                "IDLOJA",
                "ID_LOJA",
                "IdFilial",
                "IdLoja",
                "COD_FILIAL");
            var codigo = GetSavwinStringProp(
                el,
                "FILSEQUENCIAL",
                "CODIGO",
                "Codigo",
                "CODIGOLOJA",
                "COD",
                "LOJA",
                "NRLOJA");
            if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(codigo))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                id = codigo;
            }

            if (string.IsNullOrWhiteSpace(codigo))
            {
                codigo = id;
            }

            var nome = GetSavwinStringProp(
                el,
                "PESNOME",
                "NOME",
                "Nome",
                "DESCRICAO",
                "Descricao",
                "FANTASIA",
                "NOMEFANTASIA",
                "RAZAOSOCIAL",
                "PESSOBRENOME") ?? string.Empty;
            list.Add(new SavwinLojaListaItemDto
            {
                Id = id!.Trim(),
                Codigo = codigo!.Trim(),
                Nome = nome.Trim()
            });
        }

        return list;
    }

    private static IEnumerable<JsonElement> EnumerateArrayElements(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in root.EnumerateArray())
            {
                yield return x;
            }

            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var p in root.EnumerateObject())
        {
            if (p.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var x in p.Value.EnumerateArray())
            {
                yield return x;
            }

            yield break;
        }
    }

    private static string? GetSavwinStringProp(JsonElement el, params string[] candidateNames)
    {
        foreach (var p in el.EnumerateObject())
        {
            foreach (var cn in candidateNames)
            {
                if (p.Name.Equals(cn, StringComparison.OrdinalIgnoreCase))
                {
                    return JsonElementToDisplayString(p.Value);
                }
            }
        }

        return null;
    }

    private static string JsonElementToDisplayString(JsonElement v)
    {
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString() ?? string.Empty,
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "1",
            JsonValueKind.False => "0",
            _ => string.Empty
        };
    }

    private async Task<IReadOnlyList<SavwinLojaListaItemDto>> ObterListaLojasSavwinComCacheAsync(
        GestaoCliente cliente,
        CancellationToken cancellationToken)
    {
        var key = $"savwin:RetornaLista:{cliente.Id}";
        if (_memoryCache.TryGetValue(key, out IReadOnlyList<SavwinLojaListaItemDto>? cached) && cached is not null)
        {
            return cached;
        }

        var list = await FetchListaLojasAsync(cliente, cancellationToken).ConfigureAwait(false);
        _memoryCache.Set(key, list, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
        return list;
    }

    private static string NormalizarTokenLoja(string s)
    {
        var t = s.Trim();
        if (t.Length == 0)
        {
            return t;
        }

        if (t.All(static c => c >= '0' && c <= '9'))
        {
            if (long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                return n.ToString(CultureInfo.InvariantCulture);
            }
        }

        return t.ToUpperInvariant();
    }

    private static bool TryMatchItemListaLoja(
        string token,
        IReadOnlyList<SavwinLojaListaItemDto> lista,
        out SavwinLojaListaItemDto? item)
    {
        item = null;
        var n = NormalizarTokenLoja(token);
        if (n.Length == 0)
        {
            return false;
        }

        foreach (var it in lista)
        {
            var id = it.Id?.Trim() ?? string.Empty;
            var cod = it.Codigo?.Trim() ?? string.Empty;
            if (id.Length > 0 && NormalizarTokenLoja(id) == n)
            {
                item = it;
                return true;
            }

            if (cod.Length > 0 && NormalizarTokenLoja(cod) == n)
            {
                item = it;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <para><b>1)</b> Consome a API <c>POST APILojas/RetornaLista</c> com corpo vazio (com cache).</para>
    /// <para><b>2)</b> Cada token do cadastro/request casa com <c>FILSEQUENTIAL</c> (Codigo) ou <c>FILID</c> (Id) do JSON.</para>
    /// <para><b>3)</b> Devolve só os <b>Ids</b> (<c>FILID</c> no JSON da lista) separados por vírgula — valor usado em FILID/LOJAS nas outras APIs.</para>
    /// </summary>
    private async Task<string> ObterFilidDeRetornaListaAsync(
        GestaoCliente cliente,
        string? lojaIdRequest,
        CancellationToken cancellationToken)
    {
        var bruto = SavwinRelatorioParametros.BuildLojasParam(cliente, lojaIdRequest);
        var lista = await ObterListaLojasSavwinComCacheAsync(cliente, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "RetornaLista: {Count} loja(s); tokens cadastro/request = {Bruto}",
            lista.Count,
            string.IsNullOrEmpty(bruto) ? "(vazio)" : bruto);
        if (lista.Count == 0)
        {
            throw new InvalidOperationException(
                "POST APILojas/RetornaLista não retornou nenhuma loja (corpo vazio ou JSON não reconhecido). " +
                "Sem FILID não é possível montar ContasPagarPagasGrid.");
        }

        var partes = bruto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (partes.Length == 0)
        {
            return bruto;
        }

        var sb = new StringBuilder();
        foreach (var p in partes)
        {
            if (TryMatchItemListaLoja(p, lista, out var it) && it is not null && !string.IsNullOrWhiteSpace(it.Id))
            {
                if (sb.Length > 0)
                {
                    sb.Append(',');
                }

                sb.Append(it.Id.Trim());
            }
            else
            {
                throw new InvalidOperationException(
                    $"Loja '{p}' não foi encontrada em POST APILojas/RetornaLista (cruzar FILSEQUENTIAL/cadastro com a lista) ou item sem FILID.");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Igual a <see cref="ObterFilidDeRetornaListaAsync"/>, mas devolve os <b>códigos</b> de loja (<c>Codigo</c> / FILSEQUENTIAL)
    /// para uso em <c>FILID</c> na API <c>ContasReceberRecebidasGrid</c>.
    /// </summary>
    private async Task<string> ObterCodigoDeRetornaListaAsync(
        GestaoCliente cliente,
        string? lojaIdRequest,
        CancellationToken cancellationToken)
    {
        var bruto = SavwinRelatorioParametros.BuildLojasParam(cliente, lojaIdRequest);
        var lista = await ObterListaLojasSavwinComCacheAsync(cliente, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug(
            "RetornaLista (código FILID): {Count} loja(s); tokens cadastro/request = {Bruto}",
            lista.Count,
            string.IsNullOrEmpty(bruto) ? "(vazio)" : bruto);
        if (lista.Count == 0)
        {
            throw new InvalidOperationException(
                "POST APILojas/RetornaLista não retornou nenhuma loja (corpo vazio ou JSON não reconhecido). " +
                "Sem código de loja não é possível montar ContasReceberRecebidasGrid.");
        }

        var partes = bruto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (partes.Length == 0)
        {
            return bruto;
        }

        var sb = new StringBuilder();
        foreach (var p in partes)
        {
            if (TryMatchItemListaLoja(p, lista, out var it) && it is not null && !string.IsNullOrWhiteSpace(it.Codigo))
            {
                if (sb.Length > 0)
                {
                    sb.Append(',');
                }

                sb.Append(it.Codigo.Trim());
            }
            else
            {
                throw new InvalidOperationException(
                    $"Loja '{p}' não foi encontrada em POST APILojas/RetornaLista (cruzar FILSEQUENTIAL/cadastro com a lista) ou item sem código de loja.");
            }
        }

        return sb.ToString();
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
