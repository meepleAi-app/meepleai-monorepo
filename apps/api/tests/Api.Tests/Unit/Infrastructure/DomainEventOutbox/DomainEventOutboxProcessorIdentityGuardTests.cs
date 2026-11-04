using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundJobs;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Infrastructure.DomainEventOutbox;

/// <summary>
/// #4841 — the outbox row's <c>Id</c> is the event's <c>EventId</c>. When the rebuilt event comes back with
/// another id, its type does not keep the id across the JSON round trip and every dedupe keyed on it is
/// ineffective: the processor still delivers the event, and says so in a Warning.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class DomainEventOutboxProcessorIdentityGuardTests
{
    private const string KeptAlias = "test.kept";
    private const string LostAlias = "test.lost";

    [Fact]
    public async Task An_event_that_keeps_its_EventId_is_delivered_without_a_warning()
    {
        var (published, warnings) = await RunOnce<KeepsItsId>(KeptAlias);

        published.Should().ContainSingle();
        warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task An_event_that_loses_its_EventId_is_still_delivered_and_a_warning_names_both_ids()
    {
        var (published, warnings) = await RunOnce<LosesItsId>(LostAlias);

        published.Should().ContainSingle("the guard is not fatal: the event is delivered");
        var warning = warnings.Should().ContainSingle().Subject;
        warning.Should().Contain(published[0].EventId.ToString(), "the rebuilt id");
        warning.Should().Contain(LostAlias, "the event type alias");
    }

    private static async Task<(List<IDomainEvent> Published, List<string> Warnings)> RunOnce<TEvent>(string alias)
        where TEvent : IDomainEvent, new()
    {
        var database = Guid.NewGuid().ToString();
        var original = new TEvent();

        using (var seed = TestDbContextFactory.CreateInMemoryDbContext(database))
        {
            seed.DomainEventOutbox.Add(DomainEventOutboxEntity.Enqueue(
                original,
                alias,
                JsonSerializer.Serialize(original, typeof(TEvent), DomainEventJsonOptions.Default),
                payloadVersion: 1,
                correlationId: null,
                now: DateTimeOffset.UtcNow));
            await seed.SaveChangesAsync();
        }

        var published = new List<IDomainEvent>();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((e, _) => published.Add(e))
            .Returns(Task.CompletedTask);
        var resolver = new Mock<IDomainEventTypeResolver>();
        resolver.Setup(r => r.Resolve(alias)).Returns(typeof(TEvent));

        var services = new ServiceCollection();
        services.AddScoped(_ => TestDbContextFactory.CreateInMemoryDbContext(database));
        services.AddSingleton(mediator.Object);
        services.AddSingleton(resolver.Object);
        using var provider = services.BuildServiceProvider();

        var logger = new CapturingLogger<DomainEventOutboxProcessor>();
        var processor = new DomainEventOutboxProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger,
            Options.Create(new DomainEventOutboxOptions()),
            Mock.Of<IDomainEventOutboxHealthTracker>());

        await processor.RunOnceAsync(batchSize: 10, CancellationToken.None);

        using var check = TestDbContextFactory.CreateInMemoryDbContext(database);
        check.DomainEventOutbox.Single().Status.Should().Be(DomainEventOutboxStatus.Sent);

        return (published, logger.Entries.Where(e => e.Level == LogLevel.Warning).Select(e => e.Message).ToList());
    }

    public sealed class KeepsItsId : IDomainEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    }

    public sealed class LosesItsId : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
