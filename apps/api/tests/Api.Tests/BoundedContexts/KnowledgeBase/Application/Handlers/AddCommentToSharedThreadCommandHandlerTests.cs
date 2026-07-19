using Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.AddCommentToSharedThread;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for AddCommentToSharedThreadCommandHandler.
/// Issue #3171 - the handler staged the comment via ChatThreadRepository.UpdateAsync
/// (stage-only) but never called SaveChangesAsync, so the comment was never persisted.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class AddCommentToSharedThreadCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly IDistributedCache _cache;
    private readonly AddCommentToSharedThreadCommandHandler _handler;

    public AddCommentToSharedThreadCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _handler = new AddCommentToSharedThreadCommandHandler(
            _mockRepository.Object,
            _mockMediator.Object,
            _cache,
            _mockUnitOfWork.Object);
    }

    private void SetupValidCommentLink(Guid threadId, Guid shareLinkId)
    {
        _mockMediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateShareLinkResult(
                ShareLinkId: shareLinkId,
                ThreadId: threadId,
                Role: ShareLinkRole.Comment,
                CreatorId: Guid.NewGuid(),
                ExpiresAt: DateTime.UtcNow.AddDays(1),
                IsValid: true));
    }

    [Fact]
    public async Task Handle_ValidCommentLink_PersistsCommentViaSaveChanges()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var shareLinkId = Guid.NewGuid();
        var thread = new ChatThread(threadId, Guid.NewGuid());
        SetupValidCommentLink(threadId, shareLinkId);
        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        var command = new AddCommentToSharedThreadCommand("token", "Great game!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - the comment must be persisted, not merely staged
        result.Should().NotBeNull();
        _mockRepository.Verify(r => r.UpdateAsync(thread, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThreadNotFound_DoesNotSave()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        SetupValidCommentLink(threadId, Guid.NewGuid());
        _mockRepository
            .Setup(r => r.GetByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);

        var command = new AddCommentToSharedThreadCommand("token", "Great game!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
