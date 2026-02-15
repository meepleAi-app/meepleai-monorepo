using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for AdminRetryEmailCommand.
/// Resets a dead-lettered email back to pending for reprocessing.
/// No user authorization check - admin-only endpoint.
/// Issue #4430: Email queue dashboard monitoring.
/// </summary>
internal class AdminRetryEmailCommandHandler : ICommandHandler<AdminRetryEmailCommand, bool>
{
    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AdminRetryEmailCommandHandler> _logger;

    public AdminRetryEmailCommandHandler(
        IEmailQueueRepository emailQueueRepository,
        IUnitOfWork unitOfWork,
        ILogger<AdminRetryEmailCommandHandler> logger)
    {
        _emailQueueRepository = emailQueueRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<bool> Handle(AdminRetryEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var emailItem = await _emailQueueRepository.GetByIdAsync(command.EmailId, cancellationToken).ConfigureAwait(false);

        if (emailItem == null)
        {
            _logger.LogWarning("Admin retry: Email {EmailId} not found", command.EmailId);
            return false;
        }

        if (!emailItem.Status.IsDeadLetter && !emailItem.Status.IsFailed)
        {
            _logger.LogWarning("Admin retry: Email {EmailId} is in status '{Status}', cannot retry",
                command.EmailId, emailItem.Status.Value);
            return false;
        }

        emailItem.ResetToPending();
        await _emailQueueRepository.UpdateAsync(emailItem, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Admin force-retry: Email {EmailId} reset to pending", command.EmailId);

        return true;
    }
}
