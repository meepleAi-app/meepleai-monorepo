using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.External.Reranking;

/// <summary>
/// ADR-016 Phase 4: HTTP client for cross-encoder reranking service.
/// Implements resilient communication with Python reranker service.
/// </summary>
public sealed class CrossEncoderRerankerClient : ICrossEncoderReranker
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CrossEncoderRerankerClient> _logger;
    private readonly RerankerClientOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CrossEncoderRerankerClient(
        HttpClient httpClient,
        ILogger<CrossEncoderRerankerClient> logger,
        IOptions<RerankerClientOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<RerankResult> RerankAsync(
        string query,
        IReadOnlyList<RerankChunk> chunks,
        int? topK = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        if (chunks == null || chunks.Count == 0)
        {
            return new RerankResult(
                Chunks: Array.Empty<RerankedChunk>(),
                Model: "none",
                ProcessingTimeMs: 0
            );
        }

        var request = new RerankRequestDto
        {
            Query = query,
            Chunks = chunks.Select(c => new ChunkDto
            {
                Id = c.Id,
                Content = c.Content,
                Score = c.OriginalScore,
                Metadata = c.Metadata ?? new Dictionary<string, object>()
            }).ToList(),
            TopK = topK
        };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMs));

            var response = await _httpClient.PostAsJsonAsync(
                "rerank",
                request,
                JsonOptions,
                cts.Token).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<RerankResponseDto>(JsonOptions, cts.Token).ConfigureAwait(false);

            if (result == null)
            {
                throw new InvalidOperationException("Received null response from reranker service");
            }

            _logger.LogDebug(
                "Reranked {ChunkCount} chunks in {TimeMs:F1}ms using {Model}",
                chunks.Count,
                result.ProcessingTimeMs,
                result.Model);

            return new RerankResult(
                Chunks: result.Results.Select(r => new RerankedChunk(
                    Id: r.Id,
                    Content: r.Content,
                    OriginalScore: r.OriginalScore,
                    RerankScore: r.RerankScore,
                    Metadata: r.Metadata
                )).ToList(),
                Model: result.Model,
                ProcessingTimeMs: result.ProcessingTimeMs
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Reranker service request failed");
            throw new RerankerServiceException("Failed to connect to reranker service", ex);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            _logger.LogWarning("Reranker service request timed out after {TimeoutMs}ms", _options.TimeoutMs);
            throw new RerankerServiceException($"Reranker service timed out after {_options.TimeoutMs}ms", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize reranker response");
            throw new RerankerServiceException("Invalid response from reranker service", ex);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var response = await _httpClient.GetAsync("health", cts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Reranker health check failed with status {StatusCode}", response.StatusCode);
                return false;
            }

            var health = await response.Content.ReadFromJsonAsync<HealthResponseDto>(JsonOptions, cts.Token).ConfigureAwait(false);
            var isHealthy = health?.ModelLoaded == true && health.Status == "healthy";

            if (!isHealthy)
            {
                _logger.LogWarning("Reranker reports unhealthy status: {Status}, ModelLoaded: {ModelLoaded}",
                    health?.Status, health?.ModelLoaded);
            }

            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Reranker health check failed");
            return false;
        }
    }
    private sealed class RerankRequestDto
    {
        public string Query { get; set; } = string.Empty;
        public List<ChunkDto> Chunks { get; set; } = new();
        public int? TopK { get; set; }
    }

    private sealed class ChunkDto
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Score { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    private sealed class RerankResponseDto
    {
        public List<RerankResultDto> Results { get; set; } = new();
        public string Model { get; set; } = string.Empty;
        public double ProcessingTimeMs { get; set; }
    }

    private sealed class RerankResultDto
    {
        public string Id { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double OriginalScore { get; set; }
        public double RerankScore { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    private sealed class HealthResponseDto
    {
        public string Status { get; set; } = string.Empty;
        public bool ModelLoaded { get; set; }
        public string ModelName { get; set; } = string.Empty;
        public string Device { get; set; } = string.Empty;
        public double? UptimeSeconds { get; set; }
    }
}

/// <summary>
/// Configuration options for reranker client.
/// </summary>
public sealed class RerankerClientOptions
{
    /// <summary>
    /// Base URL of the reranker service.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8003";

    /// <summary>
    /// Request timeout in milliseconds.
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;
}

/// <summary>
/// Exception thrown when reranker service communication fails.
/// </summary>
public sealed class RerankerServiceException : Exception
{
    public RerankerServiceException(string message) : base(message) { }
    public RerankerServiceException(string message, Exception innerException)
        : base(message, innerException) { }
}
