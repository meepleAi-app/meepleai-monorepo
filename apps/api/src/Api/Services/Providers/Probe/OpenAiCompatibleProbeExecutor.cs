using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Microsoft.Extensions.Logging;

namespace Api.Services.Providers.Probe;

/// <summary>
/// Generic OpenAI-compatible probe executor.
/// Works against any endpoint exposing a /v1/models list (Bearer auth optional):
/// OpenRouter, DeepSeek, OpenAI cloud, Ollama (via /v1 compat layer), LocalAI, vLLM, LM Studio, etc.
///
/// Issue #936 (G1): list-models probe — zero quota cost.
/// </summary>
internal sealed class OpenAiCompatibleProbeExecutor : IProviderProbeExecutor
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiCompatibleProbeExecutor> _logger;
    private readonly string _listModelsUrl;
    private readonly bool _requiresAuth;

    public string ProviderName { get; }
    public string? ApiKeyEnvVar { get; }

    public OpenAiCompatibleProbeExecutor(
        IHttpClientFactory httpClientFactory,
        ILogger<OpenAiCompatibleProbeExecutor> logger,
        string providerName,
        string baseUrl,
        bool requiresAuth,
        string? apiKeyEnvVar)
    {
        _httpClient = httpClientFactory.CreateClient("provider-probe");
        _logger = logger;
        _listModelsUrl = $"{baseUrl.TrimEnd('/')}/models";
        _requiresAuth = requiresAuth;
        ProviderName = providerName;
        ApiKeyEnvVar = apiKeyEnvVar;
    }

    public async Task<ProbeExecutionResult> ExecuteAsync(string apiKey, string? expectedModel, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _listModelsUrl);
            if (_requiresAuth || !string.IsNullOrEmpty(apiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", string.IsNullOrEmpty(apiKey) ? "n/a" : apiKey);

            using var resp = await _httpClient.SendAsync(req, cts.Token).ConfigureAwait(false);
            sw.Stop();

            if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return new ProbeExecutionResult(ProbeOutcome.Unauthorized, "unauthorized", "Provider rejected token", (int)sw.ElapsedMilliseconds, null);

            if (!resp.IsSuccessStatusCode)
                return new ProbeExecutionResult(ProbeOutcome.UnknownError, "http_" + (int)resp.StatusCode, $"HTTP {resp.StatusCode}", (int)sw.ElapsedMilliseconds, null);

            var payload = await resp.Content.ReadFromJsonAsync<ModelListResponse>(cancellationToken: cts.Token).ConfigureAwait(false);

            // Authentication succeeded (provider returned 200 from list-models).
            // Model availability is informational only when caller specified expectedModel.
            bool? modelAvailable = expectedModel is null
                ? null
                : (payload?.Data?.Any(m => string.Equals(m.Id, expectedModel, StringComparison.Ordinal)) ?? false);

            return new ProbeExecutionResult(ProbeOutcome.Success, null, null, (int)sw.ElapsedMilliseconds, modelAvailable);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new ProbeExecutionResult(ProbeOutcome.Timeout, "timeout", "Probe exceeded 5s", (int)sw.ElapsedMilliseconds, null);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "{Provider} probe network failure", ProviderName);
            return new ProbeExecutionResult(ProbeOutcome.Unreachable, "unreachable", "Network error", (int)sw.ElapsedMilliseconds, null);
        }
    }

    private sealed record ModelListResponse([property: JsonPropertyName("data")] List<ModelEntry>? Data);
    private sealed record ModelEntry([property: JsonPropertyName("id")] string Id);
}
