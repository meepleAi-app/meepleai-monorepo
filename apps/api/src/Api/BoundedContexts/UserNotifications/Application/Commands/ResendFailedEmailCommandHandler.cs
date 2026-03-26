using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Handler for ResendFailedEmailCommand.
/// Resets a failed or dead-letter email back to pending for reprocessing.
/// Issue #4417: Email notification queue.
/// </summary>
internal class ResendFailedEmailCommandHandler : ICommandHandler<ResendFailedEmailCommand, bool>
{
    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ResendFailedEmailCommandHandler> _logger;

    public ResendFailedEmailCommandHandler(
        IEmailQueueRepository emailQueueRepository,
        IUnitOfWork unitOfWork,
        ILogger<ResendFailedEmailCommandHandler> logger)
    {
        _emailQueueRepository = emailQueueRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(ResendFailedEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var emailItem = await _emailQueueRepository.GetByIdAsync(command.EmailId, cancellationToken).ConfigureAwait(false);

        if (emailItem == null)
        {
            _logger.LogWarning("Email {EmailId} not found for resend", command.EmailId);
            return false;
        }

        // Authorization check: user can only resend their own emails
        if (emailItem.UserId != command.UserId)
        {
            _logger.LogWarning(
                "Unauthorized resend attempt: User {UserId} tried to resend email {EmailId} owned by {OwnerId}",
                command.UserId, command.EmailId, emailItem.UserId);
            throw new UnauthorizedAccessException($"User {command.UserId} cannot resend email {command.EmailId}");
        }

        emailItem.ResetToPending();
        await _emailQueueRepository.UpdateAsync(emailItem, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Email {EmailId} reset to pending for resend by user {UserId}",
            command.EmailId, command.UserId);

        return true;
    }
}
