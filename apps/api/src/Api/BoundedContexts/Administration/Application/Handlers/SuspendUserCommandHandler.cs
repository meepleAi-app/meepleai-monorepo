using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for suspending a user account.
/// </summary>
internal class SuspendUserCommandHandler : ICommandHandler<SuspendUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SuspendUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserDto> Handle(SuspendUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validation handled by SuspendUserCommandValidator via MediatR pipeline
        var userId = Guid.Parse(command.UserId);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new NotFoundException("User", command.UserId);

        user.Suspend(command.Reason);

        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UserDto(
            Id: user.Id.ToString(),
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            LastSeenAt: null,
            IsSuspended: user.IsSuspended,
            SuspendReason: user.SuspendReason
        );
    }
}
