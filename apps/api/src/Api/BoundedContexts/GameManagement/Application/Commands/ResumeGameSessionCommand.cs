using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

/// <summary>
/// Command to resume a paused game session.
/// </summary>
internal record ResumeGameSessionCommand(
    Guid SessionId
) : ICommand<GameSessionDto>;
