using Api.BoundedContexts.KnowledgeBase.Application.Queries.ExportDocumentChunks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for <see cref="ExportDocumentChunksQueryHandler"/>.
/// Validates full-content export ordered by ChunkIndex.
/// Issue #1653: F3-FU-4 — Export document chunks (full content).
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "1653")]
public sealed class ExportDocumentChunksQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private ExportDocumentChunksQueryHandler? _handler;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ExportDocumentChunksQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_export_chunks_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }

        _handler = new ExportDocumentChunksQueryHandler(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* best-effort cleanup */ }
        }
    }

    // ─── Seed helpers ────────────────────────────────────────────────────────

    private async Task<Guid> SeedDocWithChunksAsync()
    {
        var userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"export-chunks-{userId:N}@test.local",
            CreatedAt = DateTime.UtcNow
        });

        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Export Test Game",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(sharedGame);

        var pdfId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "export-test.pdf",
            FilePath = "/tmp/export-test.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id
        });

        _dbContext.TextChunks.AddRange(
            new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                Content = "A",
                ChunkIndex = 0,
                PageNumber = 1,
                Heading = null,
                CharacterCount = 1,
                CreatedAt = DateTime.UtcNow
            },
            new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                Content = "B",
                ChunkIndex = 1,
                PageNumber = 1,
                Heading = "Section B",
                CharacterCount = 1,
                CreatedAt = DateTime.UtcNow
            },
            new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = pdfId,
                Content = "C",
                ChunkIndex = 2,
                PageNumber = 2,
                Heading = null,
                CharacterCount = 1,
                CreatedAt = DateTime.UtcNow
            }
        );

        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return pdfId;
    }

    // ─── Tests ───────────────────────────────────────────────────────────────

    [Fact(Timeout = 30000)]
    public async Task Handle_ReturnsAllChunks_FullContent_OrderedByIndex()
    {
        var pdfId = await SeedDocWithChunksAsync();

        var result = await _handler!.Handle(
            new ExportDocumentChunksQuery(pdfId), TestCancellationToken);

        result.Should().HaveCount(3);
        result.Select(c => c.Content).Should().ContainInOrder("A", "B", "C");
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_ReturnsFullContent_NotTruncated()
    {
        var userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"fc-{userId:N}@test.local",
            CreatedAt = DateTime.UtcNow
        });

        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Full Content Game",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(sharedGame);

        var pdfId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "long-content.pdf",
            FilePath = "/tmp/long-content.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id
        });

        // Content that exceeds the 200-char snippet truncation used in GetKbChunksHandler
        var longContent = new string('X', 500);
        _dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            Content = longContent,
            ChunkIndex = 0,
            CharacterCount = longContent.Length,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var result = await _handler!.Handle(
            new ExportDocumentChunksQuery(pdfId), TestCancellationToken);

        result.Should().HaveCount(1);
        result[0].Content.Should().HaveLength(500);
        result[0].Content.Should().Be(longContent);
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_UnknownDocumentId_ReturnsEmptyList()
    {
        var result = await _handler!.Handle(
            new ExportDocumentChunksQuery(Guid.NewGuid()), TestCancellationToken);

        result.Should().BeEmpty();
    }

    [Fact(Timeout = 30000)]
    public async Task Handle_MapsAllFields_Correctly()
    {
        var userId = Guid.NewGuid();
        _dbContext!.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"fields-{userId:N}@test.local",
            CreatedAt = DateTime.UtcNow
        });

        var sharedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = "Fields Game",
            YearPublished = 2024,
            Description = string.Empty,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.SharedGames.Add(sharedGame);

        var pdfId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "fields.pdf",
            FilePath = "/tmp/fields.pdf",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id
        });

        var chunkId = Guid.NewGuid();
        _dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = chunkId,
            PdfDocumentId = pdfId,
            Content = "Hello World",
            ChunkIndex = 7,
            PageNumber = 3,
            Heading = "My Heading",
            CharacterCount = 11,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);

        var result = await _handler!.Handle(
            new ExportDocumentChunksQuery(pdfId), TestCancellationToken);

        result.Should().HaveCount(1);
        var chunk = result[0];
        chunk.Id.Should().Be(chunkId);
        chunk.ChunkIndex.Should().Be(7);
        chunk.PageNumber.Should().Be(3);
        chunk.Heading.Should().Be("My Heading");
        chunk.Content.Should().Be("Hello World");
    }
}
