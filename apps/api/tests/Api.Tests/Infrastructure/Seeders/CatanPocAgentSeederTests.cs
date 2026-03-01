using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Seeders;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Seeders;

/// <summary>
/// Integration tests for CatanPocAgentSeeder.
/// Validates idempotency, entity creation, FK relationships, and graceful failure.
/// Issue #4667
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CatanPocAgentSeederTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;

    private static readonly Guid TestUserId = new("80000000-0000-0000-0000-000000000088");
    private static readonly Guid TestSharedGameId = new("80000000-0000-0000-0000-000000000089");
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public CatanPocAgentSeederTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"meepleai_catanpoc_seed_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        var mockMediator = new Mock<MediatR.IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector.Setup(x => x.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
            await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    #region Prerequisite Seeding Helpers

    private async Task SeedPrerequisitesAsync()
    {
        await SeedAdminUserAsync();
        await SeedSharedGameAsync();
    }

    private async Task SeedAdminUserAsync()
    {
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test-catanpoc-seed@meepleai.dev",
            DisplayName = "Test Admin CatanPoc",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Set<UserEntity>().Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task SeedSharedGameAsync()
    {
        var sharedGame = new SharedGameEntity
        {
            Id = TestSharedGameId,
            Title = "Catan",
            BggId = 13,
            YearPublished = 1995,
            Description = "Trade, build, settle",
            MinPlayers = 3,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90,
            MinAge = 10,
            ImageUrl = "https://example.com/catan.jpg",
            ThumbnailUrl = "https://example.com/catan_thumb.jpg",
            Status = 1, // Published
            CreatedBy = TestUserId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _dbContext!.SharedGames.Add(sharedGame);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #endregion

    #region Happy Path

    [Fact]
    public async Task SeedAsync_CreatesAllEntities()
    {
        // Arrange
        await SeedPrerequisitesAsync();
        var logger = new Mock<ILogger>();

        // Act
        await CatanPocAgentSeeder.SeedAsync(_dbContext!, logger.Object, cancellationToken: TestCancellationToken);

        // Assert - All 7 entity types exist
        var games = await _dbContext!.Games
            .Where(g => g.Name == "Catan").ToListAsync(TestCancellationToken);
        games.Should().HaveCount(1);

        var typologies = await _dbContext.AgentTypologies
            .Where(t => t.Name == "Rules Expert" && !t.IsDeleted).ToListAsync(TestCancellationToken);
        typologies.Should().HaveCount(1);

        var promptTemplates = await _dbContext.Set<TypologyPromptTemplateEntity>()
            .Where(pt => pt.TypologyId == typologies[0].Id).ToListAsync(TestCancellationToken);
        promptTemplates.Should().HaveCount(1);

        var agents = await _dbContext.Set<AgentEntity>()
            .Where(a => a.Name == "Catan POC Agent").ToListAsync(TestCancellationToken);
        agents.Should().HaveCount(1);

        var configs = await _dbContext.Set<AgentConfigurationEntity>()
            .Where(c => c.AgentId == agents[0].Id).ToListAsync(TestCancellationToken);
        configs.Should().HaveCount(1);

        var gameSessions = await _dbContext.GameSessions
            .Where(gs => gs.GameId == games[0].Id).ToListAsync(TestCancellationToken);
        gameSessions.Should().HaveCount(1);

        var agentSessions = await _dbContext.AgentSessions
            .Where(s => s.AgentId == agents[0].Id && s.IsActive).ToListAsync(TestCancellationToken);
        agentSessions.Should().HaveCount(1);
    }

    #endregion

    #region Idempotency

    [Fact]
    public async Task SeedAsync_IsIdempotent_WhenRunTwice()
    {
        // Arrange
        await SeedPrerequisitesAsync();
        var logger = new Mock<ILogger>();

        // Act - Run twice
        await CatanPocAgentSeeder.SeedAsync(_dbContext!, logger.Object, cancellationToken: TestCancellationToken);
        await CatanPocAgentSeeder.SeedAsync(_dbContext!, logger.Object, cancellationToken: TestCancellationToken);

        // Assert - No duplicates
        var games = await _dbContext!.Games
            .Where(g => g.Name == "Catan").ToListAsync(TestCancellationToken);
        games.Should().HaveCount(1);

        var typologies = await _dbContext.AgentTypologies
            .Where(t => t.Name == "Rules Expert" && !t.IsDeleted).ToListAsync(TestCancellationToken);
        typologies.Should().HaveCount(1);

        var agents = await _dbContext.Set<AgentEntity>()
            .Where(a => a.Name == "Catan POC Agent").ToListAsync(TestCancellationToken);
        agents.Should().HaveCount(1);

        var configs = await _dbContext.Set<AgentConfigurationEntity>()
            .Where(c => c.AgentId == agents[0].Id).ToListAsync(TestCancellationToken);
        configs.Should().HaveCount(1);

        var agentSessions = await _dbContext.AgentSessions
            .Where(s => s.AgentId == agents[0].Id && s.IsActive).ToListAsync(TestCancellationToken);
        agentSessions.Should().HaveCount(1);
    }

    #endregion

    #region Entity Property Verification

    [Fact]
    public async Task SeedAsync_CreatesGameEntity_WithCorrectProperties()
    {
        // Arrange
        await SeedPrerequisitesAsync();
        var logger = new Mock<ILogger>();

        // Act
        await CatanPocAgentSeeder.SeedAsync(_dbContext!, logger.Object, cancellationToken: TestCancellationToken);

        // Assert
        var game = await _dbContext!.Games
            .FirstAsync(g => g.Name == "Catan", TestCancellationToken);

        game.Name.Should().Be("Catan");
        game.Publisher.Should().Be("Kosmos");
        game.BggId.Should().Be(13);
        game.YearPublished.Should().Be(1995);
        game.MinPlayers.Should().Be(3);
        game.MaxPlayers.Should().Be(4);
        game.MinPlayTimeMinutes.Should().Be(60);
        game.MaxPlayTimeMinutes.Should().Be(120);
        game.Language.Should().Be("en");
        game.VersionType.Should().Be("base");
        game.VersionNumber.Should().Be("1.0");
        game.SharedGameId.Should().Be(TestSharedGameId);
    }

    [Fact]
    public async Task SeedAsync_CreatesAgentTypology_WithApprovedStatus()
    {
        // Arrange
        await SeedPrerequisitesAsync();
        var logger = new Mock<ILogger>();

        // Act
        await CatanPocAgentSeeder.SeedAsync(_dbContext!, logger.Object, cancellationToken: TestCancellationToken);

        // Assert
        var typology = await _dbContext!.AgentTypologies
            .FirstAsync(t => t.Name == "Rules Expert" && !t.IsDeleted, TestCancellationToken);

        typology.Status.Should().Be(2); // Approved
        typology.CreatedBy.Should().Be(TestUserId);
        typology.ApprovedBy.Should().Be(TestUserId);
        typology.ApprovedAt.Should().NotBeNull();
        typology.IsDeleted.Should().BeFalse();
        typology.BasePrompt.Should().Contain("Rules Expert");

        // Verify associated prompt template
        var promptTemplate = await _dbContext.Set<TypologyPromptTemplateEntity>()
            .FirstAsync(pt => pt.TypologyId == typology.Id, TestCancellationToken);

        promptTemplate.Version.Should().Be(1);
        promptTemplate.IsCurrent.Should().BeTrue();
        promptTemplate.CreatedBy.Should().Be(TestUserId);
    }

    [Fact]
    public async Task SeedAsync_CreatesAgent_WithCorrectConfiguration()
    {
        // Arrange
        await SeedPrerequisitesAsync();
        var logger = new Mock<ILogger>();

        // Act
        await CatanPocAgentSeeder.SeedAsync(_dbContext!, logger.Object, cancellationToken: TestCancellationToken);

        // Assert - Agent
        var agent = await _dbContext!.Set<AgentEntity>()
            .FirstAsync(a => a.Name == "Catan POC Agent", TestCancellationToken);

        agent.Type.Should().Be("RAG");
        agent.StrategyName.Should().Be("SingleModel");
        agent.IsActive.Should().BeTrue();
        agent.InvocationCount.Should().Be(0);
        agent.LastInvokedAt.Should().BeNull();

        // Assert - Configuration
        var config = await _dbContext.Set<AgentConfigurationEntity>()
            .FirstAsync(c => c.AgentId == agent.Id, TestCancellationToken);

        config.LlmProvider.Should().Be(0); // OpenRouter
        config.LlmModel.Should().Be("anthropic/claude-3-haiku");
        config.AgentMode.Should().Be(0); // Chat
        config.Temperature.Should().Be(0.3m);
        config.MaxTokens.Should().Be(2048);
        config.IsCurrent.Should().BeTrue();
        config.CreatedBy.Should().Be(TestUserId);
        config.SystemPromptOverride.Should().Contain("Catan Rules Expert");
        config.SystemPromptOverride.Should().Contain("{RAG_CONTEXT}");
        config.SelectedDocumentIdsJson.Should().Be("[]");
    }

    [Fact]
    public async Task SeedAsync_CreatesAgentSession_LinkingAllEntities()
    {
        // Arrange
        await SeedPrerequisitesAsync();
        var logger = new Mock<ILogger>();

        // Act
        await CatanPocAgentSeeder.SeedAsync(_dbContext!, logger.Object, cancellationToken: TestCancellationToken);

        // Assert
        var agent = await _dbContext!.Set<AgentEntity>()
            .FirstAsync(a => a.Name == "Catan POC Agent", TestCancellationToken);
        var game = await _dbContext.Games
            .FirstAsync(g => g.Name == "Catan", TestCancellationToken);
        var typology = await _dbContext.AgentTypologies
            .FirstAsync(t => t.Name == "Rules Expert" && !t.IsDeleted, TestCancellationToken);

        var agentSession = await _dbContext.AgentSessions
            .FirstAsync(s => s.AgentId == agent.Id && s.IsActive, TestCancellationToken);

        agentSession.AgentId.Should().Be(agent.Id);
        agentSession.GameId.Should().Be(game.Id);
        agentSession.TypologyId.Should().Be(typology.Id);
        agentSession.UserId.Should().Be(TestUserId);
        agentSession.IsActive.Should().BeTrue();
        agentSession.EndedAt.Should().BeNull();
        agentSession.CurrentGameStateJson.Should().Contain("setup");

        // Verify GameSession FK
        var gameSession = await _dbContext.GameSessions
            .FirstAsync(gs => gs.Id == agentSession.GameSessionId, TestCancellationToken);
        gameSession.GameId.Should().Be(game.Id);
        gameSession.CreatedByUserId.Should().Be(TestUserId);
        gameSession.Status.Should().Be("Setup");
    }

    #endregion

    #region Graceful Failure

    [Fact]
    public async Task SeedAsync_SkipsSeeding_WhenNoAdminUser()
    {
        // Arrange - No prerequisites seeded (empty DB after migration)
        var logger = new Mock<ILogger>();

        // Act - Should not throw
        await CatanPocAgentSeeder.SeedAsync(_dbContext!, logger.Object, cancellationToken: TestCancellationToken);

        // Assert - No entities created
        var games = await _dbContext!.Games.ToListAsync(TestCancellationToken);
        games.Should().BeEmpty();

        var agents = await _dbContext.Set<AgentEntity>().ToListAsync(TestCancellationToken);
        agents.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_SkipsSeeding_WhenNoSharedGame()
    {
        // Arrange - Only admin user, no SharedGame
        await SeedAdminUserAsync();
        var logger = new Mock<ILogger>();

        // Act - Should not throw
        await CatanPocAgentSeeder.SeedAsync(_dbContext!, logger.Object, cancellationToken: TestCancellationToken);

        // Assert - No game-related entities created
        var games = await _dbContext!.Games.ToListAsync(TestCancellationToken);
        games.Should().BeEmpty();

        var agents = await _dbContext.Set<AgentEntity>().ToListAsync(TestCancellationToken);
        agents.Should().BeEmpty();
    }

    #endregion

    #region TimeProvider

    [Fact]
    public async Task SeedAsync_UsesProvidedTimeProvider()
    {
        // Arrange
        await SeedPrerequisitesAsync();
        var logger = new Mock<ILogger>();
        var fixedTime = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var fakeTimeProvider = new FakeTimeProvider(fixedTime);

        // Act
        await CatanPocAgentSeeder.SeedAsync(
            _dbContext!, logger.Object, fakeTimeProvider, TestCancellationToken);

        // Assert - Verify entities use the injected time
        var game = await _dbContext!.Games
            .FirstAsync(g => g.Name == "Catan", TestCancellationToken);
        game.CreatedAt.Should().Be(fixedTime.UtcDateTime);

        var typology = await _dbContext.AgentTypologies
            .FirstAsync(t => t.Name == "Rules Expert" && !t.IsDeleted, TestCancellationToken);
        typology.CreatedAt.Should().Be(fixedTime.UtcDateTime);
        typology.ApprovedAt.Should().Be(fixedTime.UtcDateTime);

        var agent = await _dbContext.Set<AgentEntity>()
            .FirstAsync(a => a.Name == "Catan POC Agent", TestCancellationToken);
        agent.CreatedAt.Should().Be(fixedTime.UtcDateTime);

        var config = await _dbContext.Set<AgentConfigurationEntity>()
            .FirstAsync(c => c.AgentId == agent.Id, TestCancellationToken);
        config.CreatedAt.Should().Be(fixedTime.UtcDateTime);
    }

    #endregion

    #region Partial Pre-existence

    [Fact]
    public async Task SeedAsync_ReusesExistingGameEntity_WhenAlreadyPresent()
    {
        // Arrange - Seed prerequisites + pre-create a GameEntity named "Catan"
        await SeedPrerequisitesAsync();

        var existingGameId = Guid.NewGuid();
        var existingGame = new GameEntity
        {
            Id = existingGameId,
            Name = "Catan",
            Publisher = "Kosmos (pre-existing)",
            CreatedAt = DateTime.UtcNow,
            SharedGameId = TestSharedGameId
        };
        _dbContext!.Games.Add(existingGame);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var logger = new Mock<ILogger>();

        // Act
        await CatanPocAgentSeeder.SeedAsync(_dbContext!, logger.Object, cancellationToken: TestCancellationToken);

        // Assert - Only 1 GameEntity "Catan", reuses existing
        var games = await _dbContext.Games
            .Where(g => g.Name == "Catan").ToListAsync(TestCancellationToken);
        games.Should().HaveCount(1);
        games[0].Id.Should().Be(existingGameId);
        games[0].Publisher.Should().Be("Kosmos (pre-existing)");

        // Other entities should still be created normally
        var agents = await _dbContext.Set<AgentEntity>()
            .Where(a => a.Name == "Catan POC Agent").ToListAsync(TestCancellationToken);
        agents.Should().HaveCount(1);

        var agentSessions = await _dbContext.AgentSessions
            .Where(s => s.AgentId == agents[0].Id && s.IsActive).ToListAsync(TestCancellationToken);
        agentSessions.Should().HaveCount(1);
    }

    #endregion
}
