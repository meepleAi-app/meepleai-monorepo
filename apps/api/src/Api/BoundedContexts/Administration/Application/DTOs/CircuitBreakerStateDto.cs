namespace Api.BoundedContexts.Administration.Application.DTOs;

public sealed record CircuitBreakerStateDto(
    string ServiceName, string State, int TripCount,
    DateTime? LastTrippedAt, DateTime? LastResetAt, string? LastError);
