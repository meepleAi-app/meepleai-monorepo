

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.Models;

/// <summary>
/// DTO for LLM provider health monitoring
/// ISSUE-962 (BGAI-020): Admin endpoint response
/// </summary>
public record LlmHealthStatusDto(
    Dictionary<string, ProviderHealthDto> Providers,
    string Summary
);

/// <summary>
/// Health status for a single LLM provider
/// </summary>
public record ProviderHealthDto(
    string ProviderName,
    string Status,
    int SuccessfulChecks,
    int FailedChecks,
    int TotalChecks,
    double SuccessRate,
    DateTime? LastCheckTime,
    string CircuitState,
    string LatencyStats
);
