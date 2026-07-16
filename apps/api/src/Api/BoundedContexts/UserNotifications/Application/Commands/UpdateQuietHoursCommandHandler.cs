using System.Globalization;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Handles <see cref="UpdateQuietHoursCommand"/> (ADR-076, issue #2995).
/// <para>
/// Load-or-create the user's preferences, apply the quiet-hours window via the domain mutator,
/// then persist. The repository maps the domain object to a fresh entity and calls
/// <c>DbContext.Update(...)</c> (force-modified), so persistence is correct even under the global
/// PERF-06 NoTracking policy. The explicit <see cref="IUnitOfWork.SaveChangesAsync"/> is required
/// (ADR-060): the repository Add/Update only stages the change.
/// </para>
/// </summary>
internal sealed class UpdateQuietHoursCommandHandler : ICommandHandler<UpdateQuietHoursCommand>
{
    private readonly INotificationPreferencesRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateQuietHoursCommandHandler(
        INotificationPreferencesRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(UpdateQuietHoursCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        var prefs = existing ?? new NotificationPreferences(command.UserId);

        var start = ParseTime(command.QuietHoursStart);
        var end = ParseTime(command.QuietHoursEnd);

        prefs.UpdateQuietHours(command.TimeZone, start, end);

        if (existing is null)
            await _repository.AddAsync(prefs, cancellationToken).ConfigureAwait(false);
        else
            await _repository.UpdateAsync(prefs, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TimeOnly? ParseTime(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : TimeOnly.Parse(value, CultureInfo.InvariantCulture);
}
