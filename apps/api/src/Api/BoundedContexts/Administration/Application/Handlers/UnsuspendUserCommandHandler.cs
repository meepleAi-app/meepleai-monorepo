using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for unsuspending (reactivating) a user account.
/// Issue #2886: Adds audit logging for unsuspension actions.
/// </summary>
internal class UnsuspendUserCommandHandler : ICommandHandler<UnsuspendUserCommand, UserDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UnsuspendUserCommandHandler(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserDto> Handle(UnsuspendUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validation handled by UnsuspendUserCommandValidator via MediatR pipeline
        var userId = Guid.Parse(command.UserId);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new NotFoundException("User", command.UserId);

        user.Unsuspend();

        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);

        // Issue #2886: Create audit log entry for unsuspension
        var auditLog = new Api.BoundedContexts.Administration.Domain.Entities.AuditLog(
            id: Guid.NewGuid(),
            userId: command.RequesterId,
            action: "user_unsuspended",
            resource: "User",
            result: "success",
            resourceId: userId.ToString(),
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                targetUserId = userId,
                targetEmail = user.Email.Value,
                unsuspendedAt = DateTime.UtcNow
            }),
            ipAddress: "admin-action"
        );

        await _auditLogRepository.AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
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
