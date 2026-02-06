using Api.BoundedContexts.Administration.Application.Interfaces;
using Api.Infrastructure;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.Administration.Infrastructure.External;

public sealed class OpenRouterService : IOpenRouterService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterService> _logger;

#pragma warning disable S1075 // URIs should not be hardcoded - OpenRouter base URL is a legitimate external service constant
    private const string DefaultBaseUrl = "https://openrouter.ai/api/v1/";
#pragma warning restore S1075

    public OpenRouterService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenRouterService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient = httpClient;

        // Use SecretsHelper to read from environment variable (loaded from openrouter.secret)
        var apiKey = SecretsHelper.GetSecretOrValue(configuration, "OPENROUTER_API_KEY", logger, required: true)
            ?? throw new InvalidOperationException("OPENROUTER_API_KEY not configured");

        _httpClient.BaseAddress = new Uri(DefaultBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<OpenRouterBalanceResponse> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("auth/key", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<KeyInfoResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);

        if (data?.Data == null)
            throw new InvalidOperationException("Invalid OpenRouter response");

        return new OpenRouterBalanceResponse(
            (decimal)(data.Data.Limit - data.Data.Usage),
            (decimal)data.Data.Limit,
            "USD",
            DateTime.UtcNow);
    }

    public async Task AddCreditsAsync(decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("AddCredits called but not supported by OpenRouter API. Amount: {Amount} {Currency}", amount, currency);
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException("Adding credits via API not supported. Use OpenRouter dashboard.");
    }

    private sealed record KeyInfoResponse([property: JsonPropertyName("data")] KeyData? Data);
    private sealed record KeyData(
        [property: JsonPropertyName("limit")] double Limit,
        [property: JsonPropertyName("usage")] double Usage);
}
