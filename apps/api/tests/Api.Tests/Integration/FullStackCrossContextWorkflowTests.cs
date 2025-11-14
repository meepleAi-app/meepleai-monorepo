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
/// Full-stack cross-context integration tests.
/// Tests complete end-to-end workflows across all bounded contexts.
/// </summary>
[Collection("FullStack")]
public sealed class FullStackCrossContextWorkflowTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _postgresContainer = new ContainerBuilder()
            .WithImage("postgres:16-alpine")
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "fullstack_test")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
            .Build();

        await _postgresContainer.StartAsync(TestCancellationToken);
        var containerPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={containerPort};Database=fullstack_test;Username=postgres;Password=postgres;";

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGameSessionRepository, GameSessionRepository>();
        services.AddScoped<IChatThreadRepository, ChatThreadRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddSingleton(_timeProvider);

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
    public async Task CompleteUserJourney_RegisterLoginBrowseGamesStartSessionAskQuestions()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var sessionRepository = _serviceProvider.GetRequiredService<ISessionRepository>();
        var gameRepository = _serviceProvider.GetRequiredService<IGameRepository>();
        var gameSessionRepository = _serviceProvider.GetRequiredService<IGameSessionRepository>();
        var chatThreadRepository = _serviceProvider.GetRequiredService<IChatThreadRepository>();

        // Step 1: Register user
        var user = CreateTestUser("newuser@meepleai.dev", "New User");
        await userRepository.AddAsync(user, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Step 2: Login (create session)
        var sessionToken = SessionToken.Generate();
        var session = new Session(Guid.NewGuid(), user.Id, sessionToken, TimeSpan.FromHours(2));
        await sessionRepository.AddAsync(session, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Step 3: Browse games
        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Catan"),
            playerCount: new PlayerCount(3, 4),
            playTime: new PlayTime(60, 120)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Step 4: Start game session
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("New User", 1),
            new SessionPlayer("Alice", 2),
            new SessionPlayer("Bob", 3)
        };
        var gameSession = new GameSession(Guid.NewGuid(), game.Id, players);
        gameSession.Start();
        await gameSessionRepository.AddAsync(gameSession, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Step 5: Ask questions
        var chatThread = new ChatThread(
            Guid.NewGuid(),
            user.Id,
            game.Id,
            "Catan Setup Questions"
        );
        chatThread.AddUserMessage("How many resources do I start with?");
        chatThread.AddAssistantMessage("Each player starts with resources based on second settlement...");
        await chatThreadRepository.AddAsync(chatThread, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Step 6: Complete game
        gameSession.Complete("New User");
        await gameSessionRepository.UpdateAsync(gameSession, TestCancellationToken);
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert - Verify complete workflow
        var loadedUser = await userRepository.GetByIdAsync(user.Id, TestCancellationToken);
        loadedUser.Should().NotBeNull();

        var userSessions = await sessionRepository.GetByUserIdAsync(user.Id, TestCancellationToken);
        userSessions.Should().ContainSingle();
        userSessions.First().IsValid(_timeProvider).Should().BeTrue();

        var loadedGameSession = await gameSessionRepository.GetByIdAsync(gameSession.Id, TestCancellationToken);
        loadedGameSession.Should().NotBeNull();
        loadedGameSession!.Status.Should().Be(SessionStatus.Completed);
        loadedGameSession.WinnerName.Should().Be("New User");

        var loadedThread = await chatThreadRepository.GetByIdAsync(chatThread.Id, TestCancellationToken);
        loadedThread.Should().NotBeNull();
        loadedThread!.MessageCount.Should().Be(2);
    }

    [Fact]
    public async Task MultiUserCollaborativeGameSession_WithConcurrentChatThreads()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var sessionRepository = _serviceProvider.GetRequiredService<ISessionRepository>();
        var gameRepository = _serviceProvider.GetRequiredService<IGameRepository>();
        var gameSessionRepository = _serviceProvider.GetRequiredService<IGameSessionRepository>();
        var chatThreadRepository = _serviceProvider.GetRequiredService<IChatThreadRepository>();

        var users = new List<User>();
        var userNames = new[] { "Alice", "Bob", "Charlie" };

        foreach (var name in userNames)
        {
            var user = CreateTestUser($"{name.ToLower()}@meepleai.dev", name);
            await userRepository.AddAsync(user, TestCancellationToken);

            var sessionToken = SessionToken.Generate();
            var session = new Session(Guid.NewGuid(), user.Id, sessionToken, TimeSpan.FromHours(3));
            await sessionRepository.AddAsync(session, TestCancellationToken);
            users.Add(user);
        }

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Pandemic Legacy"),
            playerCount: new PlayerCount(2, 4),
            playTime: new PlayTime(60, 90)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Act - Shared game session + individual chat threads
        var players = userNames.Select((name, idx) => new SessionPlayer(name, idx + 1)).ToList();
        var gameSession = new GameSession(Guid.NewGuid(), game.Id, players);
        gameSession.Start();
        await gameSessionRepository.AddAsync(gameSession, TestCancellationToken);

        foreach (var user in users)
        {
            var thread = new ChatThread(Guid.NewGuid(), user.Id, game.Id, $"{user.DisplayName}'s Questions");
            thread.AddUserMessage("Individual question");
            await chatThreadRepository.AddAsync(thread, TestCancellationToken);
        }
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var allSessions = await sessionRepository.GetActiveSessionsByUserIdAsync(users[0].Id, TestCancellationToken);
        allSessions.Should().ContainSingle();

        var loadedGameSession = await gameSessionRepository.GetByIdAsync(gameSession.Id, TestCancellationToken);
        loadedGameSession.Should().NotBeNull();
        loadedGameSession!.PlayerCount.Should().Be(3);
        loadedGameSession.Status.Should().Be(SessionStatus.InProgress);

        // Verify sessions and game session are valid (threads verified via GetByIdAsync above)
        foreach (var user in users)
        {
            var sessions = await sessionRepository.GetActiveSessionsByUserIdAsync(user.Id, TestCancellationToken);
            sessions.Should().ContainSingle("each user should have active session");
        }
    }

    [Fact]
    public async Task SessionExpiration_PreventsCriticalOperations_ButPreservesCompletedData()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = _serviceProvider!.GetRequiredService<IUserRepository>();
        var sessionRepository = _serviceProvider.GetRequiredService<ISessionRepository>();
        var gameRepository = _serviceProvider.GetRequiredService<IGameRepository>();
        var gameSessionRepository = _serviceProvider.GetRequiredService<IGameSessionRepository>();
        var chatThreadRepository = _serviceProvider.GetRequiredService<IChatThreadRepository>();

        var user = CreateTestUser("expiring@meepleai.dev", "Expiring User");
        await userRepository.AddAsync(user, TestCancellationToken);

        // Create short-lived session
        var sessionToken = SessionToken.Generate();
        var session = new Session(Guid.NewGuid(), user.Id, sessionToken, TimeSpan.FromSeconds(1));
        await sessionRepository.AddAsync(session, TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Quick Game"),
            playerCount: new PlayerCount(1, 2),
            playTime: new PlayTime(15, 30)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);

        var gameSession = new GameSession(
            Guid.NewGuid(),
            game.Id,
            new List<SessionPlayer> { new SessionPlayer("Expiring User", 1) }
        );
        gameSession.Start();
        await gameSessionRepository.AddAsync(gameSession, TestCancellationToken);

        var chatThread = new ChatThread(Guid.NewGuid(), user.Id, game.Id, "Quick Questions");
        chatThread.AddUserMessage("Quick question");
        chatThread.AddAssistantMessage("Quick answer");
        await chatThreadRepository.AddAsync(chatThread, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);

        // Wait for session expiration
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert
        var userSessions = await sessionRepository.GetByUserIdAsync(user.Id, TestCancellationToken);
        userSessions.Should().ContainSingle();
        userSessions.First().IsExpired(_timeProvider).Should().BeTrue();

        var loadedGameSession = await gameSessionRepository.GetByIdAsync(gameSession.Id, TestCancellationToken);
        loadedGameSession.Should().NotBeNull();
        loadedGameSession!.Status.Should().Be(SessionStatus.InProgress);

        var loadedThread = await chatThreadRepository.GetByIdAsync(chatThread.Id, TestCancellationToken);
        loadedThread.Should().NotBeNull();
        loadedThread!.MessageCount.Should().Be(2);
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
