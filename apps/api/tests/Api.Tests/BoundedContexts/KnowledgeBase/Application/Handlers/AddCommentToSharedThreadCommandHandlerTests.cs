using Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.AddCommentToSharedThread;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
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
/// Issue #4592 - the handler decided on the token alone and never looked at the share_links row;
/// a link without the comment role was refused with an InvalidOperationException (a 500 by the
/// project rule, and the endpoint only turned it into a 403 by matching the message).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class AddCommentToSharedThreadCommandHandlerTests
{
    private readonly Mock<IChatThreadRepository> _mockRepository;
    private readonly Mock<IShareLinkRepository> _mockShareLinks;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly IDistributedCache _cache;
    private readonly AddCommentToSharedThreadCommandHandler _handler;

    public AddCommentToSharedThreadCommandHandlerTests()
    {
        _mockRepository = new Mock<IChatThreadRepository>();
        _mockShareLinks = new Mock<IShareLinkRepository>();
        _mockShareLinks
            .Setup(r => r.IsActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockMediator = new Mock<IMediator>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _handler = new AddCommentToSharedThreadCommandHandler(
            _mockRepository.Object,
            _mockShareLinks.Object,
            _mockMediator.Object,
            _cache,
            _mockUnitOfWork.Object);
    }

    private void SetupValidCommentLink(Guid threadId, Guid shareLinkId) =>
        SetupValidLink(threadId, shareLinkId, ShareLinkRole.Comment);

    private void SetupValidLink(Guid threadId, Guid shareLinkId, ShareLinkRole role)
    {
        _mockMediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateShareLinkResult(
                ShareLinkId: shareLinkId,
                ThreadId: threadId,
                Role: role,
                CreatorId: Guid.NewGuid(),
                ExpiresAt: DateTime.UtcNow.AddDays(1),
                IsValid: true));
    }

    private void VerifyNothingWritten()
    {
        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        _mockShareLinks.Verify(r => r.IsActiveAsync(shareLinkId, It.IsAny<CancellationToken>()), Times.Once);
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

    // ========================================
    // #4592: LA RIGA DEL LINK
    // ========================================

    [Fact]
    public async Task Handle_LinkNotActiveOnTheRow_ReturnsNullAndWritesNothing()
    {
        // Il token è valido (firma, scadenza, blacklist) ma la riga dice revocato o scaduto: vale la riga.
        var threadId = Guid.NewGuid();
        var shareLinkId = Guid.NewGuid();
        SetupValidCommentLink(threadId, shareLinkId);
        _mockShareLinks
            .Setup(r => r.IsActiveAsync(shareLinkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new AddCommentToSharedThreadCommand("token", "Great game!"), CancellationToken.None);

        result.Should().BeNull("a link that is revoked or expired on the row is refused like an invalid token");
        _mockRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingWritten();
    }

    [Fact]
    public async Task Handle_InvalidToken_DoesNotAskTheRow()
    {
        _mockMediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ValidateShareLinkResult?)null);

        var result = await _handler.Handle(new AddCommentToSharedThreadCommand("token", "Great game!"), CancellationToken.None);

        result.Should().BeNull();
        _mockShareLinks.Verify(r => r.IsActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyNothingWritten();
    }

    [Fact]
    public async Task Handle_RowCheckFails_TheErrorIsNotSwallowedAndNothingIsWritten()
    {
        // Se il controllo non riesce non si sa se il link è revocato: il commento non passa (come #4573 per la visita).
        var threadId = Guid.NewGuid();
        SetupValidCommentLink(threadId, Guid.NewGuid());
        _mockShareLinks
            .Setup(r => r.IsActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("db unreachable"));

        var act = () => _handler.Handle(new AddCommentToSharedThreadCommand("token", "Great game!"), CancellationToken.None);

        await act.Should().ThrowAsync<TimeoutException>();
        VerifyNothingWritten();
    }

    [Fact]
    public async Task Handle_ViewLink_ThrowsForbiddenExceptionAndWritesNothing()
    {
        // Prima di #4592: InvalidOperationException, cioè 500 per la regola del progetto.
        var threadId = Guid.NewGuid();
        SetupValidLink(threadId, Guid.NewGuid(), ShareLinkRole.View);

        var act = () => _handler.Handle(new AddCommentToSharedThreadCommand("token", "Great game!"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>(
            "a valid link without the comment role is refused as forbidden, not as a server error");
        VerifyNothingWritten();
    }

    [Fact]
    public async Task Handle_ViewLinkNotActiveOnTheRow_ReturnsNullInsteadOfForbidden()
    {
        // Un link morto risponde come ogni link morto, qualunque sia il suo ruolo.
        var threadId = Guid.NewGuid();
        var shareLinkId = Guid.NewGuid();
        SetupValidLink(threadId, shareLinkId, ShareLinkRole.View);
        _mockShareLinks
            .Setup(r => r.IsActiveAsync(shareLinkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _handler.Handle(new AddCommentToSharedThreadCommand("token", "Great game!"), CancellationToken.None);

        result.Should().BeNull();
        VerifyNothingWritten();
    }
}
