using Api.BoundedContexts.Authentication.Application.Commands.CreateShareLink;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Commands.ShareLink;

/// <summary>
/// Comprehensive tests for CreateShareLinkCommandHandler.
/// Tests link creation, thread ownership validation, JWT token generation, and error cases.
/// Uses InMemory database for DbContext operations.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class CreateShareLinkCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IShareLinkRepository> _shareLinkRepositoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly CreateShareLinkCommandHandler _handler;

    private const string TestSecretKey = "test-secret-key-that-is-at-least-32-characters-long-for-hmac-sha256";
    private const string TestBaseUrl = "https://meepleai.test";

    public CreateShareLinkCommandHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _shareLinkRepositoryMock = new Mock<IShareLinkRepository>();
        _configurationMock = new Mock<IConfiguration>();

        // Setup default configuration
        _configurationMock.Setup(c => c["Jwt:ShareLinks:SecretKey"]).Returns(TestSecretKey);
        _configurationMock.Setup(c => c["App:BaseUrl"]).Returns(TestBaseUrl);

        _handler = new CreateShareLinkCommandHandler(
            _dbContext,
            _shareLinkRepositoryMock.Object,
            _configurationMock.Object
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsShareLinkResult()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        await SetupTestDataAsync(userId, threadId, gameId);

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: expiresAt,
            Label: "Test share link",
            UserId: userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.ShareLinkId);
        Assert.NotEmpty(result.Token);
        Assert.Equal(expiresAt, result.ExpiresAt);
        Assert.StartsWith(TestBaseUrl, result.ShareableUrl);
        Assert.Contains("token=", result.ShareableUrl);

        _shareLinkRepositoryMock.Verify(
            r => r.AddAsync(It.Is<Api.BoundedContexts.Authentication.Domain.Entities.ShareLink>(s =>
                s.ThreadId == threadId &&
                s.CreatorId == userId &&
                s.Role == ShareLinkRole.View),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCommentRole_CreatesCommentShareLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await SetupTestDataAsync(userId, threadId, gameId);

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.Comment,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);

        _shareLinkRepositoryMock.Verify(
            r => r.AddAsync(It.Is<Api.BoundedContexts.Authentication.Domain.Entities.ShareLink>(s => s.Role == ShareLinkRole.Comment),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ThreadNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();

        // No thread data added - thread not found

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );

        Assert.Contains(threadId.ToString(), exception.Message);
        Assert.Contains("not found", exception.Message);

        _shareLinkRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.Authentication.Domain.Entities.ShareLink>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ThreadBelongsToDifferentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var differentUserId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        // Thread belongs to different user
        await SetupTestDataAsync(differentUserId, threadId, gameId);

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );

        Assert.Contains("does not belong to user", exception.Message);

        _shareLinkRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.Authentication.Domain.Entities.ShareLink>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_MissingJwtSecretKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await SetupTestDataAsync(userId, threadId, gameId);

        _configurationMock.Setup(c => c["Jwt:ShareLinks:SecretKey"]).Returns((string?)null);

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );

        Assert.Contains("JWT secret key not configured", exception.Message);
    }

    [Fact]
    public async Task Handle_MissingAppBaseUrl_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await SetupTestDataAsync(userId, threadId, gameId);

        _configurationMock.Setup(c => c["App:BaseUrl"]).Returns((string?)null);

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None)
        );

        Assert.Contains("App base URL not configured", exception.Message);
    }

    [Fact]
    public async Task Handle_WithLabel_IncludesLabelInShareLink()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        const string testLabel = "Shared with my game group";

        await SetupTestDataAsync(userId, threadId, gameId);

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: testLabel,
            UserId: userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);

        _shareLinkRepositoryMock.Verify(
            r => r.AddAsync(It.Is<Api.BoundedContexts.Authentication.Domain.Entities.ShareLink>(s => s.Label == testLabel),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_GeneratesValidShareableUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await SetupTestDataAsync(userId, threadId, gameId);

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result.ShareableUrl);
        Assert.Equal($"{TestBaseUrl}/shared/chat?token={result.Token}", result.ShareableUrl);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await SetupTestDataAsync(userId, threadId, gameId);

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await _handler.Handle(command, token);

        // Assert
        _shareLinkRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Api.BoundedContexts.Authentication.Domain.Entities.ShareLink>(), token),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsExpirationMatchingRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var exactExpiration = DateTime.UtcNow.AddHours(24);

        await SetupTestDataAsync(userId, threadId, gameId);

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: exactExpiration,
            Label: null,
            UserId: userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(exactExpiration, result.ExpiresAt);
    }

    [Fact]
    public async Task Handle_GeneratesNonEmptyToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await SetupTestDataAsync(userId, threadId, gameId);

        var command = new CreateShareLinkCommand(
            ThreadId: threadId,
            Role: ShareLinkRole.View,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            Label: null,
            UserId: userId
        );

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result.Token);
        Assert.NotEmpty(result.Token);
        Assert.Contains(".", result.Token); // JWT format has dots
    }

    #region Helper Methods

    private async Task SetupTestDataAsync(Guid userId, Guid threadId, Guid gameId)
    {
        // Create user
        var user = new UserEntity
        {
            Id = userId,
            Email = $"test_{userId:N}@test.com",
            DisplayName = $"Test User {userId:N}",
            PasswordHash = "test-hash",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);

        // Create game
        var game = new GameEntity
        {
            Id = gameId,
            Name = $"Test Game {gameId:N}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Games.Add(game);

        // Create chat thread
        var thread = new ChatThreadEntity
        {
            Id = threadId,
            UserId = userId,
            GameId = gameId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.ChatThreads.Add(thread);

        await _dbContext.SaveChangesAsync();
    }

    #endregion
}
