using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to extend a session by updating its LastSeenAt timestamp.
/// DDD CQRS: Command for write operation.
/// AUTH-05: Session management
/// </summary>
public sealed record ExtendSessionCommand(
    string TokenHash,
    int InactivityTimeoutDays
) : ICommand<SessionStatusResponse?>;
