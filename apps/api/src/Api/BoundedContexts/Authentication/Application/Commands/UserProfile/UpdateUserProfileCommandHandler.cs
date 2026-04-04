using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles updating user profile information.
/// Updates display name and/or email for the authenticated user.
/// </summary>
internal class UpdateUserProfileCommandHandler : ICommandHandler<UpdateUserProfileCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserProfileCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(UpdateUserProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Retrieve user
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            throw new DomainException("User not found");
        }

        // Update display name if provided
        if (!string.IsNullOrWhiteSpace(command.DisplayName))
        {
            var trimmedDisplayName = command.DisplayName!.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedDisplayName))
            {
                user.UpdateDisplayName(trimmedDisplayName);
            }
        }

        // Update email if provided
        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            var normalizedEmail = command.Email!.Trim();
            var newEmail = new Email(normalizedEmail);

            // Check if email is already in use by another user
            var existingUser = await _userRepository.GetByEmailAsync(newEmail, cancellationToken).ConfigureAwait(false);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                throw new DomainException("Email is already in use");
            }

            user.UpdateEmail(newEmail);
        }

        // Update avatar URL if provided
        if (!string.IsNullOrWhiteSpace(command.AvatarUrl))
        {
            user.UpdateAvatarUrl(command.AvatarUrl!.Trim());
        }

        // Update bio if provided
        if (!string.IsNullOrWhiteSpace(command.Bio))
        {
            user.UpdateBio(command.Bio!.Trim());
        }

        // Persist updates
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
