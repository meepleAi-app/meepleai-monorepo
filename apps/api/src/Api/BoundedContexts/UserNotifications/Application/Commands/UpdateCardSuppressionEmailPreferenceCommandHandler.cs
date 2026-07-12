using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

internal sealed class UpdateCardSuppressionEmailPreferenceCommandHandler
    : ICommandHandler<UpdateCardSuppressionEmailPreferenceCommand>
{
    private readonly INotificationPreferencesRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCardSuppressionEmailPreferenceCommandHandler(
        INotificationPreferencesRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(UpdateCardSuppressionEmailPreferenceCommand command, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        var prefs = existing ?? new NotificationPreferences(command.UserId);
        prefs.UpdateCardSuppressionEmailPreference(command.EmailOnCardSuppressed);

        if (existing is null)
            await _repository.AddAsync(prefs, cancellationToken).ConfigureAwait(false);
        else
            await _repository.UpdateAsync(prefs, cancellationToken).ConfigureAwait(false);

        // ADR-060: mutating handlers commit their own unit of work (repo Add/Update only stage).
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
