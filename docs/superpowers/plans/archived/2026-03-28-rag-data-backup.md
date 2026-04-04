# RAG Data Backup & Production Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Export RAG data (PDFs, chunks, embeddings) as portable Parquet+JSON backup for production migration, with continuous event-driven + weekly scheduled backups.

**Architecture:** CQRS commands in Administration BC following existing `MigrateStorageCommand` pattern. `PgVectorEmbeddingEntity` for typed EF Core access to `pgvector_embeddings`. Backup storage reuses existing `S3_BACKUP_*` credentials from `storage.secret`. Parquet via `Parquet.Net` NuGet.

**Tech Stack:** .NET 9, EF Core + Pgvector.EntityFrameworkCore, Parquet.Net, MediatR, FluentValidation, xUnit

**Spec:** `docs/superpowers/specs/2026-03-28-rag-data-backup-design.md`

---

## Task 1: PgVectorEmbeddingEntity + EF Core Vector Mapping

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/PgVectorEmbeddingEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/PgVectorEmbeddingEntityConfiguration.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`
- Test: `tests/Api.Tests/Infrastructure/Entities/PgVectorEmbeddingEntityTests.cs`

- [ ] **Step 1: Write the test for entity mapping**

```csharp
// tests/Api.Tests/Infrastructure/Entities/PgVectorEmbeddingEntityTests.cs
using Api.Infrastructure.Entities.KnowledgeBase;
using Xunit;

namespace Api.Tests.Infrastructure.Entities;

public sealed class PgVectorEmbeddingEntityTests
{
    [Fact]
    public void Entity_HasAllRequiredProperties()
    {
        var entity = new PgVectorEmbeddingEntity
        {
            Id = Guid.NewGuid(),
            VectorDocumentId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            TextContent = "Sample chunk text",
            Model = "nomic-embed-text",
            ChunkIndex = 0,
            PageNumber = 1,
            Lang = "en",
            IsTranslation = false,
            SourceChunkId = null,
            CreatedAt = DateTime.UtcNow
        };

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("Sample chunk text", entity.TextContent);
        Assert.Equal("nomic-embed-text", entity.Model);
        Assert.Equal(0, entity.ChunkIndex);
        Assert.Equal(1, entity.PageNumber);
        Assert.Equal("en", entity.Lang);
        Assert.False(entity.IsTranslation);
    }

    [Fact]
    public void Entity_DefaultValues_AreCorrect()
    {
        var entity = new PgVectorEmbeddingEntity();

        Assert.Equal("en", entity.Lang);
        Assert.False(entity.IsTranslation);
        Assert.Null(entity.SourceChunkId);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~PgVectorEmbeddingEntityTests" -v minimal`
Expected: FAIL — `PgVectorEmbeddingEntity` type not found

- [ ] **Step 3: Create PgVectorEmbeddingEntity**

```csharp
// apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/PgVectorEmbeddingEntity.cs
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace Api.Infrastructure.Entities.KnowledgeBase;

/// <summary>
/// EF Core entity for the pgvector_embeddings table.
/// Read-only mapping for export/import operations — search queries use PgVectorStoreAdapter raw SQL.
/// Table is created/managed by PgVectorStoreAdapter.EnsureTableExistsAsync().
/// </summary>
[Table("pgvector_embeddings")]
public class PgVectorEmbeddingEntity
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("vector_document_id")]
    public Guid VectorDocumentId { get; set; }

    [Column("game_id")]
    public Guid GameId { get; set; }

    [Column("text_content")]
    public string TextContent { get; set; } = string.Empty;

    [Column("model")]
    public string Model { get; set; } = string.Empty;

    [Column("chunk_index")]
    public int ChunkIndex { get; set; }

    [Column("page_number")]
    public int PageNumber { get; set; }

    [Column("vector", TypeName = "vector(768)")]
    public Vector? Vector { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("lang")]
    public string Lang { get; set; } = "en";

    [Column("source_chunk_id")]
    public Guid? SourceChunkId { get; set; }

    [Column("is_translation")]
    public bool IsTranslation { get; set; }
}
```

- [ ] **Step 4: Create PgVectorEmbeddingEntityConfiguration**

```csharp
// apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/PgVectorEmbeddingEntityConfiguration.cs
using Api.Infrastructure.Entities.KnowledgeBase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.KnowledgeBase;

internal class PgVectorEmbeddingEntityConfiguration : IEntityTypeConfiguration<PgVectorEmbeddingEntity>
{
    public void Configure(EntityTypeBuilder<PgVectorEmbeddingEntity> builder)
    {
        builder.ToTable("pgvector_embeddings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.VectorDocumentId).IsRequired();
        builder.Property(e => e.GameId).IsRequired();
        builder.Property(e => e.TextContent).IsRequired();
        builder.Property(e => e.Model).IsRequired().HasMaxLength(128);
        builder.Property(e => e.ChunkIndex).IsRequired();
        builder.Property(e => e.PageNumber).IsRequired();
        builder.Property(e => e.Vector).HasColumnType("vector(768)");
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.Lang).IsRequired().HasMaxLength(5).HasDefaultValue("en");
        builder.Property(e => e.IsTranslation).IsRequired().HasDefaultValue(false);

        // Ignore the search_vector column — it's a generated column managed by PostgreSQL
        builder.Ignore("search_vector");

        builder.HasIndex(e => e.VectorDocumentId);
        builder.HasIndex(e => e.GameId);
    }
}
```

- [ ] **Step 5: Add DbSet to MeepleAiDbContext**

In `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`, add after the `TextChunks` DbSet (around line 82):

```csharp
public DbSet<PgVectorEmbeddingEntity> PgVectorEmbeddings => Set<PgVectorEmbeddingEntity>();
```

Also add the using at the top of the file:

```csharp
using Api.Infrastructure.Entities.KnowledgeBase;
```

And in the `OnModelCreating` method, the configuration will be auto-discovered by `ApplyConfigurationsFromAssembly`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~PgVectorEmbeddingEntityTests" -v minimal`
Expected: PASS (2 tests)

- [ ] **Step 7: Verify project builds**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/KnowledgeBase/PgVectorEmbeddingEntity.cs
git add apps/api/src/Api/Infrastructure/EntityConfigurations/KnowledgeBase/PgVectorEmbeddingEntityConfiguration.cs
git add apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs
git add tests/Api.Tests/Infrastructure/Entities/PgVectorEmbeddingEntityTests.cs
git commit -m "feat(kb): add PgVectorEmbeddingEntity with EF Core vector(768) mapping"
```

---

## Task 2: Backup Storage Service + Configuration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Services/IRagBackupStorageService.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Services/RagBackupStorageService.cs`
- Test: `tests/Api.Tests/BoundedContexts/Administration/Services/RagBackupStorageServiceTests.cs`

- [ ] **Step 1: Write the test for the storage service interface**

```csharp
// tests/Api.Tests/BoundedContexts/Administration/Services/RagBackupStorageServiceTests.cs
using Api.BoundedContexts.Administration.Application.Services;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Services;

public sealed class RagBackupStorageServiceTests
{
    [Fact]
    public void BuildSnapshotPath_WithTimestamp_ReturnsCorrectPath()
    {
        var timestamp = "2026-03-28T14-00-00Z";
        var path = RagBackupPathHelper.BuildSnapshotBasePath(timestamp);

        Assert.Equal("rag-exports/2026-03-28T14-00-00Z", path);
    }

    [Fact]
    public void BuildDocumentPath_ReturnsCorrectStructure()
    {
        var path = RagBackupPathHelper.BuildDocumentPath("latest", "ark-nova", Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.Equal("rag-exports/latest/games/ark-nova/11111111-1111-1111-1111-111111111111", path);
    }

    [Fact]
    public void Slugify_ConvertsNameToSlug()
    {
        Assert.Equal("ark-nova", RagBackupPathHelper.Slugify("Ark Nova"));
        Assert.Equal("7-wonders-duel", RagBackupPathHelper.Slugify("7 Wonders Duel"));
        Assert.Equal("twilight-imperium-4e", RagBackupPathHelper.Slugify("Twilight Imperium 4e"));
        Assert.Equal("catan", RagBackupPathHelper.Slugify("CATAN"));
    }

    [Fact]
    public void Slugify_HandlesSpecialCharacters()
    {
        Assert.Equal("kings-dilemma", RagBackupPathHelper.Slugify("King's Dilemma"));
        Assert.Equal("ticket-to-ride-europe", RagBackupPathHelper.Slugify("Ticket to Ride: Europe"));
        Assert.Equal("star-wars-rebellion", RagBackupPathHelper.Slugify("Star Wars™ Rebellion"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~RagBackupStorageServiceTests" -v minimal`
Expected: FAIL — types not found

- [ ] **Step 3: Create IRagBackupStorageService and RagBackupPathHelper**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Services/IRagBackupStorageService.cs
namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Storage service for RAG backup operations.
/// Handles S3 + local filesystem dual-write for backup data.
/// </summary>
internal interface IRagBackupStorageService
{
    /// <summary>Writes a file to the backup location (S3 + local).</summary>
    Task WriteFileAsync(string relativePath, byte[] content, CancellationToken ct = default);

    /// <summary>Writes a file to the backup location from a stream.</summary>
    Task WriteFileAsync(string relativePath, Stream content, CancellationToken ct = default);

    /// <summary>Reads a file from the backup location.</summary>
    Task<byte[]?> ReadFileAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Lists all snapshot IDs (timestamps or "latest").</summary>
    Task<List<string>> ListSnapshotsAsync(CancellationToken ct = default);

    /// <summary>Deletes a snapshot directory.</summary>
    Task DeleteSnapshotAsync(string snapshotId, CancellationToken ct = default);

    /// <summary>Gets a pre-signed download URL for a snapshot file.</summary>
    Task<string?> GetDownloadUrlAsync(string relativePath, int expirySeconds = 3600, CancellationToken ct = default);
}

/// <summary>
/// Path helpers for RAG backup file organization.
/// </summary>
internal static class RagBackupPathHelper
{
    public static string BuildSnapshotBasePath(string snapshotId)
        => $"rag-exports/{snapshotId}";

    public static string BuildDocumentPath(string snapshotId, string gameSlug, Guid pdfDocumentId)
        => $"rag-exports/{snapshotId}/games/{gameSlug}/{pdfDocumentId}";

    public static string Slugify(string name)
    {
        var slug = name.ToLowerInvariant();
        // Remove trademark symbols and apostrophes
        slug = slug.Replace("™", "").Replace("'", "").Replace("'", "");
        // Replace colons, commas, and other punctuation with nothing
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^\w\s-]", "");
        // Replace spaces and underscores with hyphens
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[\s_]+", "-");
        // Collapse multiple hyphens
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-{2,}", "-");
        // Trim leading/trailing hyphens
        return slug.Trim('-');
    }
}
```

- [ ] **Step 4: Create RagBackupStorageService**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Services/RagBackupStorageService.cs
using Amazon.S3;
using Amazon.S3.Model;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Dual-write backup storage: S3 bucket + local filesystem.
/// Uses S3_BACKUP_* credentials from storage.secret.
/// Falls back to local-only if S3 is not configured.
/// </summary>
internal sealed class RagBackupStorageService : IRagBackupStorageService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RagBackupStorageService> _logger;
    private readonly IAmazonS3? _s3Client;

    public RagBackupStorageService(
        IConfiguration configuration,
        ILogger<RagBackupStorageService> logger,
        IAmazonS3? s3Client = null)
    {
        _configuration = configuration;
        _logger = logger;
        _s3Client = s3Client;
    }

    private string LocalBasePath => _configuration["BACKUP_LOCAL_PATH"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "rag-exports");

    private string? BucketName => _configuration["S3_BACKUP_BUCKET_NAME"];
    private bool S3Enabled => _s3Client != null && !string.IsNullOrEmpty(BucketName);

    public async Task WriteFileAsync(string relativePath, byte[] content, CancellationToken ct = default)
    {
        // Always write locally
        var localPath = Path.Combine(LocalBasePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(localPath)!;
        Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(localPath, content, ct).ConfigureAwait(false);

        // Write to S3 if configured
        if (S3Enabled)
        {
            try
            {
                using var stream = new MemoryStream(content);
                await _s3Client!.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = BucketName,
                    Key = relativePath,
                    InputStream = stream,
                    ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                    DisablePayloadSigning = true
                }, ct).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "S3 backup write failed for {Path}, local copy preserved", relativePath);
            }
#pragma warning restore CA1031
        }
    }

    public async Task WriteFileAsync(string relativePath, Stream content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct).ConfigureAwait(false);
        await WriteFileAsync(relativePath, ms.ToArray(), ct).ConfigureAwait(false);
    }

    public async Task<byte[]?> ReadFileAsync(string relativePath, CancellationToken ct = default)
    {
        var localPath = Path.Combine(LocalBasePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localPath))
            return await File.ReadAllBytesAsync(localPath, ct).ConfigureAwait(false);

        if (S3Enabled)
        {
            try
            {
                var response = await _s3Client!.GetObjectAsync(BucketName, relativePath, ct).ConfigureAwait(false);
                using var ms = new MemoryStream();
                await response.ResponseStream.CopyToAsync(ms, ct).ConfigureAwait(false);
                return ms.ToArray();
            }
#pragma warning disable CA1031
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "S3 backup read failed for {Path}", relativePath);
            }
#pragma warning restore CA1031
        }

        return null;
    }

    public Task<List<string>> ListSnapshotsAsync(CancellationToken ct = default)
    {
        var snapshots = new List<string>();
        if (Directory.Exists(LocalBasePath))
        {
            foreach (var dir in Directory.GetDirectories(LocalBasePath))
            {
                var name = Path.GetFileName(dir);
                if (File.Exists(Path.Combine(dir, "manifest.json")))
                    snapshots.Add(name);
            }
        }
        snapshots.Sort();
        return Task.FromResult(snapshots);
    }

    public Task DeleteSnapshotAsync(string snapshotId, CancellationToken ct = default)
    {
        var localPath = Path.Combine(LocalBasePath, snapshotId);
        if (Directory.Exists(localPath))
            Directory.Delete(localPath, recursive: true);

        _logger.LogInformation("Deleted snapshot {SnapshotId}", snapshotId);
        return Task.CompletedTask;
    }

    public Task<string?> GetDownloadUrlAsync(string relativePath, int expirySeconds = 3600, CancellationToken ct = default)
    {
        if (!S3Enabled)
            return Task.FromResult<string?>(null);

        var url = _s3Client!.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = BucketName,
            Key = relativePath,
            Expires = DateTime.UtcNow.AddSeconds(expirySeconds)
        });
        return Task.FromResult<string?>(url);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~RagBackupStorageServiceTests" -v minimal`
Expected: PASS (6 tests)

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Services/IRagBackupStorageService.cs
git add apps/api/src/Api/BoundedContexts/Administration/Application/Services/RagBackupStorageService.cs
git add tests/Api.Tests/BoundedContexts/Administration/Services/RagBackupStorageServiceTests.cs
git commit -m "feat(admin): add RAG backup storage service with S3 + local dual-write"
```

---

## Task 3: RAG Export Service (Serialization)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Services/IRagExportService.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Services/RagExportService.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/RagExportDtos.cs`
- Test: `tests/Api.Tests/BoundedContexts/Administration/Services/RagExportServiceTests.cs`

- [ ] **Step 1: Write the DTOs**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/RagExportDtos.cs
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.Administration.Application.DTOs;

internal sealed record RagExportManifest
{
    [JsonPropertyName("exportVersion")]
    public string ExportVersion { get; init; } = "1.0";

