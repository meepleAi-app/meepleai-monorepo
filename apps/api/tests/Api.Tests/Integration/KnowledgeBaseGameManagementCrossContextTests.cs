using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Cross-context integration tests: KnowledgeBase ↔ GameManagement.
/// Tests game-specific chat threads and contextual Q&A during gameplay.
/// </summary>
[Collection("KBGame")]
public sealed class KnowledgeBaseGameManagementCrossContextTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "kb_game_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);
        var containerPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={containerPort};Database=kb_game_test;Username=postgres;Password=postgres;";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGameSessionRepository, GameSessionRepository>();
        services.AddScoped<IChatThreadRepository, ChatThreadRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else
            (_serviceProvider as IDisposable)?.Dispose();

        if (_postgresContainer != null)
        {
            await _postgresContainer.StopAsync(TestCancellationToken);
            await _postgresContainer.DisposeAsync();
        }
    }

    private async Task ResetDatabaseAsync()
    {
        var tableNames = await _dbContext!.Database
            .SqlQueryRaw<string>(
                @"SELECT tablename
                  FROM pg_tables
                  WHERE schemaname = 'public'
                  AND tablename != '__EFMigrationsHistory'")
            .ToListAsync(TestCancellationToken);

        if (tableNames.Count > 0)
        {
            await _dbContext.Database.ExecuteSqlRawAsync(
                "SET session_replication_role = 'replica';",
                TestCancellationToken);

            try
            {
                foreach (var tableName in tableNames)
                {
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        $"TRUNCATE TABLE \"{tableName}\" CASCADE;",
                        TestCancellationToken);
                }
            }
            finally
            {
                await _dbContext.Database.ExecuteSqlRawAsync(
                    "SET session_replication_role = 'origin';",
                    TestCancellationToken);
            }
        }
    }

    [Fact]
    public async Task User_CanCreateGameSpecificChatThread_WithValidGameReference()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var gameRepository = _serviceProvider.GetRequiredService<IGameRepository>();
        var chatThreadRepository = _serviceProvider.GetRequiredService<IChatThreadRepository>();

        var user = CreateTestUser("chatuser@meepleai.dev", "Chat User");
        await userRepository.AddAsync(user, TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Wingspan"),
            playerCount: new PlayerCount(1, 5),
            playTime: new PlayTime(40, 70)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var chatThread = new ChatThread(
            Guid.NewGuid(),
            user.Id,
            game.Id,
            "Wingspan Rules Questions"
        );
        chatThread.AddUserMessage("How does bird feeding work?");
        chatThread.AddAssistantMessage("In Wingspan, birds are fed using food tokens...");

        await chatThreadRepository.AddAsync(chatThread, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var loadedThread = await chatThreadRepository.GetByIdAsync(chatThread.Id, TestCancellationToken);
        loadedThread.Should().NotBeNull();
        loadedThread!.UserId.Should().Be(user.Id);
        loadedThread.GameId.Should().Be(game.Id);
        loadedThread.Title.Should().Be("Wingspan Rules Questions");
        loadedThread.MessageCount.Should().Be(2);

        var loadedGame = await gameRepository.GetByIdAsync(game.Id, TestCancellationToken);
        loadedGame.Should().NotBeNull();
        loadedGame!.Title.Value.Should().Be("Wingspan");
    }

    [Fact]
    public async Task ChatThread_CanBeLinkedToActiveGameSession_ForContextualHelp()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var gameRepository = _serviceProvider.GetRequiredService<IGameRepository>();
        var gameSessionRepository = _serviceProvider.GetRequiredService<IGameSessionRepository>();
        var chatThreadRepository = _serviceProvider.GetRequiredService<IChatThreadRepository>();

        var user = CreateTestUser("player@meepleai.dev", "Active Player");
        await userRepository.AddAsync(user, TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Terraforming Mars"),
            playerCount: new PlayerCount(1, 5),
            playTime: new PlayTime(120, 180)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);

        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Active Player", 1),
            new SessionPlayer("Alice", 2)
        };
        var gameSession = new GameSession(Guid.NewGuid(), game.Id, players);
        gameSession.Start();
        await gameSessionRepository.AddAsync(gameSession, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act
        var chatThread = new ChatThread(
            Guid.NewGuid(),
            user.Id,
            game.Id,
            "Terraforming Mars - Mid-game Questions"
        );
        chatThread.AddUserMessage("Can I place an ocean tile adjacent to another ocean?");
        chatThread.AddAssistantMessage("Yes, ocean tiles can be placed adjacent...");

        await chatThreadRepository.AddAsync(chatThread, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var loadedGameSession = await gameSessionRepository.GetByIdAsync(gameSession.Id, TestCancellationToken);
        loadedGameSession.Should().NotBeNull();
        loadedGameSession!.Status.Should().Be(SessionStatus.InProgress);

        var loadedThread = await chatThreadRepository.GetByIdAsync(chatThread.Id, TestCancellationToken);
        loadedThread.Should().NotBeNull();
        loadedThread!.GameId.Should().Be(game.Id);
        loadedThread.Status.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleUsers_CanHaveIndependentChatThreads_ForSameGame()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var gameRepository = _serviceProvider.GetRequiredService<IGameRepository>();
        var chatThreadRepository = _serviceProvider.GetRequiredService<IChatThreadRepository>();

        var users = new List<User>();
        var userNames = new[] { "Alice", "Bob", "Charlie" };

        foreach (var name in userNames)
        {
            var user = CreateTestUser($"{name.ToLower()}@meepleai.dev", name);
            await userRepository.AddAsync(user, TestCancellationToken);
            users.Add(user);
        }

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Gloomhaven"),
            playerCount: new PlayerCount(1, 4),
            playTime: new PlayTime(90, 150)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act - Each user creates their own thread
        var threadIds = new List<Guid>();
        for (int i = 0; i < users.Count; i++)
        {
            var thread = new ChatThread(
                Guid.NewGuid(),
                users[i].Id,
                game.Id,
                $"{userNames[i]}'s Gloomhaven Questions"
            );
            thread.AddUserMessage($"Question from {userNames[i]}");
            await chatThreadRepository.AddAsync(thread, TestCancellationToken);
            threadIds.Add(thread.Id);
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Verify each thread exists independently
        for (int i = 0; i < threadIds.Count; i++)
        {
            var thread = await chatThreadRepository.GetByIdAsync(threadIds[i], TestCancellationToken);
            thread.Should().NotBeNull();
            thread!.GameId.Should().Be(game.Id);
            thread.UserId.Should().Be(users[i].Id);
            thread.MessageCount.Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public async Task ChatThread_CanBeClosedAfterGameSessionCompletes_MaintainingHistory()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var gameRepository = _serviceProvider.GetRequiredService<IGameRepository>();
        var gameSessionRepository = _serviceProvider.GetRequiredService<IGameSessionRepository>();
        var chatThreadRepository = _serviceProvider.GetRequiredService<IChatThreadRepository>();

        var user = CreateTestUser("finisher@meepleai.dev", "Game Finisher");
        await userRepository.AddAsync(user, TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Scythe"),
            playerCount: new PlayerCount(1, 5),
            playTime: new PlayTime(90, 120)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);

        var gameSession = new GameSession(
            Guid.NewGuid(),
            game.Id,
            new List<SessionPlayer> { new SessionPlayer("Game Finisher", 1) }
        );
        gameSession.Start();
        await gameSessionRepository.AddAsync(gameSession, TestCancellationToken);

        var chatThread = new ChatThread(
            Guid.NewGuid(),
            user.Id,
            game.Id,
            "Scythe Combat Questions"
        );
        chatThread.AddUserMessage("How does combat work?");
        chatThread.AddAssistantMessage("Combat in Scythe is resolved by...");
        chatThread.AddUserMessage("What if both players have equal power?");
        chatThread.AddAssistantMessage("When players have equal power, the attacker wins...");

        await chatThreadRepository.AddAsync(chatThread, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act - Complete game and close thread
        gameSession.Complete("Game Finisher");
        await gameSessionRepository.UpdateAsync(gameSession, TestCancellationToken);

        chatThread.CloseThread();
        await chatThreadRepository.UpdateAsync(chatThread, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var loadedGameSession = await gameSessionRepository.GetByIdAsync(gameSession.Id, TestCancellationToken);
        loadedGameSession.Should().NotBeNull();
        loadedGameSession!.Status.Should().Be(SessionStatus.Completed);
        loadedGameSession.WinnerName.Should().Be("Game Finisher");

        var loadedThread = await chatThreadRepository.GetByIdAsync(chatThread.Id, TestCancellationToken);
        loadedThread.Should().NotBeNull();
        loadedThread!.Status.IsClosed.Should().BeTrue();
        loadedThread.MessageCount.Should().Be(4, "messages preserved after closing");

        var act = () => loadedThread.AddUserMessage("New question");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot add message to closed thread");
    }

    private static User CreateTestUser(string email, string displayName)
    {
        return new User(
            Guid.NewGuid(),
            new Email(email),
            displayName,
            PasswordHash.Create("SecurePass123!"),
            Role.User
        );
    }
}
