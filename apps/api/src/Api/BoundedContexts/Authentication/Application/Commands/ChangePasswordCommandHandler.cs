using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles password change requests.
/// Verifies current password before allowing change to new password.
/// </summary>
public class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        // Validate new password is not empty
        if (string.IsNullOrWhiteSpace(command.NewPassword))
        {
            throw new ValidationException(nameof(command.NewPassword), "New password cannot be empty");
        }

        // Retrieve user
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
        {
            throw new DomainException("User not found");
        }

        // Create new password hash
        var newPasswordHash = PasswordHash.Create(command.NewPassword);

        // Change password (domain method verifies current password)
        user.ChangePassword(command.CurrentPassword, newPasswordHash);

        // Persist updates
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