    [JsonPropertyName("exportedAt")]
    public DateTime ExportedAt { get; init; }

    [JsonPropertyName("totalDocuments")]
    public int TotalDocuments { get; init; }

    [JsonPropertyName("totalChunks")]
    public int TotalChunks { get; init; }

    [JsonPropertyName("totalEmbeddings")]
    public int TotalEmbeddings { get; init; }

    [JsonPropertyName("embeddingModel")]
    public string EmbeddingModel { get; init; } = string.Empty;

    [JsonPropertyName("documents")]
    public List<RagExportManifestEntry> Documents { get; init; } = new();
}

internal sealed record RagExportManifestEntry
{
    [JsonPropertyName("pdfDocumentId")]
    public Guid PdfDocumentId { get; init; }

    [JsonPropertyName("gameSlug")]
    public string GameSlug { get; init; } = string.Empty;

    [JsonPropertyName("gameName")]
    public string GameName { get; init; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("chunks")]
    public int Chunks { get; init; }

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";
}

internal sealed record RagExportDocumentMetadata
{
    [JsonPropertyName("exportVersion")]
    public string ExportVersion { get; init; } = "1.0";

    [JsonPropertyName("exportedAt")]
    public DateTime ExportedAt { get; init; }

    [JsonPropertyName("embeddingModel")]
    public string EmbeddingModel { get; init; } = string.Empty;

    [JsonPropertyName("embeddingDimensions")]
    public int EmbeddingDimensions { get; init; }

    [JsonPropertyName("document")]
    public RagExportDocumentInfo Document { get; init; } = new();

    [JsonPropertyName("stats")]
    public RagExportDocumentStats Stats { get; init; } = new();
}

internal sealed record RagExportDocumentInfo
{
    [JsonPropertyName("pdfDocumentId")]
    public Guid PdfDocumentId { get; init; }

    [JsonPropertyName("gameId")]
    public Guid GameId { get; init; }

    [JsonPropertyName("gameSlug")]
    public string GameSlug { get; init; } = string.Empty;

    [JsonPropertyName("gameName")]
    public string GameName { get; init; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";

    [JsonPropertyName("languageConfidence")]
    public double? LanguageConfidence { get; init; }

    [JsonPropertyName("documentCategory")]
    public string? DocumentCategory { get; init; }

    [JsonPropertyName("versionLabel")]
    public string? VersionLabel { get; init; }

    [JsonPropertyName("licenseType")]
    public int? LicenseType { get; init; }

    [JsonPropertyName("pageCount")]
    public int? PageCount { get; init; }

    [JsonPropertyName("characterCount")]
    public int? CharacterCount { get; init; }

    [JsonPropertyName("contentHash")]
    public string? ContentHash { get; init; }

    [JsonPropertyName("fileSizeBytes")]
    public long? FileSizeBytes { get; init; }
}

internal sealed record RagExportDocumentStats
{
    [JsonPropertyName("totalChunks")]
    public int TotalChunks { get; init; }

    [JsonPropertyName("totalEmbeddings")]
    public int TotalEmbeddings { get; init; }

    [JsonPropertyName("chunkSizeAvg")]
    public int ChunkSizeAvg { get; init; }
}

internal sealed record RagExportChunkLine
{
    [JsonPropertyName("chunkIndex")]
    public int ChunkIndex { get; init; }

