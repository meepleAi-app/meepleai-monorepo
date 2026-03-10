using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for sending game night reminders at 24h and 1h before the event.
/// Runs every 15 minutes, checks for events within the reminder windows.
/// Issue #45: GameNightReminderJob (Quartz) for automated reminders.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class GameNightReminderJob : IJob
{
    private readonly IGameNightEventRepository _repository;
    private readonly IMediator _mediator;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GameNightReminderJob> _logger;

    public GameNightReminderJob(
        IGameNightEventRepository repository,
        IMediator mediator,
        MeepleAiDbContext dbContext,
        ILogger<GameNightReminderJob> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogDebug("Starting game night reminder job: FireTime={FireTime}", context.FireTimeUtc);

        var reminder24hCount = 0;
        var reminder1hCount = 0;

        try
        {
            var now = DateTimeOffset.UtcNow;

            // 24h window: events where ScheduledAt is between now+23h45m and now+24h15m
            await ProcessReminders24h(now, context.CancellationToken).ConfigureAwait(false);

            // 1h window: events where ScheduledAt is between now+45m and now+1h15m
            await ProcessReminders1h(now, context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Game night reminder job completed: 24hReminders={Reminder24h}, 1hReminders={Reminder1h}",
                reminder24hCount, reminder1hCount);

            context.Result = new { Success = true, Reminder24h = reminder24hCount, Reminder1h = reminder1hCount };
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Game night reminder job failed");
            context.Result = new { Success = false, Error = ex.Message };
        }

        return;

        async Task ProcessReminders24h(DateTimeOffset now, CancellationToken ct)
        {
            var from24h = now.AddHours(23).AddMinutes(45);
            var to24h = now.AddHours(24).AddMinutes(15);

            var events24h = await _repository.GetEventsNeedingReminderAsync(from24h, to24h, ct).ConfigureAwait(false);

            foreach (var gameNight in events24h)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (gameNight.Reminder24hSentAt is not null)
                    continue;

                try
                {
                    await _mediator.Publish(
                        new GameNightReminder24hEvent(gameNight.Id, gameNight.Title, gameNight.ScheduledAt, gameNight.Location),
                        ct).ConfigureAwait(false);

                    gameNight.MarkReminder24hSent();
                    await _repository.UpdateAsync(gameNight, ct).ConfigureAwait(false);
                    await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

                    reminder24hCount++;
                    _logger.LogInformation(
                        "Sent 24h reminder for game night {GameNightId} '{Title}' scheduled at {ScheduledAt}",
                        gameNight.Id, gameNight.Title, gameNight.ScheduledAt);
                }
#pragma warning disable CA1031
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _logger.LogError(ex, "Failed to send 24h reminder for game night {GameNightId}", gameNight.Id);
                }
            }
        }

        async Task ProcessReminders1h(DateTimeOffset now, CancellationToken ct)
        {
            var from1h = now.AddMinutes(45);
            var to1h = now.AddHours(1).AddMinutes(15);

            var events1h = await _repository.GetEventsNeedingReminderAsync(from1h, to1h, ct).ConfigureAwait(false);

            foreach (var gameNight in events1h)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (gameNight.Reminder1hSentAt is not null)
                    continue;

                try
                {
                    await _mediator.Publish(
                        new GameNightReminder1hEvent(gameNight.Id, gameNight.Title, gameNight.ScheduledAt),
                        ct).ConfigureAwait(false);

                    gameNight.MarkReminder1hSent();
                    await _repository.UpdateAsync(gameNight, ct).ConfigureAwait(false);
                    await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

                    reminder1hCount++;
                    _logger.LogInformation(
                        "Sent 1h reminder for game night {GameNightId} '{Title}' scheduled at {ScheduledAt}",
                        gameNight.Id, gameNight.Title, gameNight.ScheduledAt);
                }
#pragma warning disable CA1031
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _logger.LogError(ex, "Failed to send 1h reminder for game night {GameNightId}", gameNight.Id);
                }
            }
        }
    }
}
