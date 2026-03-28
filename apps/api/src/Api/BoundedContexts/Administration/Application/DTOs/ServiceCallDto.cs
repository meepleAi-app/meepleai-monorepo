namespace Api.BoundedContexts.Administration.Application.DTOs;

public sealed record ServiceCallDto(
    Guid Id, string ServiceName, string HttpMethod, string RequestUrl,
    int? StatusCode, long LatencyMs, bool IsSuccess, string? ErrorMessage,
    string? CorrelationId, DateTime TimestampUtc, string? RequestSummary, string? ResponseSummary);

public sealed record ServiceCallSummaryDto(
    string ServiceName, int TotalCalls, int SuccessCount, int ErrorCount,
    double ErrorRate, double AvgLatencyMs, double P95LatencyMs, long MaxLatencyMs,
    DateTime? LastCallAt, DateTime? LastErrorAt);
