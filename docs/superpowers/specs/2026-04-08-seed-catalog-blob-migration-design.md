# Seed Catalog Blob Migration — Design Spec

**Date**: 2026-04-08
**Status**: Draft
**Deployment target**: meepleai.app (staging profile, current prod-like env)

## Objective

Pre-populate meepleai.app with all ~136 rulebook PDFs already present in the local `data/rulebook/` directory by moving them into a dedicated S3-compatible seed bucket. On the next merge to `main-staging` with green CI, the deploy pipeline provisions the full indexed catalog automatically. The bucket becomes the single source of truth for seed PDFs; the legacy filesystem-based seeding path is removed.

The change also fixes four runtime bugs discovered during end-to-end workflow testing in PR #267 that would otherwise leave the seeded data unusable for the game-night flow.

## Goals

1. Seeded catalog on meepleai.app within ~30 minutes of deploy (async indexing).
2. Idempotent on re-runs: `pdf_documents` records are not re-created on subsequent deploys.
3. Drift detection: a PDF update in the bucket (new SHA256) triggers a re-index for that single entry.
4. `pdf_documents.SharedGameId`, `vector_documents.shared_game_id`, and `text_chunks.SharedGameId` are populated for every seeded record, enabling RAG queries by SharedGame.
5. Existing pre-seed production data (if any) is backfilled with correct `SharedGameId` via a one-time migration.
6. Repo stays lean — no PDFs committed to git.

## Non-Goals

- Multi-language rulebook variants (all PDFs are treated as a single language per entry).
- Automatic publisher-driven PDF updates.
- Separate prod environment beyond meepleai.app (future `prod.yml` bucket is out of scope).
- Admin UI to manage seed games (catalog edits go through manifest + PR).
- Removing the 9 placeholder games without PDFs (they stay in the catalog as-is).

## Scope Summary

| Component | Change |
|-----------|--------|
| `meepleai-seeds` bucket (new) | Holds all ~136 seed PDFs under `rulebooks/v1/` |
| `infra/scripts/upload-seed-pdfs.sh` (new) | One-time uploader, emits `.seed-hashes.tsv` |
| `infra/scripts/patch-manifest-from-hashes.sh` (new) | Rewrites `staging.yml` entries from `pdf:` to `pdfBlobKey:` + `pdfSha256:` |
| `staging.yml`, `dev.yml`, `prod.yml` | All 122 existing PDF entries migrated; 14 curated entries added → 136 with `pdfBlobKey` |
| `SeedManifestEntry.cs` | New nullable fields: `PdfBlobKey`, `PdfSha256`, `PdfLanguage`, `PdfVersion` |
| `PdfSeeder.cs` | Reads only from blob storage; legacy local-path code removed |
| `ISeedBlobReader` + `S3SeedBlobReader` (new) | Readonly accessor for the seed bucket |
| `NoOpSeedBlobReader` (new) | DI fallback when `SEED_BUCKET_*` env vars are absent |
| `UploadPdfCommandHandler.cs` | Bug #2 fix: populate `SharedGameId` when the gameId matches an existing SharedGame |
| `StartGameNightSessionCommandHandler.cs` | Bug #3 fix: include the organizer as owner participant |
| `GameNightEventRepository.cs` | Bug #4 fix: `Enum.TryParse` with safe default |
| New EF migration | Backfill `pdf_documents/vector_documents/text_chunks.SharedGameId` from the `games` bridge |
| `compose.staging.yml` | Explicit `SEED_PROFILE=Staging` env var |
| `storage.secret.example` | Documents `SEED_BUCKET_*` readonly credentials |
| Tests | Unit + integration + deploy smoke |

## Architecture

### High-level flow

```
Developer (one-time)                 CI / Deploy                     Runtime
─────────────────────                ─────────────                   ───────
upload-seed-pdfs.sh                  Push to main-staging            Quartz PdfProcessingQuartzJob
  ├─ scans data/rulebook/              ↓                               polls processing_jobs
  ├─ aws s3 cp each PDF              GitHub Actions                    ↓
  └─ writes .seed-hashes.tsv           ├─ build image                  for each Pending:
       ↓                               ├─ ssh deploy                     ├─ download from blob
patch-manifest-from-hashes.sh          └─ docker compose up              ├─ extract text
  rewrites staging.yml                   ↓                                ├─ chunk
       ↓                               API container startup              ├─ embed (batched)
git commit + push PR                    ├─ EF migrations                  ├─ index in pgvector
                                         │   (incl. backfill)             └─ state → Ready
                                         └─ SeedOrchestrator
                                              ├─ CoreSeedLayer
                                              └─ CatalogSeedLayer
                                                    ├─ GameSeeder (145)
                                                    └─ PdfSeeder (136 blob)
```

