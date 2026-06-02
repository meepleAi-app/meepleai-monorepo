using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Application.EventHandlers;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.EventHandlers;

/// <summary>
/// Issue #1687 Task 8 — unit tests for the cache-invalidation handler that
/// listens to <see cref="PdfMetadataChangedEvent"/> and removes the stale
/// <c>ListUserKbDocs</c> page-cache entries by tag (D-10).
///
/// Tag pattern (matches the existing ListUserKbDocsQueryHandler):
/// <c>["kb", "user-docs", $"user:{userId}"]</c>. The invalidation handler
/// hits the user tag, the broad "user-docs" tag, and (when present) the
/// game-scoped tag $"game:{gameId}" so any sibling cache key that tracks the
/// game also gets dropped.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1687")]
public sealed class PdfMetadataChangedCacheInvalidationHandlerTests
{
    private static readonly Guid DocId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid GameId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

    [Fact]
    public async Task Handle_AlwaysInvalidates_user_userId_tag()
    {
        var cache = new Mock<IHybridCacheService>(MockBehavior.Strict);
        cache.Setup(c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cache.Setup(c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        // No GameId → game tag is NOT invoked.

        var handler = NewHandler(cache.Object);

        await handler.Handle(MakeEvent(gameId: null), CancellationToken.None);

        cache.Verify(c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AlwaysInvalidates_user_docs_tag()
    {
        var cache = new Mock<IHybridCacheService>(MockBehavior.Strict);
        cache.Setup(c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cache.Setup(c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = NewHandler(cache.Object);

        await handler.Handle(MakeEvent(gameId: null), CancellationToken.None);

        cache.Verify(c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithGameId_AlsoInvalidates_game_gameId_tag()
    {
        var cache = new Mock<IHybridCacheService>(MockBehavior.Strict);
        cache.Setup(c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cache.Setup(c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cache.Setup(c => c.RemoveByTagAsync($"game:{GameId}", It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = NewHandler(cache.Object);

        await handler.Handle(MakeEvent(gameId: GameId), CancellationToken.None);

        cache.Verify(c => c.RemoveByTagAsync($"game:{GameId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoGameId_DoesNotInvalidate_game_tag()
    {
        var cache = new Mock<IHybridCacheService>(MockBehavior.Strict);
        cache.Setup(c => c.RemoveByTagAsync($"user:{UserId}", It.IsAny<CancellationToken>())).ReturnsAsync(1);
        cache.Setup(c => c.RemoveByTagAsync("user-docs", It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = NewHandler(cache.Object);

        await handler.Handle(MakeEvent(gameId: null), CancellationToken.None);

        cache.Verify(c => c.RemoveByTagAsync(It.Is<string>(s => s.StartsWith("game:")), It.IsAny<CancellationToken>()),
            Times.Never, "no game tag invalidation when GameId is null");
    }

    [Fact]
    public async Task Handle_NullEvent_ThrowsArgumentNullException()
    {
        var handler = NewHandler(Mock.Of<IHybridCacheService>());
        Func<Task> act = async () => await handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static PdfMetadataChangedEvent MakeEvent(Guid? gameId)
    {
        return new PdfMetadataChangedEvent(
            AggregateId: DocId,
            UserId: UserId,
            EditorRole: "Owner",
            Changes: new[] { new MetadataChange("title", null, "X") },
            GameId: gameId);
    }

    private static PdfMetadataChangedCacheInvalidationHandler NewHandler(IHybridCacheService cache)
        => new(cache, NullLogger<PdfMetadataChangedCacheInvalidationHandler>.Instance);
}