    [JsonPropertyName("pageNumber")]
    public int? PageNumber { get; init; }

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("characterCount")]
    public int CharacterCount { get; init; }
}

internal sealed record RagExportEmbeddingLine
{
    [JsonPropertyName("chunkIndex")]
    public int ChunkIndex { get; init; }

    [JsonPropertyName("pageNumber")]
    public int? PageNumber { get; init; }

    [JsonPropertyName("textContent")]
    public string TextContent { get; init; } = string.Empty;

    [JsonPropertyName("vector")]
    public float[] Vector { get; init; } = Array.Empty<float>();

    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;
}
```

- [ ] **Step 2: Write the test for serialization round-trip**

```csharp
// tests/Api.Tests/BoundedContexts/Administration/Services/RagExportServiceTests.cs
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Services;

public sealed class RagExportServiceTests
{
    [Fact]
    public void SerializeManifest_RoundTrips()
    {
        var manifest = new RagExportManifest
        {
            ExportedAt = new DateTime(2026, 3, 28, 14, 0, 0, DateTimeKind.Utc),
            TotalDocuments = 1,
            TotalChunks = 10,
            TotalEmbeddings = 10,
            EmbeddingModel = "nomic-embed-text",
            Documents = new List<RagExportManifestEntry>
            {
                new()
                {
                    PdfDocumentId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    GameSlug = "ark-nova",
                    GameName = "Ark Nova",
                    Path = "games/ark-nova/11111111-1111-1111-1111-111111111111/",
                    Chunks = 10,
                    Language = "en"
                }
            }
        };

        var json = JsonSerializer.Serialize(manifest);
        var deserialized = JsonSerializer.Deserialize<RagExportManifest>(json)!;

        Assert.Equal("1.0", deserialized.ExportVersion);
        Assert.Equal(1, deserialized.TotalDocuments);
        Assert.Equal("ark-nova", deserialized.Documents[0].GameSlug);
    }

    [Fact]
    public void SerializeChunkLine_ToJsonl_HasCorrectFormat()
    {
        var chunk = new RagExportChunkLine
        {
            ChunkIndex = 0,
            PageNumber = 1,
            Content = "Ark Nova is a game about building a zoo.",
            CharacterCount = 40
        };

        var json = JsonSerializer.Serialize(chunk);
        Assert.Contains("\"chunkIndex\":0", json);
        Assert.Contains("\"content\":\"Ark Nova is a game about building a zoo.\"", json);
    }

    [Fact]
    public void SerializeEmbeddingLine_PreservesVector()
    {
        var embedding = new RagExportEmbeddingLine
        {
            ChunkIndex = 0,
            PageNumber = 1,
            TextContent = "Sample text",
            Vector = new float[] { 0.1f, -0.2f, 0.3f },
            Model = "nomic-embed-text"
        };

        var json = JsonSerializer.Serialize(embedding);
        var deserialized = JsonSerializer.Deserialize<RagExportEmbeddingLine>(json)!;

        Assert.Equal(3, deserialized.Vector.Length);
        Assert.Equal(0.1f, deserialized.Vector[0], precision: 5);
        Assert.Equal(-0.2f, deserialized.Vector[1], precision: 5);
    }

    [Fact]
    public void BuildJsonlBytes_MultipleLines_ProducesValidJsonl()
    {
        var chunks = new[]
        {
            new RagExportChunkLine { ChunkIndex = 0, Content = "Line one", CharacterCount = 8 },
            new RagExportChunkLine { ChunkIndex = 1, Content = "Line two", CharacterCount = 8 }
        };

        var bytes = RagExportSerializer.ToJsonlBytes(chunks);
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.Contains("\"chunkIndex\":0", lines[0]);
        Assert.Contains("\"chunkIndex\":1", lines[1]);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~RagExportServiceTests" -v minimal`
Expected: FAIL — `RagExportSerializer` not found

- [ ] **Step 4: Create IRagExportService and RagExportSerializer**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Services/IRagExportService.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Serializes RAG data into export bundles (JSONL + Parquet).
/// </summary>
internal interface IRagExportService
{
    /// <summary>Serializes a single document bundle to the backup storage.</summary>
    Task ExportDocumentBundleAsync(
        string snapshotId,
        VectorDocumentEntity vectorDoc,
        PdfDocumentEntity pdfDoc,
        string gameName,
        List<TextChunkEntity> chunks,
        List<PgVectorEmbeddingEntity> embeddings,
        CancellationToken ct = default);

    /// <summary>Writes the manifest to the backup storage.</summary>
    Task WriteManifestAsync(string snapshotId, RagExportManifest manifest, CancellationToken ct = default);

    /// <summary>Reads a manifest from backup storage.</summary>
    Task<RagExportManifest?> ReadManifestAsync(string snapshotId, CancellationToken ct = default);
}

/// <summary>
/// Utility for serializing export data to JSONL bytes.
/// Parquet support will be added when Parquet.Net NuGet is installed.
/// </summary>
internal static class RagExportSerializer
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public static byte[] ToJsonlBytes<T>(IEnumerable<T> items)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, leaveOpen: true);
        foreach (var item in items)
        {
            writer.WriteLine(System.Text.Json.JsonSerializer.Serialize(item, JsonOptions));
        }
        writer.Flush();
        return ms.ToArray();
    }

