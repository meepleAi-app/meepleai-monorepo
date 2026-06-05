using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.Testing.Application.Commands;
using Api.BoundedContexts.Testing.Application.DTOs;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.Unit.Testing;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Testing")]
public class SeedTestGameNightCommandHandlerTests
{
    private Api.Infrastructure.MeepleAiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<Api.Infrastructure.MeepleAiDbContext>()
            .UseInMemoryDatabase($"TestDb-{Guid.NewGuid()}")
            .Options;
        return new Api.Infrastructure.MeepleAiDbContext(
            options,
            Mock.Of<IMediator>(),
            Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>(),
            null,
            NullLogger<Api.Infrastructure.MeepleAiDbContext>.Instance);
    }

    [Fact]
    public async Task Handle_HappyPath_PublishedStatus_CreatesGameNightWithTestRunIdStamp()
    {
        using var db = CreateDbContext();
        var handler = new Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler(db, NullLogger<Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler>.Instance);
        var testRunId = "e2e-abc123def456-1717603200000";
        var command = new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Published",
            OwnerEmail = "host@e2e.test",
        };

        var response = await handler.Handle(command, CancellationToken.None);

        response.GameNightId.Should().NotBe(Guid.Empty);
        response.OwnerId.Should().NotBe(Guid.Empty);
        var seeded = await db.GameNightEvents.SingleOrDefaultAsync(g => g.Id == response.GameNightId);
        seeded.Should().NotBeNull();
        seeded!.Status.Should().Be("Published");
        var testRunIdShadow = db.Entry(seeded).Property<string?>("TestRunId").CurrentValue;
        testRunIdShadow.Should().Be(testRunId);
    }

    [Fact]
    public async Task Handle_DraftStatus_CreatesGameNightInDraftState()
    {
        using var db = CreateDbContext();
        var handler = new Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler(db, NullLogger<Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler>.Instance);
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-draftcase01234-1717603200000",
            Status = "Draft",
            OwnerEmail = "draft@e2e.test",
        };

        var response = await handler.Handle(cmd, CancellationToken.None);

        var seeded = await db.GameNightEvents.SingleAsync(g => g.Id == response.GameNightId);
        seeded.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Handle_InProgressStatus_CreatesGameNightWithLiveSession()
    {
        using var db = CreateDbContext();
        var handler = new Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler(db, NullLogger<Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler>.Instance);
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-progresscase01-1717603200000",
            Status = "InProgress",
            OwnerEmail = "progress@e2e.test",
        };

        var response = await handler.Handle(cmd, CancellationToken.None);

        var seeded = await db.GameNightEvents
            .Include(g => g.Sessions)
            .SingleAsync(g => g.Id == response.GameNightId);
        seeded.Status.Should().Be("Published");
        seeded.Sessions.Should().HaveCount(1);
        seeded.Sessions[0].StartedAt.Should().NotBeNull();
        seeded.Sessions[0].CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CompletedStatus_CreatesGameNightWithFinalizedSession()
    {
        using var db = CreateDbContext();
        var handler = new Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler(db, NullLogger<Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler>.Instance);
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-completed01234-1717603200000",
            Status = "Completed",
            OwnerEmail = "completed@e2e.test",
        };

        var response = await handler.Handle(cmd, CancellationToken.None);

        var seeded = await db.GameNightEvents
            .Include(g => g.Sessions)
            .SingleAsync(g => g.Id == response.GameNightId);
        seeded.Sessions.Should().HaveCount(1);
        seeded.Sessions[0].CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_DefaultRosterCount_CreatesGameNightWithZeroRoster()
    {
        using var db = CreateDbContext();
        var handler = new Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler(db, NullLogger<Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler>.Instance);
        var cmd = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-rostercase0123-1717603200000",
            Status = "Draft",
            OwnerEmail = "roster@e2e.test",
            // RosterCount default 0
        };

        var response = await handler.Handle(cmd, CancellationToken.None);

        response.GameNightId.Should().NotBe(Guid.Empty);
        // RosterCount > 0 is OUT of scope T1 (Player seeding is T3) — just verify no crash
    }

    [Fact]
    public async Task Handle_ParallelCalls_DifferentTestRunIds_NoCollision()
    {
        using var db = CreateDbContext();
        var handler = new Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler(db, NullLogger<Api.BoundedContexts.Testing.Application.Commands.SeedTestGameNightCommandHandler>.Instance);
        var cmd1 = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-parallel001234-1717603200000",
            Status = "Draft",
            OwnerEmail = "p1@e2e.test",
        };
        var cmd2 = new SeedTestGameNightCommand
        {
            TestRunId = "e2e-parallel002345-1717603200000",
            Status = "Draft",
            OwnerEmail = "p2@e2e.test",
        };

        var r1 = await handler.Handle(cmd1, CancellationToken.None);
        var r2 = await handler.Handle(cmd2, CancellationToken.None);

        r1.GameNightId.Should().NotBe(r2.GameNightId);
        r1.TestRunId.Should().NotBe(r2.TestRunId);
    }
}
