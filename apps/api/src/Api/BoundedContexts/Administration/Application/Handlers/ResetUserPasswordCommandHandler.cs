using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Handlers;

public class ResetUserPasswordCommandHandler : ICommandHandler<ResetUserPasswordCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ResetUserPasswordCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ResetUserPasswordCommand command, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(command.UserId);
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new DomainException($"User {command.UserId} not found");

        var newPasswordHash = PasswordHash.Create(command.NewPassword);
        user.UpdatePassword(newPasswordHash);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
