# #2436 PR-B — PlayRecord Photo Upload + OCR (Backend) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Backend = .NET 9, CQRS (MediatR, endpoints use ONLY `IMediator.Send`), DDD context GameManagement.

**Goal:** Let a PlayRecord creator upload photos (scoreboard captures) — stored via `IBlobStorageService` with SHA256 dedup + thumbnail + opt-in OCR — exposed at `POST /api/v1/play-records/{recordId}/photos`.

**Architecture:** New `PlayRecordPhoto` child entity on the `PlayRecord` aggregate (mirrors `RecordPlayer`), persisted to a new `play_record_photos` table. A `UploadPlayRecordPhotoCommand` handler: creator-guard (ADR-066) → magic-byte validate → SHA256 dedup → `StoreAsync` (`BlobCategory.PlayRecordPhoto`) → thumbnail (ImageSharp 300px/q80) → opt-in OCR via existing `IPhotoPreprocessor` (smoldocling) → persist. Reuses `SessionAttachmentService` prior-art + `ImageFileValidator` + `CryptographyHelper`.

**Tech Stack:** EF Core (Postgres, snake_case), MediatR, FluentValidation, SixLabors.ImageSharp, xUnit + Moq + Testcontainers. Decisions: DEC-3 (wire OCR now via smoldocling); ADR-067 (storage P-A + SHA256 D-A) — **amend §OCR O-C→smoldocling**. Spec: `docs/superpowers/specs/2026-06-20-issue-2436-create-deferred-spec-panel.md`.

---

## File Structure

**Create:**
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PlayRecordPhoto.cs` — child entity (`Entity<Guid>`).
- `apps/api/src/Api/Infrastructure/Entities/GameManagement/PlayRecordPhotoEntity.cs` — persistence POCO.
- `apps/api/src/Api/Infrastructure/EntityConfigurations/GameManagement/PlayRecordPhotoEntityConfiguration.cs` — EF mapping.
- `apps/api/src/Api/Helpers/ImageThumbnailHelper.cs` — extracted shared thumbnail (ImageSharp 300/q80).
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UploadPlayRecordPhotoCommand.cs` — command + result record.
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Commands/PlayRecords/UploadPlayRecordPhotoCommandHandler.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Validators/PlayRecords/UploadPlayRecordPhotoCommandValidator.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/PlayRecordPhotoUploadedEvent.cs`
- Tests under `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/...` + `Integration/GameManagement/`.

**Modify:**
- `apps/api/src/Api/Services/Pdf/IBlobStorageService.cs` — add `BlobCategory.PlayRecordPhoto` + `ToS3Folder()` case.
- `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PlayRecord.cs` — `_photos` collection + `AddPhoto` + `RestorePhoto` + `Photos`.
- `apps/api/src/Api/Infrastructure/Entities/GameManagement/PlayRecordEntity.cs` — `Photos` nav.
- `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` — `DbSet<PlayRecordPhotoEntity> PlayRecordPhotos`.
- `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/PlayRecordRepository.cs` — Include + Added-fix + Map loops.
- `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Services/SessionAttachmentService.cs` — delegate its 2 static thumbnail methods to `ImageThumbnailHelper` (keeps its tests green).
- `apps/api/src/Api/Routing/PlayRecordEndpoints.cs` — `POST /play-records/{recordId}/photos`.
- `docs/for-claude/architecture/adr/adr-067-playrecord-photo-upload-pipeline.md` — amend OCR decision.

All `dotnet` commands run from `apps/api/src/Api`. Tests: `dotnet test apps/api/tests/Api.Tests --filter "<expr>"` from repo root. **Run `tasklist | grep testhost` and kill stragglers before test runs (pitfall #2593).**

---

### Task 1: Add `BlobCategory.PlayRecordPhoto`

**Files:** Modify `apps/api/src/Api/Services/Pdf/IBlobStorageService.cs`

- [ ] **Step 1: Add the enum value.** In the `BlobCategory` enum, after `PhotoBatch,` add:

```csharp
    /// <summary>Photos attached to a PlayRecord (scoreboard captures, party shots).
    /// Target prefix <c>play-record-photos/{playRecordId}/</c>.</summary>
    PlayRecordPhoto,
```

- [ ] **Step 2: Add the `ToS3Folder()` case.** In `BlobCategoryExtensions.ToS3Folder()`, before the `_ => throw` default, add:

```csharp
        BlobCategory.PlayRecordPhoto => "play-record-photos",
```

- [ ] **Step 3: Build to verify.** Run: `dotnet build apps/api/src/Api` → Expected: succeeds (omitting the switch case would throw `ArgumentOutOfRangeException` at runtime; adding it keeps the exhaustive switch valid).

- [ ] **Step 4: Commit.**

```bash
git add apps/api/src/Api/Services/Pdf/IBlobStorageService.cs
git commit -m "feat(play-records): #2436 PR-B add PlayRecordPhoto blob category"
```

---

### Task 2: `PlayRecordPhoto` domain child entity

**Files:** Create `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/PlayRecordPhoto.cs` + test `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/PlayRecordPhotoTests.cs`

Mirrors `RecordPlayer` (`Entity<Guid>`, internal ctor `(Guid id, Guid playRecordId, ...) : base(id)`, private setters, inline validation).

- [ ] **Step 1: Write the failing test.**

```csharp
using System;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2436")]
public class PlayRecordPhotoTests
{
    private static readonly Guid RecordId = Guid.NewGuid();

    [Fact]
    public void Constructor_ValidArgs_SetsProperties()
    {
        var id = Guid.NewGuid();
        var uploader = Guid.NewGuid();
        var at = new DateTime(2026, 6, 20, 18, 0, 0, DateTimeKind.Utc);

        var photo = new PlayRecordPhoto(id, RecordId, "blob/url.jpg", "blob/thumb.jpg",
            12345, "abc123", "10 - 8", 0.91, "caption", uploader, at);

        photo.Id.Should().Be(id);
        photo.PlayRecordId.Should().Be(RecordId);
        photo.BlobUrl.Should().Be("blob/url.jpg");
        photo.ThumbnailUrl.Should().Be("blob/thumb.jpg");
        photo.FileSizeBytes.Should().Be(12345);
        photo.Sha256Hash.Should().Be("abc123");
        photo.OcrText.Should().Be("10 - 8");
        photo.OcrConfidence.Should().Be(0.91);
        photo.Caption.Should().Be("caption");
        photo.UploadedByUserId.Should().Be(uploader);
        photo.UploadedAt.Should().Be(at);
    }

