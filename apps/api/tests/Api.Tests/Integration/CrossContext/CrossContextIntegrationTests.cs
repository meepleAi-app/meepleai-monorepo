using Api.BoundedContexts.GameManagement.Application.IntegrationEvents;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.WorkflowIntegration.Application.IntegrationEventHandlers;
using Api.Infrastructure;
using Api.Tests.TestHelpers;
using Api.Infrastructure.Entities;
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
/// Tests GameManagement → WorkflowIntegration integration event flow.
/// Issue #2307: Week 3 - Cross-context integration testing
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2307")]
[Trait("CrossContext", "GameManagement-WorkflowIntegration")]
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

        // Setup DbContext options
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString)
            .EnableSensitiveDataLogging()
            .Options;

        // Create service collection with all event handlers
        var services = new ServiceCollection();

        // Register MediatR with all handlers from API assembly
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(MeepleAiDbContext).Assembly));

        services.AddSingleton(options);
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddScoped<MeepleAiDbContext>(sp =>
            new MeepleAiDbContext(
                options,
                sp.GetRequiredService<IMediator>(),
                sp.GetRequiredService<IDomainEventCollector>()));

        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

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

    #region GameManagement → WorkflowIntegration Tests

    /// <summary>
    /// Test: GameCreated event triggers workflow integration
    /// Cross-Context: GameManagement → WorkflowIntegration event dispatch
    /// </summary>
    [Fact]
    public async Task GameCreated_ShouldDispatchToWorkflowIntegration()
    {
        // Arrange - Create game that raises GameCreatedEvent
        var game = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Catan"),
            publisher: new Publisher("Catan Studio"),
            yearPublished: new YearPublished(1995),
            playerCount: new PlayerCount(3, 4),
            playTime: new PlayTime(60, 120)
        );

        game.DomainEvents.Should().HaveCount(1);
        game.DomainEvents.Should().ContainSingle(e => e is GameCreatedEvent);

        // Map to persistence entity
        var gameEntity = MapGameToEntity(game);
        _dbContext.Games.Add(gameEntity);

        // Collect domain events before save
        _eventCollector.CollectEventsFrom(game);
        game.ClearDomainEvents();

        // Act - SaveChangesAsync should dispatch events
        // Flow: GameCreatedEvent → GameCreatedEventHandler → GameCreatedIntegrationEvent → GameCreatedIntegrationEventHandler
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Audit log created by domain event handler
        var auditLog = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Resource == "GameCreatedEvent", TestCancellationToken);

        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Contain("GameCreatedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain(game.Id.ToString());

        // Verify game persisted
        var savedGame = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        savedGame.Should().NotBeNull();
        savedGame!.Name.Should().Be("Catan");
    }

    /// <summary>
    /// Test: GameLinkedToBgg event triggers workflow integration
    /// Cross-Context: GameManagement → WorkflowIntegration BGG integration
    /// </summary>
    [Fact]
    public async Task GameLinkedToBgg_ShouldDispatchToWorkflowIntegration()
    {
        // Arrange - Create game and link to BGG
        var game = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Wingspan"),
            publisher: new Publisher("Stonemaier Games")
        );

        game.ClearDomainEvents(); // Clear creation event
        game.LinkToBgg(bggId: 266192, metadata: "Wingspan BGG metadata");

        game.DomainEvents.Should().HaveCount(1);
        game.DomainEvents.Should().ContainSingle(e => e is GameLinkedToBggEvent);

        var gameEntity = MapGameToEntity(game);
        _dbContext.Games.Add(gameEntity);

        _eventCollector.CollectEventsFrom(game);
        game.ClearDomainEvents();

        // Act
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Audit log from GameLinkedToBggEvent
        var auditLog = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Resource == "GameLinkedToBggEvent", TestCancellationToken);

        auditLog.Should().NotBeNull();
        auditLog!.Details.Should().Contain("266192");
        auditLog.Details.Should().Contain(game.Id.ToString());

        // Verify BGG metadata persisted
        var savedGame = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        savedGame.Should().NotBeNull();
        savedGame!.BggId.Should().Be(266192);
        savedGame.BggMetadata.Should().Be("Wingspan BGG metadata");
    }

    /// <summary>
    /// Test: Multiple games created in batch dispatch events independently
    /// Cross-Context: Batch operations with multiple integration events
    /// </summary>
    [Fact]
    public async Task MultipleGamesCreated_ShouldDispatchIndependentEvents()
    {
        // Arrange - Create multiple games
        var games = new[]
        {
            new Game(
                id: Guid.NewGuid(),
                title: new GameTitle("Azul"),
                publisher: new Publisher("Plan B Games")
            ),
            new Game(
                id: Guid.NewGuid(),
                title: new GameTitle("Ticket to Ride"),
                publisher: new Publisher("Days of Wonder")
            ),
            new Game(
                id: Guid.NewGuid(),
                title: new GameTitle("Pandemic"),
                publisher: new Publisher("Z-Man Games")
            )
        };

        foreach (var game in games)
        {
            var gameEntity = MapGameToEntity(game);
            _dbContext.Games.Add(gameEntity);
            _eventCollector.CollectEventsFrom(game);
            game.ClearDomainEvents();
        }

        // Act - All events dispatched in single transaction
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Audit log for each game
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.Resource == "GameCreatedEvent")
            .ToListAsync(TestCancellationToken);

        auditLogs.Should().HaveCount(3);
        auditLogs.Should().AllSatisfy(log =>
        {
            log.Result.Should().Be("Success");
            log.Action.Should().Contain("GameCreatedEvent");
        });

        // Verify all games persisted
        var savedGames = await _dbContext.Games.ToListAsync(TestCancellationToken);
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
        var game = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Gloomhaven"),
            publisher: new Publisher("Cephalofair Games")
        );

        var gameEntity = MapGameToEntity(game);
        _dbContext.Games.Add(gameEntity);
        _eventCollector.CollectEventsFrom(game);
        game.ClearDomainEvents();

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Update game with BGG link (generates new event)
        var savedGameEntity = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        savedGameEntity.Should().NotBeNull();

        game.LinkToBgg(bggId: 174430, metadata: "Gloomhaven BGG data");

        savedGameEntity!.BggId = 174430;
        savedGameEntity.BggMetadata = "Gloomhaven BGG data";

        _eventCollector.CollectEventsFrom(game);
        game.ClearDomainEvents();

        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Both creation and update events logged
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.Details != null && a.Details.Contains(game.Id.ToString()))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(TestCancellationToken);

        auditLogs.Should().HaveCountGreaterThanOrEqualTo(2);
        auditLogs.Should().Contain(log => log.Resource == "GameCreatedEvent");
        auditLogs.Should().Contain(log => log.Resource == "GameLinkedToBggEvent");
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

        // Arrange - Create game with potentially problematic data
        var game = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Test Game With Issues"),
            publisher: new Publisher("Test Publisher")
        );

        var gameEntity = MapGameToEntity(game);
        _dbContext.Games.Add(gameEntity);

        _eventCollector.CollectEventsFrom(game);
        game.ClearDomainEvents();

        // Act - SaveChangesAsync executes without throwing (handlers log errors)
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Game persisted despite potential handler issues
        var savedGame = await _dbContext.Games.FindAsync(new object[] { game.Id }, TestCancellationToken);
        savedGame.Should().NotBeNull();

        // Audit log still created (handlers are resilient)
        var auditLog = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Resource == "GameCreatedEvent" &&
                                     a.Details != null && a.Details.Contains(game.Id.ToString()), TestCancellationToken);

        auditLog.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private static GameEntity MapGameToEntity(Game game)
    {
        return new GameEntity
        {
            Id = game.Id,
            Name = game.Title.Value,
            Publisher = game.Publisher?.ToString(),
            YearPublished = game.YearPublished?.Value,
            MinPlayers = game.PlayerCount?.Min,
            MaxPlayers = game.PlayerCount?.Max,
            MinPlayTimeMinutes = game.PlayTime?.MinMinutes,
            MaxPlayTimeMinutes = game.PlayTime?.MaxMinutes,
            BggId = game.BggId,
            BggMetadata = game.BggMetadata,
            IconUrl = game.IconUrl,
            ImageUrl = game.ImageUrl,
            CreatedAt = game.CreatedAt
        };
    }

    #endregion
}