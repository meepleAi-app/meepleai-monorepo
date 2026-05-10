using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Services.Providers.Probe;

internal sealed class DeepSeekProbeExecutor : IProviderProbeExecutor
{
#pragma warning disable S1075 // URIs should not be hardcoded - Default fallback for DeepSeek public API
    private const string DefaultListModelsUrl = "https://api.deepseek.com/v1/models";
#pragma warning restore S1075
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeepSeekProbeExecutor> _logger;
    private readonly string _listModelsUrl;

    public string ProviderName => "deepseek";

    public DeepSeekProbeExecutor(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DeepSeekProbeExecutor> logger)
    {
        _httpClient = httpClientFactory.CreateClient("provider-probe");
        _logger = logger;
        _listModelsUrl = configuration["Providers:DeepSeek:ListModelsUrl"] ?? DefaultListModelsUrl;
    }

    public async Task<ProbeExecutionResult> ExecuteAsync(string apiKey, string? expectedModel, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _listModelsUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var resp = await _httpClient.SendAsync(req, cts.Token).ConfigureAwait(false);
            sw.Stop();

            if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return new ProbeExecutionResult(ProbeOutcome.Unauthorized, "unauthorized", "Provider rejected token", (int)sw.ElapsedMilliseconds, false);

            if (!resp.IsSuccessStatusCode)
                return new ProbeExecutionResult(ProbeOutcome.UnknownError, "http_" + (int)resp.StatusCode, $"HTTP {resp.StatusCode}", (int)sw.ElapsedMilliseconds, false);

            var payload = await resp.Content.ReadFromJsonAsync<ModelListResponse>(cancellationToken: cts.Token).ConfigureAwait(false);
            var modelAvailable = expectedModel is null
                || (payload?.Data?.Any(m => string.Equals(m.Id, expectedModel, StringComparison.Ordinal)) ?? false);

            return new ProbeExecutionResult(
                modelAvailable ? ProbeOutcome.Success : ProbeOutcome.ModelMissing,
                modelAvailable ? null : "model_missing",
                null,
                (int)sw.ElapsedMilliseconds,
                modelAvailable);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            return new ProbeExecutionResult(ProbeOutcome.Timeout, "timeout", "Probe exceeded 5s", (int)sw.ElapsedMilliseconds, false);
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "DeepSeek probe network failure");
            return new ProbeExecutionResult(ProbeOutcome.Unreachable, "unreachable", "Network error", (int)sw.ElapsedMilliseconds, false);
        }
    }

    private sealed record ModelListResponse([property: JsonPropertyName("data")] List<ModelEntry>? Data);
    private sealed record ModelEntry([property: JsonPropertyName("id")] string Id);
}
