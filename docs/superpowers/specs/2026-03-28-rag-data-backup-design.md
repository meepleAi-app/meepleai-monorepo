# RAG Data Backup & Production Migration

**Date**: 2026-03-28
**Status**: Approved
**Scope**: Export RAG data (PDFs, chunks, embeddings) as portable backup for production migration + continuous backup system

## Problem

MeepleAI has 100-200+ board game rulebook PDFs (~1.3 GB) that will be processed through the RAG pipeline. When moving to production (potentially with a dedicated vector store like Qdrant/Pinecone), we need:

1. **One-shot export** of all processed RAG data for go-live import
2. **Continuous backup** to prevent data loss and enable disaster recovery
3. **Format-agnostic** output so data can be imported into any vector store

## Architecture Decision

**Approach**: C# Commands in Administration BC + Make targets for orchestration.

Follows the existing pattern of `MigrateStorageCommand` and `ExportAuditLogsQuery` already in the Administration BC. Logic stays in C# (testable, type-safe, direct EF Core access), Make targets provide operational UX consistent with `make snapshot`, `make restore-*`.

## Data Model & Export Format

### Bundle Structure

Each processed PDF produces a bundle. All bundles are organized under a snapshot:

```
rag-exports/
├── manifest.json                          # Global index of all documents
└── games/
    └── {game-slug}/
        └── {pdf-document-id}/
            ├── metadata.json              # Document + game metadata
            ├── chunks.jsonl               # Text chunks (human-readable)
            ├── chunks.parquet             # Text chunks (compact, columnar)
            ├── embeddings.jsonl           # 768D vectors (debug/inspection)
            ├── embeddings.parquet         # 768D vectors (import-optimized)
            └── source.pdf                 # Original PDF (optional, flag-controlled)
```

### Schema: `metadata.json`

```json
{
  "exportVersion": "1.0",
  "exportedAt": "2026-03-28T14:00:00Z",
  "embeddingModel": "nomic-embed-text",
  "embeddingDimensions": 768,
  "document": {
    "pdfDocumentId": "guid",
    "gameId": "guid",
    "gameSlug": "ark-nova",
    "gameName": "Ark Nova",
    "fileName": "ark-nova_rulebook.pdf",
    "language": "en",
    "languageConfidence": 0.98,
    "documentCategory": "Rulebook",
    "versionLabel": "1st Edition",
    "licenseType": 0,
    "pageCount": 24,
    "characterCount": 45000,
    "contentHash": "sha256:...",
    "fileSizeBytes": 12345678
  },
  "stats": {
    "totalChunks": 87,
    "totalEmbeddings": 87,
    "chunkSizeAvg": 512,
    "processingDurationMs": 34000
  }
}
```

### Schema: `chunks.jsonl` (one line per chunk)

```json
{"chunkIndex": 0, "pageNumber": 1, "content": "Ark Nova is a game about...", "characterCount": 512}
```

### Schema: `embeddings.jsonl` (one line per embedding)

```json
{"chunkIndex": 0, "pageNumber": 1, "textContent": "Ark Nova is a game about...", "vector": [0.123, -0.456, ...], "model": "nomic-embed-text"}
```

### Parquet Schema

Same fields as JSONL. Vectors stored as `FixedSizeList<Float>[768]`.

### Schema: `manifest.json` (global index)

```json
{
  "exportVersion": "1.0",
  "exportedAt": "2026-03-28T14:00:00Z",
  "totalDocuments": 126,
  "totalChunks": 10500,
  "totalEmbeddings": 10500,
  "embeddingModel": "nomic-embed-text",
  "documents": [
    {
      "pdfDocumentId": "guid",
      "gameSlug": "ark-nova",
      "gameName": "Ark Nova",
      "path": "games/ark-nova/{id}/",
      "chunks": 87,
      "language": "en"
    }
  ]
}
```

## C# Commands & Architecture

### Positioning

All commands live in `Administration/Application/Commands/` alongside `MigrateStorageCommand`.

### Commands

#### `ExportRagDataCommand` (Full Export)

