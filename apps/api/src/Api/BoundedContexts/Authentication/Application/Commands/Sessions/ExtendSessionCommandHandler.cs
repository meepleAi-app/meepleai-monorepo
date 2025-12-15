using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handler for ExtendSessionCommand with rate limiting.
/// Rate limit: Max 10 extensions per hour per user.
/// </summary>
internal class ExtendSessionCommandHandler : ICommandHandler<ExtendSessionCommand, ExtendSessionResponse>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRateLimitService _rateLimitService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExtendSessionCommandHandler> _logger;

    // Rate limiting configuration for session extensions
    private const int MaxExtensionsPerHour = 10;
    private const double RefillRatePerSecond = MaxExtensionsPerHour / 3600.0; // ~0.00278 tokens/second

    public ExtendSessionCommandHandler(
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        IRateLimitService rateLimitService,
        TimeProvider timeProvider,
        ILogger<ExtendSessionCommandHandler> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _rateLimitService = rateLimitService ?? throw new ArgumentNullException(nameof(rateLimitService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ExtendSessionResponse> Handle(ExtendSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Rate limiting check - per user
        var rateLimitKey = $"session_extend:{command.RequestingUserId}";
        var rateLimitResult = await _rateLimitService.CheckRateLimitAsync(
            rateLimitKey,
            MaxExtensionsPerHour,
            RefillRatePerSecond,
            cancellationToken).ConfigureAwait(false);

        if (!rateLimitResult.Allowed)
        {
            _logger.LogWarning(
                "Rate limit exceeded for user {UserId} extending session. Retry after {RetryAfter}s",
                command.RequestingUserId, rateLimitResult.RetryAfterSeconds);
            return new ExtendSessionResponse(
                false,
                null,
                $"Rate limit exceeded. Maximum {MaxExtensionsPerHour} extensions per hour. Please try again in {rateLimitResult.RetryAfterSeconds} seconds.");
        }

        // Retrieve session
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found", command.SessionId);
            return new ExtendSessionResponse(false, null, "Session not found");
        }

        // Authorization check: User must own the session
        if (session.UserId != command.RequestingUserId)
        {
            _logger.LogWarning(
                "User {UserId} attempted to extend session {SessionId} owned by {OwnerId}",
                command.RequestingUserId, command.SessionId, session.UserId);
            return new ExtendSessionResponse(false, null, "Unauthorized to extend this session");
        }

        // Extend session using domain logic
        try
        {
            var extensionDuration = command.ExtensionDuration ?? Session.DefaultLifetime;
            session.Extend(extensionDuration, _timeProvider);

            // Persist changes
            await _sessionRepository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Session {SessionId} extended by {Duration}. New expiration: {ExpiresAt}",
                command.SessionId, extensionDuration, session.ExpiresAt);

            return new ExtendSessionResponse(true, session.ExpiresAt, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend session {SessionId}", command.SessionId);
            return new ExtendSessionResponse(false, null, ex.Message);
        }
    }
}