    public static byte[] ToJsonBytes<T>(T item)
    {
        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        return System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(item, options);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~RagExportServiceTests" -v minimal`
Expected: PASS (4 tests)

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/RagExportDtos.cs
git add apps/api/src/Api/BoundedContexts/Administration/Application/Services/IRagExportService.cs
git add tests/Api.Tests/BoundedContexts/Administration/Services/RagExportServiceTests.cs
git commit -m "feat(admin): add RAG export DTOs and serialization service"
```

---

## Task 4: Add Parquet.Net + Parquet Serialization

**Files:**
- Modify: `apps/api/src/Api/Api.csproj`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Services/RagParquetSerializer.cs`
- Test: `tests/Api.Tests/BoundedContexts/Administration/Services/RagParquetSerializerTests.cs`

- [ ] **Step 1: Add Parquet.Net NuGet package**

Run: `cd apps/api/src/Api && dotnet add package Parquet.Net`

- [ ] **Step 2: Write the test for Parquet round-trip**

```csharp
// tests/Api.Tests/BoundedContexts/Administration/Services/RagParquetSerializerTests.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Services;

public sealed class RagParquetSerializerTests
{
    [Fact]
    public async Task ChunkRoundTrip_PreservesData()
    {
        var chunks = new List<RagExportChunkLine>
        {
            new() { ChunkIndex = 0, PageNumber = 1, Content = "Hello world", CharacterCount = 11 },
            new() { ChunkIndex = 1, PageNumber = 2, Content = "Second chunk", CharacterCount = 12 }
        };

        var parquetBytes = await RagParquetSerializer.SerializeChunksAsync(chunks);
        Assert.True(parquetBytes.Length > 0);

        var deserialized = await RagParquetSerializer.DeserializeChunksAsync(parquetBytes);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("Hello world", deserialized[0].Content);
        Assert.Equal(2, deserialized[1].PageNumber);
    }

    [Fact]
    public async Task EmbeddingRoundTrip_PreservesVectors()
    {
        var embeddings = new List<RagExportEmbeddingLine>
        {
            new()
            {
                ChunkIndex = 0,
                PageNumber = 1,
                TextContent = "Sample",
                Vector = new float[] { 0.1f, -0.2f, 0.3f },
                Model = "nomic-embed-text"
            }
        };

        var parquetBytes = await RagParquetSerializer.SerializeEmbeddingsAsync(embeddings);
        Assert.True(parquetBytes.Length > 0);

        var deserialized = await RagParquetSerializer.DeserializeEmbeddingsAsync(parquetBytes);
        Assert.Single(deserialized);
        Assert.Equal(3, deserialized[0].Vector.Length);
        Assert.Equal(0.1f, deserialized[0].Vector[0], precision: 5);
        Assert.Equal("nomic-embed-text", deserialized[0].Model);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~RagParquetSerializerTests" -v minimal`
Expected: FAIL — `RagParquetSerializer` not found

- [ ] **Step 4: Create RagParquetSerializer**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Services/RagParquetSerializer.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Parquet serialization for RAG export chunks and embeddings.
/// </summary>
internal static class RagParquetSerializer
{
    private static readonly ParquetSchema ChunkSchema = new(
        new DataField<int>("chunkIndex"),
        new DataField<int?>("pageNumber"),
        new DataField<string>("content"),
        new DataField<int>("characterCount"));

    private static readonly ParquetSchema EmbeddingSchema = new(
        new DataField<int>("chunkIndex"),
        new DataField<int?>("pageNumber"),
        new DataField<string>("textContent"),
        new DataField<byte[]>("vector"),  // float[] serialized as bytes
        new DataField<string>("model"));

    public static async Task<byte[]> SerializeChunksAsync(List<RagExportChunkLine> chunks)
    {
        using var ms = new MemoryStream();
        using (var writer = await ParquetWriter.CreateAsync(ChunkSchema, ms).ConfigureAwait(false))
        {
            using var group = writer.CreateRowGroup();
            await group.WriteColumnAsync(new DataColumn(ChunkSchema.DataFields[0], chunks.Select(c => c.ChunkIndex).ToArray())).ConfigureAwait(false);
            await group.WriteColumnAsync(new DataColumn(ChunkSchema.DataFields[1], chunks.Select(c => c.PageNumber).ToArray())).ConfigureAwait(false);
            await group.WriteColumnAsync(new DataColumn(ChunkSchema.DataFields[2], chunks.Select(c => c.Content).ToArray())).ConfigureAwait(false);
            await group.WriteColumnAsync(new DataColumn(ChunkSchema.DataFields[3], chunks.Select(c => c.CharacterCount).ToArray())).ConfigureAwait(false);
        }
        return ms.ToArray();
    }

    public static async Task<List<RagExportChunkLine>> DeserializeChunksAsync(byte[] parquetBytes)
    {
        using var ms = new MemoryStream(parquetBytes);
        using var reader = await ParquetReader.CreateAsync(ms).ConfigureAwait(false);
        using var group = await reader.ReadEntireRowGroupAsync().ConfigureAwait(false);

        var chunkIndexes = (int[])group[0].Data;
        var pageNumbers = (int?[])group[1].Data;
        var contents = (string[])group[2].Data;
        var charCounts = (int[])group[3].Data;

        var result = new List<RagExportChunkLine>(chunkIndexes.Length);
        for (int i = 0; i < chunkIndexes.Length; i++)
        {
            result.Add(new RagExportChunkLine
            {
                ChunkIndex = chunkIndexes[i],
                PageNumber = pageNumbers[i],
                Content = contents[i],
                CharacterCount = charCounts[i]
            });
        }
        return result;
    }

    public static async Task<byte[]> SerializeEmbeddingsAsync(List<RagExportEmbeddingLine> embeddings)
    {
        using var ms = new MemoryStream();
        using (var writer = await ParquetWriter.CreateAsync(EmbeddingSchema, ms).ConfigureAwait(false))
        {
            using var group = writer.CreateRowGroup();
            await group.WriteColumnAsync(new DataColumn(EmbeddingSchema.DataFields[0], embeddings.Select(e => e.ChunkIndex).ToArray())).ConfigureAwait(false);
            await group.WriteColumnAsync(new DataColumn(EmbeddingSchema.DataFields[1], embeddings.Select(e => e.PageNumber).ToArray())).ConfigureAwait(false);
            await group.WriteColumnAsync(new DataColumn(EmbeddingSchema.DataFields[2], embeddings.Select(e => e.TextContent).ToArray())).ConfigureAwait(false);
            // Store float[] as byte[] for portability
            await group.WriteColumnAsync(new DataColumn(EmbeddingSchema.DataFields[3],
                embeddings.Select(e => VectorToBytes(e.Vector)).ToArray())).ConfigureAwait(false);
            await group.WriteColumnAsync(new DataColumn(EmbeddingSchema.DataFields[4], embeddings.Select(e => e.Model).ToArray())).ConfigureAwait(false);
        }
        return ms.ToArray();
    }

    public static async Task<List<RagExportEmbeddingLine>> DeserializeEmbeddingsAsync(byte[] parquetBytes)
    {
        using var ms = new MemoryStream(parquetBytes);
        using var reader = await ParquetReader.CreateAsync(ms).ConfigureAwait(false);
        using var group = await reader.ReadEntireRowGroupAsync().ConfigureAwait(false);

        var chunkIndexes = (int[])group[0].Data;
        var pageNumbers = (int?[])group[1].Data;
        var textContents = (string[])group[2].Data;
        var vectorBytes = (byte[][])group[3].Data;
        var models = (string[])group[4].Data;

        var result = new List<RagExportEmbeddingLine>(chunkIndexes.Length);
        for (int i = 0; i < chunkIndexes.Length; i++)
        {
            result.Add(new RagExportEmbeddingLine
            {
                ChunkIndex = chunkIndexes[i],
                PageNumber = pageNumbers[i],
                TextContent = textContents[i],
                Vector = BytesToVector(vectorBytes[i]),
                Model = models[i]
            });
        }
        return result;
    }

    private static byte[] VectorToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToVector(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~RagParquetSerializerTests" -v minimal`
Expected: PASS (2 tests)

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Api.csproj
git add apps/api/src/Api/BoundedContexts/Administration/Application/Services/RagParquetSerializer.cs
git add tests/Api.Tests/BoundedContexts/Administration/Services/RagParquetSerializerTests.cs
git commit -m "feat(admin): add Parquet serialization for RAG export data"
```

---

## Task 5: RagExportService Implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Services/RagExportService.cs`
- Test: `tests/Api.Tests/BoundedContexts/Administration/Services/RagExportServiceImplTests.cs`

- [ ] **Step 1: Implement RagExportService**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Services/RagExportService.cs
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;

namespace Api.BoundedContexts.Administration.Application.Services;

internal sealed class RagExportService : IRagExportService
{
    private readonly IRagBackupStorageService _storage;
    private readonly ILogger<RagExportService> _logger;

    public RagExportService(
        IRagBackupStorageService storage,
        ILogger<RagExportService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public async Task ExportDocumentBundleAsync(
        string snapshotId,
        VectorDocumentEntity vectorDoc,
        PdfDocumentEntity pdfDoc,
        string gameName,
        List<TextChunkEntity> chunks,
        List<PgVectorEmbeddingEntity> embeddings,
        CancellationToken ct = default)
    {
        var gameSlug = RagBackupPathHelper.Slugify(gameName);
        var basePath = RagBackupPathHelper.BuildDocumentPath(snapshotId, gameSlug, pdfDoc.Id);

        // 1. Write metadata.json
        var metadata = BuildDocumentMetadata(vectorDoc, pdfDoc, gameName, gameSlug, chunks, embeddings);
        await _storage.WriteFileAsync($"{basePath}/metadata.json", RagExportSerializer.ToJsonBytes(metadata), ct).ConfigureAwait(false);

        // 2. Write chunks JSONL + Parquet
        var chunkLines = chunks.Select(c => new RagExportChunkLine
        {
            ChunkIndex = c.ChunkIndex,
            PageNumber = c.PageNumber,
            Content = c.Content,
            CharacterCount = c.CharacterCount
        }).ToList();

        await _storage.WriteFileAsync($"{basePath}/chunks.jsonl", RagExportSerializer.ToJsonlBytes(chunkLines), ct).ConfigureAwait(false);
        await _storage.WriteFileAsync($"{basePath}/chunks.parquet", await RagParquetSerializer.SerializeChunksAsync(chunkLines).ConfigureAwait(false), ct).ConfigureAwait(false);

        // 3. Write embeddings JSONL + Parquet
        var embeddingLines = embeddings.Select(e => new RagExportEmbeddingLine
        {
            ChunkIndex = e.ChunkIndex,
            PageNumber = e.PageNumber,
            TextContent = e.TextContent,
            Vector = e.Vector?.ToArray() ?? Array.Empty<float>(),
            Model = e.Model
        }).ToList();

        await _storage.WriteFileAsync($"{basePath}/embeddings.jsonl", RagExportSerializer.ToJsonlBytes(embeddingLines), ct).ConfigureAwait(false);
        await _storage.WriteFileAsync($"{basePath}/embeddings.parquet", await RagParquetSerializer.SerializeEmbeddingsAsync(embeddingLines).ConfigureAwait(false), ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Exported document bundle: {GameSlug}/{PdfId} — {Chunks} chunks, {Embeddings} embeddings",
            gameSlug, pdfDoc.Id, chunks.Count, embeddings.Count);
    }

    public async Task WriteManifestAsync(string snapshotId, RagExportManifest manifest, CancellationToken ct = default)
    {
        var path = $"{RagBackupPathHelper.BuildSnapshotBasePath(snapshotId)}/manifest.json";
        await _storage.WriteFileAsync(path, RagExportSerializer.ToJsonBytes(manifest), ct).ConfigureAwait(false);
    }

    public async Task<RagExportManifest?> ReadManifestAsync(string snapshotId, CancellationToken ct = default)
    {
        var path = $"{RagBackupPathHelper.BuildSnapshotBasePath(snapshotId)}/manifest.json";
        var bytes = await _storage.ReadFileAsync(path, ct).ConfigureAwait(false);
        if (bytes == null) return null;
        return JsonSerializer.Deserialize<RagExportManifest>(bytes);
    }

    private static RagExportDocumentMetadata BuildDocumentMetadata(
        VectorDocumentEntity vectorDoc,
        PdfDocumentEntity pdfDoc,
        string gameName,
        string gameSlug,
        List<TextChunkEntity> chunks,
        List<PgVectorEmbeddingEntity> embeddings)
    {
        return new RagExportDocumentMetadata
        {
            ExportedAt = DateTime.UtcNow,
            EmbeddingModel = vectorDoc.EmbeddingModel,
            EmbeddingDimensions = vectorDoc.EmbeddingDimensions,
            Document = new RagExportDocumentInfo
            {
                PdfDocumentId = pdfDoc.Id,
                GameId = pdfDoc.GameId ?? Guid.Empty,
                GameSlug = gameSlug,
                GameName = gameName,
                FileName = pdfDoc.FileName ?? string.Empty,
                Language = pdfDoc.Language ?? "en",
                LanguageConfidence = pdfDoc.LanguageConfidence,
                DocumentCategory = pdfDoc.DocumentCategory,
                VersionLabel = pdfDoc.VersionLabel,
                LicenseType = pdfDoc.LicenseType,
                PageCount = pdfDoc.PageCount,
                CharacterCount = pdfDoc.CharacterCount,
                ContentHash = pdfDoc.ContentHash,
                FileSizeBytes = pdfDoc.FileSizeBytes
            },
            Stats = new RagExportDocumentStats
            {
                TotalChunks = chunks.Count,
                TotalEmbeddings = embeddings.Count,
                ChunkSizeAvg = chunks.Count > 0 ? (int)chunks.Average(c => c.CharacterCount) : 0
            }
        };
    }
}
```

- [ ] **Step 2: Verify project builds**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Services/RagExportService.cs
git commit -m "feat(admin): implement RagExportService for document bundle export"
```

---

## Task 6: ExportRagDataCommand (Full Export)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ExportRagData/ExportRagDataCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ExportRagData/ExportRagDataCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ExportRagData/ExportRagDataCommandValidator.cs`
- Test: `tests/Api.Tests/BoundedContexts/Administration/Commands/ExportRagDataCommandValidatorTests.cs`

- [ ] **Step 1: Write the validator test**

```csharp
// tests/Api.Tests/BoundedContexts/Administration/Commands/ExportRagDataCommandValidatorTests.cs
using Api.BoundedContexts.Administration.Application.Commands.ExportRagData;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Commands;

public sealed class ExportRagDataCommandValidatorTests
{
    private readonly ExportRagDataCommandValidator _validator = new();

    [Fact]
    public void Valid_DefaultCommand_Passes()
    {
        var command = new ExportRagDataCommand();
        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Valid_WithGameIdFilter_Passes()
    {
        var command = new ExportRagDataCommand { GameIdFilter = Guid.NewGuid().ToString() };
        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_BadGameIdFormat_Fails()
    {
        var command = new ExportRagDataCommand { GameIdFilter = "not-a-guid" };
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ExportRagDataCommandValidatorTests" -v minimal`
Expected: FAIL — types not found

- [ ] **Step 3: Create ExportRagDataCommand**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ExportRagData/ExportRagDataCommand.cs
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.ExportRagData;

/// <summary>
/// Full export of all processed RAG data (chunks + embeddings) to backup storage.
/// </summary>
internal sealed record ExportRagDataCommand : IRequest<ExportRagDataResult>
{
    public bool IncludeSourcePdf { get; init; }
    public bool DryRun { get; init; }
    public string? GameIdFilter { get; init; }
}

internal sealed record ExportRagDataResult(
    int TotalDocuments,
    int TotalChunks,
    int TotalEmbeddings,
    int Skipped,
    int Failed,
    bool IsDryRun,
    string SnapshotId,
    List<string> Errors);
```

- [ ] **Step 4: Create ExportRagDataCommandValidator**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ExportRagData/ExportRagDataCommandValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.ExportRagData;

internal sealed class ExportRagDataCommandValidator : AbstractValidator<ExportRagDataCommand>
{
    public ExportRagDataCommandValidator()
    {
        When(x => x.GameIdFilter != null, () =>
        {
            RuleFor(x => x.GameIdFilter)
                .Must(id => Guid.TryParse(id, out _))
                .WithMessage("GameIdFilter must be a valid GUID");
        });
    }
}
```

- [ ] **Step 5: Create ExportRagDataCommandHandler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ExportRagData/ExportRagDataCommandHandler.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.ExportRagData;

internal sealed class ExportRagDataCommandHandler : IRequestHandler<ExportRagDataCommand, ExportRagDataResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IRagExportService _exportService;
    private readonly ILogger<ExportRagDataCommandHandler> _logger;

    public ExportRagDataCommandHandler(
        MeepleAiDbContext db,
        IRagExportService exportService,
        ILogger<ExportRagDataCommandHandler> logger)
    {
        _db = db;
        _exportService = exportService;
        _logger = logger;
    }

    public async Task<ExportRagDataResult> Handle(ExportRagDataCommand request, CancellationToken cancellationToken)
    {
        var snapshotId = DateTime.UtcNow.ToString("yyyy-MM-dd-HHmmss");
        var errors = new List<string>();
        var totalChunks = 0;
        var totalEmbeddings = 0;
        var skipped = 0;
        var failed = 0;

        // Query all completed vector documents
        var query = _db.VectorDocuments
            .Include(v => v.PdfDocument)
            .Include(v => v.Game)
            .Where(v => v.IndexingStatus == "completed");

        if (request.GameIdFilter != null && Guid.TryParse(request.GameIdFilter, out var gameId))
        {
            query = query.Where(v => v.GameId == gameId);
        }

        var vectorDocs = await query.ToListAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "RAG export {Mode}: found {Count} indexed documents",
            request.DryRun ? "DRY RUN" : "EXECUTE", vectorDocs.Count);

        if (request.DryRun)
        {
            // Count chunks and embeddings without exporting
            foreach (var doc in vectorDocs)
            {
                var chunkCount = await _db.TextChunks
                    .CountAsync(c => c.PdfDocumentId == doc.PdfDocumentId, cancellationToken)
                    .ConfigureAwait(false);
                var embeddingCount = await _db.PgVectorEmbeddings
                    .CountAsync(e => e.VectorDocumentId == doc.Id, cancellationToken)
                    .ConfigureAwait(false);
                totalChunks += chunkCount;
                totalEmbeddings += embeddingCount;
            }

            return new ExportRagDataResult(vectorDocs.Count, totalChunks, totalEmbeddings, 0, 0, true, snapshotId, errors);
        }

        var manifestEntries = new List<RagExportManifestEntry>();

        // Process in batches of 10
        foreach (var doc in vectorDocs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var gameName = doc.Game?.Name ?? "unknown-game";
                var gameSlug = RagBackupPathHelper.Slugify(gameName);

                var chunks = await _db.TextChunks
                    .Where(c => c.PdfDocumentId == doc.PdfDocumentId)
                    .OrderBy(c => c.ChunkIndex)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var embeddings = await _db.PgVectorEmbeddings
                    .Where(e => e.VectorDocumentId == doc.Id)
                    .OrderBy(e => e.ChunkIndex)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (chunks.Count == 0 && embeddings.Count == 0)
                {
                    skipped++;
                    continue;
                }

                await _exportService.ExportDocumentBundleAsync(
                    snapshotId, doc, doc.PdfDocument, gameName, chunks, embeddings, cancellationToken)
                    .ConfigureAwait(false);

                totalChunks += chunks.Count;
                totalEmbeddings += embeddings.Count;

                manifestEntries.Add(new RagExportManifestEntry
                {
                    PdfDocumentId = doc.PdfDocumentId,
                    GameSlug = gameSlug,
                    GameName = gameName,
                    Path = $"games/{gameSlug}/{doc.PdfDocumentId}/",
                    Chunks = chunks.Count,
                    Language = doc.PdfDocument?.Language ?? "en"
                });
            }
#pragma warning disable CA1031
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                errors.Add($"Error exporting doc {doc.PdfDocumentId}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to export document {PdfDocumentId}", doc.PdfDocumentId);
            }
#pragma warning restore CA1031
        }

        // Write manifest atomically (only after all documents exported)
        var manifest = new RagExportManifest
        {
            ExportedAt = DateTime.UtcNow,
            TotalDocuments = manifestEntries.Count,
            TotalChunks = totalChunks,
            TotalEmbeddings = totalEmbeddings,
            EmbeddingModel = vectorDocs.FirstOrDefault()?.EmbeddingModel ?? "nomic-embed-text",
            Documents = manifestEntries
        };

        await _exportService.WriteManifestAsync(snapshotId, manifest, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "RAG export complete: {Docs} docs, {Chunks} chunks, {Embeddings} embeddings, {Failed} failed",
            manifestEntries.Count, totalChunks, totalEmbeddings, failed);

        return new ExportRagDataResult(manifestEntries.Count, totalChunks, totalEmbeddings, skipped, failed, false, snapshotId, errors);
    }
}
```

- [ ] **Step 6: Run validator tests**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ExportRagDataCommandValidatorTests" -v minimal`
Expected: PASS (3 tests)

- [ ] **Step 7: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ExportRagData/
git add tests/Api.Tests/BoundedContexts/Administration/Commands/ExportRagDataCommandValidatorTests.cs
git commit -m "feat(admin): add ExportRagDataCommand for full RAG backup export"
```

---

## Task 7: IncrementalRagBackupCommand + Event Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/IncrementalRagBackup/IncrementalRagBackupCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/IncrementalRagBackup/IncrementalRagBackupCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/IncrementalRagBackup/IncrementalRagBackupCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/EventHandlers/RagBackupOnIndexedEventHandler.cs`
- Test: `tests/Api.Tests/BoundedContexts/Administration/Commands/IncrementalRagBackupCommandValidatorTests.cs`

- [ ] **Step 1: Write the validator test**

```csharp
// tests/Api.Tests/BoundedContexts/Administration/Commands/IncrementalRagBackupCommandValidatorTests.cs
using Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Commands;

public sealed class IncrementalRagBackupCommandValidatorTests
{
    private readonly IncrementalRagBackupCommandValidator _validator = new();

    [Fact]
    public void Valid_WithPdfDocumentId_Passes()
    {
        var command = new IncrementalRagBackupCommand(Guid.NewGuid());
        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_EmptyPdfDocumentId_Fails()
    {
        var command = new IncrementalRagBackupCommand(Guid.Empty);
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~IncrementalRagBackupCommandValidatorTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Create IncrementalRagBackupCommand**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/IncrementalRagBackup/IncrementalRagBackupCommand.cs
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;

/// <summary>
/// Incrementally backs up a single document to the "latest" snapshot.
/// Triggered automatically by VectorDocumentIndexedEvent.
/// </summary>
internal sealed record IncrementalRagBackupCommand(Guid PdfDocumentId) : IRequest<IncrementalRagBackupResult>;

internal sealed record IncrementalRagBackupResult(bool Success, string? Error = null);
```

- [ ] **Step 4: Create IncrementalRagBackupCommandValidator**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/IncrementalRagBackup/IncrementalRagBackupCommandValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;

internal sealed class IncrementalRagBackupCommandValidator : AbstractValidator<IncrementalRagBackupCommand>
{
    public IncrementalRagBackupCommandValidator()
    {
        RuleFor(x => x.PdfDocumentId)
            .NotEmpty().WithMessage("PdfDocumentId is required");
    }
}
```

- [ ] **Step 5: Create IncrementalRagBackupCommandHandler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/IncrementalRagBackup/IncrementalRagBackupCommandHandler.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;

internal sealed class IncrementalRagBackupCommandHandler
    : IRequestHandler<IncrementalRagBackupCommand, IncrementalRagBackupResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IRagExportService _exportService;
    private readonly ILogger<IncrementalRagBackupCommandHandler> _logger;

    public IncrementalRagBackupCommandHandler(
        MeepleAiDbContext db,
        IRagExportService exportService,
        ILogger<IncrementalRagBackupCommandHandler> logger)
    {
        _db = db;
        _exportService = exportService;
        _logger = logger;
    }

    public async Task<IncrementalRagBackupResult> Handle(
        IncrementalRagBackupCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var vectorDoc = await _db.VectorDocuments
                .Include(v => v.PdfDocument)
                .Include(v => v.Game)
                .FirstOrDefaultAsync(v => v.PdfDocumentId == request.PdfDocumentId, cancellationToken)
                .ConfigureAwait(false);

            if (vectorDoc == null)
                return new IncrementalRagBackupResult(false, $"VectorDocument not found for PDF {request.PdfDocumentId}");

            var chunks = await _db.TextChunks
                .Where(c => c.PdfDocumentId == request.PdfDocumentId)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var embeddings = await _db.PgVectorEmbeddings
                .Where(e => e.VectorDocumentId == vectorDoc.Id)
                .OrderBy(e => e.ChunkIndex)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var gameName = vectorDoc.Game?.Name ?? "unknown-game";

            await _exportService.ExportDocumentBundleAsync(
                "latest", vectorDoc, vectorDoc.PdfDocument, gameName, chunks, embeddings, cancellationToken)
                .ConfigureAwait(false);

            // Update manifest incrementally
            var manifest = await _exportService.ReadManifestAsync("latest", cancellationToken).ConfigureAwait(false)
                ?? new RagExportManifest { ExportedAt = DateTime.UtcNow, EmbeddingModel = vectorDoc.EmbeddingModel };

            var gameSlug = RagBackupPathHelper.Slugify(gameName);
            var existingEntry = manifest.Documents.FindIndex(d => d.PdfDocumentId == request.PdfDocumentId);

            var entry = new RagExportManifestEntry
            {
                PdfDocumentId = request.PdfDocumentId,
                GameSlug = gameSlug,
                GameName = gameName,
                Path = $"games/{gameSlug}/{request.PdfDocumentId}/",
                Chunks = chunks.Count,
                Language = vectorDoc.PdfDocument?.Language ?? "en"
            };

            if (existingEntry >= 0)
                manifest.Documents[existingEntry] = entry;
            else
                manifest.Documents.Add(entry);

            manifest = manifest with
            {
                ExportedAt = DateTime.UtcNow,
                TotalDocuments = manifest.Documents.Count,
                TotalChunks = manifest.Documents.Sum(d => d.Chunks),
                TotalEmbeddings = manifest.Documents.Sum(d => d.Chunks) // 1:1 with chunks
            };

            await _exportService.WriteManifestAsync("latest", manifest, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Incremental backup completed for PDF {PdfId}", request.PdfDocumentId);
            return new IncrementalRagBackupResult(true);
        }
#pragma warning disable CA1031
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Incremental backup failed for PDF {PdfId}", request.PdfDocumentId);
            return new IncrementalRagBackupResult(false, ex.Message);
        }
#pragma warning restore CA1031
    }
}
```

- [ ] **Step 6: Create RagBackupOnIndexedEventHandler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/EventHandlers/RagBackupOnIndexedEventHandler.cs
using Api.BoundedContexts.Administration.Application.Commands.IncrementalRagBackup;
using Api.SharedKernel.Application.IntegrationEvents;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Triggers incremental RAG backup when a document is indexed.
/// Listens to VectorDocumentReadyIntegrationEvent (cross-BC integration event).
/// Non-blocking: failures are logged but do not affect the indexing pipeline.
/// </summary>
internal sealed class RagBackupOnIndexedEventHandler
    : INotificationHandler<VectorDocumentReadyIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<RagBackupOnIndexedEventHandler> _logger;

