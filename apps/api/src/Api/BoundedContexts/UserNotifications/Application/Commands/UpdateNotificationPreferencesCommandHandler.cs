using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

internal class UpdateNotificationPreferencesCommandHandler : ICommandHandler<UpdateNotificationPreferencesCommand>
{
    private readonly INotificationPreferencesRepository _repository;

    public UpdateNotificationPreferencesCommandHandler(INotificationPreferencesRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task Handle(UpdateNotificationPreferencesCommand command, CancellationToken cancellationToken)
    {
        var prefs = await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false)
            ?? new NotificationPreferences(command.UserId);

        prefs.UpdateAllPreferences(
            command.EmailOnDocumentReady, command.EmailOnDocumentFailed, command.EmailOnRetryAvailable,
            command.PushOnDocumentReady, command.PushOnDocumentFailed, command.PushOnRetryAvailable,
            command.InAppOnDocumentReady, command.InAppOnDocumentFailed, command.InAppOnRetryAvailable
        );

        if (await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false) == null)
            await _repository.AddAsync(prefs, cancellationToken).ConfigureAwait(false);
        else
            await _repository.UpdateAsync(prefs, cancellationToken).ConfigureAwait(false);
    }
}
