using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to revoke a specific session by session ID.
/// </summary>
public record RevokeSessionCommand(Guid SessionId) : ICommand<bool>;
