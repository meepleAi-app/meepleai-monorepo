using Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.AddCommentToSharedThread;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Tests for AddCommentToSharedThreadCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AddCommentToSharedThreadCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IDistributedCache> _mockCache;
    private readonly AddCommentToSharedThreadCommandHandler _handler;

    public AddCommentToSharedThreadCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockCache = new Mock<IDistributedCache>();
        _handler = new AddCommentToSharedThreadCommandHandler(
            _mockRepository.Object,
            _mockMediator.Object,
            _mockCache.Object
        );
    }

    [Fact]
    public async Task Handle_WithValidCommentRole_AddsCommentSuccessfully()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shareLinkId = Guid.NewGuid();
        var token = "valid_token_123";
        var thread = new ChatThread(threadId, userId);

        var validationResult = new ValidateShareLinkResult(
            ShareLinkId: shareLinkId,
            ThreadId: threadId,
            Role: ShareLinkRole.Comment,
            CreatorId: userId,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            IsValid: true
        );

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        _mockCache
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null); // No rate limit hit

        var command = new AddCommentToSharedThreadCommand(token, "Great explanation!");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.MessageId);
        Assert.True(result.Timestamp > DateTime.MinValue);
        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithInvalidToken_ReturnsNull()
    {
        // Arrange
        var token = "invalid_token";

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ValidateShareLinkResult?)null);

        var command = new AddCommentToSharedThreadCommand(token, "Comment");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithViewOnlyRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var shareLinkId = Guid.NewGuid();
        var token = "view_only_token";

        var validationResult = new ValidateShareLinkResult(
            ShareLinkId: shareLinkId,
            ThreadId: threadId,
            Role: ShareLinkRole.View, // View-only, no comment permission
            CreatorId: Guid.NewGuid(),
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            IsValid: true
        );

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        var command = new AddCommentToSharedThreadCommand(token, "Trying to comment");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("comment permissions", exception.Message);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithRateLimitExceeded_ThrowsInvalidOperationException()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shareLinkId = Guid.NewGuid();
        var token = "valid_token";

        var validationResult = new ValidateShareLinkResult(
            ShareLinkId: shareLinkId,
            ThreadId: threadId,
            Role: ShareLinkRole.Comment,
            CreatorId: userId,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            IsValid: true
        );

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        _mockCache
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("10")); // Already at max (10 comments per hour)

        var command = new AddCommentToSharedThreadCommand(token, "Comment");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("Rate limit exceeded", exception.Message);
        Assert.Contains("10 comments per hour", exception.Message);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithRevokedToken_ReturnsNull()
    {
        // Arrange
        var token = "revoked_token";

        var validationResult = new ValidateShareLinkResult(
            ShareLinkId: Guid.NewGuid(),
            ThreadId: Guid.NewGuid(),
            Role: ShareLinkRole.Comment,
            CreatorId: Guid.NewGuid(),
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            IsValid: false // Revoked
        );

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        var command = new AddCommentToSharedThreadCommand(token, "Comment");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_IncrementsRateLimitCounter()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shareLinkId = Guid.NewGuid();
        var token = "valid_token";
        var thread = new ChatThread(threadId, userId);

        var validationResult = new ValidateShareLinkResult(
            ShareLinkId: shareLinkId,
            ThreadId: threadId,
            Role: ShareLinkRole.Comment,
            CreatorId: userId,
            ExpiresAt: DateTime.UtcNow.AddDays(7),
            IsValid: true
        );

        _mockMediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        _mockCache
            .Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(System.Text.Encoding.UTF8.GetBytes("5")); // 5 comments already

        var command = new AddCommentToSharedThreadCommand(token, "Comment");

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.MessageId);
        _mockCache.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.Is<byte[]>(b => System.Text.Encoding.UTF8.GetString(b) == "6"), // Incremented
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }
}
