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
- **Backend**: S3-compatible (Cloudflare R2 preferred, AWS S3 or Backblaze B2 also work because `S3BlobStorageService` already abstracts the provider).
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
- Computes SHA256 for each file.
- Uploads with `aws s3 cp --metadata sha256=<hash>`.
- Emits `data/rulebook/.seed-hashes.tsv` with `slug<TAB>sha256` lines.
- Idempotent: if the object already exists with matching metadata hash, it skips upload.

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

**`SeedManifestEntry`** gains four optional fields:

```csharp
public sealed record SeedManifestEntry
{
    public required string Title { get; init; }
    public int? BggId { get; init; }
    // ... existing fields ...

    // Legacy local-path PDF field — REMOVED in this spec.
    // public string? Pdf { get; init; }

    // New blob-based PDF fields
    public string? PdfBlobKey { get; init; }
    public string? PdfSha256 { get; init; }
    public string? PdfLanguage { get; init; }   // default "en"
    public string? PdfVersion { get; init; }    // default "1.0"
}
```

**Manifest entry example** (after migration):

```yaml
- title: "Mage Knight Board Game"
  bggId: 96848
  yearPublished: 2011
  minPlayers: 1
  maxPlayers: 4
  playingTimeMinutes: 240
  minAge: 14
  designers: ["Vlaada Chvátil"]
  publishers: ["WizKids"]
  categories: ["Adventure", "Exploration", "Fantasy"]
  mechanics: ["Deck Building", "Dice Rolling", "Grid Movement"]
  pdfBlobKey: "rulebooks/v1/mage-knight_rulebook.pdf"
  pdfSha256: "<hash from upload script>"
  pdfLanguage: "en"
  pdfVersion: "1.0"
```

The 9 games without rulebook PDFs keep their existing entries unchanged and have no `pdfBlobKey` field. The `PdfSeeder` skips entries without `pdfBlobKey` silently (no warning, no error).

### 4. PdfSeeder refactor

The file is simplified to a single code path. Legacy local-path logic is removed.

```csharp
internal sealed class PdfSeeder
{
    private readonly MeepleAiDbContext _db;
    private readonly IBlobStorageService _primaryBlob;
    private readonly ISeedBlobReader _seedBlob;
    private readonly ILogger<PdfSeeder> _logger;

    public async Task SeedAsync(SeedContext ctx, CancellationToken ct)
    {
        foreach (var entry in ctx.Manifest.Games)
        {
            if (string.IsNullOrEmpty(entry.PdfBlobKey))
                continue;

            await SeedBlobPdfAsync(ctx, entry, ct);
        }
    }

    private async Task SeedBlobPdfAsync(SeedContext ctx, SeedManifestEntry entry, CancellationToken ct)
    {
        var sharedGame = await _db.SharedGames
            .FirstOrDefaultAsync(sg => sg.BggId == entry.BggId, ct);
        if (sharedGame is null)
        {
            _logger.LogWarning("SharedGame not found for BggId {BggId}, skipping PDF seed", entry.BggId);
            return;
        }

        var fileName = Path.GetFileName(entry.PdfBlobKey!);
        var existing = await _db.PdfDocuments
            .FirstOrDefaultAsync(
                p => p.SharedGameId == sharedGame.Id && p.FileName == fileName,
                ct);

        if (existing is not null)
        {
            if (string.Equals(existing.ContentHash, entry.PdfSha256, StringComparison.Ordinal))
                return; // No change, idempotent skip

            // Drift detected — delete the stale record cascade and reinsert
            _logger.LogInformation(
                "PDF hash drift for {BggId}: manifest={New}, existing={Old}. Re-seeding.",
                entry.BggId, entry.PdfSha256, existing.ContentHash);
            await DeletePdfCascadeAsync(existing.Id, ct);
        }

        var blobExists = await _seedBlob.ExistsAsync(entry.PdfBlobKey!, ct);
        if (!blobExists)
        {
            _logger.LogError("Seed blob missing: {BlobKey} for BggId {BggId}", entry.PdfBlobKey, entry.BggId);
            return;
        }

        await using var stream = await _seedBlob.OpenReadAsync(entry.PdfBlobKey!, ct);
        var pdfId = Guid.NewGuid();
        var primaryKey = pdfId.ToString("N");
        var sizeBytes = await _primaryBlob.StoreAsync(
            primaryKey,
            sharedGame.Id.ToString(),
            stream,
            "application/pdf",
            ct);

        var pdfDoc = new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = sharedGame.Id,
            GameId = null,
            FileName = fileName,
            FileSizeBytes = sizeBytes,
            ContentType = "application/pdf",
            UploadedByUserId = ctx.SeedUserId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Pending",
            Language = entry.PdfLanguage ?? "en",
            VersionType = "base",
            VersionNumber = entry.PdfVersion ?? "1.0",
            ContentHash = entry.PdfSha256,
            DocumentType = "Rulebook",
            IsPublic = true,
        };

        _db.PdfDocuments.Add(pdfDoc);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seeded PDF {FileName} for SharedGame {SharedGameId}",
            fileName,
            sharedGame.Id);
    }

    private async Task DeletePdfCascadeAsync(Guid pdfDocumentId, CancellationToken ct)
    {
        // text_chunks and vector_documents have ON DELETE CASCADE to pdf_documents
        var doc = await _db.PdfDocuments.FindAsync(new object[] { pdfDocumentId }, ct);
        if (doc is null) return;
        _db.PdfDocuments.Remove(doc);
        await _db.SaveChangesAsync(ct);
    }
}
```

### 5. ISeedBlobReader

Isolated interface — the seed bucket client is separate from the primary blob service because it has different credentials, provider config, and access pattern (readonly, rare use).

```csharp
public interface ISeedBlobReader
{
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

    public async Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct)
    {
        var response = await _client.GetObjectAsync(_bucket, blobKey, ct);
        return response.ResponseStream;
    }

    public async Task<bool> ExistsAsync(string blobKey, CancellationToken ct)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, blobKey, ct);
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
    private readonly ILogger<NoOpSeedBlobReader> _logger;

    public NoOpSeedBlobReader(ILogger<NoOpSeedBlobReader> logger) => _logger = logger;

    public Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct)
    {
        _logger.LogWarning(
            "NoOp seed blob reader invoked for {BlobKey}; set SEED_BUCKET_* env vars to enable",
            blobKey);
        throw new InvalidOperationException("Seed bucket not configured");
    }

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

Replace `Enum.Parse` with `Enum.TryParse` and log on unknown values.

```csharp
private GameNightSessionStatus ParseStatus(string raw)
{
    if (Enum.TryParse<GameNightSessionStatus>(raw, ignoreCase: true, out var status))
        return status;

    _logger.LogWarning(
        "Unknown GameNightSession status '{Raw}', defaulting to Pending",
        raw);
    return GameNightSessionStatus.Pending;
}
```

Apply the same pattern to every `Enum.Parse` call in `MapToDomain`.

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

The migration is idempotent — it only updates NULL columns and can safely run on any environment.

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

The existing `validate` job in `deploy-staging.yml` adds two steps after the 5-minute warm-up:

1. `curl https://meepleai.app/api/v1/shared-games?search=Mage%20Knight%20Board%20Game` → expect HTTP 200 with at least one result.
2. `curl https://meepleai.app/api/v1/knowledge-base/{known-bggId-gameId}/status` for one reference game → expect `Pending` or `Completed`, not 500.

Neither check blocks the deploy — they surface issues in the Slack notification instead.

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
│   │   └── SeedManifestEntry.cs                         [MODIFIED — blob fields]
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
