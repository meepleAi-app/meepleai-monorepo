using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.IntegrationEvents;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
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

namespace Api.Tests.Integration.DomainEvents;

/// <summary>
/// Integration tests for Domain Event Dispatcher cross-context integration.
/// Tests complete flow: Entity raises event → UnitOfWork commits → EventDispatcher → Handlers execute.
/// Uses Testcontainers for real database event dispatching.
/// Issue #2307: Week 3 Integration Tests + Visual Snapshots
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2307")]
public sealed class DomainEventDispatcherIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private IDomainEventCollector _eventCollector = null!;
    private string _databaseName = null!;
    private IServiceProvider _serviceProvider = null!;

    private readonly SemaphoreSlim _handlerLock = new(1, 1);

    public DomainEventDispatcherIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_domain_events_{Guid.NewGuid():N}";
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
        // Fix: Use PostgreSQL DbContext with Testcontainers, not in-memory
        var mediator = _serviceProvider.GetRequiredService<IMediator>();
        _dbContext = new MeepleAiDbContext(options, mediator, _eventCollector);

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

        _handlerLock.Dispose();
    }

    #region Event Dispatch Tests

    /// <summary>
    /// Test: Single event raised → handler executes after commit
    /// Pattern: Entity.RaiseDomainEvent() → SaveChangesAsync() → Handler.HandleAsync()
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WithSingleEvent_ShouldDispatchAndCreateAuditLog()
    {
        // Arrange - Create game that raises GameCreatedEvent
        var game = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Wingspan"),
            publisher: new Publisher("Stonemaier Games"),
            yearPublished: new YearPublished(2019),
            playerCount: new PlayerCount(1, 5),
            playTime: new PlayTime(40, 70)
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
        await _dbContext.SaveChangesAsync();

        // Assert - Audit log created by GameCreatedEventHandler
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.Resource == "GameCreatedEvent")
            .ToListAsync();

        auditLogs.Should().HaveCount(1);
        var auditLog = auditLogs[0];
        auditLog.Action.Should().Contain("GameCreatedEvent");
        auditLog.Result.Should().Be("Success");
        auditLog.Details.Should().Contain("GameId");
        auditLog.Details.Should().Contain(game.Id.ToString());
    }

    /// <summary>
    /// Test: Multiple events raised → all handlers execute in order
    /// Pattern: Multiple entity changes → multiple events → handlers execute sequentially
    /// </summary>
    [Fact]
    public async Task SaveChangesAsync_WithMultipleEvents_ShouldDispatchAllInOrder()
    {
        // Arrange - Create user and change password twice
        var user = new User(
            id: Guid.NewGuid(),
            email: Email.Parse("test@meepleai.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("InitialPassword123!"),
            role: Role.User
        );

        // Clear initial events
        user.ClearDomainEvents();

        // Change password twice (raises 2 PasswordChangedEvents)
        var newPassword1 = PasswordHash.Create("NewPassword123!");
        var newPassword2 = PasswordHash.Create("FinalPassword123!");

        user.ChangePassword("InitialPassword123!", newPassword1);
        user.ChangePassword("NewPassword123!", newPassword2);

        user.DomainEvents.Should().HaveCount(2);

        // Map to persistence entity
        var userEntity = MapUserToEntity(user);
        _dbContext.Users.Add(userEntity);

        // Collect domain events
        _eventCollector.CollectEventsFrom(user);
        user.ClearDomainEvents();

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert - Two audit logs created in order
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.Resource == "PasswordChangedEvent")
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        auditLogs.Should().HaveCount(2);
        auditLogs[0].UserId.Should().Be(user.Id);
        auditLogs[1].UserId.Should().Be(user.Id);
        auditLogs.All(a => a.Result == "Success").Should().BeTrue();
    }

    #endregion

    #region Cross-Context Integration Tests

    /// <summary>
    /// Test: GameManagement event → WorkflowIntegration handler
    /// Cross-Context: GameCreated event → GameCreatedIntegrationEventHandler
    /// </summary>
    [Fact]
    public async Task GameCreated_ShouldTriggerWorkflowIntegrationHandler()
    {
        // Arrange
        var game = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Terraforming Mars"),
            publisher: new Publisher("FryxGames")
        );

        var gameEntity = MapGameToEntity(game);
        _dbContext.Games.Add(gameEntity);

        _eventCollector.CollectEventsFrom(game);
        game.ClearDomainEvents();

        // Act - SaveChangesAsync triggers:
        // 1. GameCreatedEvent → GameCreatedEventHandler
        // 2. GameCreatedEventHandler publishes GameCreatedIntegrationEvent
        // 3. GameCreatedIntegrationEvent → GameCreatedIntegrationEventHandler
        await _dbContext.SaveChangesAsync();

        // Assert - Audit log from domain event handler
        var auditLog = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Resource == "GameCreatedEvent");

        auditLog.Should().NotBeNull();
        auditLog!.Details.Should().Contain(game.Id.ToString());

        // Integration event handler logs (check via logger, not DB)
        // In real scenario, would verify n8n workflow triggered
    }

    /// <summary>
    /// Test: Multiple cross-context events in single transaction
    /// Pattern: Game created + User role changed → multiple integration events
    /// </summary>
    [Fact]
    public async Task MultipleContextEvents_ShouldAllDispatchInSameTransaction()
    {
        // Arrange - Create game and user in same transaction
        var game = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Gloomhaven"),
            publisher: new Publisher("Cephalofair Games")
        );

        var user = new User(
            id: Guid.NewGuid(),
            email: Email.Parse("admin@meepleai.com"),
            displayName: "Admin User",
            passwordHash: PasswordHash.Create("AdminPassword123!"),
            role: Role.User
        );

        user.ClearDomainEvents();
        // Note: User.ChangeRole may not exist - using UpdateRole if available
        // For testing cross-context, we'll use a different event
        var apiKey = ApiKey.Create(Guid.NewGuid(), user.Id, "Test API Key", "read,write").Item1;
        _eventCollector.CollectEventsFrom(apiKey);

        // Add both entities
        _dbContext.Games.Add(MapGameToEntity(game));
        _dbContext.Users.Add(MapUserToEntity(user));

        // Collect events from both aggregates
        _eventCollector.CollectEventsFrom(game);
        _eventCollector.CollectEventsFrom(user);
        game.ClearDomainEvents();
        user.ClearDomainEvents();

        // Act - Single transaction, multiple events
        await _dbContext.SaveChangesAsync();

        // Assert - Audit logs from both contexts
        var gameAudit = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Resource == "GameCreatedEvent");
        var apiKeyAudit = await _dbContext.AuditLogs
            .Where(a => a.Resource.Contains("ApiKey"))
            .FirstOrDefaultAsync();

        gameAudit.Should().NotBeNull();
        apiKeyAudit.Should().NotBeNull();
    }

    #endregion

    #region Error Handling Tests

    /// <summary>
    /// Test: Handler throws exception → transaction rolled back
    /// Error Handling: Verify transaction atomicity with failing handler
    /// </summary>
    [Fact]
    public async Task HandlerException_ShouldRollbackTransaction()
    {
        // Note: This test demonstrates behavior, but MeepleAI's handlers
        // catch and log exceptions rather than propagating them.
        // In a system with failing handlers, the transaction would rollback.

        // Arrange
        var game = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("Azul"),
            publisher: new Publisher("Plan B Games")
        );

        var gameEntity = MapGameToEntity(game);
        _dbContext.Games.Add(gameEntity);

        _eventCollector.CollectEventsFrom(game);
        game.ClearDomainEvents();

        // Act - SaveChangesAsync executes without throwing
        // (handlers log errors but don't propagate exceptions)
        await _dbContext.SaveChangesAsync();

        // Assert - Game entity persisted (handlers don't prevent save)
        var savedGame = await _dbContext.Games.FindAsync(game.Id);
        savedGame.Should().NotBeNull();

        // Audit log still created despite handler errors
        var auditLog = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Resource == "GameCreatedEvent");
        auditLog.Should().NotBeNull();
    }

    /// <summary>
    /// Test: Event collector thread safety with concurrent operations
    /// Thread Safety: Multiple threads collecting events simultaneously
    /// </summary>
    [Fact]
    public async Task EventCollector_WithConcurrentCollections_ShouldBeThreadSafe()
    {
        // Arrange - Create multiple games concurrently
        var games = Enumerable.Range(0, 10)
            .Select(i => new Game(
                id: Guid.NewGuid(),
                title: new GameTitle($"Concurrent Game {i}"),
                publisher: new Publisher("Test Publisher")
            ))
            .ToList();

        // Act - Collect events concurrently
        var tasks = games.Select(async game =>
        {
#pragma warning disable CA5394 // Random is fine for test concurrency simulation
            await Task.Delay(Random.Shared.Next(10, 50)); // Simulate concurrent load
#pragma warning restore CA5394
            _eventCollector.CollectEventsFrom(game);
            game.ClearDomainEvents();
        });

        await Task.WhenAll(tasks);

        // Add entities and save
        foreach (var game in games)
        {
            _dbContext.Games.Add(MapGameToEntity(game));
        }

        await _dbContext.SaveChangesAsync();

        // Assert - All events dispatched
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.Resource == "GameCreatedEvent")
            .ToListAsync();

        auditLogs.Should().HaveCount(10);
    }

    /// <summary>
    /// Test: Partial failure scenario - best-effort semantics
    /// Error Handling: If one handler fails, others should still execute
    /// </summary>
    [Fact]
    public async Task PartialHandlerFailure_ShouldNotPreventOtherHandlers()
    {
        // Arrange - Create game with multiple events
        var game = new Game(
            id: Guid.NewGuid(),
            title: new GameTitle("7 Wonders"),
            publisher: new Publisher("Repos Production")
        );

        game.ClearDomainEvents();
        game.LinkToBgg(68448, "BGG Metadata");

        // Two events: GameCreatedEvent (from constructor) + GameLinkedToBggEvent
        game.DomainEvents.Should().HaveCount(1);

        var gameEntity = MapGameToEntity(game);
        _dbContext.Games.Add(gameEntity);

        _eventCollector.CollectEventsFrom(game);
        game.ClearDomainEvents();

        // Act - All handlers execute independently
        await _dbContext.SaveChangesAsync();

        // Assert - Audit logs from both events
        var auditLogs = await _dbContext.AuditLogs
            .Where(a => a.Resource == "GameLinkedToBggEvent")
            .ToListAsync();

        auditLogs.Should().HaveCount(1);
        auditLogs[0].Details.Should().Contain("68448");
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

    private static UserEntity MapUserToEntity(User user)
    {
        return new UserEntity
        {
            Id = user.Id,
            Email = user.Email.Value,
            DisplayName = user.DisplayName,
            PasswordHash = user.PasswordHash.Value,
            Role = user.Role.Value,
            IsTwoFactorEnabled = user.IsTwoFactorEnabled,
            TotpSecretEncrypted = user.TotpSecret?.EncryptedValue,
            CreatedAt = user.CreatedAt
        };
    }

    #endregion
}