using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetNewGames;

/// <summary>
/// Issue #4085 — <see cref="GetNewGamesQueryHandler"/> must resolve <c>ImageUrl</c> via
/// <c>CoverUrlResolver</c> instead of the <c>SharedGameEntity.ImageUrl</c>/<c>ThumbnailUrl</c>
/// tombstone columns (#2123: always empty since the BGG user-side asset ban). No behavior
/// tests existed for this handler before this issue.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4085")]
public sealed class GetNewGamesQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<IBlobStorageService> _blobStorageMock;
    private readonly Mock<ILogger<GetNewGamesQueryHandler>> _loggerMock;
    private readonly GetNewGamesQueryHandler _handler;

    public GetNewGamesQueryHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _cacheMock = new Mock<IHybridCacheService>();
        _blobStorageMock = new Mock<IBlobStorageService>();
        _loggerMock = new Mock<ILogger<GetNewGamesQueryHandler>>();

        // Cache pass-through: invoke factory directly so we test handler logic, not cache.
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<NewGameDto>>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string _,
                Func<CancellationToken, Task<List<NewGameDto>>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) => factory(ct));

        _handler = new GetNewGamesQueryHandler(_db, _cacheMock.Object, _blobStorageMock.Object, _loggerMock.Object);
    }

    public void Dispose() => _db.Dispose();

    private SharedGameEntity SeedGame(string title, string? pdfCoverR2Key = null)
    {
        var entity = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            YearPublished = 2024,
            Description = "Test game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1, // Published
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false,
            PdfCoverR2Key = pdfCoverR2Key,
        };
        _db.SharedGames.Add(entity);
        return entity;
    }

    [Fact]
    public async Task Handle_ResolvesImageUrl_FromSeededPdfCoverKey()
    {
        SeedGame("Wingspan", pdfCoverR2Key: "pdf-cover-db-key");
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        const string resolvedUrl = "https://r2.example.test/covers/pdf/wingspan-preview.webp";
        _blobStorageMock
            .Setup(b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync(resolvedUrl);

        var result = await _handler.Handle(
            new GetNewGamesQuery(Limit: 10),
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].ImageUrl.Should().Be(resolvedUrl);
    }

    [Fact]
    public async Task Handle_NoCoverKeyAnywhere_ReturnsNullImageUrl_NotTheTombstone()
    {
        // #4085 regression guard: before the fix, this handler's projection fell back to
        // SharedGameEntity.ImageUrl/ThumbnailUrl when both are empty strings (the real
        // #2123 state) — ImageUrl would then be `null` in BOTH the buggy and fixed code
        // for this exact case, which is why Handle_ResolvesImageUrl_FromSeededPdfCoverKey
        // above is the test that actually discriminates. This test exists to pin the
        // "no key → null, not an empty-string tombstone value" contract explicitly.
        SeedGame("Codenames");
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetNewGamesQuery(Limit: 10),
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].ImageUrl.Should().BeNull();
        _blobStorageMock.Verify(
            b => b.GetPresignedUrlForRawKeyAsync(It.IsAny<string>(), It.IsAny<int?>()),
            Times.Never);
    }
}
