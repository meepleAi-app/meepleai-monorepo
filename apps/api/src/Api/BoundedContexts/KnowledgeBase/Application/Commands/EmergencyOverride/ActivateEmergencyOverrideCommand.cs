using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.EmergencyOverride;

/// <summary>
/// Issue #5476: Activate an emergency operational override for the LLM subsystem.
/// Supports: force-ollama-only, reset-circuit-breaker, flush-quota-cache.
/// </summary>
internal record ActivateEmergencyOverrideCommand(
    string Action,
    int DurationMinutes,
    string Reason,
    Guid AdminUserId,
    string? TargetProvider = null) : ICommand<EmergencyOverrideResult>;

/// <summary>
/// Issue #5476: Deactivate an active emergency override (early manual revert).
/// </summary>
internal record DeactivateEmergencyOverrideCommand(
    string Action,
    Guid AdminUserId) : ICommand<EmergencyOverrideResult>;

internal record EmergencyOverrideResult(
    bool Success,
    string Message);
