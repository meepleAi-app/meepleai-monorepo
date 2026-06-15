using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddGameTranslation;
using Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.AddGameTranslation;

/// <summary>
/// Unit tests for <see cref="AddGameTranslationCommandHandler"/>.
/// Issue #2339 — sub-PR 1/3 Wave 3 (Task 9).
/// Issue #2372 — added cache-invalidation guards (Wave 5 blocker fix).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AddGameTranslationCommandHandlerTests
{
    private static readonly DateTimeOffset SampleNow =
        new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISharedGameTranslationRepository> _translationRepo = new();
    private readonly Mock<ISharedGameRepository> _gameRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IHybridCacheService> _cache = new();
    private readonly Mock<ICacheInvalidationRetryPolicy> _retryPolicy = new();
    private readonly TimeProvider _clock;
    private readonly AddGameTranslationCommandHandler _sut;

    public AddGameTranslationCommandHandlerTests()
    {
        _clock = new FakeTimeProvider(SampleNow);

        // Identity passthrough: invoke the operation delegate inline so the unit test
        // can verify the inner _cache.RemoveByTagAcrossReplicasAsync calls. Matches
        // the production CacheInvalidationRetryPolicy contract (no retry on first attempt).
        _retryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, ValueTask> op, string _, CancellationToken ct) =>
                op(ct).AsTask());

        _sut = new AddGameTranslationCommandHandler(
            _translationRepo.Object,
            _gameRepo.Object,
            _uow.Object,
            _cache.Object,
            _retryPolicy.Object,
            _clock);
    }

    [Fact]
    public async Task Handle_HappyPath_PersistsAndReturnsId()
    {
        var gameId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        // Game exists — handler proceeds to the aggregate factory + persistence path.
        _gameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStubGame(gameId));
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var cmd = new AddGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "I Coloni di Catan",
            Description: "Descrizione",
            Source: "manual",
            ActorUserId: actor);

        var id = await _sut.Handle(cmd, CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        _translationRepo.Verify(
            r => r.AddAsync(It.IsAny<SharedGameTranslation>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFoundException()
    {
        var gameId = Guid.NewGuid();
        _gameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var cmd = new AddGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "Foo",
            Description: null,
            Source: "manual",
            ActorUserId: Guid.NewGuid());

        var act = async () => await _sut.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _translationRepo.Verify(
            r => r.AddAsync(It.IsAny<SharedGameTranslation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DbThrowsUniqueConstraint_RethrowsAsTranslationAlreadyExists()
    {
        var gameId = Guid.NewGuid();
        _gameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStubGame(gameId));
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint \"uq_active_translation_per_locale\"",
                innerException: (Exception?)null));

        var cmd = new AddGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "Foo",
            Description: null,
            Source: "manual",
            ActorUserId: Guid.NewGuid());

        var act = async () => await _sut.Handle(cmd, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<TranslationAlreadyExistsException>();
        thrown.Which.Locale.Should().Be("it");
        thrown.Which.GameId.Should().Be(gameId);
    }

    // ---------------------------------------------------------------------
    // Issue #2372 — cache invalidation regression guards.
    //
    // SearchSharedGamesQueryHandler caches SharedGameDto (including its
    // Translations enrichment payload) under the "search-games" tag with
    // L1 15min / L2 1h TTL. Without explicit invalidation on Add/Update/Delete
    // of a translation, the read-model serves stale SharedGameDto.Translations
    // for up to 60 minutes after CRUD — the bug that #2372 closes.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Handle_HappyPath_InvalidatesSearchGamesAndDetailTags()
    {
        var gameId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        _gameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStubGame(gameId));
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var cmd = new AddGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "I Coloni di Catan",
            Description: null,
            Source: "manual",
            ActorUserId: actor);

        await _sut.Handle(cmd, CancellationToken.None);

        _cache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync("search-games", It.IsAny<CancellationToken>()),
            Times.Once);
        _cache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync($"shared-game:{gameId}", It.IsAny<CancellationToken>()),
            Times.Once);
        _retryPolicy.Verify(
            p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(),
                "shared-games.list",
                It.IsAny<CancellationToken>()),
            Times.Once);
        _retryPolicy.Verify(
            p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(),
                "shared-games.detail",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_GameNotFound_DoesNotInvalidateCache()
    {
        var gameId = Guid.NewGuid();
        _gameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var cmd = new AddGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "Foo",
            Description: null,
            Source: "manual",
            ActorUserId: Guid.NewGuid());

        var act = async () => await _sut.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();

        _cache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_UniqueConstraintViolation_DoesNotInvalidateCache()
    {
        var gameId = Guid.NewGuid();
        _gameRepo
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeStubGame(gameId));
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException(
                "duplicate key value violates unique constraint \"uq_active_translation_per_locale\"",
                innerException: (Exception?)null));

        var cmd = new AddGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "Foo",
            Description: null,
            Source: "manual",
            ActorUserId: Guid.NewGuid());

        var act = async () => await _sut.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<TranslationAlreadyExistsException>();

        _cache.Verify(
            c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    /// <summary>
    /// Minimal SharedGame stub for "game exists" branch. We don't care about its content
    /// — only that the handler treats a non-null result as "proceed". Uses the internal
    /// rehydration ctor (visible to Api.Tests via InternalsVisibleTo).
    /// </summary>
    private static SharedGame MakeStubGame(Guid id) =>
        new(
            id: id,
            title: $"stub-{id:N}",
            yearPublished: 2020,
            description: string.Empty,
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 8,
            complexityRating: null,
            averageRating: null,
            imageUrl: string.Empty,
            thumbnailUrl: string.Empty,
            rules: null,
            status: Api.BoundedContexts.SharedGameCatalog.Domain.Entities.GameStatus.Published,
            createdBy: Guid.NewGuid(),
            modifiedBy: null,
            createdAt: DateTime.UtcNow,
            modifiedAt: null,
            isDeleted: false);

    /// <summary>Deterministic clock matching .NET 8+ TimeProvider abstraction.</summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
