using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

/// <summary>
/// Handler for RetryAllDeadLettersCommand.
/// Resets all dead-lettered emails to pending.
/// Issue #39: Admin email management.
/// </summary>
internal class RetryAllDeadLettersCommandHandler : ICommandHandler<RetryAllDeadLettersCommand, int>
{
    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RetryAllDeadLettersCommandHandler> _logger;

    public RetryAllDeadLettersCommandHandler(
        IEmailQueueRepository emailQueueRepository,
        IUnitOfWork unitOfWork,
        ILogger<RetryAllDeadLettersCommandHandler> logger)
    {
        _emailQueueRepository = emailQueueRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<int> Handle(RetryAllDeadLettersCommand command, CancellationToken cancellationToken)
    {
        var deadLetters = await _emailQueueRepository
            .GetDeadLetterAsync(0, 1000, cancellationToken)
            .ConfigureAwait(false);

        if (deadLetters.Count == 0)
        {
            _logger.LogInformation("No dead letter emails to retry");
            return 0;
        }

        foreach (var email in deadLetters)
        {
            email.ResetToPending();
            await _emailQueueRepository.UpdateAsync(email, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Admin bulk retry: {Count} dead letter emails reset to pending", deadLetters.Count);

        return deadLetters.Count;
    }
}
