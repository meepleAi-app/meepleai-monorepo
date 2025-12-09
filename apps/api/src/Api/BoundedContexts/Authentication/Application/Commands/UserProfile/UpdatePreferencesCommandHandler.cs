using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles updating user preferences.
/// Updates language, theme, email notifications, and data retention settings.
/// Returns updated UserProfileDto with all profile information.
/// </summary>
public class UpdatePreferencesCommandHandler : ICommandHandler<UpdatePreferencesCommand, UserProfileDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePreferencesCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserProfileDto> Handle(UpdatePreferencesCommand command, CancellationToken cancellationToken)
    {
        // Retrieve user
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            throw new DomainException("User not found");
        }

        // Update preferences via domain method (includes validation)
        user.UpdatePreferences(
            command.Language,
            command.Theme,
            command.EmailNotifications,
            command.DataRetentionDays);

        // Persist updates
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Return updated profile with preferences
        return new UserProfileDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Role.ToString(),
            user.CreatedAt,
            user.TotpSecret != null,
            user.TwoFactorEnabledAt,
            user.Language,
            user.Theme,
            user.EmailNotifications,
            user.DataRetentionDays
        );
    }
}
