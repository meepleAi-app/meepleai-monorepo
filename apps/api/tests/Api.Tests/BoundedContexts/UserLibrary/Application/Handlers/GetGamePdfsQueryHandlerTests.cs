using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Unit tests for GetGamePdfsQueryHandler.
/// Issue #3152: Game Detail Split View - PDF selector support
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GetGamePdfsQueryHandlerTests
{
    private readonly MeepleAiDbContext _db;
    private readonly GetGamePdfsQueryHandler _handler;

    public GetGamePdfsQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        var mockLogger = new Mock<ILogger<GetGamePdfsQueryHandler>>();
        _handler = new GetGamePdfsQueryHandler(_db, mockLogger.Object);
    }

    [Fact]
    public async Task Handle_WhenNoPdfsExist_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var query = new GetGamePdfsQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenPdfExistsForGame_ReturnsPdf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var game = new SharedGameEntity { Id = gameId, Title = "Test Game" };
        _db.SharedGames.Add(game);

        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            FileName = "TestRules.pdf",
            FilePath = "/test/path.pdf",
            FileSizeBytes = 1_000_000,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            PageCount = 10,
            Language = "EN"
        };
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();

        var query = new GetGamePdfsQuery(gameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("TestRules");
        result[0].FileSizeBytes.Should().Be(1_000_000);
        result[0].Source.Should().Be("Catalog");
        result[0].Language.Should().Be("EN");
    }

    [Fact]
    public async Task Handle_WhenPdfExistsForSharedGame_ReturnsPdf()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "SharedRules.pdf",
            FilePath = "/test/shared.pdf",
            FileSizeBytes = 500_000,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            Language = "IT"
        };
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();

        var query = new GetGamePdfsQuery(sharedGameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("SharedRules");
        result[0].Language.Should().Be("IT");
    }

    [Fact]
    public async Task Handle_WhenPdfLinkedViaGamesSharedGameId_ReturnsPdf()
    {
        // Regression for post-PR #267 fix: a PDF uploaded against a games row that is linked to a
        // shared game (games.SharedGameId = request.GameId) must be retrievable when querying
        // by the shared game id, even though pdf_documents.SharedGameId is null.
        // The previous implementation resolved a SINGLE games row and missed PDFs on other
        // games rows linked to the same SharedGameId.

        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        // Two different games rows linked to the same shared game (different version/language)
        var olderGameId = Guid.NewGuid();
        var newerGameId = Guid.NewGuid();
        _db.SharedGames.Add(new SharedGameEntity
        {
            Id = olderGameId,
            Title = "Catan (EN base)",
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        });
        _db.SharedGames.Add(new SharedGameEntity
        {
            Id = newerGameId,
            Title = "Catan (IT expansion)",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        });

        // PDF on the older games row
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId, // now direct link via SharedGameId (games.GameId column dropped)
            FileName = "EnBaseRules.pdf",
            FilePath = "/tmp/en.pdf",
            FileSizeBytes = 100,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow.AddMinutes(-30),
            Language = "EN"
        });

        // PDF on the newer games row
        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "ItExpansionRules.pdf",
            FilePath = "/tmp/it.pdf",
            FileSizeBytes = 200,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            Language = "IT"
        });
        await _db.SaveChangesAsync();

        var query = new GetGamePdfsQuery(sharedGameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — both PDFs returned, ordered by UploadedAt desc
        result.Should().HaveCount(2);
        result.Select(p => p.Name).Should().BeEquivalentTo(
            ["ItExpansionRules", "EnBaseRules"],
            options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Handle_ReturnsPdfsOrderedByUploadDateDescending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        var olderPdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "OlderRules.pdf",
            FilePath = "/test/older.pdf",
            FileSizeBytes = 100_000,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow.AddDays(-1),
            Language = "EN"
        };
        var newerPdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "NewerRules.pdf",
            FilePath = "/test/newer.pdf",
            FileSizeBytes = 200_000,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            Language = "IT"
        };
        _db.PdfDocuments.AddRange(olderPdf, newerPdf);
        await _db.SaveChangesAsync();

        var query = new GetGamePdfsQuery(sharedGameId, userId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("NewerRules");
        result[1].Name.Should().Be("OlderRules");
    }

    // ---- Issue #1529: ProcessingStatus + ChunkCount coverage ----

    [Theory]
    [InlineData("Ready", "ready")]
    [InlineData("Failed", "failed")]
    [InlineData("Pending", "indexing")]
    [InlineData("Uploading", "indexing")]
    [InlineData("Extracting", "indexing")]
    [InlineData("Chunking", "indexing")]
    [InlineData("Embedding", "indexing")]
    [InlineData("Indexing", "indexing")]
    public async Task Handle_ProcessingStatus_MapsAllEnumValues(string processingState, string expectedStatus)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "Foo.pdf",
            FilePath = "/tmp/foo.pdf",
            FileSizeBytes = 100,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = processingState,
            Language = "EN"
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetGamePdfsQuery(sharedGameId, userId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].ProcessingStatus.Should().Be(expectedStatus,
            $"PdfProcessingState.{processingState} must map to the FE badge value '{expectedStatus}'");
        result[0].ProcessingState.Should().Be(processingState, "the raw pipeline state must remain on the DTO for backward compat");
    }

    [Fact]
    public async Task Handle_ProcessingStatus_UnknownState_FallsBackToIndexing()
    {
        // Regression guard: any future enum value the FE has not learned yet should
        // render as "indexing" (safe fallback) rather than null or the raw value.
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "Foo.pdf",
            FilePath = "/tmp/foo.pdf",
            FileSizeBytes = 100,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "FuturePipelineState",
            Language = "EN"
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetGamePdfsQuery(sharedGameId, userId), CancellationToken.None);

        // Assert
        result[0].ProcessingStatus.Should().Be("indexing");
    }

    [Fact]
    public async Task Handle_ChunkCount_IsZero_WhenNoChunksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            FileName = "Foo.pdf",
            FilePath = "/tmp/foo.pdf",
            FileSizeBytes = 100,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Pending"
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetGamePdfsQuery(sharedGameId, userId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].ChunkCount.Should().Be(0, "no TextChunkEntity rows seeded");
    }

    [Fact]
    public async Task Handle_ChunkCount_CountsTextChunksForEachPdfIndependently()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        var pdf1Id = Guid.NewGuid();
        var pdf2Id = Guid.NewGuid();

        _db.PdfDocuments.AddRange(
            new PdfDocumentEntity
            {
                Id = pdf1Id,
                SharedGameId = sharedGameId,
                FileName = "P1.pdf",
                FilePath = "/tmp/p1.pdf",
                FileSizeBytes = 100,
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow.AddMinutes(-1),
                ProcessingState = "Ready"
            },
            new PdfDocumentEntity
            {
                Id = pdf2Id,
                SharedGameId = sharedGameId,
                FileName = "P2.pdf",
                FilePath = "/tmp/p2.pdf",
                FileSizeBytes = 200,
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow,
                ProcessingState = "Ready"
            });

        // 3 chunks for pdf1, 2 chunks for pdf2
        for (int i = 0; i < 3; i++)
        {
            _db.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdf1Id,
                SharedGameId = sharedGameId,
                Content = $"chunk-p1-{i}",
                ChunkIndex = i,
                CharacterCount = 100
            });
        }
        for (int i = 0; i < 2; i++)
        {
            _db.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdf2Id,
                SharedGameId = sharedGameId,
                Content = $"chunk-p2-{i}",
                ChunkIndex = i,
                CharacterCount = 100
            });
        }
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetGamePdfsQuery(sharedGameId, userId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        // Ordering: UploadedAt desc → pdf2 first, pdf1 second
        var p2 = result[0];
        var p1 = result[1];
        p2.Name.Should().Be("P2");
        p2.ChunkCount.Should().Be(2);
        p1.Name.Should().Be("P1");
        p1.ChunkCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ChunkCount_IgnoresChunksForOtherPdfs()
    {
        // Regression guard: the per-PDF group-by must not bleed across PdfDocumentId.
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        var targetPdfId = Guid.NewGuid();
        var unrelatedPdfId = Guid.NewGuid();

        _db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = targetPdfId,
            SharedGameId = sharedGameId,
            FileName = "Target.pdf",
            FilePath = "/tmp/target.pdf",
            FileSizeBytes = 100,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready"
        });

        // 1 chunk for the target PDF
        _db.TextChunks.Add(new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = targetPdfId,
            SharedGameId = sharedGameId,
            Content = "target chunk",
            ChunkIndex = 0,
            CharacterCount = 100
        });

        // 5 chunks for an unrelated PDF (NOT in pdf_documents — orphaned for test isolation)
        for (int i = 0; i < 5; i++)
        {
            _db.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = unrelatedPdfId,
                SharedGameId = sharedGameId,
                Content = $"unrelated-{i}",
                ChunkIndex = i,
                CharacterCount = 100
            });
        }
        await _db.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetGamePdfsQuery(sharedGameId, userId), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].ChunkCount.Should().Be(1, "must not double-count unrelated PDFs sharing the same SharedGameId");
    }
}
