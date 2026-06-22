using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handles retrieval of the active user for the current session.
/// Used by /auth/me endpoint to materialize a UserDto from a session ID.
/// Returns null when the session does not exist or the underlying user has been removed.
/// </summary>
internal sealed class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, UserDto?>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;

    public GetCurrentUserQueryHandler(
        ISessionRepository sessionRepository,
        IUserRepository userRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<UserDto?> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        var user = await _userRepository
            .GetByIdAsync(session.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        return MapToUserDto(user);
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
            OnboardingSkipped: user.OnboardingSkipped);
    }
}
