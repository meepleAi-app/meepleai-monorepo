using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration.UserLibrary;

/// <summary>
/// Integration tests for linking/unlinking agents to private games.
/// Issue #4228: SharedGame and PrivateGame → AgentDefinition relationship
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4228")]
public sealed class LinkAgentToPrivateGameIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IMediator? _mediator;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public LinkAgentToPrivateGameIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_privategameagent_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Repositories required by LinkAgent/UnlinkAgent command handlers and validators
        services.AddScoped<IPrivateGameRepository, PrivateGameRepository>();
        services.AddScoped<IAgentDefinitionRepository, AgentDefinitionRepository>();

        // MediatR (required by MeepleAiDbContext and for command handling)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();

        // Create database schema
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_databaseName);
        }

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task LinkAgent_WithValidGameAndAgent_LinksSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Seed owner user (FK constraint on PrivateGameEntity.OwnerId)
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"owner-{userId:N}@test.com",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Create agent
        var agent = AgentDefinition.Create(
            name: "PrivateAgent1",
            description: "Test agent for private game",
            type: AgentType.RagAgent,
            config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));

        _dbContext.Set<AgentDefinition>().Add(agent);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Create private game using persistence entity (domain PrivateGame is modelBuilder.Ignore'd)
        var gameId = Guid.NewGuid();
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = gameId,
            OwnerId = userId,
            Title = "Test Private Game",
            MinPlayers = 2,
            MaxPlayers = 4,
            YearPublished = 2024,
            Description = "Test description",
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ComplexityRating = 2.5m,
            ImageUrl = "https://example.com/image.jpg",
            Source = PrivateGameSource.Manual,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var command = new LinkAgentToPrivateGameCommand(gameId, agent.Id, userId);
        await _mediator!.Send(command, TestCancellationToken);

        // Assert via persistence entity DbSet
        var updatedGame = await _dbContext.PrivateGames
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId, TestCancellationToken);

        updatedGame.Should().NotBeNull();
        updatedGame!.AgentDefinitionId.Should().Be(agent.Id);
    }

    [Fact]
    public async Task UnlinkAgent_WithLinkedAgent_UnlinksSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Seed owner user (FK constraint on PrivateGameEntity.OwnerId)
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"owner-{userId:N}@test.com",
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Create agent
        var agent = AgentDefinition.Create(
            name: "PrivateAgent2",
            description: "Test agent for unlink test",
            type: AgentType.RagAgent,
            config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));

        _dbContext.Set<AgentDefinition>().Add(agent);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Create private game with agent linked, using persistence entity
        var gameId = Guid.NewGuid();
        _dbContext.PrivateGames.Add(new PrivateGameEntity
        {
            Id = gameId,
            OwnerId = userId,
            Title = "Test Private Game 2",
            MinPlayers = 2,
            MaxPlayers = 4,
            YearPublished = 2024,
            Description = "Test description",
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ComplexityRating = 2.5m,
            ImageUrl = "https://example.com/image.jpg",
            Source = PrivateGameSource.Manual,
            AgentDefinitionId = agent.Id,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var command = new UnlinkAgentFromPrivateGameCommand(gameId, userId);
        await _mediator!.Send(command, TestCancellationToken);

        // Assert via persistence entity DbSet
        var updatedGame = await _dbContext.PrivateGames
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == gameId, TestCancellationToken);

        updatedGame.Should().NotBeNull();
        updatedGame!.AgentDefinitionId.Should().BeNull();
    }
}
