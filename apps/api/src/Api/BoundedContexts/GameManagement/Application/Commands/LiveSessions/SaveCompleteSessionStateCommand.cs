using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;

/// <summary>
/// Orchestrates: pause session + save + create snapshot + persist agent state + generate recap.
/// Issue #122 — Enhanced Save/Resume.
/// </summary>
internal sealed record SaveCompleteSessionStateCommand(Guid SessionId)
    : ICommand<SessionSaveResultDto>;