### Data flow for a single seeded PDF

```
seed bucket:  rulebooks/v1/mage-knight_rulebook.pdf
              (sha256: a3f2...)
                      ↓
              ISeedBlobReader.OpenReadAsync
                      ↓
              stream copied to primary blob store
              (key: {pdfId:N}, prefix: {sharedGameId})
                      ↓
              PdfDocumentEntity created
                SharedGameId = <SharedGame.Id>
                GameId       = null
                ContentHash  = a3f2...
                ProcessingState = Pending
                      ↓
              Quartz picks up on next poll
                      ↓
              Pipeline: extract → chunk → embed → index
                      ↓
              vector_documents row (shared_game_id set)
              text_chunks rows     (SharedGameId set)
              PdfDocument.ProcessingState = Ready
```

## Detailed Design

### 1. Seed bucket

- **Name**: `meepleai-seeds`
- **Backend**: S3-compatible — Cloudflare R2 preferred, AWS S3 also supported. **Backblaze B2 is not supported for the seed bucket** because the upload script's idempotency check uses custom object metadata, which B2's S3 emulation does not support reliably. The primary blob storage (separate service) is unaffected.
- **Structure**:
  ```
  rulebooks/
    v1/
      7-wonders_rulebook.pdf
      ...
      masters-of-the-universe-fields-of-eternia_rulebook.pdf
  ```
- **Versioning**: `v1/` prefix reserved for future rollouts. A `v2/` path would coexist without breaking active v1 references.
- **Access model**:
  - Upload: developer credentials with write access, loaded from `storage.secret` at script run time.
  - Runtime read: API container uses readonly credentials loaded as `SEED_BUCKET_*` env vars. Separation prevents accidental writes from the running app.
  - Object metadata: each upload carries `sha256` in object metadata for quick integrity checks outside the manifest.

### 2. Upload and manifest patch scripts

**`infra/scripts/upload-seed-pdfs.sh`**:
- Sources `infra/secrets/storage.secret` for S3 credentials.
- Iterates over `data/rulebook/*_rulebook.pdf`.
- For each file:
  1. Computes SHA256 locally: `sha=$(sha256sum "$file" | cut -d' ' -f1)`
  2. Checks if object exists with matching metadata:
     ```bash
     remote_sha=$(aws s3api head-object \
       --bucket "$BUCKET" --key "$key" \
       --query 'Metadata.sha256' --output text 2>/dev/null || echo "")
     ```
  3. If `remote_sha == $sha` → skip and log `already-uploaded`
  4. Otherwise → `aws s3 cp "$file" "s3://$BUCKET/$key" --metadata "sha256=$sha"`
- Emits `data/rulebook/.seed-hashes.tsv` with `slug<TAB>sha256` lines (one per input file, regardless of upload decision).
- Exits non-zero if any SHA256 computation fails (e.g., unreadable file).
- Assumes the S3-compatible backend supports custom object metadata. Cloudflare R2 and AWS S3 do; Backblaze B2 may require the native B2 API for metadata. The spec narrows the supported backends to R2 or S3 for the seed bucket; the primary blob storage is unaffected.

**`infra/scripts/patch-manifest-from-hashes.sh`**:
- Reads `.seed-hashes.tsv`.
- For each slug, finds entries in `staging.yml` / `dev.yml` / `prod.yml` whose `pdf:` value is `data/rulebook/{slug}_rulebook.pdf`.
- Rewrites the entry in place:
  - Removes `pdf:`
  - Adds `pdfBlobKey: rulebooks/v1/{slug}_rulebook.pdf`
  - Adds `pdfSha256: {hash}`
  - Leaves `pdfLanguage` and `pdfVersion` to defaults (`en`, `1.0`) unless already specified.
