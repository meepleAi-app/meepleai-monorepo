using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Issue #1620 — Unit tests for the cache-invalidation handler that listens to
/// <see cref="PdfStateChangedEvent"/> and removes the stale
/// <c>ListUserKbDocs</c> page-cache entries by tag.
///
/// Tag pattern (matches the existing ListUserKbDocsQueryHandler at
/// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/
/// ListUserKbDocs/ListUserKbDocsQueryHandler.cs:69):
/// <c>["kb", "user-docs", $"user:{userId}"]</c>. The handler hits the per-user
/// tag (precise) AND the broad <c>user-docs</c> tag (covers any future cache
/// entry that opted into the same tag namespace without per-user scoping).
///
/// PdfStateChangedEvent does NOT carry GameId (see
/// apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Events/
/// PdfStateChangedEvent.cs) so we do not attempt a game-scoped invalidation
/// here — the metadata-changed handler covers that surface separately.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1620")]
public sealed class PdfStateChangedCacheInvalidationHandlerTests
{
    private static readonly Guid DocId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly Guid UserId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");

    [Fact]
    public async Task Handle_Invalidates_user_userId_tag()
    {
        var cache = new Mock<IHybridCacheService>(MockBehavior.Strict);
        cache.Setup(c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cache.Setup(c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = NewHandler(cache.Object);

        await handler.Handle(MakeEvent(), CancellationToken.None);

        cache.Verify(
            c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Invalidates_user_docs_tag()
    {
        var cache = new Mock<IHybridCacheService>(MockBehavior.Strict);
        cache.Setup(c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cache.Setup(c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = NewHandler(cache.Object);

        await handler.Handle(MakeEvent(), CancellationToken.None);

        cache.Verify(
            c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotInvalidateAnyGameTag()
    {
        // PdfStateChangedEvent intentionally carries no GameId; the handler
        // must NOT attempt to invalidate any game-scoped tag (that surface is
        // owned by the metadata-changed handler).
        var cache = new Mock<IHybridCacheService>(MockBehavior.Strict);
        cache.Setup(c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cache.Setup(c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = NewHandler(cache.Object);

        await handler.Handle(MakeEvent(), CancellationToken.None);

        cache.Verify(
            c => c.RemoveByTagAsync(It.Is<string>(t => t.StartsWith("game:", StringComparison.Ordinal)), It.IsAny<CancellationToken>()),
            Times.Never,
            "PdfStateChangedEvent has no GameId; no game-scoped invalidation expected");
    }

    [Fact]
    public async Task Handle_InvalidatesOnAnyStateTransition()
    {
        // Any transition is potentially user-visible (e.g. Indexing → Ready
        // surfaces the doc as ready; Failed surfaces an error badge). The
        // handler must not gate invalidation on the new-state value.
        var cache = new Mock<IHybridCacheService>(MockBehavior.Strict);
        cache.Setup(c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cache.Setup(c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = NewHandler(cache.Object);

        // Exercise a transition that does NOT end in Ready — handler must
        // still invalidate (the list view shows non-Ready docs too when
        // state=all is requested).
        await handler.Handle(
            new PdfStateChangedEvent(
                pdfDocumentId: DocId,
                previousState: PdfProcessingState.Indexing,
                newState: PdfProcessingState.Failed,
                uploadedByUserId: UserId),
            CancellationToken.None);

        cache.Verify(c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullEvent_ThrowsArgumentNullException()
    {
        var handler = NewHandler(Mock.Of<IHybridCacheService>());
        Func<Task> act = async () => await handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static PdfStateChangedEvent MakeEvent() =>
        new(
            pdfDocumentId: DocId,
            previousState: PdfProcessingState.Indexing,
            newState: PdfProcessingState.Ready,
            uploadedByUserId: UserId);

    private static PdfStateChangedCacheInvalidationHandler NewHandler(IHybridCacheService cache) =>
        new(cache, NullLogger<PdfStateChangedCacheInvalidationHandler>.Instance);
}
