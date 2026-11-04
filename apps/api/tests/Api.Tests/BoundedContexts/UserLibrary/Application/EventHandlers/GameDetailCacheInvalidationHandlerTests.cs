using Api.BoundedContexts.UserLibrary.Application.EventHandlers;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.EventHandlers;

/// <summary>
/// #4155: la dichiarazione di possesso cambia lo stato della voce (Nuovo → Owned), ma il dettaglio
/// del gioco resta in cache 5 minuti e nessuno lo invalidava: la pagina mostrava lo stato vecchio.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
[Trait("Issue", "4155")]
public sealed class GameDetailCacheInvalidationHandlerTests
{
    [Fact]
    public async Task OwnershipDeclared_EvictsTheGameDetailOfThatUserAndGame()
    {
        var cache = TestDbContextFactory.CreateInMemoryHybridCache();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var otherGameId = Guid.NewGuid();
        var key = GetGameDetailQueryHandler.BuildCacheKey(userId, gameId);
        var otherKey = GetGameDetailQueryHandler.BuildCacheKey(userId, otherGameId);
        await cache.SetAsync(key, "stale", cancellationToken: TestContext.Current.CancellationToken);
        await cache.SetAsync(otherKey, "untouched", cancellationToken: TestContext.Current.CancellationToken);

        var handler = new GameDetailCacheInvalidationHandler(cache, NullLogger<GameDetailCacheInvalidationHandler>.Instance);
        await handler.Handle(
            new OwnershipDeclaredEvent(entryId: Guid.NewGuid(), userId: userId, gameId: gameId, declaredAt: DateTime.UtcNow),
            TestContext.Current.CancellationToken);

        (await ReadAsync(cache, key)).Should().BeNull("il dettaglio di quel gioco va ricalcolato");
        (await ReadAsync(cache, otherKey)).Should().Be("untouched", "gli altri giochi dell'utente restano in cache");
    }

    private static async Task<string?> ReadAsync(Microsoft.Extensions.Caching.Hybrid.HybridCache cache, string key) =>
        await cache.GetOrCreateAsync<string?>(
            key,
            _ => ValueTask.FromResult<string?>(null),
            cancellationToken: TestContext.Current.CancellationToken);
}