    public RagBackupOnIndexedEventHandler(
        IMediator mediator,
        ILogger<RagBackupOnIndexedEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(
        VectorDocumentReadyIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "RAG backup triggered for indexed document. Game: {GameId}, Chunks: {ChunkCount}",
            notification.GameId, notification.ChunkCount);

        try
        {
            await _mediator.Send(
                new IncrementalRagBackupCommand(notification.PdfDocumentId),
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Non-blocking: backup failure must not disrupt the indexing pipeline
            _logger.LogWarning(ex,
                "RAG incremental backup failed for PDF {PdfId}. Weekly snapshot will recover.",
                notification.PdfDocumentId);
        }
#pragma warning restore CA1031
    }
}
```

**Note:** This handler needs the `VectorDocumentReadyIntegrationEvent` to have a `PdfDocumentId` property. Verify this exists:

Run: `grep -r "PdfDocumentId" apps/api/src/Api/SharedKernel/Application/IntegrationEvents/`

If `PdfDocumentId` is missing from the event, add it as part of this task.

- [ ] **Step 7: Run tests**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~IncrementalRagBackupCommandValidatorTests" -v minimal`
Expected: PASS (2 tests)

- [ ] **Step 8: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/IncrementalRagBackup/
git add apps/api/src/Api/BoundedContexts/Administration/Application/EventHandlers/RagBackupOnIndexedEventHandler.cs
git add tests/Api.Tests/BoundedContexts/Administration/Commands/IncrementalRagBackupCommandValidatorTests.cs
git commit -m "feat(admin): add incremental RAG backup command and event handler"
```

---

## Task 8: ImportRagDataCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImportRagData/ImportRagDataCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImportRagData/ImportRagDataCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImportRagData/ImportRagDataCommandValidator.cs`
- Test: `tests/Api.Tests/BoundedContexts/Administration/Commands/ImportRagDataCommandValidatorTests.cs`

- [ ] **Step 1: Write the validator test**

```csharp
// tests/Api.Tests/BoundedContexts/Administration/Commands/ImportRagDataCommandValidatorTests.cs
using Api.BoundedContexts.Administration.Application.Commands.ImportRagData;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Commands;

public sealed class ImportRagDataCommandValidatorTests
{
    private readonly ImportRagDataCommandValidator _validator = new();

    [Fact]
    public void Valid_WithSnapshotPath_Passes()
    {
        var command = new ImportRagDataCommand { SnapshotPath = "rag-exports/2026-03-28" };
        var result = _validator.Validate(command);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_EmptySnapshotPath_Fails()
    {
        var command = new ImportRagDataCommand { SnapshotPath = "" };
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_NullSnapshotPath_Fails()
    {
        var command = new ImportRagDataCommand { SnapshotPath = null! };
        var result = _validator.Validate(command);
        Assert.False(result.IsValid);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ImportRagDataCommandValidatorTests" -v minimal`
Expected: FAIL

- [ ] **Step 3: Create ImportRagDataCommand**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImportRagData/ImportRagDataCommand.cs
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands.ImportRagData;

/// <summary>
/// Imports RAG data from a backup snapshot into the current database.
/// Matches games by slug. Skips duplicates by content hash.
/// </summary>
internal sealed record ImportRagDataCommand : IRequest<ImportRagDataResult>
{
    public string SnapshotPath { get; init; } = string.Empty;
    public bool ReEmbed { get; init; }
}

internal sealed record ImportRagDataResult(
    int TotalDocuments,
    int Imported,
    int Skipped,
    int Failed,
    int ReEmbedded,
    List<string> Warnings,
    List<string> Errors);
```

- [ ] **Step 4: Create ImportRagDataCommandValidator**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImportRagData/ImportRagDataCommandValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.ImportRagData;

internal sealed class ImportRagDataCommandValidator : AbstractValidator<ImportRagDataCommand>
{
    public ImportRagDataCommandValidator()
    {
        RuleFor(x => x.SnapshotPath)
            .NotEmpty().WithMessage("SnapshotPath is required");
    }
}
```

- [ ] **Step 5: Create ImportRagDataCommandHandler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImportRagData/ImportRagDataCommandHandler.cs
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace Api.BoundedContexts.Administration.Application.Commands.ImportRagData;

internal sealed class ImportRagDataCommandHandler
    : IRequestHandler<ImportRagDataCommand, ImportRagDataResult>
{
    private readonly MeepleAiDbContext _db;
    private readonly IRagBackupStorageService _storage;
    private readonly ILogger<ImportRagDataCommandHandler> _logger;

    public ImportRagDataCommandHandler(
        MeepleAiDbContext db,
        IRagBackupStorageService storage,
        ILogger<ImportRagDataCommandHandler> logger)
    {
        _db = db;
        _storage = storage;
        _logger = logger;
    }

    public async Task<ImportRagDataResult> Handle(
        ImportRagDataCommand request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var imported = 0;
        var skipped = 0;
        var failed = 0;
        var reEmbedded = 0;

        // Read manifest
        var manifestBytes = await _storage.ReadFileAsync($"{request.SnapshotPath}/manifest.json", cancellationToken)
            .ConfigureAwait(false);

        if (manifestBytes == null)
        {
            errors.Add($"manifest.json not found at {request.SnapshotPath}");
            return new ImportRagDataResult(0, 0, 0, 1, 0, warnings, errors);
        }

        var manifest = JsonSerializer.Deserialize<RagExportManifest>(manifestBytes)!;

        _logger.LogInformation(
            "Import starting: {Docs} documents from snapshot {Path}",
            manifest.TotalDocuments, request.SnapshotPath);

        // Build game slug → GameId lookup
        var games = await _db.Set<GameEntity>().ToListAsync(cancellationToken).ConfigureAwait(false);
        var slugToGame = games.ToDictionary(
            g => RagBackupPathHelper.Slugify(g.Name),
            g => g,
            StringComparer.OrdinalIgnoreCase);

        foreach (var docEntry in manifest.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Match game by slug
                if (!slugToGame.TryGetValue(docEntry.GameSlug, out var game))
                {
                    warnings.Add($"Game not found for slug '{docEntry.GameSlug}', skipping {docEntry.PdfDocumentId}");
                    skipped++;
                    continue;
                }

                // Check for duplicate by reading metadata
                var metadataBytes = await _storage.ReadFileAsync(
                    $"{request.SnapshotPath}/{docEntry.Path}metadata.json", cancellationToken)
                    .ConfigureAwait(false);

                if (metadataBytes == null)
                {
                    errors.Add($"metadata.json missing for {docEntry.PdfDocumentId}");
                    failed++;
                    continue;
                }

                var metadata = JsonSerializer.Deserialize<RagExportDocumentMetadata>(metadataBytes)!;

                // Skip if content hash already exists
                if (metadata.Document.ContentHash != null)
                {
                    var exists = await _db.Set<PdfDocumentEntity>()
                        .AnyAsync(p => p.ContentHash == metadata.Document.ContentHash, cancellationToken)
                        .ConfigureAwait(false);

                    if (exists)
                    {
                        skipped++;
                        continue;
                    }
                }

                // Import chunks from Parquet
                var chunkParquetBytes = await _storage.ReadFileAsync(
                    $"{request.SnapshotPath}/{docEntry.Path}chunks.parquet", cancellationToken)
                    .ConfigureAwait(false);

                if (chunkParquetBytes == null)
                {
                    errors.Add($"chunks.parquet missing for {docEntry.PdfDocumentId}");
                    failed++;
                    continue;
                }

                var chunkLines = await RagParquetSerializer.DeserializeChunksAsync(chunkParquetBytes).ConfigureAwait(false);

                // Create PdfDocumentEntity
                var pdfDocId = Guid.NewGuid();
                var pdfDoc = new PdfDocumentEntity
                {
                    Id = pdfDocId,
                    GameId = game.Id,
                    FileName = metadata.Document.FileName,
                    Language = metadata.Document.Language,
                    LanguageConfidence = metadata.Document.LanguageConfidence,
                    DocumentCategory = metadata.Document.DocumentCategory,
                    VersionLabel = metadata.Document.VersionLabel,
                    LicenseType = metadata.Document.LicenseType,
                    PageCount = metadata.Document.PageCount,
                    CharacterCount = metadata.Document.CharacterCount,
                    ContentHash = metadata.Document.ContentHash,
                    FileSizeBytes = metadata.Document.FileSizeBytes ?? 0,
                    ProcessingState = "Ready",
                    IsActiveForRag = true,
                    UploadedAt = DateTime.UtcNow
                };
                _db.Set<PdfDocumentEntity>().Add(pdfDoc);

                // Create VectorDocumentEntity
                var vectorDocId = Guid.NewGuid();
                var vectorDoc = new VectorDocumentEntity
                {
                    Id = vectorDocId,
                    GameId = game.Id,
                    PdfDocumentId = pdfDocId,
                    ChunkCount = chunkLines.Count,
                    IndexingStatus = "completed",
                    IndexedAt = DateTime.UtcNow,
                    EmbeddingModel = metadata.EmbeddingModel,
                    EmbeddingDimensions = metadata.EmbeddingDimensions
                };
                _db.VectorDocuments.Add(vectorDoc);

                // Create TextChunkEntities
                foreach (var chunk in chunkLines)
                {
                    _db.TextChunks.Add(new TextChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        GameId = game.Id,
                        PdfDocumentId = pdfDocId,
                        Content = chunk.Content,
                        ChunkIndex = chunk.ChunkIndex,
                        PageNumber = chunk.PageNumber,
                        CharacterCount = chunk.CharacterCount
                    });
                }

                // Import embeddings (or re-embed)
                if (!request.ReEmbed)
                {
                    var embeddingParquetBytes = await _storage.ReadFileAsync(
                        $"{request.SnapshotPath}/{docEntry.Path}embeddings.parquet", cancellationToken)
                        .ConfigureAwait(false);

                    if (embeddingParquetBytes != null)
                    {
                        var embeddingLines = await RagParquetSerializer.DeserializeEmbeddingsAsync(embeddingParquetBytes)
                            .ConfigureAwait(false);

                        foreach (var emb in embeddingLines)
                        {
                            _db.PgVectorEmbeddings.Add(new PgVectorEmbeddingEntity
                            {
                                Id = Guid.NewGuid(),
                                VectorDocumentId = vectorDocId,
                                GameId = game.Id,
                                TextContent = emb.TextContent,
                                Model = emb.Model,
                                ChunkIndex = emb.ChunkIndex,
                                PageNumber = emb.PageNumber ?? 0,
                                Vector = new Vector(emb.Vector),
                                Lang = metadata.Document.Language ?? "en"
                            });
                        }
                    }
                }
                else
                {
                    reEmbedded++;
                    warnings.Add($"Re-embedding required for {docEntry.PdfDocumentId} — trigger reprocessing after import");
                }

                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                imported++;
            }
#pragma warning disable CA1031
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                errors.Add($"Import error for {docEntry.PdfDocumentId}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to import document {PdfId}", docEntry.PdfDocumentId);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation(
            "Import complete: {Imported} imported, {Skipped} skipped, {Failed} failed, {ReEmbed} need re-embedding",
            imported, skipped, failed, reEmbedded);

        return new ImportRagDataResult(manifest.TotalDocuments, imported, skipped, failed, reEmbedded, warnings, errors);
    }
}
```

- [ ] **Step 6: Run tests**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~ImportRagDataCommandValidatorTests" -v minimal`
Expected: PASS (3 tests)

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImportRagData/
git add tests/Api.Tests/BoundedContexts/Administration/Commands/ImportRagDataCommandValidatorTests.cs
git commit -m "feat(admin): add ImportRagDataCommand with slug matching and duplicate detection"
```

---

## Task 9: DownloadRagSnapshotQuery

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/RagBackup/DownloadRagSnapshotQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/RagBackup/DownloadRagSnapshotQueryHandler.cs`

