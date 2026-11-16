using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for UpdateUserCommand.
/// Updates existing user details (email, display name, role).
/// </summary>
internal class UpdateUserCommandHandler : ICommandHandler<UpdateUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserDto> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Find user
        var userId = Guid.Parse(command.UserId);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new DomainException($"User {command.UserId} not found");

        // #4907: with the update now saved, a new email plus «forgot password» would hand over an account of the
        // same rank or above.
        if (!Role.TryParse(command.AdminRole, out var adminRole) || !adminRole.Outranks(user.Role))
            throw new ForbiddenException("You can only update users with a role below your own");

        // Update email if provided
        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            var newEmail = new Email(command.Email);
            if (!user.Email.Equals(newEmail))
            {
                // Check uniqueness
                var existingUser = await _userRepository.GetByEmailAsync(newEmail, cancellationToken).ConfigureAwait(false);
                if (existingUser != null && existingUser.Id != userId)
                    throw new DomainException($"Email {command.Email} is already in use");

                user.UpdateEmail(newEmail);
            }
        }

        // Update display name if provided
        if (!string.IsNullOrWhiteSpace(command.DisplayName))
        {
            user.UpdateDisplayName(command.DisplayName.Trim());
        }

        // Role changes are NOT allowed via UpdateUser — use PUT /admin/users/{id}/role (SuperAdmin only)
        if (!string.IsNullOrWhiteSpace(command.Role))
        {
            throw new DomainException("Role changes must use the dedicated role change endpoint (requires SuperAdmin)");
        }

        // #4907: GetByIdAsync returns a detached aggregate; without UpdateAsync nothing was saved.
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTO
        return new UserDto(
            Id: user.Id.ToString(),
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            LastSeenAt: null // Not fetching sessions for update
        );
    }
}
