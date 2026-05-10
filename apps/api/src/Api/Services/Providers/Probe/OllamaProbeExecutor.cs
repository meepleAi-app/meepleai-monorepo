using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.Services.Providers.Probe;

internal sealed class OllamaProbeExecutor : IProviderProbeExecutor
{
#pragma warning disable S1075 // URIs should not be hardcoded - Default fallback for local Ollama instance
    private const string DefaultBaseUrl = "http://localhost:11434";
#pragma warning restore S1075
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaProbeExecutor> _logger;
    private readonly string _tagsUrl;

    public string ProviderName => "ollama";

    public OllamaProbeExecutor(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<OllamaProbeExecutor> logger)
    {
        _httpClient = httpClientFactory.CreateClient("provider-probe");
        _logger = logger;
        var baseUrl = configuration["Providers:Ollama:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")
            ?? DefaultBaseUrl;
        _tagsUrl = $"{baseUrl.TrimEnd('/')}/api/tags";
    }

    public async Task<ProbeExecutionResult> ExecuteAsync(string apiKey, string? expectedModel, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProbeTimeout);

        try
        {
            using var resp = await _httpClient.GetAsync(_tagsUrl, cts.Token).ConfigureAwait(false);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
                return new ProbeExecutionResult(ProbeOutcome.UnknownError, "http_" + (int)resp.StatusCode, $"HTTP {resp.StatusCode}", (int)sw.ElapsedMilliseconds, false);

            var payload = await resp.Content.ReadFromJsonAsync<TagsResponse>(cancellationToken: cts.Token).ConfigureAwait(false);
            var modelAvailable = expectedModel is null
                || (payload?.Models?.Any(m => string.Equals(m.Name, expectedModel, StringComparison.Ordinal)) ?? false);

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
            _logger.LogWarning(ex, "Ollama probe network failure");
            return new ProbeExecutionResult(ProbeOutcome.Unreachable, "unreachable", "Network error", (int)sw.ElapsedMilliseconds, false);
        }
    }

    private sealed record TagsResponse([property: JsonPropertyName("models")] List<ModelEntry>? Models);
    private sealed record ModelEntry([property: JsonPropertyName("name")] string Name);
}
