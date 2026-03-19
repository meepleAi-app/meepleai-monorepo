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

        var prefs = await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false)
            ?? new NotificationPreferences(command.UserId);

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

        if (await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false) == null)
            await _repository.AddAsync(prefs, cancellationToken).ConfigureAwait(false);
        else
            await _repository.UpdateAsync(prefs, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated Slack preferences for user {UserId}", command.UserId);
    }
}
