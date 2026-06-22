using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
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
internal class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Validate new password is not empty
        if (string.IsNullOrWhiteSpace(command.NewPassword))
        {
            throw new ValidationException(nameof(command.NewPassword), "New password cannot be empty");
        }

        // Retrieve user
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            throw new DomainException("User not found");
        }

        // Create new password hash
        var newPasswordHash = PasswordHash.Create(command.NewPassword);

        // Change password (domain method verifies current password)
        user.ChangePassword(command.CurrentPassword, newPasswordHash);

        // C7: revoke other sessions atomically with the password update so a
        // stolen cookie can't outlive the credential it was bound to. The
        // current session is preserved by default — the user shouldn't be
        // logged out of the device they just changed the password from.
        // IncludeCurrentInRevoke=true (or a missing CurrentSessionId, e.g.
        // service-driven password reset) falls back to revoke-everything.
        if (command.IncludeCurrentInRevoke || command.CurrentSessionId is null)
        {
            await _sessionRepository
                .RevokeAllUserSessionsAsync(command.UserId, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await _sessionRepository
                .RevokeAllUserSessionsExceptAsync(
                    command.UserId,
                    command.CurrentSessionId.Value,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        // Persist updates (user + session revocations in the same UoW commit)
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
