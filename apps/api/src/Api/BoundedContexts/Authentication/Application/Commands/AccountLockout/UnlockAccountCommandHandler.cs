using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccountLockout;

/// <summary>
/// Handles the UnlockAccountCommand to manually unlock a user account.
/// Issue #3339: Account lockout after failed login attempts.
/// </summary>
internal class UnlockAccountCommandHandler : ICommandHandler<UnlockAccountCommand, UnlockAccountResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UnlockAccountCommandHandler> _logger;

    public UnlockAccountCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<UnlockAccountCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UnlockAccountResult> Handle(UnlockAccountCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Admin {AdminId} attempting to unlock account {UserId}", command.AdminId, command.UserId);

        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for unlock", command.UserId);
            throw new NotFoundException("User", command.UserId.ToString());
        }

        // Unlock the account
        user.Unlock(command.AdminId);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Account {UserId} unlocked successfully by admin {AdminId}", command.UserId, command.AdminId);

        return new UnlockAccountResult(
            UserId: user.Id,
            Email: user.Email.Value,
            Message: "Account unlocked successfully"
        );
    }
}
