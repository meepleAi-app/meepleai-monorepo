namespace Api.BoundedContexts.Administration.Application.DTOs;

public sealed record ApplicationLogDto(
    string Id,
    DateTime Timestamp,
    string Level,
    string Message,
    string? Source,
    string? CorrelationId,
    string? Exception,
    Dictionary<string, string>? Properties);
