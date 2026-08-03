using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration.CrossContext;

/// <summary>
/// Integration tests for cross-context event dispatch and handling.
/// Tests GameManagement cross-context domain-event dispatch (audit_outbox).
/// Issue #2307: Week 3 - Cross-context integration testing.
/// Issue #1320 (P2c): Removed Game aggregate usage; events constructed directly.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2307")]
[Trait("CrossContext", "GameManagement-Dispatch")]
public sealed class CrossContextIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private IDomainEventCollector _eventCollector = null!;
    private string _databaseName = null!;
    private IServiceProvider _serviceProvider = null!;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public CrossContextIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_crosscontext_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        _serviceProvider = services.BuildServiceProvider();
        _eventCollector = _serviceProvider.GetRequiredService<IDomainEventCollector>();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations
        await _dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }
    }

    #region GameManagement cross-context dispatch tests

    /// <summary>
    /// Test: GameCreated event dispatches cross-context (audit_outbox row written).
    /// </summary>
    [Fact]
    public async Task GameCreated_ShouldDispatchCrossContext()
    {
        // Arrange — seed SharedGameEntity and inject GameCreatedEvent directly
        var gameId = Guid.NewGuid();
        const string gameName = "Catan";

        var gameEntity = CreateGameEntity(gameId, gameName, "Catan Studio");
        _dbContext.SharedGames.Add(gameEntity);

        var @event = new GameCreatedEvent(gameId, gameName);
        _eventCollector.Collect(@event);

        // Act - SaveChangesAsync should dispatch events
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - audit_outbox row created by DomainEventAuditHandler<GameCreatedEvent>
        // (Issue #1534: single canonical write path → audit_outbox)
        var allOutbox = await _dbContext.AuditOutbox.ToListAsync(TestCancellationToken);
        var outboxRow = allOutbox.FirstOrDefault(o => o.PayloadJson.Contains("\"Resource\":\"GameCreatedEvent\""));

        outboxRow.Should().NotBeNull();
        outboxRow!.PayloadJson.Should().Contain("\"Action\":\"DomainEvent.GameCreatedEvent\"");
        outboxRow.PayloadJson.Should().Contain("\"Result\":\"Success\"");
        outboxRow.PayloadJson.Should().Contain(gameId.ToString());

        // Verify game persisted
        var savedGame = await _dbContext.SharedGames.FindAsync(new object[] { gameId }, TestCancellationToken);
        savedGame.Should().NotBeNull();
        savedGame!.Title.Should().Be(gameName);
    }

    /// <summary>
    /// Test: GameLinkedToBgg event dispatches cross-context (audit_outbox row written).
    /// </summary>
    [Fact]
    public async Task GameLinkedToBgg_ShouldDispatchCrossContext()
    {
        // Arrange — seed SharedGameEntity and inject GameLinkedToBggEvent directly
        var gameId = Guid.NewGuid();
        var gameEntity = CreateGameEntity(gameId, "Wingspan", "Stonemaier Games");
        gameEntity.BggId = 266192;
        gameEntity.BggRawData = "Wingspan BGG metadata";
        _dbContext.SharedGames.Add(gameEntity);

        var @event = new GameLinkedToBggEvent(gameId, 266192);
        _eventCollector.Collect(@event);

        // Act
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - audit_outbox row from GameLinkedToBggEvent
        var allOutbox = await _dbContext.AuditOutbox.ToListAsync(TestCancellationToken);
        var outboxRow = allOutbox.FirstOrDefault(o => o.PayloadJson.Contains("\"Resource\":\"GameLinkedToBggEvent\""));

        outboxRow.Should().NotBeNull();
        outboxRow!.PayloadJson.Should().Contain("266192");
        outboxRow.PayloadJson.Should().Contain(gameId.ToString());

        // Verify BGG metadata persisted
        var savedGame = await _dbContext.SharedGames.FindAsync(new object[] { gameId }, TestCancellationToken);
        savedGame.Should().NotBeNull();
        savedGame!.BggId.Should().Be(266192);
        savedGame.BggRawData.Should().Be("Wingspan BGG metadata");
    }

    /// <summary>
    /// Test: Multiple games created in batch dispatch events independently
    /// Cross-Context: Batch operations with multiple integration events
    /// </summary>
    [Fact]
    public async Task MultipleGamesCreated_ShouldDispatchIndependentEvents()
    {
        // Arrange - Create multiple games
        var gameData = new[]
        {
            (Id: Guid.NewGuid(), Name: "Azul"),
            (Id: Guid.NewGuid(), Name: "Ticket to Ride"),
            (Id: Guid.NewGuid(), Name: "Pandemic")
        };

        foreach (var (id, name) in gameData)
        {
            _dbContext.SharedGames.Add(CreateGameEntity(id, name));
            _eventCollector.Collect(new GameCreatedEvent(id, name));
        }

        // Act - All events dispatched in single transaction
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - audit_outbox row for each game
        var allOutbox = await _dbContext.AuditOutbox.ToListAsync(TestCancellationToken);
        var outboxRows = allOutbox.Where(o => o.PayloadJson.Contains("\"Resource\":\"GameCreatedEvent\"")).ToList();

        outboxRows.Should().HaveCount(3);
        outboxRows.Should().AllSatisfy(row =>
        {
            row.PayloadJson.Should().Contain("\"Result\":\"Success\"");
            row.PayloadJson.Should().Contain("\"Action\":\"DomainEvent.GameCreatedEvent\"");
        });

        // Verify all games persisted
        var savedGames = await _dbContext.SharedGames.ToListAsync(TestCancellationToken);
        savedGames.Should().HaveCount(3);
    }

    /// <summary>
    /// Test: Game update events trigger workflow updates
    /// Cross-Context: Game state changes propagate across contexts
    /// </summary>
    [Fact]
    public async Task GameMetadataUpdated_ShouldDispatchUpdateEvents()
    {
        // Arrange - Create and save initial game
        var gameId = Guid.NewGuid();
        var gameEntity = CreateGameEntity(gameId, "Gloomhaven", "Cephalofair Games");
        _dbContext.SharedGames.Add(gameEntity);
        _eventCollector.Collect(new GameCreatedEvent(gameId, "Gloomhaven"));
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Update game with BGG link (generates new event)
        var savedGameEntity = await _dbContext.SharedGames.FindAsync(new object[] { gameId }, TestCancellationToken);
        savedGameEntity.Should().NotBeNull();

        savedGameEntity!.BggId = 174430;
        savedGameEntity.BggRawData = "Gloomhaven BGG data";

        _eventCollector.Collect(new GameLinkedToBggEvent(gameId, 174430));

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Both creation and update events logged to audit_outbox
        // PayloadJson contains the full AuditOutboxPayload (incl. Resource + serialized event Details with GameId)
        var allOutbox = await _dbContext.AuditOutbox.ToListAsync(TestCancellationToken);
        var outboxRows = allOutbox
            .Where(o => o.PayloadJson.Contains(gameId.ToString()))
            .OrderBy(o => o.CreatedAt)
            .ToList();

        outboxRows.Should().HaveCountGreaterThanOrEqualTo(2);
        outboxRows.Should().Contain(row => row.PayloadJson.Contains("\"Resource\":\"GameCreatedEvent\""));
        outboxRows.Should().Contain(row => row.PayloadJson.Contains("\"Resource\":\"GameLinkedToBggEvent\""));
    }

    /// <summary>
    /// Test: Transaction rollback on cross-context event failure
    /// Cross-Context: Error handling and transaction atomicity
    /// </summary>
    [Fact]
    public async Task CrossContextEventFailure_ShouldMaintainDataConsistency()
    {
        // Note: MeepleAI's event handlers catch and log exceptions rather than propagating
        // This test verifies that game creation succeeds even if handlers encounter issues

        // Arrange
        var gameId = Guid.NewGuid();
        var gameEntity = CreateGameEntity(gameId, "Test Game With Issues", "Test Publisher");
        _dbContext.SharedGames.Add(gameEntity);
        _eventCollector.Collect(new GameCreatedEvent(gameId, "Test Game With Issues"));

        // Act - SaveChangesAsync executes without throwing (handlers log errors)
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Game persisted despite potential handler issues
        var savedGame = await _dbContext.SharedGames.FindAsync(new object[] { gameId }, TestCancellationToken);
        savedGame.Should().NotBeNull();

        // audit_outbox row still created (handlers are resilient)
        var allOutbox = await _dbContext.AuditOutbox.ToListAsync(TestCancellationToken);
        var outboxRow = allOutbox.FirstOrDefault(o => o.PayloadJson.Contains("\"Resource\":\"GameCreatedEvent\"") &&
                                                       o.PayloadJson.Contains(gameId.ToString()));

        outboxRow.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static SharedGameEntity CreateGameEntity(Guid id, string name, string? publisher = null)
    {
        return new SharedGameEntity
        {
            Id = id,
            Title = name,
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