- For slugs that are in `.seed-hashes.tsv` but have no existing manifest entry (the 14 new games whose PDFs were uploaded via PR #267 workflow), the script appends a skeleton entry to the end of `staging.yml` containing `title`, `slug`, `pdfBlobKey`, and `pdfSha256` already filled in. The developer fills in the remaining metadata (`bggId`, `yearPublished`, player counts, designers, publishers, categories, mechanics) using the values already present in `data/rulebook/manifest.json` from PR #267.
- Uses `yq` (or a small Python helper) — not raw sed — to keep YAML valid.

Both scripts live only on the developer machine. They never run in CI.

### 3. Manifest schema update

**`SeedManifestGame`** (the actual type name in `SeedManifest.cs`; the record is a mutable class, not a record) gains four optional fields:

```csharp
internal sealed class SeedManifestGame
{
    public string Title { get; set; } = string.Empty;
    public int? BggId { get; set; }
    public string Language { get; set; } = "en";
    public string? Pdf { get; set; }                // REMOVED after migration
    // ... other existing fields ...

    // New blob-based PDF fields
    public string? PdfBlobKey { get; set; }
    public string? PdfSha256 { get; set; }
    public string? PdfVersion { get; set; }         // default "1.0"
    // Note: no PdfLanguage — reuses the existing Language field
}
```

The YAML deserializer uses `CamelCaseNamingConvention`, so the YAML keys are `pdfBlobKey`, `pdfSha256`, `pdfVersion` (matching the existing `pdf` key convention).

**Manifest entry example** (after migration):

```yaml
- title: "Mage Knight Board Game"
  bggId: 96848
  language: "en"              # existing field, unchanged
  yearPublished: 2011
  minPlayers: 1
  maxPlayers: 4
  playingTime: 240
  minAge: 14
  designers: ["Vlaada Chvátil"]
  publishers: ["WizKids"]
  categories: ["Adventure", "Exploration", "Fantasy"]
  mechanics: ["Deck Building", "Dice Rolling", "Grid Movement"]
  pdfBlobKey: "rulebooks/v1/mage-knight_rulebook.pdf"
  pdfSha256: "<hash from upload script>"
  pdfVersion: "1.0"
```

The 9 games without rulebook PDFs keep their existing entries unchanged and have no `pdfBlobKey` field. The `PdfSeeder` skips entries without `pdfBlobKey` silently (no warning, no error).

### 4. PdfSeeder refactor

The file stays `internal static class PdfSeeder` (current shape) but gains an additional `ISeedBlobReader` parameter on the existing `SeedAsync` entry point. Legacy local-path logic is removed. `CatalogSeeder` is updated to pass the new parameter.

**Key contract notes** (must match existing code exactly):

- `IBlobStorageService.StoreAsync(Stream stream, string fileName, string gameId, CancellationToken ct)` returns `Task<BlobStorageResult>` containing `FileId`, `FilePath`, `FileSizeBytes`. **Not** a bare long.
- `PdfDocumentEntity` required fields: `FilePath` (not nullable — set to the `BlobStorageResult.FilePath` returned by the primary blob store), `DocumentType` (enum-string: `"base"` / `"expansion"` / `"errata"` / `"homerule"`), `DocumentCategory` (enum-string: `"Rulebook"` default), `VersionLabel` (optional string for version tag).
- `gameMap` from `GameSeeder` maps `BggId → GameEntity.Id` (the games table PK, which carries a FK to `SharedGameEntity.Id`). The seeded PDF is linked to `GameEntity.Id`, not directly to `SharedGame.Id`. The backfill and upload-handler fixes handle the `SharedGameId` propagation downstream.

```csharp
internal static class PdfSeeder
{
    public static async Task SeedAsync(
        MeepleAiDbContext db,
        SeedManifest manifest,
        Dictionary<int, Guid> gameMap,
        Guid systemUserId,
        IBlobStorageService primaryBlob,
        ISeedBlobReader seedBlob,
        ILogger logger,
        CancellationToken ct)
    {
        if (!seedBlob.IsConfigured)
        {
            logger.LogInformation(
                "PdfSeeder: ISeedBlobReader is NoOp (SEED_BUCKET_* env vars unset). Skipping PDF seeding.");
            return;
        }

        var pdfEntries = manifest.Catalog.Games
            .Where(g => !string.IsNullOrWhiteSpace(g.PdfBlobKey) && g.BggId is > 0)
            .ToList();

        if (pdfEntries.Count == 0)
        {
            logger.LogInformation("PdfSeeder: no blob PDF entries in manifest. Skipping.");
            return;
        }

        // Idempotency check: existing pdf_documents keyed by (GameId, FileName)
        var existing = await db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.GameId != null)
            .Select(p => new { p.Id, p.GameId, p.FileName, p.ContentHash })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var existingByKey = existing.ToDictionary(
            e => $"{e.GameId}:{e.FileName}",
            StringComparer.OrdinalIgnoreCase);

        var seeded = 0; var skipped = 0; var drifted = 0;

        foreach (var entry in pdfEntries)
        {
            try
            {
                if (!gameMap.TryGetValue(entry.BggId!.Value, out var gameId))
                {
                    logger.LogWarning(
                        "PdfSeeder: no GameEntity for BggId={BggId} ('{Title}'). Skipping.",
                        entry.BggId, entry.Title);
                    skipped++;
                    continue;
                }

                var fileName = Path.GetFileName(entry.PdfBlobKey!);
                var key = $"{gameId}:{fileName}";

                if (existingByKey.TryGetValue(key, out var existingRow))
                {
                    if (string.Equals(existingRow.ContentHash, entry.PdfSha256, StringComparison.Ordinal))
                    {
                        skipped++;
                        continue;
                    }
                    // Drift — delete old cascade + primary blob
                    logger.LogInformation(
                        "PdfSeeder: hash drift for '{FileName}' (BggId {BggId}). Re-seeding.",
                        fileName, entry.BggId);
                    await DeletePdfCascadeAsync(db, primaryBlob, existingRow.Id, gameId, ct).ConfigureAwait(false);
                    existingByKey.Remove(key);
                    drifted++;
                }

                if (!await seedBlob.ExistsAsync(entry.PdfBlobKey!, ct).ConfigureAwait(false))
                {
                    logger.LogWarning(
                        "PdfSeeder: seed blob missing at '{BlobKey}' for BggId {BggId}. Skipping.",
                        entry.PdfBlobKey, entry.BggId);
                    skipped++;
                    continue;
                }

                await using var stream = await seedBlob.OpenReadAsync(entry.PdfBlobKey!, ct).ConfigureAwait(false);
                var result = await primaryBlob
                    .StoreAsync(stream, fileName, gameId.ToString(), ct)
                    .ConfigureAwait(false);

                if (!result.Success || string.IsNullOrEmpty(result.FileId))
                {
                    logger.LogWarning(
                        "PdfSeeder: primary blob store failed for '{FileName}': {Error}",
                        fileName, result.ErrorMessage);
                    skipped++;
                    continue;
                }

                var pdfEntity = new PdfDocumentEntity
                {
                    Id = Guid.Parse(result.FileId!),
                    GameId = gameId,                         // games.Id (FK to SharedGame via SharedGameId)
                    FileName = fileName,
                    FilePath = result.FilePath ?? string.Empty,
                    FileSizeBytes = result.FileSizeBytes,
                    ContentType = "application/pdf",
                    Language = entry.Language ?? "en",
                    ProcessingState = nameof(PdfProcessingState.Pending),
                    IsPublic = true,
                    DocumentType = "base",
                    DocumentCategory = "Rulebook",
                    VersionLabel = entry.PdfVersion ?? "1.0",
                    ContentHash = entry.PdfSha256,
                    IsActiveForRag = true,
                    ProcessingPriority = "Normal",
                    SortOrder = 0,
                    UploadedAt = DateTime.UtcNow,
                    UploadedByUserId = systemUserId,
                };

                db.PdfDocuments.Add(pdfEntity);
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                existingByKey[key] = new { Id = pdfEntity.Id, GameId = (Guid?)gameId, FileName = fileName, ContentHash = entry.PdfSha256 };
                seeded++;

                logger.LogInformation(
                    "PdfSeeder: queued '{FileName}' for game '{Title}' ({Size} bytes, sha256={Hash})",
                    fileName, entry.Title, pdfEntity.FileSizeBytes, entry.PdfSha256?[..8]);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "PdfSeeder: failed to seed BggId {BggId} ('{Title}'). Continuing.",
                    entry.BggId, entry.Title);
                db.ChangeTracker.Clear();
                skipped++;
            }
        }

        logger.LogInformation(
            "PdfSeeder completed: {Seeded} queued, {Drifted} re-seeded (drift), {Skipped} skipped",
            seeded, drifted, skipped);
    }

    private static async Task DeletePdfCascadeAsync(
        MeepleAiDbContext db,
        IBlobStorageService primaryBlob,
        Guid pdfDocumentId,
        Guid gameId,
        CancellationToken ct)
    {
        // text_chunks + vector_documents cascade on pdf_documents FK
        var doc = await db.PdfDocuments.FindAsync(new object[] { pdfDocumentId }, ct).ConfigureAwait(false);
        if (doc is null) return;

        // Remove orphaned primary blob first (failures are logged but do not block)
        try
        {
            await primaryBlob.DeleteAsync(doc.Id.ToString("N"), gameId.ToString(), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Not throwing: a missing primary blob should not block re-seed
            // logger not injected here — caller logs the drift, this is best-effort cleanup
        }

        db.PdfDocuments.Remove(doc);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
```

**CatalogSeeder update**: the existing call site in `CatalogSeeder.cs` gains two extra args:

```csharp
await PdfSeeder.SeedAsync(
    db, manifest, gameMap, systemUserId,
    primaryBlob, seedBlob,   // NEW — resolved from DI
    logger, ct);
```

`CatalogSeeder` receives `IBlobStorageService` and `ISeedBlobReader` via its own constructor/DI if it becomes instance-based, or via method parameters if it stays static. Either shape keeps the ordering guarantee: `GameSeeder.SeedAsync` completes before `PdfSeeder.SeedAsync` is called, ensuring `gameMap` is populated.

### 5. ISeedBlobReader

Isolated interface — the seed bucket client is separate from the primary blob service because it has different credentials, provider config, and access pattern (readonly, rare use).

```csharp
public interface ISeedBlobReader
{
    /// <summary>
    /// True if the reader is backed by a real seed bucket. False for NoOp fallback,
    /// in which case PdfSeeder should skip entirely instead of logging per-entry errors.
    /// </summary>
    bool IsConfigured { get; }

    Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct);
    Task<bool> ExistsAsync(string blobKey, CancellationToken ct);
}

internal sealed class S3SeedBlobReader : ISeedBlobReader
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3SeedBlobReader(IAmazonS3 client, string bucket)
    {
        _client = client;
        _bucket = bucket;
    }

    public bool IsConfigured => true;

    public async Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct)
    {
        var response = await _client.GetObjectAsync(_bucket, blobKey, ct).ConfigureAwait(false);
        return response.ResponseStream;
    }

    public async Task<bool> ExistsAsync(string blobKey, CancellationToken ct)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, blobKey, ct).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}

internal sealed class NoOpSeedBlobReader : ISeedBlobReader
{
    public bool IsConfigured => false;

    public Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct) =>
        throw new InvalidOperationException(
            "Seed bucket not configured. Set SEED_BUCKET_* env vars to enable PDF seeding.");

    public Task<bool> ExistsAsync(string blobKey, CancellationToken ct) => Task.FromResult(false);
}
```

**DI registration** (in `KnowledgeBaseServiceExtensions` or a new `SeedingServiceExtensions`):

```csharp
services.AddSingleton<ISeedBlobReader>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var bucket = config["SEED_BUCKET_NAME"];
    if (string.IsNullOrEmpty(bucket))
    {
        return new NoOpSeedBlobReader(sp.GetRequiredService<ILogger<NoOpSeedBlobReader>>());
    }

    var s3Config = new AmazonS3Config
    {
        ServiceURL = config["SEED_BUCKET_ENDPOINT"],
        ForcePathStyle = bool.Parse(config["SEED_BUCKET_FORCE_PATH_STYLE"] ?? "false"),
    };
    var creds = new BasicAWSCredentials(
        config["SEED_BUCKET_ACCESS_KEY"],
        config["SEED_BUCKET_SECRET_KEY"]);
    var client = new AmazonS3Client(creds, s3Config);
    return new S3SeedBlobReader(client, bucket);
});
```

### 6. Bug fixes

#### Bug #2: SharedGameId linkage in UploadPdfCommandHandler

The upload endpoint currently creates a per-upload private `games` row whenever the client POSTs a `gameId`, regardless of whether the gameId already refers to a SharedGame. Fix by checking the `shared_games` table first.

```csharp
// Before building the PdfDocumentEntity
var sharedGame = await _db.SharedGames
    .FirstOrDefaultAsync(sg => sg.Id == cmd.GameId, ct);

if (sharedGame != null)
{
    pdfDoc.SharedGameId = sharedGame.Id;
    pdfDoc.GameId = null;
}
else
{
    pdfDoc.GameId = cmd.GameId; // legacy private-game path
}
```

Non-breaking: callers that pass a private `gameId` see the same behavior as before.

#### Bug #3: StartGameNightSessionCommandHandler empty participants

The handler dispatches `CreateSessionCommand` with an empty `participants` list, which fails validation (`At least one participant must be owner`). Fix by constructing the organizer as the initial owner.

```csharp
var organizer = await _db.Users
    .AsNoTracking()
    .FirstOrDefaultAsync(u => u.Id == command.UserId, ct);

var participants = new List<ParticipantDto>
{
    new ParticipantDto
    {
        UserId = command.UserId,
        DisplayName = organizer?.DisplayName ?? "Organizer",
        IsOwner = true,
        JoinOrder = 0,
    }
};

var createResult = await _mediator.Send(new CreateSessionCommand(
    command.UserId,
    command.GameId,
    "GameSpecific",
    DateTime.UtcNow,
    null,
    participants), ct);
```

#### Bug #4: GameNightEventRepository.MapToDomain fragile Enum.Parse

`GameNightEventRepository.cs` currently has **three** `Enum.Parse<T>()` calls that throw on unknown strings:

- Line 182: `Enum.Parse<GameNightStatus>(entity.Status)`
- Line 217: `Enum.Parse<RsvpStatus>(r.Status)`
- Line 234 (approx): `Enum.Parse<GameNightSessionStatus>(s.Status)` in the Sessions mapping

All three must be replaced with safe helpers. Add three static helpers on the repository class:

```csharp
private GameNightStatus ParseGameNightStatus(string raw)
{
    if (Enum.TryParse<GameNightStatus>(raw, ignoreCase: true, out var v)) return v;
    _logger.LogWarning("Unknown GameNightStatus '{Raw}', defaulting to Draft", raw);
    return GameNightStatus.Draft;
}

private RsvpStatus ParseRsvpStatus(string raw)
{
    if (Enum.TryParse<RsvpStatus>(raw, ignoreCase: true, out var v)) return v;
    _logger.LogWarning("Unknown RsvpStatus '{Raw}', defaulting to Pending", raw);
    return RsvpStatus.Pending;
}

private GameNightSessionStatus ParseSessionStatus(string raw)
{
    if (Enum.TryParse<GameNightSessionStatus>(raw, ignoreCase: true, out var v)) return v;
    _logger.LogWarning("Unknown GameNightSessionStatus '{Raw}', defaulting to Pending", raw);
    return GameNightSessionStatus.Pending;
}
```

The call sites in `MapToDomain` replace each `Enum.Parse<X>(raw)` with the corresponding `ParseX(raw)` helper. The exact default per enum should be picked from a "safe initial" value (`Draft` for GameNight, `Pending` for the other two).

### 7. Backfill migration

A forward-only EF migration runs before the first blob-based seed. It populates `SharedGameId` on existing rows created by the old upload path.

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql(@"
        UPDATE pdf_documents pd
        SET ""SharedGameId"" = g.""SharedGameId""
        FROM games g
        WHERE pd.""GameId"" = g.""Id""
          AND pd.""SharedGameId"" IS NULL
          AND g.""SharedGameId"" IS NOT NULL;

        UPDATE vector_documents vd
        SET shared_game_id = pd.""SharedGameId""
        FROM pdf_documents pd
        WHERE vd.""PdfDocumentId"" = pd.""Id""
          AND vd.shared_game_id IS NULL
          AND pd.""SharedGameId"" IS NOT NULL;

        UPDATE text_chunks tc
        SET ""SharedGameId"" = pd.""SharedGameId""
        FROM pdf_documents pd
        WHERE tc.""PdfDocumentId"" = pd.""Id""
          AND tc.""SharedGameId"" IS NULL
          AND pd.""SharedGameId"" IS NOT NULL;
    ");
}

protected override void Down(MigrationBuilder migrationBuilder) { /* no-op, forward-only */ }
```

The migration is idempotent — it only updates NULL columns and can safely run on any environment. EF Core migrations run inside a transaction by default on PostgreSQL, so a partial failure rolls back all three `UPDATE` statements atomically; no intermediate inconsistent state is possible.

### 8. Deploy profile fix

`infra/compose.staging.yml` gains an explicit `SEED_PROFILE`:

```yaml
services:
  api:
    environment:
      ASPNETCORE_ENVIRONMENT: Staging
      SEED_PROFILE: Staging
      SEED_BUCKET_NAME: meepleai-seeds
      SEED_BUCKET_ENDPOINT: ${SEED_BUCKET_ENDPOINT}
      SEED_BUCKET_REGION: auto
      SEED_BUCKET_FORCE_PATH_STYLE: "false"
      # SEED_BUCKET_ACCESS_KEY and SEED_BUCKET_SECRET_KEY come from storage.secret
    env_file:
      - ./secrets/storage.secret
```

Without this, `SeedOrchestrator` defaulted to `Dev` profile because the env-var fallback logic treated `ASPNETCORE_ENVIRONMENT=Staging` as "unrecognized" and fell through.

### 9. Error handling

| Failure mode | Behavior |
|--------------|----------|
| Seed bucket unreachable | `PdfSeeder` logs error per entry, skips. API still starts. |
| Seed blob missing for a given entry | Logs error with blob key and BggId, skips the entry. |
| SHA256 mismatch on existing record | Logs info, deletes old row (cascade), reinserts. |
| SharedGame row missing for a BggId | Logs warning, skips. GameSeeder should have run first. |
| Concurrent seed run | Advisory lock in `SeedOrchestrator` blocks the second instance. |
| Primary blob store write failure | Exception bubbles up, seeding stops for this entry. Next startup retries. |

## Testing

### Unit tests (new and updated)

- `PdfSeederTests`
  - `SeedAsync_BlobEntry_CreatesWithSharedGameId`
  - `SeedAsync_BlobEntry_ExistingMatchingHash_Skips`
  - `SeedAsync_BlobEntry_ExistingDifferentHash_DeletesAndRecreates`
  - `SeedAsync_BlobMissing_LogsErrorAndSkips`
  - `SeedAsync_EntryWithoutPdfBlobKey_Ignored`
  - `SeedAsync_NoOpReader_EntryWithBlobKey_SkipsAndWarns`
- `S3SeedBlobReaderTests`
  - `OpenReadAsync_ExistingKey_ReturnsStream`
  - `OpenReadAsync_MissingKey_Throws`
  - `ExistsAsync_Missing_ReturnsFalse`
  - `ExistsAsync_Present_ReturnsTrue`
- `UploadPdfCommandHandlerTests` (add)
  - `Handle_GameIdMatchesSharedGame_SetsSharedGameIdAndClearsGameId`
  - `Handle_GameIdMatchesPrivateGame_RetainsLegacyBehavior`
- `StartGameNightSessionCommandHandlerTests` (add)
  - `Handle_CreatesSessionWithOrganizerAsOwnerParticipant`
  - `Handle_OrganizerLookupFails_UsesFallbackDisplayName`
- `GameNightEventRepositoryTests` (add)
  - `MapToDomain_UnknownStatusString_DefaultsToPendingAndLogsWarning`

### Integration tests

- `CuratedCatalogSeedingIntegrationTests`
  - Uses an in-memory `ISeedBlobReader` stub providing a small PDF fixture.
  - Runs the full `SeedOrchestrator` against a Testcontainers Postgres instance.
  - Asserts that after seeding, `pdf_documents`, `vector_documents`, and `text_chunks` all contain rows with `SharedGameId` populated.
- `BackfillSharedGameIdMigrationTests`
  - Seeds legacy rows (games with `SharedGameId`, pdf_documents with only `GameId`).
  - Applies the migration.
  - Asserts all NULL columns are now populated.

### Deploy smoke test

The existing `validate` job in `deploy-staging.yml` adds two steps after the 5-minute warm-up. Both use the public shared-games search endpoint to avoid hardcoded internal IDs:

1. `curl https://meepleai.app/api/v1/shared-games?search=Mage%20Knight%20Board%20Game` → expect HTTP 200 with at least one result.
2. Parse the first `items[0].id` from step 1. That returns a `shared_games.id`, not a `games.Id` — so the workflow follows up with `curl https://meepleai.app/api/v1/shared-games/{id}` to fetch the associated `games` bridge record, takes `gameEntityId` from the response, and then calls `GET /api/v1/knowledge-base/{gameEntityId}/status`. Expect status `Pending`, `Processing`, or `Completed` — any 5xx fails the step.

Neither check blocks the deploy — they surface issues in the Slack notification instead. The script skips gracefully if step 1 returns zero results (e.g., before the first seed has completed).

### Manual verification checklist

Post-merge:

- [ ] Deploy workflow green on main-staging.
- [ ] Wait ~30 minutes for Quartz processing.
- [ ] Log in to meepleai.app as SuperAdmin.
- [ ] Catalog shows all expected games with `Published` status.
- [ ] For a random sample (5 games): `GET /knowledge-base/{id}/status` → `Completed`.
- [ ] Create a game night with one of the seeded games.
- [ ] `POST /knowledge-base/search` with a gameplay question → relevant RAG results returned.

## Rollout

1. **Developer pre-merge**:
   1. Provision `meepleai-seeds` bucket on R2 (or equivalent).
   2. Generate two credential pairs: one write-capable for the upload script, one readonly for the API.
   3. Update local `infra/secrets/storage.secret` with readonly credentials.
   4. Run `./infra/scripts/upload-seed-pdfs.sh` → produces `.seed-hashes.tsv`.
   5. Run `./infra/scripts/patch-manifest-from-hashes.sh` → rewrites `staging.yml`, `dev.yml`, `prod.yml`.
   6. Fill in any placeholder blocks left by the patch script for the 14 previously-unreferenced games.
   7. `dotnet build` + run unit and integration tests.
   8. Deploy server: update `/opt/meepleai/secrets/storage.secret` with readonly credentials + bucket env vars.
   9. Commit and push branch; open PR targeting `main-dev`.
2. **CI on merge to main-staging**:
   1. `deploy-staging.yml` builds and pushes the new image.
   2. Deploy job SSHes into meepleai.app and runs `docker compose up`.
   3. API container starts: runs EF migrations (including the backfill), then `SeedOrchestrator`.
   4. `PdfSeeder` walks the manifest, creates `PdfDocumentEntity` rows in `Pending`.
   5. `PdfProcessingQuartzJob` picks them up one at a time and indexes.
3. **Post-deploy**:
   1. Smoke test hits `/api/v1/health`.
   2. Validate job runs the two added RAG checks.
   3. Within ~30 minutes, all entries indexed.

## Rollback

- **Migration is forward-only but non-destructive**: it only populates NULL columns. Reverting the code does not require a schema rollback.
- **Manifest change is a data change**: reverting the PR restores the old local-path manifest. Since the `PdfSeeder` legacy path has been removed, a pure-revert is not enough to restore old behavior — a follow-up PR restores the legacy branch if needed. For that reason, rolling back via `git revert` is acceptable only if the deploy is not already using blob keys in production.
- **Partial-deploy failure mode**: if the new image ran `SeedOrchestrator` once and was then rolled back to the pre-PR image, the `pdf_documents` rows seeded by the new code remain. The old `PdfSeeder` keyed its idempotency check on `(GameId, FileName)` with `FileName` being the manifest `pdf:` value — the new rows have a matching `FileName` and the old seeder will skip them (it looks up by GameEntity, which still exists). So the seeded data stays intact and the catalog keeps working, but the old seeder can no longer create new PDFs for those games without first deleting the new rows. This is acceptable because rollback is an emergency path.
- **Data already seeded**: rows remain in the database; they are not cleaned up on rollback. This is intentional — the seeded data is still valid.
- **Bucket**: never touched on rollback.

## Open Questions and Deferred Decisions

- **R2 vs AWS S3 vs B2**: deferred to the developer doing the one-time upload. `S3BlobStorageService` already abstracts the provider.
- **Public vs signed-URL seed bucket**: the design assumes readonly credentials. If the team prefers public-read, the credentials env vars become unnecessary but the code path is unchanged.
- **Language variants**: only one PDF per game in v1. Multi-language is future work.

## File Layout

```
apps/api/src/Api/
├── Infrastructure/
│   ├── Seeders/
│   │   ├── Catalog/
│   │   │   ├── CatalogSeedLayer.cs                      [existing]
│   │   │   ├── GameSeeder.cs                            [existing]
│   │   │   ├── PdfSeeder.cs                             [MODIFIED — blob-only]
│   │   │   ├── SeedBlob/
│   │   │   │   ├── ISeedBlobReader.cs                   [NEW]
│   │   │   │   ├── S3SeedBlobReader.cs                  [NEW]
│   │   │   │   └── NoOpSeedBlobReader.cs                [NEW]
│   │   │   └── Manifests/
│   │   │       ├── staging.yml                          [MODIFIED — blob keys]
│   │   │       ├── dev.yml                              [MODIFIED — blob keys]
│   │   │       └── prod.yml                             [MODIFIED — blob keys]
│   │   └── SeedManifest.cs                              [MODIFIED — SeedManifestGame blob fields]
│   └── Migrations/
│       └── 20260408_BackfillSharedGameIdFromGames.cs    [NEW]
└── BoundedContexts/
    ├── DocumentProcessing/Application/Commands/
    │   └── UploadPdfCommandHandler.cs                   [MODIFIED — bug #2]
    └── GameManagement/
        ├── Application/Commands/GameNights/
        │   └── StartGameNightSessionCommandHandler.cs   [MODIFIED — bug #3]
        └── Infrastructure/Persistence/
            └── GameNightEventRepository.cs              [MODIFIED — bug #4]

apps/api/tests/Api.Tests/
├── Infrastructure/Seeders/Catalog/
│   ├── PdfSeederTests.cs                                [NEW]
│   └── SeedBlob/
│       └── S3SeedBlobReaderTests.cs                     [NEW]
├── BoundedContexts/
│   ├── DocumentProcessing/.../UploadPdfCommandHandlerTests.cs            [UPDATED]
│   └── GameManagement/
│       ├── .../StartGameNightSessionCommandHandlerTests.cs               [UPDATED]
│       └── .../GameNightEventRepositoryTests.cs                          [UPDATED]
└── Integration/
    ├── CuratedCatalogSeedingIntegrationTests.cs                          [NEW]
    └── BackfillSharedGameIdMigrationTests.cs                             [NEW]

infra/
├── scripts/
│   ├── upload-seed-pdfs.sh                              [NEW]
│   └── patch-manifest-from-hashes.sh                    [NEW]
├── compose.staging.yml                                  [MODIFIED — SEED_PROFILE]
└── secrets/
    └── storage.secret.example                           [MODIFIED — SEED_BUCKET_*]
```

## Success Criteria

1. `./infra/scripts/upload-seed-pdfs.sh` uploads every PDF in `data/rulebook/` to `s3://meepleai-seeds/rulebooks/v1/` and emits a hash manifest.
2. `staging.yml` contains `pdfBlobKey` + `pdfSha256` for every entry that used to have `pdf:`, plus the 14 previously-unreferenced games.
3. A clean `docker compose up` on meepleai.app with the new image creates all expected `PdfDocumentEntity` rows with `SharedGameId` populated.
4. Within 30 minutes of deploy, `vector_documents.shared_game_id` is populated for every seeded entry with `IndexingStatus = completed`.
5. `POST /knowledge-base/search` for a seeded game returns relevant RAG chunks.
6. A game night created via `POST /game-nights` and started via `POST /game-nights/{id}/sessions` succeeds (bug #3 fix verified).
7. `GET /game-nights/{id}` succeeds even if a `game_night_sessions.status` value is unrecognized (bug #4 fix verified).
8. Running the deploy a second time without any code change produces zero new `pdf_documents` rows (idempotency verified).
