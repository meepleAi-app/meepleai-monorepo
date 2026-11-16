using Api.BoundedContexts.Administration.Application.Commands;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Guards;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal class ResetUserPasswordCommandHandler : ICommandHandler<ResetUserPasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResetUserPasswordCommandHandler(
        IUserRepository userRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate input before domain operations
        Guard.AgainstNullOrWhiteSpace(command.UserId, nameof(command.UserId));
        if (!Guid.TryParse(command.UserId, out var userId))
            throw new ValidationException($"Invalid UserId format: {command.UserId}");
        Guard.AgainstNullOrWhiteSpace(command.NewPassword, nameof(command.NewPassword));
        Guard.AgainstTooShort(command.NewPassword, nameof(command.NewPassword), 8);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new NotFoundException("User", command.UserId); // #4907: 404, the reset endpoint catches it

        // #4907: with the reset now saved, an admin could take over an account of the same rank or above.
        if (!Role.TryParse(command.AdminRole, out var adminRole) || !adminRole.Outranks(user.Role))
            throw new ForbiddenException("You can only reset the password of users with a role below your own");

        var newPasswordHash = PasswordHash.Create(command.NewPassword);
        user.UpdatePassword(newPasswordHash);

        // #5040: an admin resets a password when the account may be in someone else's hands, so every session the user
        // has open is closed, like ChangePasswordCommandHandler does (C7), and in the same order: the revoke saves on its
        // own, before the password. If the save below fails, the user is logged out with the old password still valid;
        // never logged in with sessions the reset should have closed.
        await _sessionRepository.RevokeAllUserSessionsAsync(user.Id, cancellationToken).ConfigureAwait(false);

        // #4907: GetByIdAsync returns a detached aggregate. Without UpdateAsync, SaveChangesAsync had nothing
        // to write, and the reset answered 200 while the old password kept working.
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