```csharp
public record ExportRagDataCommand : IRequest<ExportRagDataResult>
{
    public bool IncludeSourcePdf { get; init; } = false;
    public bool DryRun { get; init; } = false;
    public string? GameIdFilter { get; init; }
}
```

Handler flow:
1. Query all `VectorDocument` with state `Ready` (join `PdfDocument`, `Game`)
2. For each document, in batches of 10:
   - Read `TextChunkEntity` from DB
   - Read `PgVectorEmbeddingEntity` (with EF Core vector mapping)
   - Serialize to JSONL + Parquet
   - Generate `metadata.json`
   - (Optional) Copy source PDF from S3
3. Generate global `manifest.json`
4. Upload bundle to S3 backup bucket: `s3://meepleai-backups/rag-exports/{timestamp}/`
5. Save local copy in `data/rag-exports/{timestamp}/`
6. Return `ExportRagDataResult` with statistics

#### `IncrementalRagBackupCommand` (Single Document)

```csharp
public record IncrementalRagBackupCommand : IRequest<IncrementalRagBackupResult>
{
    public Guid PdfDocumentId { get; init; }
}
```

Handler flow:
1. Read single document + chunks + embeddings
2. Serialize bundle to `games/{slug}/{id}/`
3. Upload to S3 at `rag-exports/latest/games/{slug}/{id}/`
4. Update `manifest.json` incrementally (read-modify-write)

#### `ImportRagDataCommand` (Production Import)

```csharp
public record ImportRagDataCommand : IRequest<ImportRagDataResult>
{
    public string SnapshotPath { get; init; }
    public bool ReEmbed { get; init; } = false;
}
```

Handler flow:
1. Read `manifest.json` from path
2. For each document:
   - Read `metadata.json` — check `embeddingModel` match
   - If match: import vectors from `embeddings.parquet` directly
   - If mismatch or `ReEmbed=true`: read `chunks.parquet`, re-generate embeddings
   - Match game by **slug** (automatic, no manual ID mapping needed)
   - Create `PdfDocumentEntity` + `VectorDocumentEntity` + chunks/embeddings via `IVectorStoreAdapter`
   - (Optional) Copy `source.pdf` to production storage
3. Skip duplicates by `contentHash` match
4. Generate import report

#### `DownloadRagSnapshotQuery` (Admin Download)

```csharp
public record DownloadRagSnapshotQuery : IRequest<DownloadRagSnapshotResult>
{
    public string? SnapshotId { get; init; }  // null = list available snapshots
    public string? GameSlug { get; init; }    // null = entire snapshot
}
```

Returns: list of snapshots with dates/sizes, or pre-signed S3 URL for download.

### Event Handler

```csharp
public class RagBackupOnIndexedEventHandler
    : INotificationHandler<VectorDocumentIndexedEvent>
{
    // Triggers IncrementalRagBackupCommand after each successful indexing
}
```

### Admin Endpoints

```
POST /admin/rag-backup/export              -> ExportRagDataCommand (full)
POST /admin/rag-backup/export?dryRun=true  -> Preview
POST /admin/rag-backup/export?gameId={id}  -> Single game
POST /admin/rag-backup/import              -> ImportRagDataCommand
POST /admin/rag-backup/import?reEmbed=true -> With re-embedding
GET  /admin/rag-backup/snapshots           -> List snapshots
GET  /admin/rag-backup/snapshots/{id}      -> Download (pre-signed URL)
GET  /admin/rag-backup/snapshots/latest    -> Latest snapshot
```

## Scheduling & Backup Lifecycle

### Hybrid Strategy: Event-Driven + Weekly Snapshot

**Real-time (event-driven)**:
- `RagBackupOnIndexedEventHandler` listens to `VectorDocumentIndexedEvent`
- Each processed PDF triggers immediate incremental export to `rag-exports/latest/`
- `latest/manifest.json` is always up-to-date

**Weekly snapshot (scheduled)**:
- `RagBackupSchedulerService` (`BackgroundService`) runs full export every Sunday 03:00 UTC
- Saves to `rag-exports/{yyyy-MM-dd}/` (immutable)
- Retention: keeps last 4 snapshots (1 month), deletes older ones

