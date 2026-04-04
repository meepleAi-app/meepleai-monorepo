using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for SendUserEmailCommand (Issue #2890).
/// Sends custom email to user via email service.
/// Note: Email service integration pending - currently logs only.
/// </summary>
internal sealed class SendUserEmailCommandHandler : IRequestHandler<SendUserEmailCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendUserEmailCommandHandler> _logger;

    public SendUserEmailCommandHandler(
        IUserRepository userRepository,
        IAuditLogRepository auditLogRepository,
        IUnitOfWork unitOfWork,
        ILogger<SendUserEmailCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SendUserEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Sending email to user {UserId} by admin {AdminId}",
            command.UserId, command.RequesterId);

        // Verify user exists
        var user = await _userRepository.GetByIdAsync(command.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new NotFoundException($"User with ID '{command.UserId}' not found");
        }

        // NOTE: Email service integration pending - email infrastructure not yet available
        // Placeholder implementation: logs action for audit trail
        _logger.LogInformation("Email would be sent to {Email}: Subject='{Subject}'",
            user.Email.Value, command.Subject);

        // Create audit log for the action
        var auditLog = new Api.BoundedContexts.Administration.Domain.Entities.AuditLog(
            id: Guid.NewGuid(),
            userId: command.RequesterId,
            action: "admin_send_email",
            resource: "User",
            result: "success",
            resourceId: command.UserId.ToString(),
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                targetUserId = command.UserId,
                subject = command.Subject,
                bodyLength = command.Body.Length
            }),
            ipAddress: null
        );

        await _auditLogRepository.AddAsync(auditLog, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Email sending logged for user {UserId}", command.UserId);
    }
}
