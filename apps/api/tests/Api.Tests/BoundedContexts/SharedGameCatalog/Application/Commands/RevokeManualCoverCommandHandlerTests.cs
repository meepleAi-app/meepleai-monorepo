using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 3a-3 — orchestration tests for the manual-cover revoke. Mocked repo /
/// storage / cache over a REAL <see cref="SharedGame"/> aggregate. Verifies the persist-first
/// order (DB revoke authoritative, R2 delete best-effort after), the idempotent 204 no-op, and
/// the 404.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class RevokeManualCoverCommandHandlerTests
{
    private static readonly Guid AdminId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly Mock<ISharedGameRepository> _repository = new();
    private readonly Mock<IBlobStorageService> _blobStorage = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IHybridCacheService> _cache = new();
    private readonly Mock<ICacheInvalidationRetryPolicy> _cacheRetryPolicy = new();

    public RevokeManualCoverCommandHandlerTests()
    {
        _cacheRetryPolicy
            .Setup(p => p.ExecuteAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, ValueTask> op, string _, CancellationToken ct) => op(ct).AsTask());
        _cache
            .Setup(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private RevokeManualCoverCommandHandler CreateHandler() =>
        new(_repository.Object, _blobStorage.Object, _unitOfWork.Object, _cache.Object,
            _cacheRetryPolicy.Object, NullLogger<RevokeManualCoverCommandHandler>.Instance);

    private static SharedGame NewGame() => SharedGame.Create(
        "Catan", 1995, "desc", 3, 4, 90, 10, 2.5m, 7.8m,
        "https://example.com/c.jpg", "https://example.com/c-thumb.jpg", null, AdminId);

    private static SharedGame GameWithManualCover()
    {
        var game = NewGame();
        var dbKey = CoverKeyBuilder.ForManual(game.Id).DbKey;
        game.SetManualCover(dbKey, "CC0", null, "https://example.com/src", AdminId, DateTime.UtcNow);
        return game;
    }

    [Fact]
    public async Task Handle_WithManualCoverSet_ClearsAggregate_Persists_DeletesR2_InvalidatesCache()
    {
        var game = GameWithManualCover();
        var physicalKey = $"covers/manual/{game.Id:D}/cover.webp";
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _blobStorage
            .Setup(b => b.DeleteRawKeyAsync(physicalKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateHandler().Handle(new RevokeManualCoverCommand(game.Id, AdminId), CancellationToken.None);

        game.ManualCoverR2Key.Should().BeNull();
        game.ManualCoverLicense.Should().BeNull();
        game.ManualCoverAttestedBy.Should().BeNull();

        _repository.Verify(r => r.Update(game), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _blobStorage.Verify(b => b.DeleteRawKeyAsync(physicalKey, It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveByTagAcrossReplicasAsync("search-games", It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveByTagAcrossReplicasAsync($"shared-game:{game.Id}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithManualPinnedToContext_DropsManualAssignment_Reconciles_KeepsOthers()
    {
        // Slice 3f: revoking the manual source cascades to its per-context pins — the Manual
        // assignment is removed (and persisted via the child-safe reconcile) while a non-Manual
        // pin survives, so no phantom Manual pin lingers to auto-resurface a future manual cover.
        var game = GameWithManualCover();
        game.AssignCover(CoverContext.Hero, CoverAssignmentSource.Manual, AdminId);
        game.AssignCover(CoverContext.Card, CoverAssignmentSource.Wikidata, AdminId);
        var physicalKey = $"covers/manual/{game.Id:D}/cover.webp";
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _blobStorage
            .Setup(b => b.DeleteRawKeyAsync(physicalKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateHandler().Handle(new RevokeManualCoverCommand(game.Id, AdminId), CancellationToken.None);

        game.CoverAssignments.Should().ContainSingle();
        game.CoverAssignments.Single().Source.Should().Be(CoverAssignmentSource.Wikidata);
        _repository.Verify(r => r.ReconcileCoverAssignmentsAsync(game, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoManualCoverSet_Idempotent_NoPersist_NoDelete_NoInvalidate()
    {
        // A game that never had a manual cover → 204 no-op: nothing to persist, delete, or evict.
        var game = NewGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        await CreateHandler().Handle(new RevokeManualCoverCommand(game.Id, AdminId), CancellationToken.None);

        _repository.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _blobStorage.Verify(b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFound_NoSideEffects()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var act = () => CreateHandler().Handle(new RevokeManualCoverCommand(Guid.NewGuid(), AdminId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _blobStorage.Verify(b => b.DeleteRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_R2DeleteThrows_StillSucceeds_DbAlreadyRevoked()
    {
        // Persist-first: the DB revoke is committed BEFORE the best-effort R2 delete, so a
        // storage failure must be swallowed and the request must still succeed.
        var game = GameWithManualCover();
        var physicalKey = $"covers/manual/{game.Id:D}/cover.webp";
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _blobStorage
            .Setup(b => b.DeleteRawKeyAsync(physicalKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated R2 outage"));

        var act = () => CreateHandler().Handle(new RevokeManualCoverCommand(game.Id, AdminId), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once, "DB revoke commits before the best-effort delete");
        _cache.Verify(c => c.RemoveByTagAcrossReplicasAsync("search-games", It.IsAny<CancellationToken>()), Times.Once, "cache still evicted after a swallowed R2 failure");
    }
}
