using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
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
/// Issue #3611 — the Social context is the only one that needs a rendered file: an
/// OpenGraph crawler does not execute CSS, unlike Card/Hero which are framed
/// client-side via <c>object-position</c>. This gives
/// <see cref="GameCoverAssignment.SetGeneratedKey"/> its first production caller.
/// A render failure MUST NOT block the assignment save — the resolver already
/// falls through to the base cover when <c>GeneratedR2Key</c> is null.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AssignCoverSocialCropTests
{
    private static readonly Guid AdminId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly Mock<ISharedGameRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IHybridCacheService> _cache = new();
    private readonly Mock<ICacheInvalidationRetryPolicy> _cacheRetryPolicy = new();
    private readonly Mock<IWebpVariantGenerator> _webp = new();
    private readonly Mock<IBlobStorageService> _blob = new();

    public AssignCoverSocialCropTests()
    {
        // Passthrough: actually run the wrapped cache operation, mirroring the real
        // retry policy on the happy path (copied from AssignCoverCommandHandlerTests).
        _cacheRetryPolicy
            .Setup(p => p.ExecuteAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, ValueTask> op, string _, CancellationToken ct) => op(ct).AsTask());
    }

    private AssignCoverCommandHandler CreateHandler() =>
        new(
            _repository.Object,
            _unitOfWork.Object,
            _cache.Object,
            _cacheRetryPolicy.Object,
            _webp.Object,
            _blob.Object,
            NullLogger<AssignCoverCommandHandler>.Instance);

    /// <summary>A game with a PDF-derived base cover already on file — the source the Social crop reads from.</summary>
    private static SharedGame NewGameWithPdfCover()
    {
        var game = SharedGame.Create(
            "Catan", 1995, "desc", 3, 4, 90, 10, 2.5m, 7.8m,
            "https://example.com/c.jpg", "https://example.com/c-thumb.jpg", null, AdminId);
        game.SetPdfCoverR2Key($"covers/pdf/{Guid.NewGuid():D}/cover");
        return game;
    }

    [Fact]
    public async Task Handle_SocialContext_RendersTheCropAndStampsTheGeneratedKey()
    {
        var game = NewGameWithPdfCover();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        _blob.Setup(b => b.RetrieveRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => new MemoryStream(new byte[] { 1, 2, 3 }));
        _webp.Setup(w => w.GenerateWebpAsync(
                It.IsAny<byte[]>(), 1200, 630, 0.5, 0.2, It.IsAny<CancellationToken>()))
             .ReturnsAsync(new byte[] { 9 });
        _blob.Setup(b => b.StoreRawKeyAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        var handler = CreateHandler();

        await handler.Handle(
            new AssignCoverCommand(game.Id, CoverContext.Social, CoverAssignmentSource.Pdf, AdminId, 0.5, 0.2),
            CancellationToken.None);

        var expectedKey = $"covers/crops/{game.Id:D}/social.webp";
        _blob.Verify(b => b.StoreRawKeyAsync(
            expectedKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()),
            Times.Once);

        game.CoverAssignments.Single(a => a.Context == CoverContext.Social)
            .GeneratedR2Key.Should().Be(expectedKey);
    }

    [Fact]
    public async Task Handle_CardContext_DoesNotRenderAnything()
    {
        var game = NewGameWithPdfCover();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var handler = CreateHandler();

        await handler.Handle(
            new AssignCoverCommand(game.Id, CoverContext.Card, CoverAssignmentSource.Pdf, AdminId, 0.5, 0.5),
            CancellationToken.None);

        _blob.Verify(b => b.StoreRawKeyAsync(
            It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_RenderFails_StillPersistsTheAssignment()
    {
        var game = NewGameWithPdfCover();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _blob.Setup(b => b.RetrieveRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((Stream?)null);

        var handler = CreateHandler();

        var dto = await handler.Handle(
            new AssignCoverCommand(game.Id, CoverContext.Social, CoverAssignmentSource.Pdf, AdminId, 0.5, 0.2),
            CancellationToken.None);

        dto.Context.Should().Be(CoverContext.Social);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        game.CoverAssignments.Single(a => a.Context == CoverContext.Social).GeneratedR2Key.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmptySourceBlob_StillPersistsTheAssignment()
    {
        // A non-null but empty stream (base cover present in R2 but zero-length/corrupted)
        // must not reach GenerateWebpAsync — it throws ArgumentException on empty bytes,
        // which would otherwise propagate out of Handle() and block SaveChangesAsync.
        var game = NewGameWithPdfCover();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _blob.Setup(b => b.RetrieveRawKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(() => new MemoryStream(Array.Empty<byte>()));

        var handler = CreateHandler();

        var dto = await handler.Handle(
            new AssignCoverCommand(game.Id, CoverContext.Social, CoverAssignmentSource.Pdf, AdminId, 0.5, 0.2),
            CancellationToken.None);

        dto.Context.Should().Be(CoverContext.Social);
        _webp.Verify(w => w.GenerateWebpAsync(
            It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        game.CoverAssignments.Single(a => a.Context == CoverContext.Social).GeneratedR2Key.Should().BeNull();
    }
}
