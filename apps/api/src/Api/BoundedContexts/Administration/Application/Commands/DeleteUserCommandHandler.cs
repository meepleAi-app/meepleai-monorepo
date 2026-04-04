using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Guards;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for DeleteUserCommand.
/// Prevents self-deletion and deletion of the last admin.
/// </summary>
internal class DeleteUserCommandHandler : ICommandHandler<DeleteUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate input before domain operations
        Guard.AgainstNullOrWhiteSpace(command.UserId, nameof(command.UserId));
        Guard.AgainstNullOrWhiteSpace(command.RequestingUserId, nameof(command.RequestingUserId));
        if (!Guid.TryParse(command.UserId, out var userId))
            throw new ValidationException($"Invalid UserId format: {command.UserId}");
        if (!Guid.TryParse(command.RequestingUserId, out _))
            throw new ValidationException($"Invalid RequestingUserId format: {command.RequestingUserId}");

        // Prevent self-deletion
        if (string.Equals(command.UserId, command.RequestingUserId, StringComparison.Ordinal))
            throw new DomainException("Cannot delete your own account");

        // Find user
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new DomainException($"User {command.UserId} not found");

        // Prevent deletion of last admin
        if (user.Role.IsAdmin())
        {
            var adminCount = await _userRepository.CountAdminsAsync(cancellationToken).ConfigureAwait(false);
            if (adminCount <= 1)
                throw new DomainException("Cannot delete the last admin user");
        }

        // Delete user
        await _userRepository.DeleteAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
