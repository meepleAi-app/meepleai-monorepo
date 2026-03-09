using Api.BoundedContexts.Authentication.Application.Commands.CreateShareLink;
using Api.BoundedContexts.Authentication.Application.Commands.RevokeShareLink;
using Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetSharedThread;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for GetSharedThreadQueryHandler.
/// Tests share link access, analytics recording, and thread retrieval.
/// Issue #2150: Verifies synchronous analytics recording works correctly
/// (removed fire-and-forget Task.Run pattern to avoid DI scope violation).
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Dependency", "Redis")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetSharedThreadIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private const string TestSecretKey = "test-secret-key-that-is-at-least-32-characters-long-for-hmac-sha256";
    private const string TestBaseUrl = "https://meepleai.test";

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetSharedThreadIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private void EnsureTestInfrastructureAvailable()
    {
        if (_serviceProvider == null || _dbContext == null)
        {
            Assert.Skip("Shared Testcontainers fixture not available. GetSharedThread integration tests require PostgreSQL and Redis.");
        }
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            // Create isolated database for this test class
            _databaseName = $"test_getsharedthread_{Guid.NewGuid():N}";
            _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

            // Build service provider with all required dependencies
            var services = new ServiceCollection();

            // Configuration
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:ShareLinks:SecretKey"] = TestSecretKey,
                    ["App:BaseUrl"] = TestBaseUrl
                })
                .Build();
            services.AddSingleton<IConfiguration>(configuration);

            // Logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

            // DbContext with EF Core
            services.AddDbContext<MeepleAiDbContext>(options =>
                options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector()) // Issue #3547
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

            // Redis distributed cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = _fixture.RedisConnectionString;
                options.InstanceName = $"test_getsharedthread_{Guid.NewGuid():N}:";
            });

            // Domain event collector (required by repositories)
            var mockEventCollector = new Mock<IDomainEventCollector>();
            mockEventCollector.Setup(x => x.GetAndClearEvents()).Returns(new List<IDomainEvent>().AsReadOnly());
            services.AddScoped<IDomainEventCollector>(_ => mockEventCollector.Object);

            // Repositories
            services.AddScoped<IShareLinkRepository, ShareLinkRepository>();
            services.AddScoped<IChatThreadRepository, ChatThreadRepository>();

            // MediatR for handlers
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetSharedThreadQueryHandler).Assembly));

            _serviceProvider = services.BuildServiceProvider();

            // Initialize database with migrations
            _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
            await _dbContext.Database.MigrateAsync(TestCancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize GetSharedThread integration test infrastructure: {ex.Message}");
            // Tests will be skipped via EnsureTestInfrastructureAvailable()
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    #region Analytics Recording Tests (Issue #2150)

    [Fact]
    public async Task GetSharedThread_WithValidToken_RecordsAccessAnalytics()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"analytics-test-{Guid.NewGuid():N}@example.com");
        await CreateTestGameAsync(gameId, "Test Game");
        await CreateTestChatThreadAsync(threadId, userId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var repository = _serviceProvider!.GetRequiredService<IShareLinkRepository>();

        // Create share link
        var createCommand = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: "Analytics test link",
            UserId: userId
        );

        var createResult = await mediator.Send(createCommand, TestCancellationToken);

        // Verify initial access count is 0
        var shareLinkBefore = await repository.GetByIdAsync(createResult.ShareLinkId, TestCancellationToken);
        Assert.NotNull(shareLinkBefore);
        Assert.Equal(0, shareLinkBefore.AccessCount);

        // Act - access the shared thread
        var getSharedThreadQuery = new GetSharedThreadQuery(createResult.Token);
        var result = await mediator.Send(getSharedThreadQuery, TestCancellationToken);

        // Assert - thread returned successfully
        Assert.NotNull(result);
        Assert.Equal(threadId, result.ThreadId);

        // Assert - access count incremented (Issue #2150 fix verification)
        var shareLinkAfter = await repository.GetByIdAsync(createResult.ShareLinkId, TestCancellationToken);
        Assert.NotNull(shareLinkAfter);
        Assert.Equal(1, shareLinkAfter.AccessCount);
        Assert.NotNull(shareLinkAfter.LastAccessedAt);
    }

    [Fact]
    public async Task GetSharedThread_MultipleAccesses_IncrementsAccessCountCorrectly()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"multi-access-{Guid.NewGuid():N}@example.com");
        await CreateTestGameAsync(gameId, "Multi Access Game");
        await CreateTestChatThreadAsync(threadId, userId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var repository = _serviceProvider!.GetRequiredService<IShareLinkRepository>();

        // Create share link
        var createCommand = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        var createResult = await mediator.Send(createCommand, TestCancellationToken);

        // Act - access the shared thread 3 times
        var getSharedThreadQuery = new GetSharedThreadQuery(createResult.Token);

        for (int i = 0; i < 3; i++)
        {
            var result = await mediator.Send(getSharedThreadQuery, TestCancellationToken);
            Assert.NotNull(result);
        }

        // Assert - access count is 3
        var shareLinkAfter = await repository.GetByIdAsync(createResult.ShareLinkId, TestCancellationToken);
        Assert.NotNull(shareLinkAfter);
        Assert.Equal(3, shareLinkAfter.AccessCount);
    }

    [Fact]
    public async Task GetSharedThread_WithInvalidToken_ReturnsNull_NoAnalyticsRecorded()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Act - try to access with invalid token
        var getSharedThreadQuery = new GetSharedThreadQuery("invalid-token-value");
        var result = await mediator.Send(getSharedThreadQuery, TestCancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetSharedThread_WithRevokedToken_ReturnsNull_NoAnalyticsRecorded()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"revoked-test-{Guid.NewGuid():N}@example.com");
        await CreateTestGameAsync(gameId, "Revoked Test Game");
        await CreateTestChatThreadAsync(threadId, userId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();
        var repository = _serviceProvider!.GetRequiredService<IShareLinkRepository>();

        // Create and then revoke share link
        var createCommand = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        var createResult = await mediator.Send(createCommand, TestCancellationToken);

        // Revoke the share link using the proper command (handles DB + Redis blacklist)
        var revokeCommand = new RevokeShareLinkCommand(
            ShareLinkId: createResult.ShareLinkId,
            UserId: userId
        );
        var revokeResult = await mediator.Send(revokeCommand, TestCancellationToken);
        Assert.True(revokeResult, "Revoke command should succeed");

        // Verify the share link is marked as revoked in the database
        var shareLinkAfterRevoke = await repository.GetByIdAsync(createResult.ShareLinkId, TestCancellationToken);
        Assert.NotNull(shareLinkAfterRevoke);
        Assert.True(shareLinkAfterRevoke.IsRevoked, "Share link should be marked as revoked in database");

        // Verify token is marked as invalid via ValidateShareLinkQuery (checks Redis blacklist)
        var validateQuery = new ValidateShareLinkQuery(createResult.Token);
        var validateResult = await mediator.Send(validateQuery, TestCancellationToken);
        Assert.NotNull(validateResult);
        Assert.False(validateResult.IsValid, "Token should be marked as invalid after revocation (Redis blacklist)");

        // Act - try to access with revoked token
        var getSharedThreadQuery = new GetSharedThreadQuery(createResult.Token);
        var result = await mediator.Send(getSharedThreadQuery, TestCancellationToken);

        // Assert
        Assert.Null(result);

        // Verify access count is still 0 (no analytics for revoked tokens)
        var shareLinkAfter = await repository.GetByIdAsync(createResult.ShareLinkId, TestCancellationToken);
        Assert.NotNull(shareLinkAfter);
        Assert.Equal(0, shareLinkAfter.AccessCount);
    }

    #endregion

    #region Thread Content Tests

    [Fact]
    public async Task GetSharedThread_WithCommentRole_ReturnsCorrectRole()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"role-test-{Guid.NewGuid():N}@example.com");
        await CreateTestGameAsync(gameId, "Role Test Game");
        await CreateTestChatThreadAsync(threadId, userId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Create share link with Comment role
        var createCommand = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.Comment,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        var createResult = await mediator.Send(createCommand, TestCancellationToken);

        // Act
        var getSharedThreadQuery = new GetSharedThreadQuery(createResult.Token);
        var result = await mediator.Send(getSharedThreadQuery, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ShareLinkRole.Comment, result.Role);
    }

    [Fact]
    public async Task GetSharedThread_ReturnsCorrectGameId()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"gameid-test-{Guid.NewGuid():N}@example.com");
        await CreateTestGameAsync(gameId, "GameId Test Game");
        await CreateTestChatThreadAsync(threadId, userId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Create share link
        var createCommand = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        var createResult = await mediator.Send(createCommand, TestCancellationToken);

        // Act
        var getSharedThreadQuery = new GetSharedThreadQuery(createResult.Token);
        var result = await mediator.Send(getSharedThreadQuery, TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(gameId, result.GameId);
    }

    #endregion

    #region Helper Methods

    private async Task CreateTestUserAsync(Guid userId, string email)
    {
        var user = new UserEntity
        {
            Id = userId,
            Email = email,
            DisplayName = $"Test User {userId:N}",
            PasswordHash = "test-hash",
            Role = "user",
            IsTwoFactorEnabled = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext!.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task CreateTestGameAsync(Guid gameId, string name)
    {
        var game = new GameEntity
        {
            Id = gameId,
            Name = name,
            Publisher = "Test Publisher",
            YearPublished = 2024,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext!.Games.Add(game);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    private async Task CreateTestChatThreadAsync(Guid threadId, Guid userId, Guid gameId)
    {
        // Create thread using the domain model via repository
        var threadRepository = _serviceProvider!.GetRequiredService<IChatThreadRepository>();
        var thread = new ChatThread(
            id: threadId,
            userId: userId,
            gameId: gameId,
            title: "Test Thread"
        );

        await threadRepository.AddAsync(thread, TestCancellationToken);
        await _dbContext!.SaveChangesAsync(TestCancellationToken);
    }

    #endregion
}
