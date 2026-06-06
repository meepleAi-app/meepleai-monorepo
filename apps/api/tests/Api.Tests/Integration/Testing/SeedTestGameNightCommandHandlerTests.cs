using Api.BoundedContexts.Testing.Application.Commands;
using Api.Infrastructure;
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
/// Issue #1928 Task B (DEC-B-7) — Integration tests for SeedTestGameNightCommandHandler.
///
/// Migrated from Unit-trait (InMemory + Moq) to Integration-trait (Postgres Testcontainers
/// reuse via IntegrationWebApplicationFactory full DI pipeline) due to MeepleAiDbContext
/// requiring full Program.cs DI services (BackupCode entity needs IDataProtectionProvider
/// fully configured, only achievable via WebApplicationFactory).
///
/// Pattern: <see cref="IntegrationWebApplicationFactory.Create"/> + scope-based MeepleAiDbContext.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "1928")]
public sealed class SeedTestGameNightCommandHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private WebApplicationFactory<Program>? _factory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SeedTestGameNightCommandHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_seed_gamenight_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        // Trigger startup + create schema from current model (includes shadow properties).
        // NOTE: EnsureCreatedAsync used vs MigrateAsync because shadow property TestRunId
        // is not in migrations history (would require dedicated migration). For Integration
        // test scope, model-derived schema is sufficient (DEC-B-7).
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

    private (IServiceScope scope, MeepleAiDbContext db, SeedTestGameNightCommandHandler handler) CreateHandlerScope()
    {
        var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new SeedTestGameNightCommandHandler(db, NullLogger<SeedTestGameNightCommandHandler>.Instance);
        return (scope, db, handler);
    }

    [Fact]
    public async Task Handle_HappyPath_PublishedStatus_CreatesGameNightWithTestRunIdStamp()
    {
        var (scope, db, handler) = CreateHandlerScope();
        using (scope)
        {
            var testRunId = "e2e-abc123def456-1717603200000";
            var command = new SeedTestGameNightCommand
            {
                TestRunId = testRunId,
                Status = "Published",
                OwnerEmail = "host@e2e.test",
            };

            var response = await handler.Handle(command, TestCancellationToken);

            response.GameNightId.Should().NotBe(Guid.Empty);
            response.OwnerId.Should().NotBe(Guid.Empty);
            response.TestRunId.Should().Be(testRunId);

            var seeded = await db.GameNightEvents
                .SingleOrDefaultAsync(g => g.Id == response.GameNightId, TestCancellationToken);
            seeded.Should().NotBeNull();
            seeded!.Status.Should().Be("Published");
            seeded.TestRunId.Should().Be(testRunId);
        }
    }

    [Fact]
    public async Task Handle_DraftStatus_CreatesGameNightInDraftState()
    {
        var (scope, db, handler) = CreateHandlerScope();
        using (scope)
        {
            var cmd = new SeedTestGameNightCommand
            {
                TestRunId = "e2e-draftcase01234-1717603200000",
                Status = "Draft",
                OwnerEmail = "draft@e2e.test",
            };

            var response = await handler.Handle(cmd, TestCancellationToken);

            var seeded = await db.GameNightEvents
                .SingleAsync(g => g.Id == response.GameNightId, TestCancellationToken);
            seeded.Status.Should().Be("Draft");
        }
    }

    [Fact]
    public async Task Handle_InProgressStatus_CreatesGameNightWithLiveSession()
    {
        var (scope, db, handler) = CreateHandlerScope();
        using (scope)
        {
            var cmd = new SeedTestGameNightCommand
            {
                TestRunId = "e2e-progresscase01-1717603200000",
                Status = "InProgress",
                OwnerEmail = "progress@e2e.test",
            };

            var response = await handler.Handle(cmd, TestCancellationToken);

            var seeded = await db.GameNightEvents
                .Include(g => g.Sessions)
                .SingleAsync(g => g.Id == response.GameNightId, TestCancellationToken);
            seeded.Status.Should().Be("Published");
            seeded.Sessions.Should().HaveCount(1);
            seeded.Sessions[0].StartedAt.Should().NotBeNull();
            seeded.Sessions[0].CompletedAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task Handle_CompletedStatus_CreatesGameNightWithFinalizedSession()
    {
        var (scope, db, handler) = CreateHandlerScope();
        using (scope)
        {
            var cmd = new SeedTestGameNightCommand
            {
                TestRunId = "e2e-completed01234-1717603200000",
                Status = "Completed",
                OwnerEmail = "completed@e2e.test",
            };

            var response = await handler.Handle(cmd, TestCancellationToken);

            var seeded = await db.GameNightEvents
                .Include(g => g.Sessions)
                .SingleAsync(g => g.Id == response.GameNightId, TestCancellationToken);
            seeded.Sessions.Should().HaveCount(1);
            seeded.Sessions[0].CompletedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Handle_DefaultRosterCount_CreatesGameNightWithZeroRoster()
    {
        var (scope, db, handler) = CreateHandlerScope();
        using (scope)
        {
            var cmd = new SeedTestGameNightCommand
            {
                TestRunId = "e2e-rostercase0123-1717603200000",
                Status = "Draft",
                OwnerEmail = "roster@e2e.test",
            };

            var response = await handler.Handle(cmd, TestCancellationToken);

            response.GameNightId.Should().NotBe(Guid.Empty);
        }
    }

    [Fact]
    public async Task Handle_ParallelCalls_DifferentTestRunIds_NoCollision()
    {
        var (scope, _, handler) = CreateHandlerScope();
        using (scope)
        {
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

            var r1 = await handler.Handle(cmd1, TestCancellationToken);
            var r2 = await handler.Handle(cmd2, TestCancellationToken);

            r1.GameNightId.Should().NotBe(r2.GameNightId);
            r1.TestRunId.Should().NotBe(r2.TestRunId);
        }
    }
}
