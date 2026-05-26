using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles session creation for an authenticated user.
/// Used after OAuth callback or 2FA verification.
/// </summary>
internal class CreateSessionCommandHandler : ICommandHandler<CreateSessionCommand, CreateSessionResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSessionCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CreateSessionResponse> Handle(CreateSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Get user by ID
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        if (user == null)
            throw new DomainException($"User with ID {command.UserId} not found");

        // Create session. SP5 S2: optional impersonate-pair (subject/UserId + actor/ImpersonatedByUserId)
        // propagated to the domain Session aggregate so the repository persists the two new
        // user_sessions columns. When both are null this behaves exactly as before. When
        // ImpersonatedUntil is set, the session lifetime is capped to the impersonation window
        // (typically 15min) rather than the default 30-day login lifetime — D-S2-4.
        var sessionId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var now = DateTime.UtcNow;
        TimeSpan? lifetime = null;
        if (command.ImpersonatedUntil is { } until)
        {
            lifetime = until - now;
            if (lifetime <= TimeSpan.Zero)
                throw new DomainException("ImpersonatedUntil must be strictly in the future.");
        }
        var session = new Session(
            id: sessionId,
            userId: user.Id,
            token: sessionToken,
            lifetime: lifetime,
            ipAddress: command.IpAddress,
            userAgent: command.UserAgent,
            impersonatedByUserId: command.ImpersonatedByUserId,
            impersonatedUntil: command.ImpersonatedUntil
        );

        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTO
        var userDto = MapToUserDto(user);

        return new CreateSessionResponse(
            User: userDto,
            SessionToken: sessionToken.Value,
            ExpiresAt: session.ExpiresAt,
            SessionId: sessionId
        );
    }

    private static UserDto MapToUserDto(User user)
    {
        return new UserDto(
            Id: user.Id,
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            Tier: user.Tier.Value,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: user.IsTwoFactorEnabled,
            TwoFactorEnabledAt: user.TwoFactorEnabledAt,
            Level: user.Level,
            ExperiencePoints: user.ExperiencePoints,
            EmailVerified: user.EmailVerified,
            EmailVerifiedAt: user.EmailVerifiedAt,
            VerificationGracePeriodEndsAt: user.VerificationGracePeriodEndsAt,
            OnboardingCompleted: user.OnboardingCompleted,
            OnboardingSkipped: user.OnboardingSkipped
        );
    }
}
