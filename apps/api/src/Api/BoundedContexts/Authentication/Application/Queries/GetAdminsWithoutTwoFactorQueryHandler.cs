using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handles <see cref="GetAdminsWithoutTwoFactorQuery"/> (SP5 Admin Security S3 — T6).
/// </summary>
internal class GetAdminsWithoutTwoFactorQueryHandler
    : IQueryHandler<GetAdminsWithoutTwoFactorQuery, IReadOnlyList<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public GetAdminsWithoutTwoFactorQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<IReadOnlyList<UserDto>> Handle(GetAdminsWithoutTwoFactorQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // GetAdminUsersAsync already scopes to Role in (admin, superadmin); we keep only the
        // accounts that have NOT enrolled in 2FA — the ones ops must chase before the strict flip.
        var admins = await _userRepository.GetAdminUsersAsync(cancellationToken).ConfigureAwait(false);
        return admins
            .Where(u => !u.IsTwoFactorEnabled)
            .Select(MapToUserDto)
            .ToList();
    }

    private static UserDto MapToUserDto(User user) => new(
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
