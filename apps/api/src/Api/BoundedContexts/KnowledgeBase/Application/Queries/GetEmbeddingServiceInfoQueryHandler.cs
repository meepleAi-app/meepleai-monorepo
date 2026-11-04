using System.Text.Json;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for <see cref="GetEmbeddingServiceInfoQuery"/>. Model, device and languages are asked to
/// the embedding service; the dimension is the API's own. Nothing here is a copy of a value: a
/// value no source gives is null, and the page says so.
/// </summary>
internal sealed class GetEmbeddingServiceInfoQueryHandler
    : IQueryHandler<GetEmbeddingServiceInfoQuery, EmbeddingServiceInfoDto>
{
    internal const string HttpClientName = "EmbeddingService";
    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<GetEmbeddingServiceInfoQueryHandler> _logger;

    public GetEmbeddingServiceInfoQueryHandler(
        IHttpClientFactory httpClientFactory,
        IEmbeddingService embeddingService,
        ILogger<GetEmbeddingServiceInfoQueryHandler> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EmbeddingServiceInfoDto> Handle(
        GetEmbeddingServiceInfoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dimension = _embeddingService.GetEmbeddingDimensions();
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ServiceTimeout);

        try
        {
            using var health = await client.GetAsync("/health", cts.Token).ConfigureAwait(false);
            if (!health.IsSuccessStatusCode)
            {
                return NotHealthy("unhealthy", dimension);
            }

            var json = await health.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var languages = root.TryGetProperty("supported_languages", out var langs)
                ? langs.EnumerateArray().Select(l => l.GetString() ?? "").ToArray()
                : Array.Empty<string>();

            return new EmbeddingServiceInfoDto(
                Status: root.TryGetProperty("status", out var s) ? s.GetString() ?? "unknown" : "unknown",
                Model: root.TryGetProperty("model", out var m) ? m.GetString() : null,
                Device: root.TryGetProperty("device", out var d) ? d.GetString() : null,
                SupportedLanguages: languages,
                Dimension: dimension,
                MaxInputChars: await GetMaxInputCharsAsync(client, cts.Token, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (IsServiceFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Embedding service info unavailable");
            return NotHealthy("unavailable", dimension);
        }
    }

    /// <summary>
    /// <c>max_text_chars</c> from the service's <c>/config</c>. A service that does not answer it — an
    /// older image, a timeout — leaves the value unknown rather than failing the whole info.
    /// </summary>
    private async Task<int?> GetMaxInputCharsAsync(
        HttpClient client, CancellationToken requestToken, CancellationToken callerToken)
    {
        try
        {
            using var response = await client.GetAsync("/config", requestToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(requestToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            // A limit that is not a positive integer is no limit the pages can show.
            return doc.RootElement.TryGetProperty("max_text_chars", out var max)
                && max.TryGetInt32(out var value) && value > 0
                ? value
                : null;
        }
        catch (Exception ex) when (IsServiceFailure(ex, callerToken))
        {
            _logger.LogWarning(ex, "Embedding service config unavailable");
            return null;
        }
    }

    /// <summary>
    /// A failure of the service — unreachable, timed out, an answer that is not the expected JSON —
    /// as opposed to the caller giving up, which must propagate.
    /// </summary>
    private static bool IsServiceFailure(Exception ex, CancellationToken callerToken) => ex switch
    {
        OperationCanceledException => !callerToken.IsCancellationRequested,
        HttpRequestException or JsonException or InvalidOperationException => true,
        _ => false,
    };

    private static EmbeddingServiceInfoDto NotHealthy(string status, int dimension) => new(
        Status: status,
        Model: null,
        Device: null,
        SupportedLanguages: Array.Empty<string>(),
        Dimension: dimension,
        MaxInputChars: null);
}
