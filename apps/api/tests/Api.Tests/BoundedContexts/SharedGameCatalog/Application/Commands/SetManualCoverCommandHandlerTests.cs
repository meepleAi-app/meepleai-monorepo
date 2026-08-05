using System.Net;
using Api.BoundedContexts.SecurityAudit.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using ImageMagick;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Epic #3470 Slice 3a — orchestration tests for the manual (arbitrary-URL) cover set.
/// Wires the REAL fetch + re-encode pipeline (<see cref="SsrfSafeHttpClient"/> over a stub
/// <see cref="HttpMessageHandler"/> + the REAL <see cref="WebpVariantGenerator"/>) and mocks only
/// the storage / repo / cache boundary — per the <c>acceptance_tests_must_exercise_real_pipeline</c>
/// lesson (fixture-only setup masks handler-level gaps). The license copyright gate lives in
/// <see cref="SetManualCoverCommandValidator"/> and is covered by its own tests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SetManualCoverCommandHandlerTests
{
    private const string SourceUrl = "https://commons.example.org/cover.png";
    private static readonly Guid AdminId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly Mock<ISharedGameRepository> _repository = new();
    private readonly Mock<IBlobStorageService> _blobStorage = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IHybridCacheService> _cache = new();
    private readonly Mock<ICacheInvalidationRetryPolicy> _cacheRetryPolicy = new();
    private readonly Mock<ITransactionalAuditWriter> _auditWriter = new();
    private readonly StubImageHandler _httpHandler = new();

    public SetManualCoverCommandHandlerTests()
    {
        // Passthrough: actually run the wrapped cache op so RemoveByTag* calls are exercised.
        _cacheRetryPolicy
            .Setup(p => p.ExecuteAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((Func<CancellationToken, ValueTask> op, string _, CancellationToken ct) => op(ct).AsTask());
        _cache
            .Setup(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    private SetManualCoverCommandHandler CreateHandler()
    {
        var fetcher = new SsrfSafeHttpClient(new HttpClient(_httpHandler));
        var webp = new WebpVariantGenerator(NullLogger<WebpVariantGenerator>.Instance);
        return new SetManualCoverCommandHandler(
            _repository.Object,
            fetcher,
            webp,
            _blobStorage.Object,
            _unitOfWork.Object,
            _cache.Object,
            _cacheRetryPolicy.Object,
            _auditWriter.Object,
            NullLogger<SetManualCoverCommandHandler>.Instance);
    }

    private static SharedGame NewGame() => SharedGame.Create(
        "Catan", 1995, "desc", 3, 4, 90, 10, 2.5m, 7.8m,
        "https://example.com/c.jpg", "https://example.com/c-thumb.jpg", null, AdminId);

    private static SetManualCoverCommand Command(Guid gameId) =>
        new(gameId, SourceUrl, "CC BY-SA 4.0", "Jane Doe", AdminId);

    [Fact]
    public async Task Handle_HappyPath_Downloads_ReEncodes_StoresRawKey_Persists_ReturnsResult()
    {
        var game = NewGame();
        var dbKey = $"covers/manual/{game.Id:D}/cover";
        var physicalKey = $"{dbKey}.webp";

        _httpHandler.ImageBytes = SolidPng(400, 600);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _blobStorage
            .Setup(b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blobStorage
            .Setup(b => b.GetPresignedUrlForRawKeyAsync(physicalKey, 3600))
            .ReturnsAsync("https://r2.example.com/presigned");

        var result = await CreateHandler().Handle(Command(game.Id), CancellationToken.None);

        // Result carries the suffix-free DB key (the resolver appends .webp) + the preview URL.
        result.DbKey.Should().Be(dbKey);
        result.DbKey.Should().NotEndWith(".webp");
        result.PresignedUrl.Should().Be("https://r2.example.com/presigned");

        // The aggregate got the manual cover + the license attestation (normalized).
        game.ManualCoverR2Key.Should().Be(dbKey);
        game.ManualCoverLicense.Should().Be("CC-BY-SA-4.0");
        game.ManualCoverAttribution.Should().Be("Jane Doe");
        game.ManualCoverSourceUrl.Should().Be(SourceUrl);
        game.ManualCoverAttestedBy.Should().Be(AdminId);
        game.ManualCoverAttestedAt.Should().NotBeNull();

        // The deterministic physical key is written verbatim via the RAW-KEY path (NOT categorized StoreAsync).
        _blobStorage.Verify(
            b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()),
            Times.Once);
        _blobStorage.Verify(
            b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _repository.Verify(r => r.Update(game), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AppendsImmutableManualCoverSetEvidence_WithSha256AndAdminActor()
    {
        var game = NewGame();
        _httpHandler.ImageBytes = SolidPng(400, 600);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _blobStorage
            .Setup(b => b.StoreRawKeyAsync(It.IsAny<string>(), It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateHandler().Handle(Command(game.Id), CancellationToken.None);

        // #3495 H6 — exactly one immutable evidence event: ManualCoverSet, actor = attesting admin,
        // metadata carrying a 64-hex-char SHA-256 of the re-encoded WebP + the normalized license + URL.
        _auditWriter.Verify(w => w.Append(
            AuditEventType.ManualCoverSet,
            It.Is<Guid?>(id => id == AdminId),
            It.IsAny<Guid?>(),
            It.Is<string>(m => m != null
                && System.Text.RegularExpressions.Regex.IsMatch(m, "\"sha256\":\"[0-9a-f]{64}\"")
                && m.Contains("CC-BY-SA-4.0")
                && m.Contains("commons.example.org")),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SuccessfulSet_InvalidatesSearchAndDetailCache()
    {
        var game = NewGame();
        var physicalKey = $"covers/manual/{game.Id:D}/cover.webp";

        _httpHandler.ImageBytes = SolidPng(400, 600);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _blobStorage
            .Setup(b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _blobStorage.Setup(b => b.GetPresignedUrlForRawKeyAsync(physicalKey, 3600)).ReturnsAsync("url");

        await CreateHandler().Handle(Command(game.Id), CancellationToken.None);

        _cache.Verify(c => c.RemoveByTagAcrossReplicasAsync("search-games", It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveByTagAcrossReplicasAsync($"shared-game:{game.Id}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFound_NoDownload_NoSave()
    {
        _repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        var act = () => CreateHandler().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _httpHandler.RequestCount.Should().Be(0, "a 404 must short-circuit before any egress");
        _blobStorage.Verify(
            b => b.StoreRawKeyAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RawKeyWriteFails_ThrowsExternal_DoesNotPersistNorInvalidate()
    {
        // A failed object write (false — S3 error or local-storage mode) must NOT persist an
        // unresolvable key: throw and leave the aggregate + DB + cache untouched (#3384 parity).
        var game = NewGame();
        var physicalKey = $"covers/manual/{game.Id:D}/cover.webp";

        _httpHandler.ImageBytes = SolidPng(400, 600);
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        _blobStorage
            .Setup(b => b.StoreRawKeyAsync(physicalKey, It.IsAny<Stream>(), "image/webp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = () => CreateHandler().Handle(Command(game.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ExternalServiceException>();
        game.ManualCoverR2Key.Should().BeNull("no key is persisted when the object write failed");
        _repository.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.RemoveByTagAcrossReplicasAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NonHttpsSourceUrl_ThrowsBeforeDownload()
    {
        // Defence in depth: the validator rejects non-HTTPS at 400, but the SSRF-safe fetch path
        // ALSO re-validates the scheme, so a handler invoked directly still refuses egress.
        // #3495 Slice D: that refusal is now a typed HardenedFetchException carrying a bounded
        // reason (still mapped to 400 by ApiExceptionHandlerMiddleware) instead of a bare
        // ArgumentException whose meaning had to be inferred from its message.
        var game = NewGame();
        _repository.Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>())).ReturnsAsync(game);

        var act = () => CreateHandler().Handle(
            Command(game.Id) with { SourceUrl = "http://commons.example.org/cover.png" },
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<Api.SharedKernel.Infrastructure.Http.HardenedFetchException>();
        thrown.Which.Reason.Should().Be(Api.SharedKernel.Infrastructure.Http.HardenedFetchBlockReason.Scheme);
        _httpHandler.RequestCount.Should().Be(0);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static byte[] SolidPng(int width, int height)
    {
        using var image = new MagickImage(new MagickColor("#4682B4"), (uint)width, (uint)height);
        image.Format = MagickFormat.Png;
        using var ms = new MemoryStream();
        image.Write(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Minimal egress stub: returns the configured image bytes on the first (non-redirect) hop and
    /// counts requests so tests can assert "no download happened" on the short-circuit paths.
    /// </summary>
    private sealed class StubImageHandler : HttpMessageHandler
    {
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var content = new ByteArrayContent(ImageBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
