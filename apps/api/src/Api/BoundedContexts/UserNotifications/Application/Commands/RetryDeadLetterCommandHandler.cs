using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Handler for RetryDeadLetterCommand.
/// Resets a dead-lettered notification queue item to Pending status for reprocessing.
/// Clears the error message and resets retry count.
/// </summary>
internal class RetryDeadLetterCommandHandler : ICommandHandler<RetryDeadLetterCommand, bool>
{
    private readonly MeepleAiDbContext _dbContext;

    public RetryDeadLetterCommandHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(RetryDeadLetterCommand command, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Set<NotificationQueueEntity>()
            .FirstOrDefaultAsync(e => e.Id == command.ItemId && e.Status == "dead_letter", cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return false;

        entity.Status = "pending";
        entity.LastError = null;
        entity.RetryCount = 0;
        entity.NextRetryAt = null;
        entity.ProcessedAt = null;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
