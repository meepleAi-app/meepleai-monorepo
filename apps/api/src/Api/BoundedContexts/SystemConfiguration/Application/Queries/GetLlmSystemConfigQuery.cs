using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Issue #5495: Query to get current LLM system configuration (DB-first, appsettings fallback).
/// </summary>
internal record GetLlmSystemConfigQuery : IQuery<LlmSystemConfigDto>;

/// <summary>
/// DTO representing the current LLM system configuration from all sources.
/// </summary>
internal record LlmSystemConfigDto(
    int CircuitBreakerFailureThreshold,
    int CircuitBreakerOpenDurationSeconds,
    int CircuitBreakerSuccessThreshold,
    decimal DailyBudgetUsd,
    decimal MonthlyBudgetUsd,
    string FallbackChainJson,
    string Source, // "database" or "appsettings"
    DateTime? LastUpdatedAt,
    Guid? LastUpdatedByUserId);
