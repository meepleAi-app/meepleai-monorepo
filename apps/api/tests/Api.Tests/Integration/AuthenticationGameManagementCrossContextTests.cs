using System;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Cross-context integration tests: Authentication ↔ GameManagement.
/// Tests user session validation during game play sessions.
/// Pattern: OAuthIntegrationTests (ServiceCollection + DI + Repositories)
/// </summary>
[Collection("AuthGame")]
public sealed class AuthenticationGameManagementCrossContextTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;
    private readonly TestTimeProvider _timeProvider = new();

    private IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("Service provider not initialized.");
    private MeepleAiDbContext DbContext => _dbContext ?? throw new InvalidOperationException("DbContext not initialized.");

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var externalConn = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNSTRING");
        string connectionString;

        if (!string.IsNullOrWhiteSpace(externalConn))
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(externalConn)
            {
                Database = "auth_game_test",
                SslMode = Npgsql.SslMode.Disable,
                KeepAlive = 30,
                Pooling = false
            };
            connectionString = builder.ConnectionString;
        }
        else
        {
            _postgresContainer = new ContainerBuilder()
                .WithImage("postgres:16-alpine")
                .WithEnvironment("POSTGRES_USER", "postgres")
                .WithEnvironment("POSTGRES_PASSWORD", "postgres")
                .WithEnvironment("POSTGRES_DB", "auth_game_test")
                .WithPortBinding(5432, true)
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
                .Build();

            await _postgresContainer.StartAsync(TestCancellationToken);
            var containerPort = _postgresContainer.GetMappedPublicPort(5432);
            connectionString = $"Host=localhost;Port={containerPort};Database=auth_game_test;Username=postgres;Password=postgres;Ssl Mode=Disable;Trust Server Certificate=true;KeepAlive=30;Pooling=false;";
        }

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
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddSingleton<TestTimeProvider>(_timeProvider);
        services.AddSingleton<TimeProvider>(_timeProvider);

        // Register domain event infrastructure
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();

        // Register MediatR (required by MeepleAiDbContext for domain event dispatching)
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(Api.BoundedContexts.Authentication.Application.Commands.LoginCommandHandler).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Use EnsureCreated instead of Migrate to avoid migration ordering issues
        // See: docs/testing/sprint-5-integration-tests-plan.md for details
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await DbContext.Database.EnsureCreatedAsync(TestCancellationToken);
                break;
            }
            catch (Exception ex) when ((ex is NpgsqlException or InvalidOperationException) && attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
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
            var tableNames = await DbContext.Database
                .SqlQueryRaw<string>(
                    @"SELECT tablename
                  FROM pg_tables
                  WHERE schemaname = 'public'
                  AND tablename != '__EFMigrationsHistory'")
                .ToListAsync(TestCancellationToken);

            if (tableNames.Count > 0)
            {
                await DbContext.Database.ExecuteSqlRawAsync(
                    "SET session_replication_role = 'replica';",
                    TestCancellationToken);

                try
                {
                    foreach (var tableName in tableNames)
                    {
                        await DbContext.Database.ExecuteSqlRawAsync(
                            $"TRUNCATE TABLE \"{tableName}\" CASCADE;",
                            TestCancellationToken);
                    }
                }
                finally
                {
                    await DbContext.Database.ExecuteSqlRawAsync(
                        "SET session_replication_role = 'origin';",
                        TestCancellationToken);
                }
            }
        }

    [Fact]
    public async Task AuthenticatedUser_CanCreateGameSession_WithValidSession()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = ServiceProvider.GetRequiredService<IUserRepository>();
        var sessionRepository = ServiceProvider.GetRequiredService<ISessionRepository>();
        var gameRepository = ServiceProvider.GetRequiredService<IGameRepository>();
        var gameSessionRepository = ServiceProvider.GetRequiredService<IGameSessionRepository>();

        var user = CreateTestUser("gamer@meepleai.dev", "Test Gamer");
        await userRepository.AddAsync(user, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var sessionToken = SessionToken.Generate();
        var session = new Session(
            Guid.NewGuid(),
            user.Id,
            sessionToken,
            TimeSpan.FromHours(2),
            timeProvider: _timeProvider
        );
        await sessionRepository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Catan"),
            playerCount: new PlayerCount(3, 4),
            playTime: new PlayTime(60, 120)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var players = new List<SessionPlayer>
        {
            new SessionPlayer("Test Gamer", 1),
            new SessionPlayer("Alice", 2),
            new SessionPlayer("Bob", 3)
        };
        var gameSession = new GameSession(Guid.NewGuid(), game.Id, players);
        gameSession.Start();

        await gameSessionRepository.AddAsync(gameSession, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var loadedUser = await userRepository.GetByIdAsync(user.Id, TestCancellationToken);
        loadedUser.Should().NotBeNull();
        loadedUser!.Email.Value.Should().Be("gamer@meepleai.dev");

        var userSessions = await sessionRepository.GetByUserIdAsync(user.Id, TestCancellationToken);
        userSessions.Should().ContainSingle();
        userSessions.First().IsValid(_timeProvider).Should().BeTrue();

        var loadedGameSession = await gameSessionRepository.GetByIdAsync(gameSession.Id, TestCancellationToken);
        loadedGameSession.Should().NotBeNull();
        loadedGameSession!.Status.Should().Be(SessionStatus.InProgress);
        loadedGameSession.PlayerCount.Should().Be(3);
    }

    [Fact]
    public async Task ExpiredSession_PreventsCriticalOperations_ButPreservesGameData()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = ServiceProvider.GetRequiredService<IUserRepository>();
        var sessionRepository = ServiceProvider.GetRequiredService<ISessionRepository>();
        var gameRepository = ServiceProvider.GetRequiredService<IGameRepository>();
        var gameSessionRepository = ServiceProvider.GetRequiredService<IGameSessionRepository>();

        // Use FakeTimeProvider to control time for expired session test
        var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        var user = CreateTestUser("expired@meepleai.dev", "Expired User");
        await userRepository.AddAsync(user, TestCancellationToken);

        var sessionToken = SessionToken.Generate();
        // Create session with 1 hour lifetime using fake time provider
        var session = new Session(
            Guid.NewGuid(),
            user.Id,
            sessionToken,
            TimeSpan.FromHours(1),
            timeProvider: fakeTimeProvider
        );
        await sessionRepository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Clear change tracker to avoid "already tracked" error when reloading
        DbContext.ChangeTracker.Clear();

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
            new List<SessionPlayer> { new SessionPlayer("Expired User", 1) }
        );
        gameSession.Start();
        await gameSessionRepository.AddAsync(gameSession, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Advance time by 2 hours to expire the session
        fakeTimeProvider.Advance(TimeSpan.FromHours(2));

        // Act & Assert
        var userSessions = await sessionRepository.GetByUserIdAsync(user.Id, TestCancellationToken);
        userSessions.Should().ContainSingle();
        var loadedSession = userSessions.First();
        loadedSession.IsValid(fakeTimeProvider).Should().BeFalse();
        loadedSession.IsExpired(fakeTimeProvider).Should().BeTrue();

        var loadedGameSession = await gameSessionRepository.GetByIdAsync(gameSession.Id, TestCancellationToken);
        loadedGameSession.Should().NotBeNull();
        loadedGameSession!.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public async Task MultipleUsers_CanParticipateInSameGameSession_WithValidSessions()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = ServiceProvider.GetRequiredService<IUserRepository>();
        var sessionRepository = ServiceProvider.GetRequiredService<ISessionRepository>();
        var gameRepository = ServiceProvider.GetRequiredService<IGameRepository>();
        var gameSessionRepository = ServiceProvider.GetRequiredService<IGameSessionRepository>();

        var userIds = new List<Guid>();
        var userNames = new[] { "Alice", "Bob", "Charlie" };

        for (int i = 0; i < userNames.Length; i++)
        {
            var user = CreateTestUser($"{userNames[i].ToLower()}@meepleai.dev", userNames[i]);
            await userRepository.AddAsync(user, TestCancellationToken);
            userIds.Add(user.Id);

            var sessionToken = SessionToken.Generate();
            var session = new Session(
                Guid.NewGuid(),
                user.Id,
                sessionToken,
                TimeSpan.FromHours(2),
                timeProvider: _timeProvider
            );
            await sessionRepository.AddAsync(session, TestCancellationToken);
        }
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Ticket to Ride"),
            playerCount: new PlayerCount(2, 5),
            playTime: new PlayTime(30, 60)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var players = userNames.Select((name, index) => new SessionPlayer(name, index + 1)).ToList();
        var gameSession = new GameSession(Guid.NewGuid(), game.Id, players);
        gameSession.Start();

        await gameSessionRepository.AddAsync(gameSession, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        foreach (var userId in userIds)
        {
            var userSessions = await sessionRepository.GetByUserIdAsync(userId, TestCancellationToken);
            userSessions.Should().ContainSingle();
            userSessions.First().IsValid(_timeProvider).Should().BeTrue();
        }

        var loadedGameSession = await gameSessionRepository.GetByIdAsync(gameSession.Id, TestCancellationToken);
        loadedGameSession.Should().NotBeNull();
        loadedGameSession!.PlayerCount.Should().Be(3);
        loadedGameSession.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public async Task SessionRevocation_ViaRevokeAll_DoesNotAffectExistingGameSessions()
    {
        // Arrange
        await ResetDatabaseAsync();

        var userRepository = ServiceProvider.GetRequiredService<IUserRepository>();
        var sessionRepository = ServiceProvider.GetRequiredService<ISessionRepository>();
        var gameRepository = ServiceProvider.GetRequiredService<IGameRepository>();
        var gameSessionRepository = ServiceProvider.GetRequiredService<IGameSessionRepository>();

        var user = CreateTestUser("revoked@meepleai.dev", "Revoked User");
        await userRepository.AddAsync(user, TestCancellationToken);

        var sessionToken = SessionToken.Generate();
        var session = new Session(
            Guid.NewGuid(),
            user.Id,
            sessionToken,
            TimeSpan.FromHours(1),
            timeProvider: _timeProvider
        );
        await sessionRepository.AddAsync(session, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        var game = new Game(
            Guid.NewGuid(),
            new GameTitle("Carcassonne"),
            playerCount: new PlayerCount(2, 5),
            playTime: new PlayTime(30, 45)
        );
        await gameRepository.AddAsync(game, TestCancellationToken);

        var gameSession = new GameSession(
            Guid.NewGuid(),
            game.Id,
            new List<SessionPlayer> { new SessionPlayer("Revoked User", 1) }
        );
        gameSession.Start();
        await gameSessionRepository.AddAsync(gameSession, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Revoke all user sessions after game started
        await sessionRepository.RevokeAllUserSessionsAsync(user.Id, TestCancellationToken);

        // Assert
        var userSessions = await sessionRepository.GetActiveSessionsByUserIdAsync(user.Id, TestCancellationToken);
        userSessions.Should().BeEmpty("all sessions should be revoked");

        var allUserSessions = await sessionRepository.GetByUserIdAsync(user.Id, TestCancellationToken);
        allUserSessions.Should().ContainSingle();
        allUserSessions.First().IsRevoked().Should().BeTrue();

        var loadedGameSession = await gameSessionRepository.GetByIdAsync(gameSession.Id, TestCancellationToken);
        loadedGameSession.Should().NotBeNull();
        loadedGameSession!.Status.Should().Be(SessionStatus.InProgress);
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

