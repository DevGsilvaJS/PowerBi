namespace PowerBi.Server.Services.Integracoes;

/// <summary>
/// Serviço base para consumo de APIs externas via <see cref="HttpClient"/>.
/// </summary>
public class IntegracaoApiService : IIntegracaoApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IntegracaoApiService> _logger;

    public IntegracaoApiService(HttpClient httpClient, ILogger<IntegracaoApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Implementar chamadas quando os endpoints estiverem definidos.
}