### S3 Layout

```
s3://meepleai-backups/
└── rag-exports/
    ├── latest/                    # Always up-to-date (incremental)
    │   ├── manifest.json
    │   └── games/...
    ├── 2026-03-23/                # Weekly snapshot (immutable)
    │   ├── manifest.json
    │   └── games/...
    └── 2026-03-16/
```

### Consistency Check

Weekly snapshot compares `latest/manifest.json` against a fresh export. If counts diverge (document missing from latest), logs warning and includes the missing document. Catches silent event handler failures.

### Scheduler Implementation

`RagBackupSchedulerService` uses `PeriodicTimer` with day/hour check. No external scheduling framework (Hangfire/Quartz) to keep dependencies minimal.

### Make Targets

```makefile
rag-export:          ## Full RAG export (one-shot for go-live)
rag-export-dry:      ## Preview export without executing
rag-backup-status:   ## Status of latest backup
rag-snapshots:       ## List all available snapshots
```

## Import & Portability

### Embedding Model Detection

On import, `metadata.json` declares the `embeddingModel` used. If production uses the same model, vectors are imported directly. If different, only text chunks are imported and re-embedded with the production model.

### Game Matching by Slug

Production games are matched by `gameSlug` from `manifest.json`. No manual Game ID mapping required. If a slug is not found in the target database, the document is skipped with a warning in the import report.

### Vector Store Adapter Pattern

Import writes through `IVectorStoreAdapter`, so the target backend is transparent:

```
IVectorStoreAdapter
├── PgVectorStoreAdapter        (current, pgvector)
├── QdrantVectorStoreAdapter    (future, production)
└── PineconeVectorStoreAdapter  (future, alternative)
```

## EF Core Vector Mapping

Currently `pgvector_embeddings` is accessed via raw SQL in `PgVectorStoreAdapter` — no EF Core entity exists. This spec introduces a new `PgVectorEmbeddingEntity` + `PgVectorEmbeddingEntityConfiguration` to enable typed queries for export/import.

```csharp
// New entity: Infrastructure/Entities/KnowledgeBase/PgVectorEmbeddingEntity.cs
public class PgVectorEmbeddingEntity
{
    public Guid Id { get; set; }
    public Guid VectorDocumentId { get; set; }
    public Guid GameId { get; set; }
    public string TextContent { get; set; }
    public string Model { get; set; }
    public int ChunkIndex { get; set; }
    public int PageNumber { get; set; }
    public float[] Vector { get; set; }  // mapped to vector(768)
}

// In PgVectorEmbeddingEntityConfiguration
builder.ToTable("pgvector_embeddings");
builder.Property(e => e.Vector)
    .HasColumnType("vector(768)")
    .IsRequired();
```

This entity is used **only for read operations** (export/import). The existing `PgVectorStoreAdapter` raw SQL for search is not modified — it remains optimized for similarity queries.

## Configuration & Security

### Backup Storage Secret

New file `infra/secrets/backup-storage.secret`:

```bash
BACKUP_STORAGE_PROVIDER=s3
BACKUP_S3_ENDPOINT=https://xxx.r2.cloudflarestorage.com
BACKUP_S3_ACCESS_KEY=your_backup_key
BACKUP_S3_SECRET_KEY=your_backup_secret
BACKUP_S3_BUCKET_NAME=meepleai-backups
BACKUP_S3_REGION=auto
BACKUP_LOCAL_PATH=data/rag-exports
BACKUP_RETENTION_WEEKS=4
```

Separate from `storage.secret` — backup credentials must not have access to upload bucket and vice versa (least privilege).

### Security

- **Encryption at rest**: AES256 server-side on S3
- **Admin-only access**: All endpoints under `/admin/rag-backup/` require admin session
- **Pre-signed URLs**: Download via temporary URL (1h expiry), no direct streaming
- **No vectors in logs**: Embeddings are never logged (too large, no diagnostic value)
- **Content hash validation**: On import, verify PDF `contentHash` against metadata to detect corruption
- **Duplicate detection**: Import skips documents with matching `contentHash` already in target DB

