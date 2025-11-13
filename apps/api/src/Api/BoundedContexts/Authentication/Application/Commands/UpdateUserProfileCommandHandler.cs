using Api.BoundedContexts.Authentication.Domain.ValueObjects;
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
public class UpdateUserProfileCommandHandler : ICommandHandler<UpdateUserProfileCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserProfileCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(UpdateUserProfileCommand command, CancellationToken cancellationToken)
    {
        // Retrieve user
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
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
            var existingUser = await _userRepository.GetByEmailAsync(newEmail, cancellationToken);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                throw new DomainException("Email is already in use");
            }

            user.UpdateEmail(newEmail);
        }

        // Persist updates
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
