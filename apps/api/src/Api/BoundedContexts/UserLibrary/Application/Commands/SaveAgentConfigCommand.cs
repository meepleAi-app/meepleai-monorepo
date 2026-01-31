using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Command to save simplified agent configuration for a game (Issue #3212)
/// Maps frontend simplified config to complete domain AgentConfiguration
/// </summary>
internal sealed record SaveAgentConfigCommand(
    Guid UserId,
    Guid GameId,
    Guid TypologyId,
    string ModelName,
    double CostEstimate
) : ICommand<SaveAgentConfigResponse>;

/// <summary>
/// Response for SaveAgentConfigCommand
/// </summary>
public sealed record SaveAgentConfigResponse(
    bool Success,
    Guid ConfigId,
    string Message
);
