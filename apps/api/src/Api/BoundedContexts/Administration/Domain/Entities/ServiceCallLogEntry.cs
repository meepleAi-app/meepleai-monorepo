namespace Api.BoundedContexts.Administration.Domain.Entities;

public sealed partial class ServiceCallLogEntry
{
    public Guid Id { get; private set; }
    public string ServiceName { get; private set; } = null!;
    public string HttpMethod { get; private set; } = null!;
    public string RequestUrl { get; private set; } = null!;
    public int? StatusCode { get; private set; }
    public long LatencyMs { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string? RequestSummary { get; private set; }
    public string? ResponseSummary { get; private set; }

    private ServiceCallLogEntry() { } // EF

    public static ServiceCallLogEntry Create(
        string serviceName, string httpMethod, string requestUrl,
        int? statusCode, long latencyMs, bool isSuccess,
        string? errorMessage, string? correlationId,
        string? requestSummary = null, string? responseSummary = null)
    {
        return new ServiceCallLogEntry
        {
            Id = Guid.NewGuid(),
            ServiceName = serviceName,
            HttpMethod = httpMethod,
            RequestUrl = TruncateAndSanitize(requestUrl, 2000)!,
            StatusCode = statusCode,
            LatencyMs = latencyMs,
            IsSuccess = isSuccess,
            ErrorMessage = TruncateAndSanitize(errorMessage, 2000),
            CorrelationId = correlationId,
            TimestampUtc = DateTime.UtcNow,
            RequestSummary = TruncateAndSanitize(requestSummary, 1000),
            ResponseSummary = TruncateAndSanitize(responseSummary, 1000),
        };
    }

    private static string? TruncateAndSanitize(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var sanitized = SanitizeSecrets(value);
        return sanitized.Length > maxLength ? sanitized[..maxLength] + "..." : sanitized;
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"(?:api[_-]?key|token|password|secret|authorization)[=:]\s*[""']?[A-Za-z0-9_\-\.]+",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000)]
    private static partial System.Text.RegularExpressions.Regex SecretPattern();

    private static string SanitizeSecrets(string value)
    {
        return SecretPattern().Replace(value, "***REDACTED***");
    }
}
