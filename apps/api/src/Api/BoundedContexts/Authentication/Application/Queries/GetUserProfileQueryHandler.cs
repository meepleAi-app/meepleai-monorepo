using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Handles retrieval of user profile information.
/// Returns UserProfileDto with complete profile data.
/// </summary>
internal class GetUserProfileQueryHandler : IQueryHandler<GetUserProfileQuery, UserProfileDto?>
{
    private readonly IUserRepository _userRepository;

    public GetUserProfileQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<UserProfileDto?> Handle(GetUserProfileQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var user = await _userRepository.GetByIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        return user != null ? MapToUserProfileDto(user) : null;
    }

    private static UserProfileDto MapToUserProfileDto(User user)
    {
        return new UserProfileDto(
            Id: user.Id,
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            IsTwoFactorEnabled: user.IsTwoFactorEnabled,
            TwoFactorEnabledAt: user.TwoFactorEnabledAt,
            Language: user.Language,
            Theme: user.Theme,
            EmailNotifications: user.EmailNotifications,
            DataRetentionDays: user.DataRetentionDays,
            ShowProfile: user.ShowProfile,
            ShowActivity: user.ShowActivity,
            ShowLibrary: user.ShowLibrary
        );
    }
}