- [ ] **Step 1: Create DownloadRagSnapshotQuery**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/RagBackup/DownloadRagSnapshotQuery.cs
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.RagBackup;

/// <summary>
/// Lists available snapshots or gets a download URL for a specific one.
/// </summary>
internal sealed record DownloadRagSnapshotQuery(
    string? SnapshotId = null,
    string? GameSlug = null
) : IRequest<DownloadRagSnapshotResult>;

internal sealed record DownloadRagSnapshotResult(
    List<SnapshotInfo>? Snapshots = null,
    string? DownloadUrl = null,
    string? Error = null);

internal sealed record SnapshotInfo(
    string Id,
    DateTime? ExportedAt,
    int? TotalDocuments,
    int? TotalChunks);
```

- [ ] **Step 2: Create DownloadRagSnapshotQueryHandler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/RagBackup/DownloadRagSnapshotQueryHandler.cs
using Api.BoundedContexts.Administration.Application.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.RagBackup;

internal sealed class DownloadRagSnapshotQueryHandler
    : IRequestHandler<DownloadRagSnapshotQuery, DownloadRagSnapshotResult>
{
    private readonly IRagBackupStorageService _storage;
    private readonly IRagExportService _exportService;
    private readonly ILogger<DownloadRagSnapshotQueryHandler> _logger;

    public DownloadRagSnapshotQueryHandler(
        IRagBackupStorageService storage,
        IRagExportService exportService,
        ILogger<DownloadRagSnapshotQueryHandler> logger)
    {
        _storage = storage;
        _exportService = exportService;
        _logger = logger;
    }

    public async Task<DownloadRagSnapshotResult> Handle(
        DownloadRagSnapshotQuery request, CancellationToken cancellationToken)
    {
        // List mode: return all snapshots
        if (request.SnapshotId == null)
        {
            var ids = await _storage.ListSnapshotsAsync(cancellationToken).ConfigureAwait(false);
            var snapshots = new List<SnapshotInfo>();

            foreach (var id in ids)
            {
                var manifest = await _exportService.ReadManifestAsync(id, cancellationToken).ConfigureAwait(false);
                snapshots.Add(new SnapshotInfo(
                    id,
                    manifest?.ExportedAt,
                    manifest?.TotalDocuments,
                    manifest?.TotalChunks));
            }

            return new DownloadRagSnapshotResult(Snapshots: snapshots);
        }

        // Download mode: get pre-signed URL
        var snapshotId = request.SnapshotId == "latest" ? "latest" : request.SnapshotId;
        var path = request.GameSlug != null
            ? $"rag-exports/{snapshotId}/games/{request.GameSlug}/"
            : $"rag-exports/{snapshotId}/manifest.json";

        var url = await _storage.GetDownloadUrlAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (url == null)
            return new DownloadRagSnapshotResult(Error: $"Snapshot '{snapshotId}' not found or S3 not configured");

        return new DownloadRagSnapshotResult(DownloadUrl: url);
    }
}
```

