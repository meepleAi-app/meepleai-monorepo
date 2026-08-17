using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Infrastructure.Scheduling;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Reliability tests for <see cref="GameNightReminderJob"/> — Issue #2720. Proves the reminder
/// flag is durably persisted so a subsequent job run (fresh scope, as in production) does NOT
/// re-publish the reminder. Before the fix, the job loaded the event tracked and persisted the
/// flag via the detached <c>UpdateAsync</c>, which threw an EF identity conflict (a second
/// instance of the same key) — swallowed by the per-event catch — so the flag was never written
/// and every run re-sent the reminder.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2720")]
public sealed class GameNightReminderJobReliabilityTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public GameNightReminderJobReliabilityTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"reminder_job_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private static IJobExecutionContext JobContext()
    {
        var ctx = new Mock<IJobExecutionContext>();
        ctx.SetupAllProperties();
        ctx.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        ctx.Setup(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        return ctx.Object;
    }

    private async Task RunJobInFreshScopeAsync(IMediator mediator)
    {
        await using var db = _fixture.CreateDbContext(_connectionString);
        var repo = new GameNightEventRepository(
            db, Mock.Of<IDomainEventCollector>(), NullLogger<GameNightEventRepository>.Instance);
        var job = new GameNightReminderJob(repo, mediator, db, NullLogger<GameNightReminderJob>.Instance);
        await job.Execute(JobContext());
    }

    [Fact(DisplayName = "Reminder is marked once and not re-published on a subsequent run")]
    public async Task Execute_MarksReminder_AndDoesNotRepublish()
    {
        // Seed a Published event whose ScheduledAt sits in the 24h reminder window.
        var eventId = Guid.NewGuid();
        _dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = Guid.NewGuid(),
            Title = "Serata promemoria",
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(24),
            GameIdsJson = "[]",
            Status = "Published",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _dbContext.SaveChangesAsync(Ct);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Publish(It.IsAny<GameNightReminder24hEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Two independent job runs (each with a fresh DbContext scope, as in production).
        await RunJobInFreshScopeAsync(mediator.Object);
        await RunJobInFreshScopeAsync(mediator.Object);

        // The reminder must have been published exactly once...
        mediator.Verify(
            m => m.Publish(It.IsAny<GameNightReminder24hEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // ...and the SentAt flag durably persisted (so the second run skipped it).
        await using var verify = _fixture.CreateDbContext(_connectionString);
        var row = await verify.GameNightEvents.AsNoTracking().FirstAsync(e => e.Id == eventId, Ct);
        row.Reminder24hSentAt.Should().NotBeNull();
    }
}
