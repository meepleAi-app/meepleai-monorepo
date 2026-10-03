using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// La fixture di <see cref="CreateLiveSessionCompanionIntegrationTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// #4050. Host condiviso per la classe, database fresco per test. Prima questa classe
/// costruiva l'host dentro il proprio <c>InitializeAsync</c>, che xUnit chiama una volta per
/// METODO: ~24s a test, contro ~0,1s per il clone del database.
/// </para>
/// </remarks>
public sealed class CreateLiveSessionCompanionIntegrationTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "create_live_companion");

/// <summary>
/// Integration test for the ADR-083 SP0 companion Saga wired into
/// <see cref="CreateLiveSessionCommandHandler"/> (Issue #2501 SP0).
///
/// Proves the happy path end-to-end against a real PostgreSQL (Testcontainers):
/// creating a live session with a catalog GameId persists BOTH the LiveGameSession
/// (with a non-null TrackingSessionId) AND a companion SessionTracking.Session whose
/// Id equals that TrackingSessionId — committed atomically in one EF transaction.
///
/// The companion Session FKs both <c>users</c> and <c>shared_games</c> (Restrict), so the
/// test seeds a user and a shared game before sending the command.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreateLiveSessionCompanionIntegrationTests
    : IClassFixture<CreateLiveSessionCompanionIntegrationTestsHostFixture>, IAsyncLifetime
{
    private readonly CreateLiveSessionCompanionIntegrationTestsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;

    public CreateLiveSessionCompanionIntegrationTests(CreateLiveSessionCompanionIntegrationTestsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
    }

    // Host, client e database appartengono alla fixture.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact(DisplayName = "Create live session with GameId persists a companion Session atomically and links TrackingSessionId")]
    public async Task Handle_WithGameId_PersistsCompanion_AndLinksTrackingSessionId()
    {
        // Arrange — seed a user + a shared game (both FK targets of the companion Session)
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);

        // Act — create the live session linked to the catalog game
        var liveId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Mage Knight",
            GameId: gameId));

        // Assert — LiveGameSession persisted with a non-null TrackingSessionId
        var live = await db.LiveGameSessions
            .AsNoTracking()
            .SingleAsync(s => s.Id == liveId);

        live.TrackingSessionId.Should().NotBeNull(
            "creating a live session with a catalog GameId must populate the companion id");

        // Assert — a companion SessionTracking.Session row exists with that exact id (atomic commit)
        var companionExists = await db.SessionTrackingSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == live.TrackingSessionId);

        companionExists.Should().BeTrue(
            "the companion Session and the LiveGameSession must be committed together in one transaction");
    }

    [Fact(DisplayName = "Create live session with a nonexistent GameId throws 404 NotFound and persists nothing (#2552)")]
    public async Task Handle_WithNonexistentGameId_ThrowsNotFound_AndPersistsNothing()
    {
        // Arrange — seed only the user; deliberately NO shared game, so the GameId is well-formed
        // (Guid != Empty, passes the validator) but points at no catalog row.
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var nonexistentGameId = Guid.NewGuid();

        // Act — the companion pre-flight existence check must reject the GameId BEFORE any insert,
        // so the FK violation never reaches SaveChanges as a 500.
        var act = () => mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Ghost Game",
            GameId: nonexistentGameId));

        // Assert — 404 NotFound for the missing Game (#2552, respects #2568), not a 500 FK violation.
        var ex = (await act.Should().ThrowAsync<NotFoundException>()).Which;
        ex.ResourceType.Should().Be("Game");

        // Assert — nothing persisted (no LiveGameSession, no orphan companion Session).
        (await db.LiveGameSessions.AsNoTracking().AnyAsync(s => s.CreatedByUserId == userId))
            .Should().BeFalse("the pre-flight guard must reject before persisting a LiveGameSession");
        (await db.SessionTrackingSessions.AsNoTracking().AnyAsync(s => s.UserId == userId))
            .Should().BeFalse("no orphan companion Session must be persisted when the game does not exist");
    }

    /// <summary>
    /// Seeds a minimal user entity required by CreateLiveSessionCommand and the companion Session FK.
    /// </summary>
    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"test-{userId:N}@companion-test.local",
            DisplayName = "Companion Test User",
            PasswordHash = "not-a-real-hash",
            Role = "user",
            Tier = "free",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return userId;
    }

    /// <summary>
    /// Seeds a minimal shared game entity — the companion Session FKs shared_games.id (Restrict).
    /// </summary>
    private static async Task<Guid> SeedSharedGameAsync(MeepleAiDbContext db)
    {
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Mage Knight",
            YearPublished = 2011,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 240,
            MinAge = 14,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return gameId;
    }
}