- [ ] **Step 3: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Queries/RagBackup/
git commit -m "feat(admin): add DownloadRagSnapshotQuery for listing and downloading backups"
```

---

## Task 10: Admin Endpoints + DI Registration

**Files:**
- Create: `apps/api/src/Api/Routing/AdminRagBackupEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs`

- [ ] **Step 1: Create AdminRagBackupEndpoints**

```csharp
// apps/api/src/Api/Routing/AdminRagBackupEndpoints.cs
using Api.BoundedContexts.Administration.Application.Commands.ExportRagData;
using Api.BoundedContexts.Administration.Application.Commands.ImportRagData;
using Api.BoundedContexts.Administration.Application.Queries.RagBackup;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoints for RAG data backup and restore.
/// </summary>
internal static class AdminRagBackupEndpoints
{
    public static void MapAdminRagBackupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/rag-backup")
            .WithTags("Admin - RAG Backup")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/export", ExportRagData)
            .WithName("ExportRagData")
            .WithSummary("Export all processed RAG data to backup storage")
            .WithDescription("Full export of chunks + embeddings in Parquet + JSONL format. " +
                "Use dryRun=true to preview. Optionally filter by gameId.");

        group.MapPost("/import", ImportRagData)
            .WithName("ImportRagData")
            .WithSummary("Import RAG data from a backup snapshot")
            .WithDescription("Imports chunks and embeddings from a snapshot. " +
                "Games matched by slug. Duplicates skipped by content hash. " +
                "Use reEmbed=true to skip embedding import and re-generate.");

