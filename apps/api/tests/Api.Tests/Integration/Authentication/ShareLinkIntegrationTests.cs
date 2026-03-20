using Api.BoundedContexts.Authentication.Application.Commands.CreateShareLink;
using Api.BoundedContexts.Authentication.Application.Commands.RevokeShareLink;
using Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for ShareLink feature using Testcontainers (PostgreSQL + Redis).
/// Tests the complete lifecycle: creation, validation, revocation, and blacklist enforcement.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Dependency", "Redis")]
[Trait("BoundedContext", "Authentication")]
public sealed class ShareLinkIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IServiceProvider? _serviceProvider;

    private const string TestSecretKey = "test-secret-key-that-is-at-least-32-characters-long-for-hmac-sha256";
    private const string TestBaseUrl = "https://meepleai.test";

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ShareLinkIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private void EnsureTestInfrastructureAvailable()
    {
        if (_serviceProvider == null || _dbContext == null)
        {
            Assert.Skip("Shared Testcontainers fixture not available. ShareLink integration tests require PostgreSQL and Redis.");
        }
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            // Create isolated database for this test class
            _databaseName = $"test_sharelink_{Guid.NewGuid():N}";
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

            // DbContext with EF Core
            services.AddDbContext<MeepleAiDbContext>(options =>
                options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector()) // Issue #3547
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

            // Redis distributed cache
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = _fixture.RedisConnectionString;
                options.InstanceName = $"test_sharelink_{Guid.NewGuid():N}:";
            });

            // Repositories
            services.AddScoped<IShareLinkRepository, ShareLinkRepository>();

            // MediatR for handlers
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateShareLinkCommandHandler).Assembly));

            services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector,
                Api.SharedKernel.Application.Services.DomainEventCollector>();

            _serviceProvider = services.BuildServiceProvider();

            // Initialize database with migrations
            _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
            await _dbContext.Database.MigrateAsync(TestCancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize ShareLink integration test infrastructure: {ex.Message}");
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

    #region Create Share Link Tests

    [Fact]
    public async Task CreateShareLink_WithValidRequest_PersistsToDatabase()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Create test user and thread
        await CreateTestUserAsync(userId, "test@example.com");
        await CreateTestChatThreadAsync(threadId, userId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: "Integration test link",
            UserId: userId
        );

        // Act
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.ShareLinkId.Should().NotBe(Guid.Empty);
        result.Token.Should().NotBeEmpty();
        result.ShareableUrl.Should().StartWith(TestBaseUrl);

        // Verify persistence
        var repository = _serviceProvider!.GetRequiredService<IShareLinkRepository>();
        var persistedLink = await repository.GetByIdAsync(result.ShareLinkId, TestCancellationToken);

        persistedLink.Should().NotBeNull();
        persistedLink.ThreadId.Should().Be(threadId);
        persistedLink.CreatorId.Should().Be(userId);
        persistedLink.Role.Should().Be(ShareLinkRole.View);
        persistedLink.Label.Should().Be("Integration test link");
    }

    [Fact]
    public async Task CreateShareLink_WithCommentRole_PersistsCorrectRole()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"comment-test-{Guid.NewGuid():N}@example.com");
        await CreateTestChatThreadAsync(threadId, userId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.Comment,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        // Act
        var result = await mediator.Send(command, TestCancellationToken);

        // Assert
        var repository = _serviceProvider!.GetRequiredService<IShareLinkRepository>();
        var persistedLink = await repository.GetByIdAsync(result.ShareLinkId, TestCancellationToken);

        persistedLink.Should().NotBeNull();
        persistedLink.Role.Should().Be(ShareLinkRole.Comment);
    }

    [Fact]
    public async Task CreateShareLink_ForNonOwnedThread_ThrowsException()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(ownerId, $"owner-{Guid.NewGuid():N}@example.com");
        await CreateTestUserAsync(otherUserId, $"other-{Guid.NewGuid():N}@example.com");
        await CreateTestChatThreadAsync(threadId, ownerId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: otherUserId // Different user
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(command, TestCancellationToken)
        );
    }

    #endregion

    #region Validate Share Link Tests

    [Fact]
    public async Task ValidateShareLink_WithValidToken_ReturnsValidResult()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"validate-test-{Guid.NewGuid():N}@example.com");
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

        // Act - validate the token
        var validateQuery = new ValidateShareLinkQuery(createResult.Token);
        var validateResult = await mediator.Send(validateQuery, TestCancellationToken);

        // Assert
        validateResult.Should().NotBeNull();
        validateResult.IsValid.Should().BeTrue();
        validateResult.ShareLinkId.Should().Be(createResult.ShareLinkId);
        validateResult.ThreadId.Should().Be(threadId);
        validateResult.Role.Should().Be(ShareLinkRole.View);
    }

    [Fact]
    public async Task ValidateShareLink_WithInvalidToken_ReturnsNull()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Act
        var validateQuery = new ValidateShareLinkQuery("invalid-token-value");
        var validateResult = await mediator.Send(validateQuery, TestCancellationToken);

        // Assert
        validateResult.Should().BeNull();
    }

    [Fact]
    public async Task ValidateShareLink_WithExpiredToken_ReturnsInvalid()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"expired-test-{Guid.NewGuid():N}@example.com");
        await CreateTestChatThreadAsync(threadId, userId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Create share link with very short expiration (200ms)
        var createCommand = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddMilliseconds(200),
            Label: "Expiration test link",
            UserId: userId
        );

        var createResult = await mediator.Send(createCommand, TestCancellationToken);

        // Verify token is valid initially
        var validateQuery1 = new ValidateShareLinkQuery(createResult.Token);
        var validateResult1 = await mediator.Send(validateQuery1, TestCancellationToken);
        validateResult1.Should().NotBeNull();
        validateResult1.IsValid.Should().BeTrue("Token should be valid immediately after creation");

        // Wait for token to expire
        await Task.Delay(300, TestCancellationToken);

        // Act - validate the expired token
        var validateQuery2 = new ValidateShareLinkQuery(createResult.Token);
        var validateResult2 = await mediator.Send(validateQuery2, TestCancellationToken);

        // Assert - token should be invalid due to expiration (returns null for invalid JWT)
        validateResult2.Should().BeNull();
    }

    #endregion

    #region Revoke Share Link Tests

    [Fact]
    public async Task RevokeShareLink_WithValidRequest_BlacklistsToken()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"revoke-test-{Guid.NewGuid():N}@example.com");
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

        // Verify token is valid before revocation
        var validateQuery1 = new ValidateShareLinkQuery(createResult.Token);
        var validateResult1 = await mediator.Send(validateQuery1, TestCancellationToken);
        validateResult1.Should().NotBeNull();
        validateResult1.IsValid.Should().BeTrue();

        // Act - revoke the share link
        var revokeCommand = new RevokeShareLinkCommand(
            ShareLinkId: createResult.ShareLinkId,
            UserId: userId
        );

        var revokeResult = await mediator.Send(revokeCommand, TestCancellationToken);

        // Assert
        revokeResult.Should().BeTrue();

        // Verify token is now invalid (blacklisted)
        var validateQuery2 = new ValidateShareLinkQuery(createResult.Token);
        var validateResult2 = await mediator.Send(validateQuery2, TestCancellationToken);

        validateResult2.Should().NotBeNull();
        validateResult2.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeShareLink_ByNonOwner_ReturnsFalse()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(ownerId, $"owner-revoke-{Guid.NewGuid():N}@example.com");
        await CreateTestUserAsync(otherUserId, $"other-revoke-{Guid.NewGuid():N}@example.com");
        await CreateTestChatThreadAsync(threadId, ownerId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Create share link as owner
        var createCommand = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: ownerId
        );

        var createResult = await mediator.Send(createCommand, TestCancellationToken);

        // Act - try to revoke as different user
        var revokeCommand = new RevokeShareLinkCommand(
            ShareLinkId: createResult.ShareLinkId,
            UserId: otherUserId
        );

        var revokeResult = await mediator.Send(revokeCommand, TestCancellationToken);

        // Assert
        revokeResult.Should().BeFalse();

        // Verify token is still valid
        var validateQuery = new ValidateShareLinkQuery(createResult.Token);
        var validateResult = await mediator.Send(validateQuery, TestCancellationToken);

        validateResult.Should().NotBeNull();
        validateResult.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeShareLink_NonExistent_ReturnsFalse()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        await CreateTestUserAsync(userId, $"nonexistent-revoke-{Guid.NewGuid():N}@example.com");

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Act - try to revoke non-existent share link
        var revokeCommand = new RevokeShareLinkCommand(
            ShareLinkId: Guid.NewGuid(),
            UserId: userId
        );

        var revokeResult = await mediator.Send(revokeCommand, TestCancellationToken);

        // Assert
        revokeResult.Should().BeFalse();
    }

    #endregion

    #region Full Lifecycle Tests

    [Fact]
    public async Task ShareLink_FullLifecycle_CreateValidateRevokeValidate()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"lifecycle-test-{Guid.NewGuid():N}@example.com");
        await CreateTestChatThreadAsync(threadId, userId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Step 1: Create share link
        var createCommand = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.Comment,
            ExpiresAt: DateTime.UtcNow.AddDays(30),
            Label: "Full lifecycle test",
            UserId: userId
        );

        var createResult = await mediator.Send(createCommand, TestCancellationToken);
        createResult.Should().NotBeNull();
        createResult.Token.Should().NotBeEmpty();

        // Step 2: Validate - should be valid
        var validateQuery1 = new ValidateShareLinkQuery(createResult.Token);
        var validateResult1 = await mediator.Send(validateQuery1, TestCancellationToken);

        validateResult1.Should().NotBeNull();
        validateResult1.IsValid.Should().BeTrue();
        validateResult1.Role.Should().Be(ShareLinkRole.Comment);
        validateResult1.CreatorId.Should().Be(userId);

        // Step 3: Revoke
        var revokeCommand = new RevokeShareLinkCommand(
            ShareLinkId: createResult.ShareLinkId,
            UserId: userId
        );

        var revokeResult = await mediator.Send(revokeCommand, TestCancellationToken);
        revokeResult.Should().BeTrue();

        // Step 4: Validate again - should be invalid (blacklisted)
        var validateQuery2 = new ValidateShareLinkQuery(createResult.Token);
        var validateResult2 = await mediator.Send(validateQuery2, TestCancellationToken);

        validateResult2.Should().NotBeNull();
        validateResult2.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ShareLink_MultipleLinks_IndependentRevocation()
    {
        // Arrange
        EnsureTestInfrastructureAvailable();

        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await CreateTestUserAsync(userId, $"multi-link-{Guid.NewGuid():N}@example.com");
        await CreateTestChatThreadAsync(threadId, userId, gameId);

        var mediator = _serviceProvider!.GetRequiredService<IMediator>();

        // Create two share links for the same thread
        var createCommand1 = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: "Link 1",
            UserId: userId
        );

        var createCommand2 = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.Comment,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: "Link 2",
            UserId: userId
        );

        var result1 = await mediator.Send(createCommand1, TestCancellationToken);
        var result2 = await mediator.Send(createCommand2, TestCancellationToken);

        // Verify both are valid initially
        var validate1Before = await mediator.Send(new ValidateShareLinkQuery(result1.Token), TestCancellationToken);
        var validate2Before = await mediator.Send(new ValidateShareLinkQuery(result2.Token), TestCancellationToken);

        validate1Before.Should().NotBeNull();
        validate1Before.IsValid.Should().BeTrue();
        validate2Before.Should().NotBeNull();
        validate2Before.IsValid.Should().BeTrue();

        // Act - revoke only the first link
        var revokeCommand = new RevokeShareLinkCommand(
            ShareLinkId: result1.ShareLinkId,
            UserId: userId
        );

        await mediator.Send(revokeCommand, TestCancellationToken);

        // Assert - first link is invalid, second is still valid
        var validate1After = await mediator.Send(new ValidateShareLinkQuery(result1.Token), TestCancellationToken);
        var validate2After = await mediator.Send(new ValidateShareLinkQuery(result2.Token), TestCancellationToken);

        validate1After.Should().NotBeNull();
        validate1After.IsValid.Should().BeFalse(); // Revoked

        validate2After.Should().NotBeNull();
        validate2After.IsValid.Should().BeTrue(); // Still valid
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

    private async Task CreateTestChatThreadAsync(Guid threadId, Guid userId, Guid gameId)
    {
        // First create a game if it doesn't exist
        var game = await _dbContext!.Games.FindAsync([gameId], TestCancellationToken);
        if (game == null)
        {
            game = new GameEntity
            {
                Id = gameId,
                Name = $"Test Game {gameId:N}",
                Publisher = "Test Publisher",
                YearPublished = 2024,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Games.Add(game);
            await _dbContext.SaveChangesAsync(TestCancellationToken);
        }

        var thread = new ChatThreadEntity
        {
            Id = threadId,
            UserId = userId,
            GameId = gameId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ChatThreads.Add(thread);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #endregion
}
