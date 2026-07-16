using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Handler for UpdateSlackPreferencesCommand.
/// Updates the Slack notification preferences for a user.
/// </summary>
internal class UpdateSlackPreferencesCommandHandler : ICommandHandler<UpdateSlackPreferencesCommand>
{
    private readonly INotificationPreferencesRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateSlackPreferencesCommandHandler> _logger;

    public UpdateSlackPreferencesCommandHandler(
        INotificationPreferencesRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateSlackPreferencesCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _unitOfWork = unitOfWork;
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task Handle(UpdateSlackPreferencesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Capture the load result once. The previous double-read (a second GetByUserIdAsync to decide
        // Add-vs-Update) is the TOCTOU pattern #2849/#2872 removed from the sibling handler: on a
        // first-time save it could pick UpdateAsync against a freshly-generated Id, affecting 0 rows and
        // throwing DbUpdateConcurrencyException (spurious 409). This PR makes the Slack save path live
        // (FE now calls it on every save), so we apply the same one-read fix here.
        var existing = await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        var prefs = existing ?? new NotificationPreferences(command.UserId);

        prefs.UpdateSlackPreferences(
            command.SlackEnabled,
            command.SlackOnDocumentReady,
            command.SlackOnDocumentFailed,
            command.SlackOnRetryAvailable,
            command.SlackOnGameNightInvitation,
            command.SlackOnGameNightReminder,
            command.SlackOnShareRequestCreated,
            command.SlackOnShareRequestApproved,
            command.SlackOnBadgeEarned);

        if (existing is null)
            await _repository.AddAsync(prefs, cancellationToken).ConfigureAwait(false);
        else
            await _repository.UpdateAsync(prefs, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated Slack preferences for user {UserId}", command.UserId);
    }
}
