using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

internal class UpdateNotificationPreferencesCommandHandler : ICommandHandler<UpdateNotificationPreferencesCommand>
{
    private readonly INotificationPreferencesRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateNotificationPreferencesCommandHandler(
        INotificationPreferencesRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(UpdateNotificationPreferencesCommand command, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        var prefs = existing ?? new NotificationPreferences(command.UserId);

        prefs.UpdateAllPreferences(
            command.EmailOnDocumentReady, command.EmailOnDocumentFailed, command.EmailOnRetryAvailable,
            command.PushOnDocumentReady, command.PushOnDocumentFailed, command.PushOnRetryAvailable,
            command.InAppOnDocumentReady, command.InAppOnDocumentFailed, command.InAppOnRetryAvailable
        );

        if (existing is null)
            await _repository.AddAsync(prefs, cancellationToken).ConfigureAwait(false);
        else
            await _repository.UpdateAsync(prefs, cancellationToken).ConfigureAwait(false);

        // Issue #2849 / ADR-060: the repository Add/Update only stages the change;
        // the mutating handler must commit its own unit of work. Without this the
        // PUT returned 204 but nothing persisted (GET kept returning the old value).
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
