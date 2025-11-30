using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Handlers;

public class ChangeUserRoleCommandHandler : ICommandHandler<ChangeUserRoleCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeUserRoleCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserDto> Handle(ChangeUserRoleCommand command, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(command.UserId);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new DomainException($"User {command.UserId} not found");

        var newRole = Role.Parse(command.NewRole);
        user.UpdateRole(newRole);

        // Persist updates - required because repository uses AsNoTracking
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UserDto(
            Id: user.Id.ToString(),
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            LastSeenAt: null
        );
    }
}
