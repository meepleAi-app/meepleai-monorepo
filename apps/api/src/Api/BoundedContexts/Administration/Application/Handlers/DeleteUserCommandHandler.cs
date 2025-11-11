using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for DeleteUserCommand.
/// Prevents self-deletion and deletion of the last admin.
/// </summary>
public class DeleteUserCommandHandler : ICommandHandler<DeleteUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        // Prevent self-deletion
        if (command.UserId == command.RequestingUserId)
            throw new DomainException("Cannot delete your own account");

        // Find user
        var userId = Guid.Parse(command.UserId);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new DomainException($"User {command.UserId} not found");

        // Prevent deletion of last admin
        if (user.Role.IsAdmin())
        {
            var adminCount = await _userRepository.CountAdminsAsync(cancellationToken);
            if (adminCount <= 1)
                throw new DomainException("Cannot delete the last admin user");
        }

        // Delete user
        await _userRepository.DeleteAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
