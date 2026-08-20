using Api.BoundedContexts.Testing.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
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
/// Issue #1929 Task C Macro 4 (DEC-C-10 REVISION, DEC-B-8) — Integration tests for
/// SeedTestUserGameSessionCommandHandler. Validates UserGameSession rows persist with
/// explicit TestRunId column stamping; verifies FK guard for missing LibraryEntry;
/// verifies optional-field defaults; verifies cleanup cascade (via CleanupHandler).
///
/// Pattern: clone of <see cref="SeedTestLibraryGameCommandHandlerTests"/>;
/// EnsureCreatedAsync rebuilds schema from current model.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "1929")]
public sealed class SeedTestUserGameSessionCommandHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private WebApplicationFactory<Program>? _factory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SeedTestUserGameSessionCommandHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_seed_ugame_session_{Guid.NewGuid():N}";
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

    // ─── Helper ───────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedLibraryEntryAsync(MeepleAiDbContext db, string testRunId, string ownerEmail)
    {
        var libHandler = new SeedTestLibraryGameCommandHandler(
            db, NullLogger<SeedTestLibraryGameCommandHandler>.Instance);
        var result = await libHandler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunId,
            OwnerEmail = ownerEmail,
        }, TestCancellationToken);
        return result.LibraryEntryId;
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HappyPath_CreatesSessionWithTestRunIdStamp()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-ugame-happy000-1717603200000";

        var libraryEntryId = await SeedLibraryEntryAsync(db, testRunId, "happy-session@e2e.test");

        var handler = new SeedTestUserGameSessionCommandHandler(
            db, NullLogger<SeedTestUserGameSessionCommandHandler>.Instance);

        var response = await handler.Handle(new SeedTestUserGameSessionCommand
        {
            TestRunId = testRunId,
            UserLibraryEntryId = libraryEntryId,
            PlayedAt = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            DurationMinutes = 90,
            DidWin = true,
            Players = "Anna, Marco",
        }, TestCancellationToken);

        response.SessionId.Should().NotBe(Guid.Empty);
        response.UserLibraryEntryId.Should().Be(libraryEntryId);
        response.TestRunId.Should().Be(testRunId);

        var session = await db.Set<UserGameSessionEntity>()
            .SingleOrDefaultAsync(s => s.Id == response.SessionId, TestCancellationToken);
        session.Should().NotBeNull();
        session!.TestRunId.Should().Be(testRunId);
        session.UserLibraryEntryId.Should().Be(libraryEntryId);
        session.DurationMinutes.Should().Be(90);
        session.DidWin.Should().BeTrue();
        session.Players.Should().Be("Anna, Marco");
    }

    [Fact]
    public async Task Handle_UnknownLibraryEntryId_ThrowsBadRequestException()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-ugame-notfound-1717603200000";

        var handler = new SeedTestUserGameSessionCommandHandler(
            db, NullLogger<SeedTestUserGameSessionCommandHandler>.Instance);

        var act = async () => await handler.Handle(new SeedTestUserGameSessionCommand
        {
            TestRunId = testRunId,
            UserLibraryEntryId = Guid.NewGuid(), // does not exist
        }, TestCancellationToken);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*UserLibraryEntryId*not found*");
    }

    [Fact]
    public async Task Handle_OptionalFieldsNull_DefaultsApplied()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-ugame-defaults0-1717603200000";

        var libraryEntryId = await SeedLibraryEntryAsync(db, testRunId, "defaults-session@e2e.test");

        var handler = new SeedTestUserGameSessionCommandHandler(
            db, NullLogger<SeedTestUserGameSessionCommandHandler>.Instance);

        var beforeCall = DateTime.UtcNow;
        var response = await handler.Handle(new SeedTestUserGameSessionCommand
        {
            TestRunId = testRunId,
            UserLibraryEntryId = libraryEntryId,
            // PlayedAt, DurationMinutes, DidWin, Players all omitted → defaults
        }, TestCancellationToken);

        var session = await db.Set<UserGameSessionEntity>()
            .SingleAsync(s => s.Id == response.SessionId, TestCancellationToken);

        session.DurationMinutes.Should().Be(60);
        session.PlayedAt.Should().BeOnOrAfter(beforeCall);
        session.DidWin.Should().BeNull();
        session.Players.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MultipleSessionsSameEntry_AllCreatedWithSameLibraryEntryId()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-ugame-multisess-1717603200000";

        var libraryEntryId = await SeedLibraryEntryAsync(db, testRunId, "multi-session@e2e.test");

        var handler = new SeedTestUserGameSessionCommandHandler(
            db, NullLogger<SeedTestUserGameSessionCommandHandler>.Instance);

        for (var i = 0; i < 3; i++)
        {
            await handler.Handle(new SeedTestUserGameSessionCommand
            {
                TestRunId = testRunId,
                UserLibraryEntryId = libraryEntryId,
                PlayedAt = new DateTime(2026, 5, i + 1, 10, 0, 0, DateTimeKind.Utc),
            }, TestCancellationToken);
        }

        var count = await db.Set<UserGameSessionEntity>()
            .CountAsync(s => s.UserLibraryEntryId == libraryEntryId && s.TestRunId == testRunId, TestCancellationToken);
        count.Should().Be(3);
    }

    [Fact]
    public async Task Handle_TestRunIdScope_CleanupCascadesUserGameSessions()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-ugame-cascade00-1717603200000";

        var libraryEntryId = await SeedLibraryEntryAsync(db, testRunId, "cascade-session@e2e.test");

        var handler = new SeedTestUserGameSessionCommandHandler(
            db, NullLogger<SeedTestUserGameSessionCommandHandler>.Instance);

        await handler.Handle(new SeedTestUserGameSessionCommand
        {
            TestRunId = testRunId,
            UserLibraryEntryId = libraryEntryId,
        }, TestCancellationToken);

        // Verify session exists before cleanup
        var beforeCleanup = await db.Set<UserGameSessionEntity>()
            .AnyAsync(s => s.TestRunId == testRunId, TestCancellationToken);
        beforeCleanup.Should().BeTrue();

        // Execute cleanup
        var cleanupHandler = new CleanupTestEntitiesCommandHandler(
            db, NullLogger<CleanupTestEntitiesCommandHandler>.Instance);
        var cleanupResponse = await cleanupHandler.Handle(
            new CleanupTestEntitiesCommand { TestRunId = testRunId },
            TestCancellationToken);

        // Session deleted before library entry (FK cascade order)
        cleanupResponse.DeletedUserGameSessions.Should().Be(1);
        cleanupResponse.DeletedLibraryEntries.Should().Be(1);

        var afterCleanup = await db.Set<UserGameSessionEntity>()
            .AnyAsync(s => s.TestRunId == testRunId, TestCancellationToken);
        afterCleanup.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ResponseShape_SessionIdMatchesDbRow()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-ugame-shape0000-1717603200000";

        var libraryEntryId = await SeedLibraryEntryAsync(db, testRunId, "shape-session@e2e.test");

        var handler = new SeedTestUserGameSessionCommandHandler(
            db, NullLogger<SeedTestUserGameSessionCommandHandler>.Instance);

        var response = await handler.Handle(new SeedTestUserGameSessionCommand
        {
            TestRunId = testRunId,
            UserLibraryEntryId = libraryEntryId,
        }, TestCancellationToken);

        var session = await db.Set<UserGameSessionEntity>()
            .SingleOrDefaultAsync(s => s.Id == response.SessionId, TestCancellationToken);
        session.Should().NotBeNull();
        session!.UserLibraryEntryId.Should().Be(response.UserLibraryEntryId);
        session.TestRunId.Should().Be(response.TestRunId);
    }
}
