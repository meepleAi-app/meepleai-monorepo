using System.Globalization;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// #4512: reads <c>/metrics</c> from the embedding service and parses the four counters
/// <c>apps/embedding-service/main.py</c> writes. Before, <c>/admin/embedding/metrics</c> and
/// <c>/admin/kb/pipeline/health</c> each did this in the endpoint, with a parser each and timeouts
/// of 5 s and 3 s.
/// <para>
/// One timeout, 5 s, the same as <see cref="GetEmbeddingServiceInfoQueryHandler"/> for the same
/// service. On the pipeline health it costs nothing extra: the read runs alongside
/// <c>GetServiceHealthStatusesQuery</c>, whose embedding check waits up to 10 s on the same
/// service.
/// </para>
/// </summary>
internal sealed class EmbeddingServiceMetricsReader : IEmbeddingServiceMetricsReader
{
    internal const string HttpClientName = "EmbeddingService";
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmbeddingServiceMetricsReader> _logger;

    public EmbeddingServiceMetricsReader(
        IHttpClientFactory httpClientFactory,
        TimeProvider timeProvider,
        ILogger<EmbeddingServiceMetricsReader> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EmbeddingServiceMetricsDto> ReadAsync(CancellationToken cancellationToken)
    {
        // The timeout runs on the injected clock, so a test can make it elapse without waiting.
        using var timeout = new CancellationTokenSource(Timeout, _timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync("/metrics", linked.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Embedding service metrics unavailable: /metrics answered {StatusCode}",
                    (int)response.StatusCode);
                return EmbeddingServiceMetricsDto.Unavailable;
            }

            var text = await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            return ParsePrometheusMetrics(text);
        }
        catch (Exception ex) when (IsServiceFailure(ex, cancellationToken))
        {
            _logger.LogWarning(ex, "Embedding service metrics unavailable");
            return EmbeddingServiceMetricsDto.Unavailable;
        }
    }

    /// <summary>
    /// Anything that goes wrong reading the metrics — unreachable, timed out, a body that breaks the
    /// read — is the service's failure, and the pages show zeros: both endpoints have always
    /// answered this way rather than fail. The one exception is the caller giving up, which must
    /// propagate (the endpoints used to swallow it too).
    /// </summary>
    private static bool IsServiceFailure(Exception ex, CancellationToken callerToken) =>
        ex is not OperationCanceledException || !callerToken.IsCancellationRequested;

    /// <summary>
    /// Lines of the Prometheus text format, <c>name value</c>. Comments, other metrics and values
    /// that do not parse are skipped: a counter the service does not report stays 0.
    /// </summary>
    private static EmbeddingServiceMetricsDto ParsePrometheusMetrics(string text)
    {
        long requestsTotal = 0;
        long failuresTotal = 0;
        double durationMsSum = 0;
        double totalCharsSum = 0;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var parts = trimmed.Split(' ', 2);
            if (parts.Length < 2)
            {
                continue;
            }

            var key = parts[0];
            var value = parts[1];

            if (string.Equals(key, "embed_requests_total", StringComparison.Ordinal)
                && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requests))
            {
                requestsTotal = requests;
            }
            else if (string.Equals(key, "embed_failures_total", StringComparison.Ordinal)
                     && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var failures))
            {
                failuresTotal = failures;
            }
            else if (string.Equals(key, "embed_duration_ms_sum", StringComparison.Ordinal)
                     && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
            {
                durationMsSum = duration;
            }
            else if (string.Equals(key, "embed_total_chars_sum", StringComparison.Ordinal)
                     && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var chars))
            {
                totalCharsSum = chars;
            }
        }

        var avgDuration = requestsTotal > 0 ? durationMsSum / requestsTotal : 0.0;
        var failureRate = requestsTotal > 0 ? (double)failuresTotal / requestsTotal * 100.0 : 0.0;

        return new EmbeddingServiceMetricsDto(
            RequestsTotal: requestsTotal,
            FailuresTotal: failuresTotal,
            DurationMsSum: Math.Round(durationMsSum, 2),
            TotalCharsSum: Math.Round(totalCharsSum, 0),
            AvgDurationMs: Math.Round(avgDuration, 2),
            FailureRate: Math.Round(failureRate, 2));
    }
}