        group.MapGet("/snapshots", ListSnapshots)
            .WithName("ListRagSnapshots")
            .WithSummary("List available RAG backup snapshots");

        group.MapGet("/snapshots/{id}", GetSnapshot)
            .WithName("GetRagSnapshot")
            .WithSummary("Get download URL for a specific snapshot");
    }

    private static async Task<IResult> ExportRagData(
        bool dryRun,
        Guid? gameId,
        bool includeSourcePdf,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExportRagDataCommand
        {
            DryRun = dryRun,
            GameIdFilter = gameId?.ToString(),
            IncludeSourcePdf = includeSourcePdf
        }, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> ImportRagData(
        string snapshotPath,
        bool reEmbed,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ImportRagDataCommand
        {
            SnapshotPath = snapshotPath,
            ReEmbed = reEmbed
        }, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListSnapshots(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DownloadRagSnapshotQuery(), cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSnapshot(
        string id,
        string? gameSlug,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DownloadRagSnapshotQuery(id, gameSlug), cancellationToken)
            .ConfigureAwait(false);

        if (result.Error != null)
            return Results.NotFound(result);

        return Results.Ok(result);
    }
}
```

- [ ] **Step 2: Register endpoint in Program.cs**

In `apps/api/src/Api/Program.cs`, add after the `MapAdminStorageMigrationEndpoints()` line (around line 721):

```csharp
v1Api.MapAdminRagBackupEndpoints();        // RAG data backup & import
```

- [ ] **Step 3: Register DI services**

Find the DI registration section in `Program.cs` (search for `AddScoped` or `AddSingleton` near service registrations) and add:

```csharp
builder.Services.AddScoped<IRagBackupStorageService, RagBackupStorageService>();
builder.Services.AddScoped<IRagExportService, RagExportService>();
```

Add the using at the top:
```csharp
using Api.BoundedContexts.Administration.Application.Services;
```

- [ ] **Step 4: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/AdminRagBackupEndpoints.cs
git add apps/api/src/Api/Program.cs
git commit -m "feat(admin): add RAG backup admin endpoints and DI registration"
```

---

## Task 11: RagBackupSchedulerService (Weekly Snapshot)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/BackgroundServices/RagBackupSchedulerService.cs`
- Modify: `apps/api/src/Api/Program.cs`

- [ ] **Step 1: Create RagBackupSchedulerService**

```csharp
// apps/api/src/Api/Infrastructure/BackgroundServices/RagBackupSchedulerService.cs
using Api.BoundedContexts.Administration.Application.Commands.ExportRagData;
using Api.BoundedContexts.Administration.Application.Services;
using MediatR;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Weekly full RAG backup scheduler.
/// Runs every Sunday at 03:00 UTC. Prunes snapshots older than retention period.
/// </summary>
internal sealed class RagBackupSchedulerService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RagBackupSchedulerService> _logger;

    public RagBackupSchedulerService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RagBackupSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RagBackupSchedulerService started. Weekly snapshot: Sunday 03:00 UTC");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false);

            if (!IsScheduledTime())
                continue;

#pragma warning disable CA1031
            try
            {
                await RunWeeklyBackupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Weekly RAG backup failed");
            }
#pragma warning restore CA1031
        }
    }

    private static bool IsScheduledTime()
    {
        var now = DateTime.UtcNow;
        // Sunday between 03:00 and 03:59 UTC
        return now.DayOfWeek == DayOfWeek.Sunday && now.Hour == 3;
    }

    private async Task RunWeeklyBackupAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting weekly RAG backup");

        await using var scope = _scopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var storage = scope.ServiceProvider.GetRequiredService<IRagBackupStorageService>();

        // Run full export
        var result = await mediator.Send(new ExportRagDataCommand(), ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Weekly backup complete: {Docs} documents, {Chunks} chunks. Snapshot: {Id}",
            result.TotalDocuments, result.TotalChunks, result.SnapshotId);

        // Prune old snapshots
        var retentionWeeks = int.TryParse(_configuration["BACKUP_RETENTION_WEEKS"], out var weeks) ? weeks : 4;
        await PruneOldSnapshotsAsync(storage, retentionWeeks, ct).ConfigureAwait(false);
    }

    private async Task PruneOldSnapshotsAsync(
        IRagBackupStorageService storage, int retentionWeeks, CancellationToken ct)
    {
        var snapshots = await storage.ListSnapshotsAsync(ct).ConfigureAwait(false);
        var cutoff = DateTime.UtcNow.AddDays(-retentionWeeks * 7);

        foreach (var id in snapshots)
        {
            // Skip "latest" — it's the incremental snapshot
            if (id == "latest") continue;

            // Parse snapshot ID as date (format: yyyy-MM-dd-HHmmss)
            if (DateTime.TryParseExact(id, "yyyy-MM-dd-HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var snapshotDate))
            {
                if (snapshotDate < cutoff)
                {
                    await storage.DeleteSnapshotAsync(id, ct).ConfigureAwait(false);
                    _logger.LogInformation("Pruned old snapshot: {Id}", id);
                }
            }
        }
    }
}
```

- [ ] **Step 2: Register in Program.cs**

Find the hosted services registration section and add:

```csharp
builder.Services.AddHostedService<RagBackupSchedulerService>();
```

Add the using:
```csharp
using Api.Infrastructure.BackgroundServices;
```

- [ ] **Step 3: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/BackgroundServices/RagBackupSchedulerService.cs
git add apps/api/src/Api/Program.cs
git commit -m "feat(admin): add weekly RAG backup scheduler with retention pruning"
```

---

## Task 12: Make Targets + Secret Template

**Files:**
- Modify: `infra/Makefile`

- [ ] **Step 1: Add Make targets**

In `infra/Makefile`, add after the `seed-games` target (around line 73):

```makefile
# === RAG Backup ===
rag-export: ## Full RAG export (one-shot for production go-live)
	@echo "📦 Running full RAG data export..."
	curl -s -X POST "http://localhost:8080/admin/rag-backup/export?dryRun=false&includeSourcePdf=false" | python3 -m json.tool

rag-export-dry: ## Preview RAG export without executing
	@echo "🔍 Previewing RAG data export (dry run)..."
	curl -s -X POST "http://localhost:8080/admin/rag-backup/export?dryRun=true&includeSourcePdf=false" | python3 -m json.tool

rag-backup-status: ## Status of latest RAG backup
	@echo "📊 Checking latest RAG backup status..."
	curl -s "http://localhost:8080/admin/rag-backup/snapshots/latest" | python3 -m json.tool

rag-snapshots: ## List all available RAG backup snapshots
	@echo "📋 Available RAG backup snapshots:"
	curl -s "http://localhost:8080/admin/rag-backup/snapshots" | python3 -m json.tool
```

- [ ] **Step 2: Verify make help shows new targets**

Run: `cd infra && make help | grep rag`
Expected: Shows all 4 rag targets with descriptions

- [ ] **Step 3: Commit**

```bash
git add infra/Makefile
git commit -m "feat(infra): add RAG backup make targets"
```

---

## Task 13: Final Build Verification + Run All Tests

- [ ] **Step 1: Full build**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded with 0 errors

- [ ] **Step 2: Run all new tests**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests --filter "FullyQualifiedName~PgVectorEmbedding|FullyQualifiedName~RagBackup|FullyQualifiedName~RagExport|FullyQualifiedName~RagParquet|FullyQualifiedName~ExportRagData|FullyQualifiedName~IncrementalRagBackup|FullyQualifiedName~ImportRagData" -v minimal`
Expected: All tests pass

- [ ] **Step 3: Run full test suite to check for regressions**

Run: `cd apps/api/src/Api && dotnet test`
Expected: No regressions

- [ ] **Step 4: Final commit if any fixups needed**

Only commit if test failures required fixes. Otherwise skip.
