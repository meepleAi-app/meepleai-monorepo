using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.Operations;

/// <summary>
/// Command to gracefully restart the API service.
/// Issue #3696: Operations - Service Control Panel.
/// HIGH SECURITY RISK: SuperAdmin only, Level 2 confirmation required.
/// Triggers graceful shutdown; orchestrator (Docker/K8s) handles restart.
/// </summary>
internal record RestartServiceCommand(
    string ServiceName,
    Guid AdminUserId
) : ICommand<RestartServiceResponseDto>;

/// <summary>
/// Response DTO for service restart command.
/// </summary>
public record RestartServiceResponseDto(
    string Message,
    string EstimatedDowntime
);
