using Api.BoundedContexts.Authentication.Application.Queries.ValidateShareLink;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetSharedThread;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Unit tests for GetSharedThreadQueryHandler (#4573).
/// La visita conta sé stessa con <c>TryRecordAccessAsync</c>, che controlla la validità nello stesso statement.
/// Non legge né riscrive mai l'aggregato: prima di #4573 lo faceva, e una revoca concorrente tornava
/// <c>revoked_at = NULL</c>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4573")]
public class GetSharedThreadQueryHandlerTests
{
    private readonly Mock<IChatThreadRepository> _threadRepository = new();
    private readonly Mock<IShareLinkRepository> _shareLinkRepository = new(MockBehavior.Strict);
    private readonly Mock<IMediator> _mediator = new();

    private readonly Guid _threadId = Guid.NewGuid();
    private readonly Guid _shareLinkId = Guid.NewGuid();

    private GetSharedThreadQueryHandler CreateHandler() =>
        new(_threadRepository.Object, _shareLinkRepository.Object, _mediator.Object, NullLogger<GetSharedThreadQueryHandler>.Instance);

    private void SetupValidTokenAndThread()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidateShareLinkResult(
                ShareLinkId: _shareLinkId,
                ThreadId: _threadId,
                Role: ShareLinkRole.View,
                CreatorId: Guid.NewGuid(),
                ExpiresAt: DateTime.UtcNow.AddDays(1),
                IsValid: true));
        _threadRepository
            .Setup(r => r.GetByIdAsync(_threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatThread(_threadId, Guid.NewGuid(), title: "Chat condivisa"));
    }

    [Fact]
    public async Task Handle_LinkStillValid_ReturnsTheThreadAndCountsTheVisitOnce()
    {
        SetupValidTokenAndThread();
        _shareLinkRepository
            .Setup(r => r.TryRecordAccessAsync(_shareLinkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateHandler().Handle(new GetSharedThreadQuery("token"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ThreadId.Should().Be(_threadId);
        // Mock stretto: qualunque altra chiamata al repository (GetByIdAsync, UpdateAsync) farebbe fallire il test.
        _shareLinkRepository.Verify(r => r.TryRecordAccessAsync(_shareLinkId, It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_LinkRevokedBeforeTheVisitCounts_ReturnsNull()
    {
        SetupValidTokenAndThread();
        _shareLinkRepository
            .Setup(r => r.TryRecordAccessAsync(_shareLinkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateHandler().Handle(new GetSharedThreadQuery("token"), CancellationToken.None);

        result.Should().BeNull("the link was valid when the token was checked but no longer when the visit counted itself: 404");
    }

    [Fact]
    public async Task Handle_CountingFails_StillReturnsTheThread()
    {
        // Il conteggio è accessorio: un errore del database non deve far fallire la lettura della chat.
        SetupValidTokenAndThread();
        _shareLinkRepository
            .Setup(r => r.TryRecordAccessAsync(_shareLinkId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var result = await CreateHandler().Handle(new GetSharedThreadQuery("token"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ThreadId.Should().Be(_threadId);
    }

    [Fact]
    public async Task Handle_InvalidToken_DoesNotCountTheVisit()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<ValidateShareLinkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ValidateShareLinkResult?)null);

        var result = await CreateHandler().Handle(new GetSharedThreadQuery("bad-token"), CancellationToken.None);

        result.Should().BeNull();
        _shareLinkRepository.Verify(r => r.TryRecordAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
