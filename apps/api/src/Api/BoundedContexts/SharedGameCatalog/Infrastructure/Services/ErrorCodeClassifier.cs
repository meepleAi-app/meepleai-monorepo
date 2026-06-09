using System.Net;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Classifies exceptions raised during BGG enrichment iterations into structured machine-readable
/// error codes (#1907). The codes drive the admin Failed Items panel grouping plus the
/// <c>error_code</c> column on <c>enrichment_attempts</c>. Categories are deliberately coarse
/// (network / schema / unknown) — the human-readable detail lives in <c>error_detail</c>.
/// </summary>
internal static class ErrorCodeClassifier
{
    /// <summary>BGG API replied with 429 Too Many Requests (rate-limit exceeded).</summary>
    public const string BggRateLimit = "BGG_API_RATE_LIMIT_429";

    /// <summary>BGG API replied with a 5xx server error. Appended with the numeric status (e.g. "BGG_SERVER_ERROR_503").</summary>
    public const string BggServerErrorPrefix = "BGG_SERVER_ERROR_";

    /// <summary>HTTP request timed out or was cancelled (network slow / DNS / TCP).</summary>
    public const string BggTimeout = "BGG_TIMEOUT";

    /// <summary>BGG response failed JSON/XML deserialization or aggregate factory invariant.</summary>
    public const string SchemaMismatch = "SCHEMA_MISMATCH";

    /// <summary>Catch-all for exceptions that don't map to a known category.</summary>
    public const string Unknown = "UNKNOWN";

    /// <summary>
    /// Maps an exception to a structured error code. Never throws — callers can rely on a
    /// non-null, non-empty return value even for unexpected exception shapes.
    /// </summary>
    public static string Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Cancellation is treated as a timeout from the BGG iteration's POV: the iteration
        // either ran past its own retry window or its outer scope was disposed mid-flight.
        if (exception is TaskCanceledException or OperationCanceledException)
        {
            return BggTimeout;
        }

        if (exception is HttpRequestException http)
        {
            if (http.StatusCode is HttpStatusCode status)
            {
                if (status == HttpStatusCode.TooManyRequests)
                {
                    return BggRateLimit;
                }

                var code = (int)status;
                if (code >= 500 && code < 600)
                {
                    return $"{BggServerErrorPrefix}{code}";
                }
            }

            // No status code (DNS failure, TCP reset, TLS error) → treat as transient timeout
            // class. Callers retry these on the same exponential schedule as 5xx and 429.
            return BggTimeout;
        }

        if (exception is System.Text.Json.JsonException
            || exception is System.Xml.XmlException
            || exception is InvalidOperationException
            || exception is ArgumentException)
        {
            return SchemaMismatch;
        }

        return Unknown;
    }
}
