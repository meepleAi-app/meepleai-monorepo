using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.Infrastructure;
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
[Collection("SharedTestcontainers")]
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

    [Fact(Skip = "Integration test requires database setup investigation")]
    public async Task LinkAgent_WithValidGameAndAgent_LinksSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Create agent
        var agent = AgentDefinition.Create(
            name: "PrivateAgent1",
            description: "Test agent for private game",
            type: AgentType.RagAgent,
            config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));

        _dbContext!.Set<AgentDefinition>().Add(agent);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Create private game
        var game = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Test Private Game",
            minPlayers: 2,
            maxPlayers: 4,
            yearPublished: 2024,
            description: "Test description",
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            imageUrl: "https://example.com/image.jpg");

        _dbContext.Set<PrivateGame>().Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var command = new LinkAgentToPrivateGameCommand(game.Id, agent.Id, userId);
        await _mediator!.Send(command, TestCancellationToken);

        // Assert
        var updatedGame = await _dbContext.Set<PrivateGame>()
            .FirstOrDefaultAsync(g => g.Id == game.Id, TestCancellationToken);

        updatedGame.Should().NotBeNull();
        updatedGame!.AgentDefinitionId.Should().Be(agent.Id);
    }

    [Fact(Skip = "Integration test requires database setup investigation")]
    public async Task UnlinkAgent_WithLinkedAgent_UnlinksSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Create agent
        var agent = AgentDefinition.Create(
            name: "PrivateAgent2",
            description: "Test agent for unlink test",
            type: AgentType.RagAgent,
            config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));

        _dbContext!.Set<AgentDefinition>().Add(agent);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Create private game and link agent
        var game = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Test Private Game 2",
            minPlayers: 2,
            maxPlayers: 4,
            yearPublished: 2024,
            description: "Test description",
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            imageUrl: "https://example.com/image.jpg");

        game.LinkAgent(agent.Id);
        _dbContext.Set<PrivateGame>().Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        _dbContext.ChangeTracker.Clear();

        // Act
        var command = new UnlinkAgentFromPrivateGameCommand(game.Id, userId);
        await _mediator!.Send(command, TestCancellationToken);

        // Assert
        var updatedGame = await _dbContext.Set<PrivateGame>()
            .FirstOrDefaultAsync(g => g.Id == game.Id, TestCancellationToken);

        updatedGame.Should().NotBeNull();
        updatedGame!.AgentDefinitionId.Should().BeNull();
    }
}
