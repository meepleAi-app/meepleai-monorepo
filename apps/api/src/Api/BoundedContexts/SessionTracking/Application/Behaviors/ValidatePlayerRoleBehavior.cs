using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that validates the requesting user has the required
/// session role before executing the command handler.
/// Runs after FluentValidation, before the command handler.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
/// <typeparam name="TRequest">Command type implementing IRequireSessionRole.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
internal sealed class ValidatePlayerRoleBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequireSessionRole
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<ValidatePlayerRoleBehavior<TRequest, TResponse>> _logger;

    public ValidatePlayerRoleBehavior(
        ISessionRepository sessionRepository,
        ILogger<ValidatePlayerRoleBehavior<TRequest, TResponse>> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // Find participant by user ID
        var participant = session.Participants.FirstOrDefault(p => p.UserId == request.RequesterId)
            ?? throw new NotFoundException(
                $"User {request.RequesterId} is not a participant in session {request.SessionId}");

        // Check role
        if (participant.Role < request.MinimumRole)
        {
            _logger.LogWarning(
                "User {UserId} with role {Role} attempted action requiring {Required} on session {SessionId}",
                request.RequesterId, participant.Role, request.MinimumRole, request.SessionId);

            throw new ForbiddenException(
                $"Insufficient role. Required: {request.MinimumRole}, actual: {participant.Role}.");
        }

        return await next().ConfigureAwait(false);
    }
}
