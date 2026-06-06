using Api.BoundedContexts.Testing.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
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
/// Issue #1929 Task C Macro 3a (DEC-C-8, DEC-B-7, DEC-B-8) — Integration tests for
/// SeedTestLibraryGameCommandHandler. Validates SharedGame + UserLibraryEntry rows
/// persist with explicit TestRunId column stamping; verifies handler reuses an
/// existing user when found by email.
///
/// Pattern: clone of <see cref="SeedTestPlayerCommandHandlerTests"/>; EnsureCreatedAsync
/// rebuilds schema from current model (TestRunId columns not in migrations yet).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Testing")]
[Trait("Issue", "1929")]
public sealed class SeedTestLibraryGameCommandHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private WebApplicationFactory<Program>? _factory;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public SeedTestLibraryGameCommandHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_seed_library_{Guid.NewGuid():N}";
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

    [Fact]
    public async Task Handle_HappyPath_NewUser_CreatesGameAndLibraryEntryWithTestRunIdStamps()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-libgamenewuser-1717603200000";

        var handler = new SeedTestLibraryGameCommandHandler(
            db, NullLogger<SeedTestLibraryGameCommandHandler>.Instance);

        var response = await handler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunId,
            OwnerEmail = "newuser@e2e.test",
        }, TestCancellationToken);

        response.GameId.Should().NotBe(Guid.Empty);
        response.LibraryEntryId.Should().NotBe(Guid.Empty);
        response.OwnerId.Should().NotBe(Guid.Empty);
        response.TestRunId.Should().Be(testRunId);

        var sharedGame = await db.SharedGames
            .SingleOrDefaultAsync(g => g.Id == response.GameId, TestCancellationToken);
        sharedGame.Should().NotBeNull();
        sharedGame!.TestRunId.Should().Be(testRunId);

        var libraryEntry = await db.UserLibraryEntries
            .SingleOrDefaultAsync(e => e.Id == response.LibraryEntryId, TestCancellationToken);
        libraryEntry.Should().NotBeNull();
        libraryEntry!.TestRunId.Should().Be(testRunId);
        libraryEntry.UserId.Should().Be(response.OwnerId);
        libraryEntry.SharedGameId.Should().Be(response.GameId);
    }

    [Fact]
    public async Task Handle_HappyPath_ExistingUser_ReusesUserIdAndCreatesNewLibraryEntry()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-libgameexisti-1717603200000";

        var handler = new SeedTestLibraryGameCommandHandler(
            db, NullLogger<SeedTestLibraryGameCommandHandler>.Instance);

        // 1st call creates user + game1
        var first = await handler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunId,
            OwnerEmail = "shared-owner@e2e.test",
        }, TestCancellationToken);

        // 2nd call should reuse user
        var second = await handler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunId,
            OwnerEmail = "shared-owner@e2e.test",
        }, TestCancellationToken);

        second.OwnerId.Should().Be(first.OwnerId);
        second.GameId.Should().NotBe(first.GameId);
        second.LibraryEntryId.Should().NotBe(first.LibraryEntryId);
    }

    [Fact]
    public async Task Handle_DefaultTitle_ConstructsFromTestRunIdPrefix()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-defaulttitle0-1717603200000";

        var handler = new SeedTestLibraryGameCommandHandler(
            db, NullLogger<SeedTestLibraryGameCommandHandler>.Instance);

        var response = await handler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunId,
            OwnerEmail = "default-title@e2e.test",
            Title = null,
        }, TestCancellationToken);

        var sharedGame = await db.SharedGames
            .SingleAsync(g => g.Id == response.GameId, TestCancellationToken);
        sharedGame.Title.Should().StartWith("E2E Library Game ");
        sharedGame.Title.Should().Contain(testRunId[..8]);
    }

    [Fact]
    public async Task Handle_CustomTitle_RespectedExactly()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-customtitle00-1717603200000";

        var handler = new SeedTestLibraryGameCommandHandler(
            db, NullLogger<SeedTestLibraryGameCommandHandler>.Instance);

        var response = await handler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunId,
            OwnerEmail = "custom-title@e2e.test",
            Title = "Catan E2E Edition",
        }, TestCancellationToken);

        var sharedGame = await db.SharedGames
            .SingleAsync(g => g.Id == response.GameId, TestCancellationToken);
        sharedGame.Title.Should().Be("Catan E2E Edition");
    }

    [Fact]
    public async Task Handle_TestRunIdStamped_OnBothSharedGameAndLibraryEntry()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-trsstamp00000-1717603200000";

        var handler = new SeedTestLibraryGameCommandHandler(
            db, NullLogger<SeedTestLibraryGameCommandHandler>.Instance);

        var response = await handler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunId,
            OwnerEmail = "stamp-test@e2e.test",
        }, TestCancellationToken);

        var sharedGameTestRunId = await db.SharedGames
            .Where(g => g.Id == response.GameId)
            .Select(g => g.TestRunId)
            .SingleAsync(TestCancellationToken);
        sharedGameTestRunId.Should().Be(testRunId);

        var libraryEntryTestRunId = await db.UserLibraryEntries
            .Where(e => e.Id == response.LibraryEntryId)
            .Select(e => e.TestRunId)
            .SingleAsync(TestCancellationToken);
        libraryEntryTestRunId.Should().Be(testRunId);
    }

    [Fact]
    public async Task Handle_ResponseShape_GameIdAndLibraryEntryIdMatchDbRows()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var testRunId = "e2e-respshape0000-1717603200000";

        var handler = new SeedTestLibraryGameCommandHandler(
            db, NullLogger<SeedTestLibraryGameCommandHandler>.Instance);

        var response = await handler.Handle(new SeedTestLibraryGameCommand
        {
            TestRunId = testRunId,
            OwnerEmail = "resp-shape@e2e.test",
            MinPlayers = 3,
            MaxPlayers = 6,
        }, TestCancellationToken);

        var sharedGame = await db.SharedGames
            .SingleAsync(g => g.Id == response.GameId, TestCancellationToken);
        sharedGame.MinPlayers.Should().Be(3);
        sharedGame.MaxPlayers.Should().Be(6);
        sharedGame.Status.Should().Be(1); // Published
        sharedGame.GameDataStatus.Should().Be(5); // Complete

        var libraryEntry = await db.UserLibraryEntries
            .Include(e => e.SharedGame)
            .SingleAsync(e => e.Id == response.LibraryEntryId, TestCancellationToken);
        libraryEntry.SharedGame.Should().NotBeNull();
        libraryEntry.SharedGame!.Id.Should().Be(response.GameId);
    }
}