### Error Handling

- **Export fails mid-way**: Already-exported documents remain valid. `manifest.json` is written atomically only on full completion
- **Event handler fails**: Error logged, does not block processing pipeline. Weekly snapshot recovers
- **S3 unreachable**: Fallback to local path, retry on next attempt
- **Import duplicate**: Skip with warning (match by `contentHash`)

## File Changes

### New Files

```
apps/api/src/Api/
├── BoundedContexts/Administration/Application/Commands/
│   ├── ExportRagData/
│   │   ├── ExportRagDataCommand.cs
│   │   ├── ExportRagDataCommandHandler.cs
│   │   └── ExportRagDataCommandValidator.cs
│   ├── IncrementalRagBackup/
│   │   ├── IncrementalRagBackupCommand.cs
│   │   ├── IncrementalRagBackupCommandHandler.cs
│   │   └── IncrementalRagBackupCommandValidator.cs
│   ├── ImportRagData/
│   │   ├── ImportRagDataCommand.cs
│   │   ├── ImportRagDataCommandHandler.cs
│   │   └── ImportRagDataCommandValidator.cs
│   └── DownloadRagSnapshot/
│       ├── DownloadRagSnapshotQuery.cs
│       └── DownloadRagSnapshotQueryHandler.cs
├── BoundedContexts/Administration/Application/EventHandlers/
│   └── RagBackupOnIndexedEventHandler.cs
├── BoundedContexts/Administration/Application/Services/
│   ├── IRagExportService.cs
│   ├── RagExportService.cs
│   ├── IRagBackupStorageService.cs
│   └── RagBackupStorageService.cs
├── Infrastructure/BackgroundServices/
│   └── RagBackupSchedulerService.cs
├── Routing/
│   └── AdminRagBackupEndpoints.cs
infra/
├── secrets/backup-storage.secret.example
```

### New Infrastructure Files

```
apps/api/src/Api/
├── Infrastructure/Entities/KnowledgeBase/
│   └── PgVectorEmbeddingEntity.cs                 # New: EF Core entity for pgvector_embeddings table
├── Infrastructure/EntityConfigurations/KnowledgeBase/
│   └── PgVectorEmbeddingEntityConfiguration.cs    # New: vector(768) mapping configuration
```

### Modified Files

```
apps/api/src/Api/
├── Infrastructure/MeepleAiDbContext.cs             # Add DbSet<PgVectorEmbeddingEntity>
infra/
├── Makefile                                        # Add rag-export targets
```

### NuGet Dependencies

- `Parquet.Net` — Parquet serialization (MIT license, no native dependencies)

## Testing Strategy

### Unit Tests

- Parquet/JSONL serialization: round-trip (export then import produces identical data)
- Manifest generation: correct counts, valid paths
- Game slug matching: exact match, case insensitive, slug not found
- Retention pruning: keeps 4 snapshots, deletes oldest
- Vector EF Core mapping: `float[]` <-> pgvector round-trip

### Integration Tests (Testcontainers + PostgreSQL)

- `ExportRagDataCommandHandler`: full export with seed data, verify bundle structure
- `IncrementalRagBackupCommand`: single document export, verify manifest updated
- `ImportRagDataCommand`: import from exported bundle, verify DB data matches
- Re-embed flow: import with `ReEmbed=true`, verify chunks are re-processed
- Duplicate detection: import same bundle twice, verify skip

### E2E Tests

- `POST /admin/rag-backup/export?dryRun=true` -> 200 with preview stats
- `GET /admin/rag-backup/snapshots` -> snapshot list
- Full cycle: seed game -> upload PDF -> process -> export -> wipe -> import -> verify RAG search works

## Out of Scope

- Admin UI for managing backups (future)
- Backup of conversation memory / strategy patterns (only RAG documents)
- Archive compression (Parquet files are internally compressed)
- Multi-region S3 replication
