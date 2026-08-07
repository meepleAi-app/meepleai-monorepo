using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Issue #3611 — the detail DTO must expose the cover crop's focal point so the FE can
/// translate it into <c>object-position</c>. Setup mirrors
/// <see cref="GetSharedGameByIdQueryHandlerTests"/> (same fixture/mock/seed pattern).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetSharedGameByIdQueryHandlerCoverFocalTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IBlobStorageService> _blobStorageMock;
    private readonly Mock<ILogger<GetSharedGameByIdQueryHandler>> _loggerMock;

    public GetSharedGameByIdQueryHandlerCoverFocalTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _loggerMock = new Mock<ILogger<GetSharedGameByIdQueryHandler>>();
        _blobStorageMock = new Mock<IBlobStorageService>();
        // Default: presigned URL returns null (dev/local — no R2 configured).
        _blobStorageMock
            .Setup(b => b.GetPresignedDownloadUrlAsync(
                It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((string?)null);
    }

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    private GetSharedGameByIdQueryHandler CreateHandler(MeepleAiDbContext db) =>
        new(_repositoryMock.Object, db, _blobStorageMock.Object, CreateHybridCache(), CreateConfiguration(), _loggerMock.Object);

    [Fact]
    public async Task Handle_PdfCover_ExposesTheHighAnchorFocalOnTheDetailDto()
    {
        // Arrange: gioco con la sola cover da PDF, nessuna assegnazione admin.
        // Seed pattern riusato da Handle_SharedGameWithPdfCover_ProjectsCoverUrl.
        var gameId = Guid.NewGuid();
        const string pdfKey = "pdf-cover-key";
        const string expectedUrl = "https://r2/pdf-cover-key-preview.webp";

        var game = SharedGame.Create(
            "Cover Game",
            2021,
            "Description",
            2,
            4,
            45,
            8,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        var sharedGameEntity = new SharedGameEntity
        {
            Id = gameId,
            Title = "Cover Game",
            YearPublished = 2021,
            Description = "Description",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Status = (int)GameStatus.Published,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            PdfCoverR2Key = pdfKey,
        };

        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // CoverUrlResolver calls GetPresignedUrlForRawKeyAsync("{key}-preview.webp", null)
        _blobStorageMock
            .Setup(b => b.GetPresignedUrlForRawKeyAsync(
                $"{pdfKey}-preview.webp",
                null))
            .ReturnsAsync(expectedUrl);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        await db.SharedGames.AddAsync(sharedGameEntity);
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        // Act
        var dto = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        dto.Should().NotBeNull();
        dto!.CoverFocalX.Should().Be(0.5);
        dto.CoverFocalY.Should().Be(0.2);
    }
}