    [Fact]
    public void Constructor_EmptyPlayRecordId_Throws()
    {
        var act = () => new PlayRecordPhoto(Guid.NewGuid(), Guid.Empty, "u", null, 1, "h", null, null, null, Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_BlankBlobUrl_Throws()
    {
        var act = () => new PlayRecordPhoto(Guid.NewGuid(), RecordId, "  ", null, 1, "h", null, null, null, Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<System.ComponentModel.DataAnnotations.ValidationException>();
    }

    [Fact]
    public void Constructor_BlankSha_Throws()
    {
        var act = () => new PlayRecordPhoto(Guid.NewGuid(), RecordId, "u", null, 1, "  ", null, null, null, Guid.NewGuid(), DateTime.UtcNow);
        act.Should().Throw<System.ComponentModel.DataAnnotations.ValidationException>();
    }
}
```

- [ ] **Step 2: Run, verify FAIL.** `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~PlayRecordPhotoTests"` → FAIL (type missing).

- [ ] **Step 3: Implement.**

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using Api.SharedKernel.Domain;

namespace Api.BoundedContexts.GameManagement.Domain.Entities;

/// <summary>
/// A photo attached to a <see cref="PlayRecord"/> (scoreboard capture, party shot).
/// Child entity of the PlayRecord aggregate. #2436 PR-B (ADR-067).
/// </summary>
internal sealed class PlayRecordPhoto : Entity<Guid>
{
    public Guid PlayRecordId { get; private set; }
    public string BlobUrl { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public long FileSizeBytes { get; private set; }
    public string Sha256Hash { get; private set; }
    public string? OcrText { get; private set; }
    public double? OcrConfidence { get; private set; }
    public string? Caption { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public DateTime UploadedAt { get; private set; }

#pragma warning disable CS8618
    private PlayRecordPhoto() : base() { }
#pragma warning restore CS8618

    internal PlayRecordPhoto(
        Guid id,
        Guid playRecordId,
        string blobUrl,
        string? thumbnailUrl,
        long fileSizeBytes,
        string sha256Hash,
        string? ocrText,
        double? ocrConfidence,
        string? caption,
        Guid uploadedByUserId,
        DateTime uploadedAt) : base(id)
    {
        if (playRecordId == Guid.Empty)
            throw new ArgumentException("PlayRecordId cannot be empty", nameof(playRecordId));
        if (string.IsNullOrWhiteSpace(blobUrl))
            throw new ValidationException("BlobUrl cannot be empty");
        if (string.IsNullOrWhiteSpace(sha256Hash))
            throw new ValidationException("Sha256Hash cannot be empty");
        if (caption is { Length: > 500 })
            throw new ValidationException("Caption cannot exceed 500 characters");

        PlayRecordId = playRecordId;
        BlobUrl = blobUrl;
        ThumbnailUrl = thumbnailUrl;
        FileSizeBytes = fileSizeBytes;
        Sha256Hash = sha256Hash;
        OcrText = ocrText;
        OcrConfidence = ocrConfidence;
        Caption = caption?.Trim();
        UploadedByUserId = uploadedByUserId;
        UploadedAt = uploadedAt;
    }
}
```

> Verify `Entity<Guid>` namespace matches `RecordPlayer.cs` (`using Api.SharedKernel.Domain;`). If `RecordPlayer` uses a different base namespace, mirror it exactly.

- [ ] **Step 4: Run, verify PASS.** Same filter → PASS (4 tests).

- [ ] **Step 5: Commit.** `git commit -m "feat(play-records): #2436 PR-B PlayRecordPhoto domain entity"`

---

### Task 3: Aggregate — `_photos` collection + `AddPhoto` + `RestorePhoto`

**Files:** Modify `PlayRecord.cs` + test `apps/api/tests/Api.Tests/BoundedContexts/GameManagement/Domain/PlayRecordAddPhotoTests.cs`

- [ ] **Step 1: Write failing test.**

```csharp
using System;
using System.Linq;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2436")]
public class PlayRecordAddPhotoTests
{
    private static PlayRecord NewRecord(Guid creator) =>
        PlayRecord.CreateFreeForm(Guid.NewGuid(), "Catan", creator, DateTime.UtcNow.AddDays(-1),
            PlayRecordVisibility.Private, SessionScoringConfig.CreateDefault());

    [Fact]
    public void AddPhoto_AppendsPhotoAndRaisesEvent()
    {
        var creator = Guid.NewGuid();
        var record = NewRecord(creator);
        record.ClearDomainEvents();
        var photoId = Guid.NewGuid();

        record.AddPhoto(photoId, "blob/u.jpg", "blob/t.jpg", 100, "sha", "ocr", 0.9, "cap", creator);

        record.Photos.Should().HaveCount(1);
        record.Photos[0].Id.Should().Be(photoId);
        record.DomainEvents.OfType<PlayRecordPhotoUploadedEvent>().Should().HaveCount(1);
    }

    [Fact]
    public void AddPhoto_Eleventh_Throws()
    {
        var creator = Guid.NewGuid();
        var record = NewRecord(creator);
        for (var i = 0; i < 10; i++)
            record.AddPhoto(Guid.NewGuid(), $"u{i}", null, 1, $"sha{i}", null, null, null, creator);

        var act = () => record.AddPhoto(Guid.NewGuid(), "u", null, 1, "shaX", null, null, null, creator);
        act.Should().Throw<Api.SharedKernel.Domain.Exceptions.DomainException>();
    }

    [Fact]
    public void RestorePhoto_AppendsWithoutEvent()
    {
        var record = NewRecord(Guid.NewGuid());
        record.ClearDomainEvents();

        record.RestorePhoto(Guid.NewGuid(), "u", null, 1, "sha", null, null, null, Guid.NewGuid(),
            DateTime.UtcNow);

        record.Photos.Should().HaveCount(1);
        record.DomainEvents.Should().BeEmpty();
    }
}
```

> Confirm `ClearDomainEvents()`/`DomainEvents` exist on the aggregate base (used by `RecordPlayer` tests). If named differently, mirror the existing test idiom. Confirm `DomainException` namespace from `PlayRecord.cs` (`AddPlayer` throws it for the 100-cap).

- [ ] **Step 2: Run, verify FAIL.**

- [ ] **Step 3: Implement in `PlayRecord.cs`.** Add beside `_players`:

```csharp
    private readonly List<PlayRecordPhoto> _photos = new();
    public IReadOnlyList<PlayRecordPhoto> Photos => _photos.AsReadOnly();
```

Add the mutator (mirrors `AddPlayer`, max 10 per ADR-067):

```csharp
    /// <summary>Attach an uploaded photo. Max 10 per record (#2436 PR-B). Raises a domain event.</summary>
    public void AddPhoto(
        Guid photoId, string blobUrl, string? thumbnailUrl, long fileSizeBytes,
        string sha256Hash, string? ocrText, double? ocrConfidence, string? caption,
        Guid uploadedByUserId, TimeProvider? timeProvider = null)
    {
        if (_photos.Count >= 10)
            throw new DomainException("Cannot attach more than 10 photos to a play record");

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var photo = new PlayRecordPhoto(photoId, Id, blobUrl, thumbnailUrl, fileSizeBytes,
            sha256Hash, ocrText, ocrConfidence, caption, uploadedByUserId, now);
        _photos.Add(photo);
        UpdatedAt = now;
        AddDomainEvent(new PlayRecordPhotoUploadedEvent(Id, photo.Id, uploadedByUserId));
    }

    /// <summary>Repository-only reconstitution from persistence (no event).</summary>
    internal void RestorePhoto(
        Guid photoId, string blobUrl, string? thumbnailUrl, long fileSizeBytes,
        string sha256Hash, string? ocrText, double? ocrConfidence, string? caption,
        Guid uploadedByUserId, DateTime uploadedAt)
    {
        _photos.Add(new PlayRecordPhoto(photoId, Id, blobUrl, thumbnailUrl, fileSizeBytes,
            sha256Hash, ocrText, ocrConfidence, caption, uploadedByUserId, uploadedAt));
    }
```

- [ ] **Step 4: Run, verify PASS.**

- [ ] **Step 5: Commit.** `git commit -m "feat(play-records): #2436 PR-B aggregate AddPhoto/RestorePhoto (max 10)"`

---

### Task 4: `PlayRecordPhotoUploadedEvent`

**Files:** Create `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Events/PlayRecordPhotoUploadedEvent.cs`

- [ ] **Step 1: Implement** (mirror `PlayerAddedToRecordEvent` shape — find it in the Events folder and match its base interface, e.g. `IDomainEvent`):

```csharp
using System;
using Api.SharedKernel.Domain;

namespace Api.BoundedContexts.GameManagement.Domain.Events;

internal sealed record PlayRecordPhotoUploadedEvent(
    Guid PlayRecordId,
    Guid PhotoId,
    Guid UploadedByUserId) : IDomainEvent;
```

> Match the EXACT base type/interface of the existing `PlayerAddedToRecordEvent` (could be `DomainEvent` base record or `IDomainEvent`). Read it first and mirror. This task is folded into Task 3's first build — if Task 3's test references the event, create this file in Task 3. (Commit together with Task 3 if so.)

- [ ] **Step 2: Build.** `dotnet build apps/api/src/Api` → succeeds.

---

### Task 5: Persistence entity + DbContext + EF config

**Files:** Create `PlayRecordPhotoEntity.cs` + `PlayRecordPhotoEntityConfiguration.cs`; modify `PlayRecordEntity.cs` + `MeepleAiDbContext.cs`

- [ ] **Step 1: `PlayRecordPhotoEntity.cs`** (POCO, mirror `RecordPlayerEntity`):

```csharp
using System;

namespace Api.Infrastructure.Entities.GameManagement;

public class PlayRecordPhotoEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayRecordId { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = string.Empty;
    public string? OcrText { get; set; }
    public double? OcrConfidence { get; set; }
    public string? Caption { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; }

    public PlayRecordEntity? PlayRecord { get; set; }
}
```

- [ ] **Step 2: Add nav to `PlayRecordEntity.cs`** beside `Players`:

```csharp
    public ICollection<PlayRecordPhotoEntity> Photos { get; set; } = new List<PlayRecordPhotoEntity>();
```

- [ ] **Step 3: `PlayRecordPhotoEntityConfiguration.cs`** (mirror `RecordPlayerEntityConfiguration`; snake_case table + UNIQUE index on `(PlayRecordId, Sha256Hash)`):

```csharp
using Api.Infrastructure.Entities.GameManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.GameManagement;

internal class PlayRecordPhotoEntityConfiguration : IEntityTypeConfiguration<PlayRecordPhotoEntity>
{
    public void Configure(EntityTypeBuilder<PlayRecordPhotoEntity> builder)
    {
        builder.ToTable("play_record_photos");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PlayRecordId).HasColumnName("play_record_id").IsRequired();
        builder.Property(e => e.BlobUrl).HasColumnName("blob_url").HasMaxLength(1024).IsRequired();
        builder.Property(e => e.ThumbnailUrl).HasColumnName("thumbnail_url").HasMaxLength(1024);
        builder.Property(e => e.FileSizeBytes).HasColumnName("file_size_bytes").IsRequired();
        builder.Property(e => e.Sha256Hash).HasColumnName("sha256_hash").HasMaxLength(64).IsRequired();
        builder.Property(e => e.OcrText).HasColumnName("ocr_text");
        builder.Property(e => e.OcrConfidence).HasColumnName("ocr_confidence");
        builder.Property(e => e.Caption).HasColumnName("caption").HasMaxLength(500);
        builder.Property(e => e.UploadedByUserId).HasColumnName("uploaded_by_user_id").IsRequired();
        builder.Property(e => e.UploadedAt).HasColumnName("uploaded_at").IsRequired();

        builder.HasOne(e => e.PlayRecord)
            .WithMany(r => r.Photos)
            .HasForeignKey(e => e.PlayRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.PlayRecordId, e.Sha256Hash })
            .IsUnique()
            .HasDatabaseName("UX_play_record_photos_playrecord_sha256");
    }
}
```

- [ ] **Step 4: DbSet in `MeepleAiDbContext.cs`** beside `RecordPlayers`:

```csharp
    public DbSet<PlayRecordPhotoEntity> PlayRecordPhotos => Set<PlayRecordPhotoEntity>();
```

- [ ] **Step 5: Build.** `dotnet build apps/api/src/Api` → succeeds.

- [ ] **Step 6: Commit.** `git commit -m "feat(play-records): #2436 PR-B play_record_photos persistence + config"`

---

### Task 6: Repository wiring (Include + Added-fix + Map loops)

**Files:** Modify `PlayRecordRepository.cs`

- [ ] **Step 1: Add `.Include(r => r.Photos)`** to EVERY load query (the 4 sites that have `.Include(r => r.Players)`), e.g.:

```csharp
        .Include(r => r.Players)
            .ThenInclude(p => p.Scores)
        .Include(r => r.Photos)
```

- [ ] **Step 2: Extend the detached-graph Added-fix in `UpdateAsync`.** After the existing player/score `existing*IdSet` blocks, add photos:

```csharp
        var existingPhotoIds = await DbContext.PlayRecordPhotos
            .Where(p => p.PlayRecordId == record.Id).Select(p => p.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var existingPhotoIdSet = new HashSet<Guid>(existingPhotoIds);
```

And inside the post-`Update(entity)` loop region:

```csharp
        foreach (var photo in entity.Photos)
            if (!existingPhotoIdSet.Contains(photo.Id))
                DbContext.Entry(photo).State = EntityState.Added;
```

- [ ] **Step 3: MapToDomain restore loop.** After the players restore loop, add:

```csharp
        foreach (var photoEntity in entity.Photos)
        {
            record.RestorePhoto(
                photoEntity.Id, photoEntity.BlobUrl, photoEntity.ThumbnailUrl,
                photoEntity.FileSizeBytes, photoEntity.Sha256Hash, photoEntity.OcrText,
                photoEntity.OcrConfidence, photoEntity.Caption, photoEntity.UploadedByUserId,
                photoEntity.UploadedAt);
        }
```

- [ ] **Step 4: MapToPersistence loop.** After the players persistence loop, add:

```csharp
        foreach (var photo in record.Photos)
        {
            entity.Photos.Add(new PlayRecordPhotoEntity
            {
                Id = photo.Id,
                PlayRecordId = record.Id,
                BlobUrl = photo.BlobUrl,
                ThumbnailUrl = photo.ThumbnailUrl,
                FileSizeBytes = photo.FileSizeBytes,
                Sha256Hash = photo.Sha256Hash,
                OcrText = photo.OcrText,
                OcrConfidence = photo.OcrConfidence,
                Caption = photo.Caption,
                UploadedByUserId = photo.UploadedByUserId,
                UploadedAt = photo.UploadedAt,
            });
        }
```

- [ ] **Step 5: Build.** `dotnet build apps/api/src/Api` → succeeds. (Round-trip is verified by the Task 12 integration test.)

- [ ] **Step 6: Commit.** `git commit -m "feat(play-records): #2436 PR-B repository photo include + graph mapping"`

---

### Task 7: EF migration

**Files:** Generates `apps/api/src/Api/Infrastructure/Migrations/<timestamp>_AddPlayRecordPhotos.cs`

- [ ] **Step 1: Generate.** From `apps/api/src/Api`: `dotnet ef migrations add AddPlayRecordPhotos`
- [ ] **Step 2: Review the generated migration.** Confirm: `CreateTable("play_record_photos")` with snake_case columns; FK to `play_records` PK (whatever its actual column name is — EF resolves it); `CreateIndex("UX_play_record_photos_playrecord_sha256", unique: true)`. No unintended changes to other tables (if the diff includes unrelated changes, STOP and report — the model may have drifted).
- [ ] **Step 3: Apply to a scratch DB to verify it runs** (optional if Docker available): `dotnet ef database update` against a dev DB, or rely on the Task 12 integration test's `MigrateAsync`.
- [ ] **Step 4: Commit.** `git commit -m "feat(play-records): #2436 PR-B migration play_record_photos table"`

---

### Task 8: Extract `ImageThumbnailHelper` (shared, keeps SessionAttachmentService tests green)

**Files:** Create `apps/api/src/Api/Helpers/ImageThumbnailHelper.cs`; modify `SessionAttachmentService.cs`

- [ ] **Step 1: Create the helper** (copy the two `internal static` methods verbatim from `SessionAttachmentService`):

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Api.Helpers;

/// <summary>Shared image thumbnail generation (300px max, JPEG q80). #2436 PR-B.</summary>
internal static class ImageThumbnailHelper
{
    private const int ThumbnailMaxDimension = 300;
    private const int ThumbnailJpegQuality = 80;

    public static async Task<MemoryStream?> GenerateThumbnailAsync(Stream sourceStream, CancellationToken ct = default)
    {
        using var image = await Image.LoadAsync(sourceStream, ct).ConfigureAwait(false);
        var (newWidth, newHeight) = CalculateThumbnailDimensions(image.Width, image.Height);
        image.Mutate(ctx => ctx.Resize(newWidth, newHeight));
        var outputStream = new MemoryStream();
        await image.SaveAsync(outputStream, new JpegEncoder { Quality = ThumbnailJpegQuality }, ct).ConfigureAwait(false);
        outputStream.Position = 0;
        return outputStream;
    }

    public static (int width, int height) CalculateThumbnailDimensions(int originalWidth, int originalHeight)
    {
        if (originalWidth <= ThumbnailMaxDimension && originalHeight <= ThumbnailMaxDimension)
            return (originalWidth, originalHeight);
        var ratio = originalWidth >= originalHeight
            ? (double)ThumbnailMaxDimension / originalWidth
            : (double)ThumbnailMaxDimension / originalHeight;
        return (Math.Max(1, (int)(originalWidth * ratio)), Math.Max(1, (int)(originalHeight * ratio)));
    }
}
```

- [ ] **Step 2: Delegate in `SessionAttachmentService.cs`.** Replace the BODIES of its `GenerateThumbnailAsync` and `CalculateThumbnailDimensions` static methods with delegations (keep the same signatures so its existing tests still compile + pass):

```csharp
    internal static Task<MemoryStream?> GenerateThumbnailAsync(Stream sourceStream, CancellationToken ct = default)
        => ImageThumbnailHelper.GenerateThumbnailAsync(sourceStream, ct);

    internal static (int width, int height) CalculateThumbnailDimensions(int originalWidth, int originalHeight)
        => ImageThumbnailHelper.CalculateThumbnailDimensions(originalWidth, originalHeight);
```

Remove the now-unused `ThumbnailMaxDimension`/`ThumbnailJpegQuality` consts from `SessionAttachmentService` ONLY if nothing else references them (keep `DownloadUrlExpirySeconds`).

- [ ] **Step 3: Run the SessionAttachmentService suite** (regression gate): `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~SessionAttachmentServiceTests"` → PASS (unchanged).

- [ ] **Step 4: Commit.** `git commit -m "refactor(api): #2436 PR-B extract ImageThumbnailHelper (shared)"`

---

### Task 9: `UploadPlayRecordPhotoCommand` + validator

**Files:** Create the command/result + validator + test `UploadPlayRecordPhotoCommandValidatorTests.cs`

- [ ] **Step 1: Command + result.**

```csharp
using System;
using System.IO;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

internal record UploadPlayRecordPhotoCommand(
    Guid RecordId,
    Guid UserId,
    Stream FileStream,
    long FileSizeBytes,
    string MimeType,
    bool ExtractScoreFromPhoto,
    string? Caption
) : ICommand<PlayRecordPhotoUploadResult>;

internal record PlayRecordPhotoUploadResult(
    Guid PhotoId,
    string PhotoUrl,
    string? ThumbnailUrl,
    string? OcrText,
    bool WasDeduplicated);
```

- [ ] **Step 2: Validator test** (mirror `UploadCustomCoverCommandValidator` rules; 5MB cap per spec):

```csharp
using System;
using System.IO;
using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2436")]
public class UploadPlayRecordPhotoCommandValidatorTests
{
    private readonly UploadPlayRecordPhotoCommandValidator _validator = new();

    private static UploadPlayRecordPhotoCommand Cmd(long size = 1000, string mime = "image/jpeg") =>
        new(Guid.NewGuid(), Guid.NewGuid(), new MemoryStream(), size, mime, false, null);

    [Fact]
    public void Valid_Passes() => _validator.Validate(Cmd()).IsValid.Should().BeTrue();

    [Fact]
    public void TooLarge_Fails() =>
        _validator.Validate(Cmd(size: 5 * 1024 * 1024 + 1)).IsValid.Should().BeFalse();

    [Fact]
    public void EmptyFile_Fails() => _validator.Validate(Cmd(size: 0)).IsValid.Should().BeFalse();

    [Fact]
    public void BadMime_Fails() =>
        _validator.Validate(Cmd(mime: "application/pdf")).IsValid.Should().BeFalse();

    [Fact]
    public void EmptyRecordId_Fails() =>
        _validator.Validate(new UploadPlayRecordPhotoCommand(Guid.Empty, Guid.NewGuid(),
            new MemoryStream(), 100, "image/png", false, null)).IsValid.Should().BeFalse();
}
```

- [ ] **Step 3: Run, verify FAIL.**

- [ ] **Step 4: Validator.**

```csharp
using System;
using System.Collections.Generic;
using FluentValidation;
using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

namespace Api.BoundedContexts.GameManagement.Application.Validators.PlayRecords;

internal sealed class UploadPlayRecordPhotoCommandValidator : AbstractValidator<UploadPlayRecordPhotoCommand>
{
    internal const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5MB (spec AC)
    private static readonly HashSet<string> AllowedMimeTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    public UploadPlayRecordPhotoCommandValidator()
    {
        RuleFor(c => c.RecordId).NotEmpty();
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.FileSizeBytes)
            .GreaterThan(0).WithMessage("File cannot be empty")
            .LessThanOrEqualTo(MaxFileSizeBytes).WithMessage("File cannot exceed 5MB");
        RuleFor(c => c.MimeType)
            .NotEmpty()
            .Must(m => AllowedMimeTypes.Contains(m))
            .WithMessage("Only JPEG, PNG, and WebP images are allowed");
        RuleFor(c => c.Caption).MaximumLength(500).When(c => c.Caption != null);
    }
}
```

- [ ] **Step 5: Run, verify PASS (5 tests). Commit.** `git commit -m "feat(play-records): #2436 PR-B upload command + validator (5MB)"`

---

### Task 10: `UploadPlayRecordPhotoCommandHandler` (blob + SHA256 dedup + thumbnail + OCR)

**Files:** Create handler + test `UploadPlayRecordPhotoCommandHandlerTests.cs`

- [ ] **Step 1: Write failing test** (Moq `IPlayRecordRepository` + `IUnitOfWork` + `IBlobStorageService` + `IPhotoPreprocessor`; real `PlayRecordPermissionChecker`):

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Services.Pdf;
using FluentAssertions;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.PlayRecords;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2436")]
public class UploadPlayRecordPhotoCommandHandlerTests
{
    private readonly Mock<IPlayRecordRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IBlobStorageService> _blob = new();
    private readonly Mock<IPhotoPreprocessor> _ocr = new();

    private UploadPlayRecordPhotoCommandHandler CreateSut() =>
        new(_repo.Object, _uow.Object, _blob.Object,
            new PlayRecordPermissionChecker(_repo.Object), _ocr.Object,
            new UploadPlayRecordPhotoCommandValidatorWrapperOrTimeProvider());

    private static PlayRecord NewRecord(Guid creator) =>
        PlayRecord.CreateFreeForm(Guid.NewGuid(), "Catan", creator, DateTime.UtcNow.AddDays(-1),
            PlayRecordVisibility.Private, SessionScoringConfig.CreateDefault());

    private static MemoryStream PngStream()
    {
        var ms = new MemoryStream();
        using var img = new Image<Rgba32>(10, 10);
        img.Save(ms, new PngEncoder());
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task Handle_NonCreator_ThrowsForbidden()
    {
        var record = NewRecord(Guid.NewGuid());
        var stranger = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _repo.Setup(r => r.CanUserEditAsync(stranger, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => CreateSut().Handle(
            new UploadPlayRecordPhotoCommand(record.Id, stranger, PngStream(), 100, "image/png", false, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        _blob.Verify(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingRecord_ThrowsNotFound()
    {
        var id = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((PlayRecord?)null);
        var act = () => CreateSut().Handle(
            new UploadPlayRecordPhotoCommand(id, Guid.NewGuid(), PngStream(), 100, "image/png", false, null),
            CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_Creator_StoresPhotoAndSaves()
    {
        var creator = Guid.NewGuid();
        var record = NewRecord(creator);
        _repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _repo.Setup(r => r.CanUserEditAsync(creator, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _blob.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), BlobCategory.PlayRecordPhoto, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "fid", "play-record-photos/x/fid.png", 100));
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://signed/url");

        var result = await CreateSut().Handle(
            new UploadPlayRecordPhotoCommand(record.Id, creator, PngStream(), 100, "image/png", false, null),
            CancellationToken.None);

        result.PhotoId.Should().NotBeEmpty();
        record.Photos.Should().HaveCount(1);
        _repo.Verify(r => r.UpdateAsync(record, It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _ocr.Verify(o => o.PreprocessAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never); // flag false
    }

    [Fact]
    public async Task Handle_ExtractScore_RunsOcrAndStoresText()
    {
        var creator = Guid.NewGuid();
        var record = NewRecord(creator);
        _repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _repo.Setup(r => r.CanUserEditAsync(creator, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _blob.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "fid", "path/fid.png", 100));
        _ocr.Setup(o => o.PreprocessAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoPreprocessResult(Array.Empty<byte>(), "Alice 10 Bob 8", 0.93, PageOrientation.Portrait, false, Array.Empty<string>()));

        var result = await CreateSut().Handle(
            new UploadPlayRecordPhotoCommand(record.Id, creator, PngStream(), 100, "image/png", true, null),
            CancellationToken.None);

        result.OcrText.Should().Be("Alice 10 Bob 8");
        record.Photos[0].OcrText.Should().Be("Alice 10 Bob 8");
    }

    [Fact]
    public async Task Handle_DuplicateSha_ReturnsExistingWithoutSecondStore()
    {
        var creator = Guid.NewGuid();
        var record = NewRecord(creator);
        _repo.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _repo.Setup(r => r.CanUserEditAsync(creator, record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _blob.Setup(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BlobStorageResult(true, "fid", "path/fid.png", 100));
        _blob.Setup(b => b.GetPresignedDownloadUrlAsync(It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync("https://signed/url");

        var cmd1 = new UploadPlayRecordPhotoCommand(record.Id, creator, PngStream(), 100, "image/png", false, null);
        await CreateSut().Handle(cmd1, CancellationToken.None);
        var cmd2 = new UploadPlayRecordPhotoCommand(record.Id, creator, PngStream(), 100, "image/png", false, null);
        var result2 = await CreateSut().Handle(cmd2, CancellationToken.None);

        record.Photos.Should().HaveCount(1);              // same bytes → deduped
        result2.WasDeduplicated.Should().BeTrue();
        _blob.Verify(b => b.StoreAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<BlobCategory>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

> NOTE for implementer: the `CreateSut()` ctor arg list above is illustrative — match it to the ACTUAL handler ctor you write in Step 3 (drop the placeholder wrapper; inject `TimeProvider` if used). The two `PngStream()` calls in the dedup test produce byte-identical PNGs (same 10×10 blank image) → identical SHA256. Adjust if ImageSharp PNG output is nondeterministic — if so, feed identical `byte[]` directly instead of re-encoding.

- [ ] **Step 2: Run, verify FAIL.**

- [ ] **Step 3: Handler.**

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Validators;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Helpers;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Commands.PlayRecords;

internal sealed class UploadPlayRecordPhotoCommandHandler
    : ICommandHandler<UploadPlayRecordPhotoCommand, PlayRecordPhotoUploadResult>
{
    private const int PresignExpirySeconds = 3600;
    private readonly IPlayRecordRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBlobStorageService _blobStorage;
    private readonly PlayRecordPermissionChecker _permissionChecker;
    private readonly IPhotoPreprocessor _photoPreprocessor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UploadPlayRecordPhotoCommandHandler> _logger;

    public UploadPlayRecordPhotoCommandHandler(
        IPlayRecordRepository repository,
        IUnitOfWork unitOfWork,
        IBlobStorageService blobStorage,
        PlayRecordPermissionChecker permissionChecker,
        IPhotoPreprocessor photoPreprocessor,
        TimeProvider timeProvider,
        ILogger<UploadPlayRecordPhotoCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _photoPreprocessor = photoPreprocessor ?? throw new ArgumentNullException(nameof(photoPreprocessor));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PlayRecordPhotoUploadResult> Handle(UploadPlayRecordPhotoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var record = await _repository.GetByIdAsync(command.RecordId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("PlayRecord", command.RecordId.ToString());

        if (!await _permissionChecker.CanEditAsync(command.UserId, command.RecordId, cancellationToken).ConfigureAwait(false))
            throw new ForbiddenException("You do not have permission to add photos to this play record.");

        // Buffer the upload (needed for magic-byte, hash, thumbnail, OCR + re-reads).
        byte[] bytes;
        using (var buffer = new MemoryStream())
        {
            await command.FileStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            bytes = buffer.ToArray();
        }

        using (var validateStream = new MemoryStream(bytes, writable: false))
        {
            var ok = await ImageFileValidator.ValidateMagicBytesAsync(validateStream, command.MimeType).ConfigureAwait(false);
            if (!ok)
                throw new ValidationException("File content does not match its declared type.");
        }

        var sha = Api.Helpers.CryptographyHelper.ComputeSha256Hash(bytes);

        // Idempotent re-upload: same (record, sha) → return existing.
        var existing = record.Photos.FirstOrDefault(p => p.Sha256Hash == sha);
        if (existing != null)
        {
            var existingUrl = await PresignAsync(existing.BlobUrl, cancellationToken).ConfigureAwait(false);
            return new PlayRecordPhotoUploadResult(existing.Id, existingUrl, existing.ThumbnailUrl, existing.OcrText, WasDeduplicated: true);
        }

        var photoId = Guid.NewGuid();
        var ext = command.MimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png"
                : command.MimeType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".jpg";
        var resourceKey = $"play-record-{record.Id:N}";
        var fileName = $"{photoId:N}-{sha[..8]}{ext}";

        using (var storeStream = new MemoryStream(bytes, writable: false))
        {
            var stored = await _blobStorage.StoreAsync(storeStream, fileName, BlobCategory.PlayRecordPhoto, resourceKey, cancellationToken).ConfigureAwait(false);
            if (!stored.Success || stored.FilePath is null)
                throw new ValidationException(stored.ErrorMessage ?? "Failed to store the photo.");

            // Thumbnail — best-effort.
            string? thumbUrl = null;
            try
            {
                using var thumbSource = new MemoryStream(bytes, writable: false);
                using var thumb = await ImageThumbnailHelper.GenerateThumbnailAsync(thumbSource, cancellationToken).ConfigureAwait(false);
                if (thumb != null)
                {
                    var thumbResult = await _blobStorage.StoreAsync(thumb, $"{photoId:N}-thumb.jpg", BlobCategory.PlayRecordPhoto, resourceKey, cancellationToken).ConfigureAwait(false);
                    if (thumbResult.Success) thumbUrl = thumbResult.FilePath;
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Thumbnail generation failed for photo {PhotoId}", photoId); }

            // OCR — opt-in, best-effort (DEC-3 smoldocling).
            string? ocrText = null;
            double? ocrConfidence = null;
            if (command.ExtractScoreFromPhoto)
            {
                try
                {
                    var ocr = await _photoPreprocessor.PreprocessAsync(bytes, cancellationToken).ConfigureAwait(false);
                    ocrText = ocr.ExtractedText;
                    ocrConfidence = ocr.ConfidenceScore;
                }
                catch (Exception ex) { _logger.LogWarning(ex, "OCR failed for photo {PhotoId}", photoId); }
            }

            record.AddPhoto(photoId, stored.FilePath, thumbUrl, command.FileSizeBytes, sha, ocrText, ocrConfidence, command.Caption, command.UserId, _timeProvider);
            await _repository.UpdateAsync(record, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var url = await PresignAsync(stored.FilePath, cancellationToken).ConfigureAwait(false);
            return new PlayRecordPhotoUploadResult(photoId, url, thumbUrl, ocrText, WasDeduplicated: false);
        }
    }

    private async Task<string> PresignAsync(string blobPath, CancellationToken ct)
    {
        // blobPath is the stored FilePath; presign by fileId+folder. For local storage the
        // presign returns null → fall back to the raw path.
        var fileName = Path.GetFileName(blobPath);
        var folder = Path.GetFileName(Path.GetDirectoryName(blobPath) ?? string.Empty);
        var fileId = fileName.Contains('_') ? fileName[..fileName.IndexOf('_')] : fileName;
        var signed = await _blobStorage.GetPresignedDownloadUrlAsync(fileId, BlobCategory.PlayRecordPhoto, folder, PresignExpirySeconds).ConfigureAwait(false);
        return signed ?? blobPath;
    }
}
```

> IMPLEMENTER: verify exact namespaces by reading `UploadCustomCoverCommandHandler` + `SessionAttachmentService.ParseBlobPath` — the `PresignAsync` fileId/folder parse must match how `StoreAsync` composes `FilePath` (the cover/session services derive `fileId` as the substring before the first `_`). If `ImageFileValidator` rejects `image/webp` thumbnails or the stored path shape differs, adjust the parse accordingly. `CryptographyHelper` namespace is `Api.Helpers`.

- [ ] **Step 4: Run, verify PASS (5 tests).**

- [ ] **Step 5: Commit.** `git commit -m "feat(play-records): #2436 PR-B upload handler (dedup+thumb+OCR)"`

---

### Task 11: Endpoint `POST /play-records/{recordId}/photos`

**Files:** Modify `PlayRecordEndpoints.cs`

- [ ] **Step 1: Register the route** in `MapPlayRecordEndpoints` (beside the others):

```csharp
        group.MapPost("/play-records/{recordId:guid}/photos", HandleUploadPhoto)
            .RequireAuthenticatedUser()
            .DisableAntiforgery()
            .Produces<PlayRecordPhotoUploadResult>(201)
            .Produces(400).Produces(401).Produces(StatusCodes.Status403Forbidden).Produces(404)
            .WithTags("PlayRecords")
            .WithSummary("Upload a photo to a play record (creator-only, ≤5MB, opt-in OCR)")
            .WithOpenApi();
```

- [ ] **Step 2: Handler** (multipart; mirror `SessionAttachmentEndpoints` form-read + `httpContext.User.GetUserId()`):

```csharp
    private static async Task<IResult> HandleUploadPhoto(
        Guid recordId,
        [FromServices] IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var form = await httpContext.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var file = form.Files.GetFile("file");
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "File is required" });

        var extractScore = form.TryGetValue("extractScoreFromPhoto", out var raw)
            && bool.TryParse(raw.ToString(), out var b) && b;
        var caption = form.TryGetValue("caption", out var cap) ? cap.ToString() : null;

        using var stream = file.OpenReadStream();
        var command = new UploadPlayRecordPhotoCommand(
            recordId, httpContext.User.GetUserId(), stream, file.Length, file.ContentType, extractScore, caption);

        try
        {
            var result = await mediator.Send(command, cancellationToken).ConfigureAwait(false);
            return Results.Created($"/api/v1/play-records/{recordId}/photos/{result.PhotoId}", result);
        }
        catch (ForbiddenException ex) { return Results.Json(new { error = ex.Message }, statusCode: 403); }
        catch (NotFoundException ex) { return Results.NotFound(new { error = ex.Message }); }
    }
```

> Add `using` for `UploadPlayRecordPhotoCommand`/`PlayRecordPhotoUploadResult`, `Api.Middleware.Exceptions`, `Microsoft.AspNetCore.Http`. `ValidationException` from the FluentValidation pipeline behavior already maps to 400 (don't catch it). Verify the project's validation behavior maps `ValidationException`→400 by checking an existing endpoint; if not, add a catch.

- [ ] **Step 3: Build + smoke.** `dotnet build apps/api/src/Api` → succeeds. No Program.cs change (route is inside `MapPlayRecordEndpoints`).

- [ ] **Step 4: Commit.** `git commit -m "feat(play-records): #2436 PR-B POST photos endpoint"`

---

### Task 12: Integration test (Testcontainers round-trip)

**Files:** Create `apps/api/tests/Api.Tests/Integration/GameManagement/PlayRecordPhotoUploadTests.cs` (mirror `PlayRecordCommandTests` setup; register a `LocalStorageService`/`IBlobStorageService` double + `IPhotoPreprocessor` mock)

- [ ] **Step 1: Write the test** — seed a user + record, dispatch `UploadPlayRecordPhotoCommand`, then read back via a fresh `MeepleAiDbContext` scope and assert `play_record_photos` has 1 row with the sha + blob url; assert a non-creator gets `ForbiddenException`; assert the unique index blocks a duplicate `(recordId, sha)` at the DB level. Use the `IBlobStorageService` registered as a fake returning `BlobStorageResult(true, "fid", "play-record-photos/x/fid.png", 100)`, and an `IPhotoPreprocessor` Moq.

> Follow `PlayRecordCommandTests.cs` for: isolated DB (`CreateIsolatedDatabaseAsync`), `MigrateAsync` (runs the Task 7 migration), `SeedTestUserAsync`, `CreateTestRecordAsync(creatorId)`, `SendInScopeAsync`, fresh-scope read-back. Register `IBlobStorageService` + `IPhotoPreprocessor` + `UploadPlayRecordPhotoCommandHandler` deps in the service collection (the base builder does NOT include blob storage).

- [ ] **Step 2: Run.** `dotnet test apps/api/tests/Api.Tests --filter "FullyQualifiedName~PlayRecordPhotoUploadTests"` → PASS. (Requires Docker.)

- [ ] **Step 3: Commit.** `git commit -m "test(play-records): #2436 PR-B photo upload integration round-trip"`

---

### Task 13: Amend ADR-067 (OCR decision)

**Files:** Modify `docs/for-claude/architecture/adr/adr-067-playrecord-photo-upload-pipeline.md`

- [ ] **Step 1: Update Status + add an amendment** documenting that OCR was wired (DEC-3), superseding O-C:
  - Change `**Status**: Proposed` → `**Status**: Accepted (amended 2026-06-20)`.
  - In the OCR section, mark O-C superseded and add **O-D — Reuse smoldocling `/api/v1/preprocess`**: the `IPhotoPreprocessor` (`SmoldoclingPhotoPreprocessor`) already does image OCR; the §Context premise "no image endpoint exists" is corrected. The upload handler calls `PreprocessAsync` when `ExtractScoreFromPhoto=true` (best-effort), populating `OcrText`/`OcrConfidence`.
  - Note `BlobCategory.PlayRecordPhoto`, max-10-photos invariant, and 5MB server-side enforcement now exist.

- [ ] **Step 2: Commit.** `git commit -m "docs(adr-067): #2436 PR-B amend OCR decision to smoldocling (O-D)"`

---

## Final gates (before PR)

- [ ] `dotnet build apps/api/src/Api` clean.
- [ ] `dotnet test apps/api/tests/Api.Tests --filter "BoundedContext=GameManagement&Category=Unit"` green (incl. all new unit tests + the unchanged SessionAttachmentService + existing PlayRecord suites — **no fail-count growth above baseline**).
- [ ] Integration test green (Docker).
- [ ] Open PR to `main-dev`, `Refs #2436` (PR-C remains — do NOT close).

## Self-Review notes
- **Cross-BC injection**: handler injects `IPhotoPreprocessor` (DocumentProcessing, internal, DI-wide). Acceptable per single-assembly convention; if a reviewer objects, wrap in a thin GameManagement-local interface (follow-up).
- **Presign parse** (`PresignAsync`) is the riskiest detail — it must mirror how `StoreAsync` composes `FilePath`. The implementer must verify against `SessionAttachmentService.ParseBlobPath` + the local/S3 storage path shape; the integration test (local storage → presign returns null → raw-path fallback) covers the happy path.
- **Dedup**: in-aggregate (`record.Photos.Any(sha)`) AND DB unique index (defense in depth). Concurrent identical uploads race to the unique index → `DbUpdateException`; acceptable for MVP (rare; user retries).
