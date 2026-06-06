using Api.BoundedContexts.Testing.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.Integration.Testing;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-3, DEC-B-8) — Integration tests for
/// CleanupTestEntitiesCommandHandler cascade-by-TestRunId.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "1928")]
public sealed class CleanupTestEntitiesCommandHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private WebApplicationFactory<Program>? _factory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public CleanupTestEntitiesCommandHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_cleanup_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.EnsureCreatedAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null)
        {
            await _factory.DisposeAsync();
        }
    }

    /// <summary>Seeds 1 GameNight Published + 1 RSVP player + 1 guest invitation + 1 live session.</summary>
    private async Task SeedFullScopeAsync(MeepleAiDbContext db, string testRunId)
    {
        var gnHandler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
        var sessHandler = new SeedTestSessionCommandHandler(db, NullLogger<SeedTestSessionCommandHandler>.Instance);
        var playerHandler = new SeedTestPlayerCommandHandler(db, NullLogger<SeedTestPlayerCommandHandler>.Instance);

        var gn = await gnHandler.Handle(new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Published",
            OwnerEmail = $"owner-{testRunId[..16]}@e2e.test",
        }, TestCancellationToken);

        await sessHandler.Handle(new SeedTestSessionCommand
        {
            TestRunId = testRunId,
            GameNightId = gn.GameNightId,
            IsLive = true,
        }, TestCancellationToken);

        await playerHandler.Handle(new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = gn.GameNightId,
            Role = "player",
        }, TestCancellationToken);

        await playerHandler.Handle(new SeedTestPlayerCommand
        {
            TestRunId = testRunId,
            GameNightId = gn.GameNightId,
            Role = "guest",
            DisplayName = "E2E Guest",
        }, TestCancellationToken);
    }

    [Fact]
    public async Task Handle_HappyPath_DeletesAllScopedEntities_PreservesOthers()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunIdA = "e2e-cleanupAaaaaa-1717603200000";
        var testRunIdB = "e2e-cleanupBbbbbb-1717603200000";

        await SeedFullScopeAsync(db, testRunIdA);
        await SeedFullScopeAsync(db, testRunIdB);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var response = await handler.Handle(
            new CleanupTestEntitiesCommand { TestRunId = testRunIdA },
            TestCancellationToken);

        response.DeletedGameNights.Should().Be(1);
        response.DeletedSessions.Should().Be(1);
        response.DeletedInvitations.Should().Be(1);
        response.DeletedRsvps.Should().Be(1);
        response.DeletedUsers.Should().BeGreaterThanOrEqualTo(1);

        // Scope A fully deleted
        (await db.GameNightEvents.AnyAsync(g => g.TestRunId == testRunIdA, TestCancellationToken))
            .Should().BeFalse();
        // Scope B preserved
        (await db.GameNightEvents.AnyAsync(g => g.TestRunId == testRunIdB, TestCancellationToken))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Handle_EmptyScope_TestRunIdNoEntities_ReturnsZeros()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var response = await handler.Handle(
            new CleanupTestEntitiesCommand { TestRunId = "e2e-emptyscope0000-1717603200000" },
            TestCancellationToken);

        response.DeletedGameNights.Should().Be(0);
        response.DeletedSessions.Should().Be(0);
        response.DeletedInvitations.Should().Be(0);
        response.DeletedRsvps.Should().Be(0);
        response.DeletedUsers.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OnlyGameNight_NoChildren_DeletesGameNightAndUser()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-onlygn0000000-1717603200000";

        var gnHandler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
        await gnHandler.Handle(new SeedTestGameNightCommand
        {
            TestRunId = testRunId,
            Status = "Draft",
            OwnerEmail = "onlygn@e2e.test",
        }, TestCancellationToken);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var response = await handler.Handle(
            new CleanupTestEntitiesCommand { TestRunId = testRunId },
            TestCancellationToken);

        response.DeletedGameNights.Should().Be(1);
        response.DeletedUsers.Should().Be(1);
        response.DeletedSessions.Should().Be(0);
    }

    [Fact]
    public async Task Handle_IdempotentRetry_SecondCallReturnsZeros()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-idempotent000-1717603200000";
        await SeedFullScopeAsync(db, testRunId);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var first = await handler.Handle(new CleanupTestEntitiesCommand { TestRunId = testRunId }, TestCancellationToken);
        var second = await handler.Handle(new CleanupTestEntitiesCommand { TestRunId = testRunId }, TestCancellationToken);

        first.DeletedGameNights.Should().Be(1);
        second.DeletedGameNights.Should().Be(0);
        second.DeletedSessions.Should().Be(0);
        second.DeletedUsers.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ParallelCleanups_DifferentTestRunIds_NoCollision()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunIdA = "e2e-parallelcleA0-1717603200000";
        var testRunIdB = "e2e-parallelcleB0-1717603200000";
        await SeedFullScopeAsync(db, testRunIdA);
        await SeedFullScopeAsync(db, testRunIdB);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var rA = await handler.Handle(new CleanupTestEntitiesCommand { TestRunId = testRunIdA }, TestCancellationToken);
        var rB = await handler.Handle(new CleanupTestEntitiesCommand { TestRunId = testRunIdB }, TestCancellationToken);

        rA.DeletedGameNights.Should().Be(1);
        rB.DeletedGameNights.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ResponseShape_CorrectFields()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-shapecase0123-1717603200000";
        await SeedFullScopeAsync(db, testRunId);

        var handler = new CleanupTestEntitiesCommandHandler(db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var response = await handler.Handle(new CleanupTestEntitiesCommand { TestRunId = testRunId }, TestCancellationToken);

        response.TestRunId.Should().Be(testRunId);
        response.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }
}
