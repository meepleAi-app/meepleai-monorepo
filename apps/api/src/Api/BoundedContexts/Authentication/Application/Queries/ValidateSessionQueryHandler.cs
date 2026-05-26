using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handles session validation and returns user information if valid.
/// </summary>
internal class ValidateSessionQueryHandler : IQueryHandler<ValidateSessionQuery, SessionStatusDto>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly TimeProvider _timeProvider;

    public ValidateSessionQueryHandler(
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        TimeProvider timeProvider)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<SessionStatusDto> Handle(ValidateSessionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Parse and hash the token.
        // Guard: malformed cookies (non-Base-64, empty, oversized) must return an invalid
        // session rather than propagating a raw exception. FromStored() validates the format
        // and throws ValidationException; ComputeHash() is safe once the token is valid.
        string tokenHash;
        try
        {
            var sessionToken = SessionToken.FromStored(query.SessionToken);
            tokenHash = sessionToken.ComputeHash();
        }
        catch
        {
            return new SessionStatusDto(IsValid: false, Principal: null, ExpiresAt: null, LastSeenAt: null);
        }

        // Find session by token hash
        var session = await _sessionRepository.GetByTokenHashAsync(tokenHash, cancellationToken).ConfigureAwait(false);

        if (session == null)
        {
            return new SessionStatusDto(
                IsValid: false,
                Principal: null,
                ExpiresAt: null,
                LastSeenAt: null
            );
        }

        // Check if session is valid
        if (!session.IsValid(_timeProvider))
        {
            // SP5 S2 D-S2-4: distinguish an EXPIRED IMPERSONATION from a plain invalid session.
            // When the impersonation window (ImpersonatedUntil, mirrored into ExpiresAt) elapsed,
            // surface the subject/actor ids so the middleware can emit a 401 + ImpersonationAutoEnded
            // audit. This is read-only — the audit write happens in the middleware, keeping the
            // query side-effect-free.
            var wasImpersonationAutoEnded = session.IsImpersonation && session.RevokedAt is null;
            return new SessionStatusDto(
                IsValid: false,
                Principal: null,
                ExpiresAt: session.ExpiresAt,
                LastSeenAt: session.LastSeenAt,
                WasImpersonationAutoEnded: wasImpersonationAutoEnded,
                ImpersonationSubjectUserId: wasImpersonationAutoEnded ? session.UserId : null,
                ImpersonationActorUserId: wasImpersonationAutoEnded ? session.ImpersonatedByUserId : null
            );
        }

        // Update last seen timestamp
        session.UpdateLastSeen(_timeProvider);
        var lastSeenAt = session.LastSeenAt ?? _timeProvider.GetUtcNow().UtcDateTime;
        await _sessionRepository.UpdateLastSeenAsync(session.Id, lastSeenAt, cancellationToken).ConfigureAwait(false);

        // Get user information (the SUBJECT of the session — for regular login this IS the user;
        // for impersonate sessions this is the impersonated target, NOT the admin actor).
        var user = await _userRepository.GetByIdAsync(session.UserId, cancellationToken).ConfigureAwait(false);

        if (user == null)
        {
            return new SessionStatusDto(
                IsValid: false,
                Principal: null,
                ExpiresAt: null,
                LastSeenAt: null
            );
        }

        var subjectDto = MapToUserDto(user);

        // SP5 Admin Security S2 T1: Actor remains null in this commit (regular login behavior).
        // T4 populates Actor by loading the admin (session.ImpersonatedByUserId) when non-null.
        // Until then, all sessions are mono-principal: Principal.Subject = user, Principal.Actor = null.
        var principal = new Principal(subjectDto, Actor: null);

        return new SessionStatusDto(
            IsValid: true,
            Principal: principal,
            ExpiresAt: session.ExpiresAt,
            LastSeenAt: session.LastSeenAt,
            SessionId: session.Id
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
            EmailVerified: user.EmailVerified,                           // Issue #3672
            EmailVerifiedAt: user.EmailVerifiedAt,                       // Issue #3672
            VerificationGracePeriodEndsAt: user.VerificationGracePeriodEndsAt,  // Issue #3672
            OnboardingCompleted: user.OnboardingCompleted,              // Issue #323
            OnboardingSkipped: user.OnboardingSkipped                   // Issue #323
        );
    }
}
