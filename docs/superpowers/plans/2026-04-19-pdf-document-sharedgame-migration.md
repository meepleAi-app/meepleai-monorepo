# PdfDocument + DocumentCollection → SharedGame Full Migration Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rimuovere completamente `PdfDocumentEntity.GameId` e `DocumentCollectionEntity.GameId` (FK legacy verso `games`) e unificare tutte le reference su `SharedGameId` (FK verso `shared_games`), correggendo al passaggio il bug del Mechanic Extractor (status case + cross-FK filter) e disaccoppiando lo storage bucket dal game (bucket per `pdf.Id`).

**Architecture:** Migrazione big-bang:
1. **EF migration** con backfill `SharedGameId ← games.SharedGameId[GameId]` sia su `pdf_documents` che su `document_collections`, DROP delle colonne `GameId` da entrambe le tabelle, DROP FK/indici obsoleti, CREATE indici su `SharedGameId`.
2. **Storage refactor**: bucket key passa da `{gameId}.ToString("N")` a `{pdf.Id}.ToString("N")` — disaccoppiamento strutturale. Script di rebucket per oggetti pre-migration (local `mv` + S3 `CopyObject`).
3. **Entity + consumer refactor**: PdfDocumentEntity + DocumentCollectionEntity + ~30 consumer.

`PrivateGameId` resta invariato (FK ortogonale verso game privati user-owned).

**Decisions carried from spec `2026-04-19-legacy-games-removal-design.md`:**
- Q5=A: cascade delete ovunque → `OnDelete(DeleteBehavior.Cascade)` per tutte le nuove FK a `shared_games`
- Q2=D: hard-cut orphans — PDF senza mapping a `shared_games` perdono il link (non archiviati)
- Q3=A: big-bang release, nessun periodo dual-write

**Review decisions (2026-04-19, post-brainstorm):**
- C1=A: estensione scope a `DocumentCollectionEntity.GameId`
- C2=B: refactor bucket a `pdf.Id` (decouple storage/game)
- C3: `OnDelete(Cascade)` (fix)
- I1: regression test su `VectorDocumentRepository`
- I2: `Join` esplicito invece di subquery correlata in `GetAllPdfsQueryHandler`
- I3: path frontend corretto `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/page.tsx`
- I4: split Task 5 in 5a (4 file) + 5b (5 file) con build intermedio

**Tech Stack:** .NET 9, EF Core 9, PostgreSQL 16, xUnit + Testcontainers, MediatR CQRS.

**Base branch:** `main-dev`

**Working branch:** `feature/pdf-sharedgame-migration`

---

## Pre-flight

Surface area (da grep `pdf.GameId|pdfDoc.GameId|PdfDocument.GameId|collection.GameId`):

**DocumentProcessing BC — commands:**
- `UploadPdfCommandHandler.cs` (2x) + `.Processing.cs` (5x)
- `CompleteChunkedUploadCommandHandler.cs` (2x)
- `IndexPdfCommandHandler.cs` (2x)
- `ExtractPdfTextCommandHandler.cs` (1x)
- `DeletePdfCommandHandler.cs` (1x)
- `CreateDocumentCollectionCommandHandler.cs` (3x — **usa cross-FK check**)
- `AddDocumentToCollectionCommandHandler.cs` (1x)
- `PdfProcessingPipelineService.cs` (6x)

**DocumentProcessing BC — queries:**
- `GetAllPdfsQueryHandler.cs` (filter + projection — **bug Mechanic Extractor**)
- `GetCollectionsByUserQueryHandler.cs` (1x)
- `GetCollectionByIdQueryHandler.cs` (1x)
- `GetCollectionByGameQueryHandler.cs` (1x)
- `Queue/GetProcessingQueueQueryHandler.cs` (2x — già dual-FK)
- `DownloadPdfQueryHandler.cs` (2x)
- `GetPdfPageImageQueryHandler.cs` (1x)

**KnowledgeBase BC:**
- `VectorDocumentRepository.cs` (3x + commenti bridge legacy da rimuovere)
- `GetPdfChunksPreviewQueryHandler.cs` (1x)
- `CopyrightDataProjection.cs` (1x)

**GameManagement BC:**
- `GenerateRuleSpecFromPdfCommandHandler.cs` (1x)
- `GetGamesWithKbQueryHandler.cs` (solo commento)

**Administration BC:**
- `LaunchAdminPdfProcessingCommandHandler.cs` (1x)
- `RagExportService.cs` (1x)
- `AdminStatsService.cs` (1x)

**SharedGameCatalog BC:**
- `ExtractGameMetadataFromPdfByPdfIdQueryHandler.cs` (1x)

**UserLibrary BC (verify):**
- `GetGamePdfsQueryHandler.cs`
- `UserLibraryRepository.cs`
- `GetGameDetailQueryHandler.cs`

**Infrastructure:**
- `Entities/DocumentProcessing/PdfDocumentEntity.cs`
- `Entities/DocumentProcessing/DocumentCollectionEntity.cs`
- `EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs`
- `EntityConfigurations/DocumentProcessing/DocumentCollectionEntityConfiguration.cs`
- `Seeders/Catalog/PdfSeeder.cs`

**Routing / DTO:**
- `Routing/AdminGameWizardEndpoints.cs`
- `Models/Contracts.cs`

**Frontend:**
- `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/page.tsx` — fix status `'Completed'` → `'completed'`

**Storage (rebucket script):**
- File filesystem sotto `apps/api/src/Api/storage/pdfs/{gameId}/{pdfId}.pdf`
- S3/R2 objects sotto `pdfs/{gameId}/{pdfId}.pdf`

---

## Task 1: Spike & backfill SQL audit

**Files:**
- Read-only: DB via `docker exec`

- [ ] **Step 1: Verificare `pdf_documents` mapping**

Run (Git Bash con `MSYS_NO_PATHCONV=1`):
```bash
docker exec meepleai-postgres psql -U meeple -d meepleai -c \
  "SELECT COUNT(*) AS total,
          COUNT(*) FILTER (WHERE \"SharedGameId\" IS NOT NULL) AS with_shared,
          COUNT(*) FILTER (WHERE \"SharedGameId\" IS NULL AND \"GameId\" IS NOT NULL) AS missing_shared,
          COUNT(*) FILTER (WHERE \"SharedGameId\" IS NULL AND \"GameId\" IS NULL AND \"PrivateGameId\" IS NOT NULL) AS only_private,
          COUNT(*) FILTER (WHERE \"SharedGameId\" IS NULL AND \"GameId\" IS NULL AND \"PrivateGameId\" IS NULL) AS orphan
   FROM pdf_documents;"
```

- [ ] **Step 2: Verificare backfill source `pdf_documents`**

```bash
docker exec meepleai-postgres psql -U meeple -d meepleai -c \
  "SELECT COUNT(DISTINCT p.\"GameId\") AS distinct_legacy_games,
          COUNT(DISTINCT p.\"GameId\") FILTER (WHERE g.\"SharedGameId\" IS NOT NULL) AS mappable,
          COUNT(DISTINCT p.\"GameId\") FILTER (WHERE g.\"SharedGameId\" IS NULL) AS unmappable
   FROM pdf_documents p JOIN games g ON g.\"Id\" = p.\"GameId\"
   WHERE p.\"SharedGameId\" IS NULL AND p.\"GameId\" IS NOT NULL;"
```

Expected: `unmappable = 0` (spec D accepta hard-cut).

- [ ] **Step 3: Verificare `document_collections` (nuovo — C1)**

```bash
docker exec meepleai-postgres psql -U meeple -d meepleai -c \
  "SELECT COUNT(*) AS total,
          COUNT(DISTINCT c.\"GameId\") AS distinct_games,
          COUNT(DISTINCT c.\"GameId\") FILTER (WHERE g.\"SharedGameId\" IS NOT NULL) AS mappable,
          COUNT(DISTINCT c.\"GameId\") FILTER (WHERE g.\"SharedGameId\" IS NULL) AS unmappable
   FROM document_collections c LEFT JOIN games g ON g.\"Id\" = c.\"GameId\";"
```

Expected: `unmappable = 0`. Se > 0 → orfani hard-cut come per PDF.

- [ ] **Step 4: Baseline storage bucket**

```bash
ls apps/api/src/Api/storage/pdfs/ 2>/dev/null | wc -l
# oppure, per S3:
# aws s3 ls s3://{bucket}/pdfs/ --summarize
```

Annotare il conteggio di directory (bucket gameId attuali) → input per rebucket script Task 4.

- [ ] **Step 5: Registrare baseline nello spec appendix**

Edit `docs/superpowers/specs/2026-04-19-legacy-games-removal-design.md` aggiungendo sezione `## Migration Baseline (2026-04-19)` con risultati Step 1-4.

- [ ] **Step 6: Commit baseline**

```bash
git checkout -b feature/pdf-sharedgame-migration main-dev
git add docs/superpowers/specs/2026-04-19-legacy-games-removal-design.md
git commit -m "docs(spec): add pdf_documents + document_collections migration baseline"
```

---

## Task 2: EF migration — backfill + drop `GameId` (pdf_documents + document_collections)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Migrations/{timestamp}_DropPdfAndCollectionGameId.cs`
- Create: `apps/api/src/Api/Infrastructure/Migrations/{timestamp}_DropPdfAndCollectionGameId.Designer.cs`
- Modify: `apps/api/src/Api/Infrastructure/Migrations/MeepleDbContextModelSnapshot.cs`

- [ ] **Step 1: Generare migration vuota**

Run (da `apps/api/src/Api/`):
```bash
dotnet ef migrations add DropPdfAndCollectionGameId
```

- [ ] **Step 2: Sostituire `Up()`**

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // ========= pdf_documents =========

    // 1a. Backfill SharedGameId
    migrationBuilder.Sql(@"
        UPDATE pdf_documents p
        SET ""SharedGameId"" = g.""SharedGameId""
        FROM games g
        WHERE p.""GameId"" = g.""Id""
          AND p.""SharedGameId"" IS NULL
          AND g.""SharedGameId"" IS NOT NULL;
    ");

    // 1b. Drop FK + indices + column
    migrationBuilder.DropForeignKey(
        name: "FK_pdf_documents_games_GameId",
        table: "pdf_documents");

    migrationBuilder.DropIndex(
        name: "IX_pdf_documents_GameId_UploadedAt",
        table: "pdf_documents");

    migrationBuilder.DropIndex(
        name: "ix_pdf_documents_content_hash_game_id",
        table: "pdf_documents");

    migrationBuilder.DropColumn(
        name: "GameId",
        table: "pdf_documents");

    // 1c. New indices on SharedGameId
    migrationBuilder.CreateIndex(
        name: "IX_pdf_documents_SharedGameId_UploadedAt",
        table: "pdf_documents",
        columns: new[] { "SharedGameId", "UploadedAt" });

    migrationBuilder.CreateIndex(
        name: "ix_pdf_documents_content_hash_shared_game_id",
        table: "pdf_documents",
        columns: new[] { "ContentHash", "SharedGameId" });

    // ========= document_collections (C1) =========

    // 2a. Add nullable SharedGameId column (backfill target)
    migrationBuilder.AddColumn<Guid>(
        name: "SharedGameId",
        table: "document_collections",
        type: "uuid",
        nullable: true);

    // 2b. Backfill from games.SharedGameId
    migrationBuilder.Sql(@"
        UPDATE document_collections c
        SET ""SharedGameId"" = g.""SharedGameId""
        FROM games g
        WHERE c.""GameId"" = g.""Id""
          AND g.""SharedGameId"" IS NOT NULL;
    ");

    // 2c. Hard-cut orphans (Q2=D): collections without shared_game mapping are deleted
    //     Cascade rule applies — children (PDFs in collection) lose CollectionId (SetNull per PdfDocumentEntityConfiguration).
    migrationBuilder.Sql(@"
        DELETE FROM document_collections
        WHERE ""SharedGameId"" IS NULL;
    ");

    // 2d. Drop FK + index + column
    migrationBuilder.DropForeignKey(
        name: "FK_document_collections_games_GameId",
        table: "document_collections");

    migrationBuilder.DropIndex(
        name: "IX_document_collections_GameId",
        table: "document_collections");

    migrationBuilder.DropColumn(
        name: "GameId",
        table: "document_collections");

    // 2e. Make SharedGameId required (after backfill + orphan cleanup)
    migrationBuilder.AlterColumn<Guid>(
        name: "SharedGameId",
        table: "document_collections",
        type: "uuid",
        nullable: false);

    // 2f. FK + index on SharedGameId (Cascade per spec Q5=A)
    migrationBuilder.CreateIndex(
        name: "IX_document_collections_SharedGameId",
        table: "document_collections",
        column: "SharedGameId");

    migrationBuilder.AddForeignKey(
        name: "FK_document_collections_shared_games_SharedGameId",
        table: "document_collections",
        column: "SharedGameId",
        principalTable: "shared_games",
        principalColumn: "id",
        onDelete: ReferentialAction.Cascade);
}
```

- [ ] **Step 3: Implementare `Down()` no-op safe (rollback lossy)**

```csharp
protected override void Down(MigrationBuilder migrationBuilder)
{
    // WARNING: Down is lossy — GameId values NOT restored. Restore from backup if needed.

    // document_collections
    migrationBuilder.DropForeignKey(
        name: "FK_document_collections_shared_games_SharedGameId",
        table: "document_collections");

    migrationBuilder.DropIndex(
        name: "IX_document_collections_SharedGameId",
        table: "document_collections");

    migrationBuilder.AlterColumn<Guid>(
        name: "SharedGameId",
        table: "document_collections",
        type: "uuid",
        nullable: true);

    migrationBuilder.AddColumn<Guid>(
        name: "GameId",
        table: "document_collections",
        type: "uuid",
        nullable: true);

    migrationBuilder.CreateIndex(
        name: "IX_document_collections_GameId",
        table: "document_collections",
        column: "GameId");

    migrationBuilder.AddForeignKey(
        name: "FK_document_collections_games_GameId",
        table: "document_collections",
        column: "GameId",
        principalTable: "games",
        principalColumn: "Id",
        onDelete: ReferentialAction.Cascade);

    // pdf_documents
    migrationBuilder.DropIndex(
        name: "IX_pdf_documents_SharedGameId_UploadedAt",
        table: "pdf_documents");

    migrationBuilder.DropIndex(
        name: "ix_pdf_documents_content_hash_shared_game_id",
        table: "pdf_documents");

    migrationBuilder.AddColumn<Guid>(
        name: "GameId",
        table: "pdf_documents",
        type: "uuid",
        nullable: true);

    migrationBuilder.CreateIndex(
        name: "IX_pdf_documents_GameId_UploadedAt",
        table: "pdf_documents",
        columns: new[] { "GameId", "UploadedAt" });

    migrationBuilder.CreateIndex(
        name: "ix_pdf_documents_content_hash_game_id",
        table: "pdf_documents",
        columns: new[] { "ContentHash", "GameId" });

    migrationBuilder.AddForeignKey(
        name: "FK_pdf_documents_games_GameId",
        table: "pdf_documents",
        column: "GameId",
        principalTable: "games",
        principalColumn: "Id",
        onDelete: ReferentialAction.Cascade);
}
```

- [ ] **Step 4: Verificare compilazione** (errori attesi su entità — fixati da Task 3)

Run: `dotnet build apps/api/src/Api`

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/*DropPdfAndCollectionGameId*
git commit -m "feat(db): migration to drop pdf_documents+document_collections.GameId with backfill"
```

---

## Task 3: Entities — rimuovere `GameId` da Pdf + DocumentCollection

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs`
- Modify: `apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/DocumentCollectionEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/DocumentCollectionEntityConfiguration.cs`

- [ ] **Step 1: `PdfDocumentEntity` — rimuovere `GameId` + `Game` navigation**

Cancellare:
```csharp
public Guid? GameId { get; set; }
// ...
public GameEntity Game { get; set; } = default!;
```

- [ ] **Step 2: `PdfDocumentEntityConfiguration` — rimuovere config GameId + aggiungere SharedGame FK**

Cancellare:
```csharp
builder.Property(e => e.GameId).IsRequired(false).HasMaxLength(64);
builder.HasOne(e => e.Game).WithMany().HasForeignKey(e => e.GameId)
    .IsRequired(false).OnDelete(DeleteBehavior.Cascade);
builder.HasIndex(e => new { e.GameId, e.UploadedAt });
builder.HasIndex(e => new { e.ContentHash, e.GameId })
    .HasDatabaseName("ix_pdf_documents_content_hash_game_id");
```

Aggiungere:
```csharp
builder.Property(e => e.SharedGameId).IsRequired(false);

builder.HasOne<SharedGameEntity>()
    .WithMany()
    .HasForeignKey(e => e.SharedGameId)
    .IsRequired(false)
    .OnDelete(DeleteBehavior.Cascade);  // Q5=A: cascade ovunque

builder.HasIndex(e => new { e.SharedGameId, e.UploadedAt })
    .HasDatabaseName("IX_pdf_documents_SharedGameId_UploadedAt");

builder.HasIndex(e => new { e.ContentHash, e.SharedGameId })
    .HasDatabaseName("ix_pdf_documents_content_hash_shared_game_id");
```

- [ ] **Step 3: `DocumentCollectionEntity` — sostituire `GameId` con `SharedGameId`**

Edit (riga 13):
```csharp
// BEFORE
public Guid GameId { get; set; }

// AFTER
public Guid SharedGameId { get; set; }
```

Edit (riga 24 — navigation):
```csharp
// BEFORE
public GameEntity Game { get; set; } = default!;

// AFTER
public SharedGameEntity SharedGame { get; set; } = default!;
```

Aggiornare `using` → aggiungere `using Api.Infrastructure.Entities.SharedGameCatalog;`.

- [ ] **Step 4: `DocumentCollectionEntityConfiguration` — rimappare FK**

```csharp
// BEFORE
builder.HasOne(e => e.Game)
    .WithMany()
    .HasForeignKey(e => e.GameId)
    .OnDelete(DeleteBehavior.Cascade);
builder.HasIndex(e => e.GameId);

// AFTER
builder.HasOne(e => e.SharedGame)
    .WithMany()
    .HasForeignKey(e => e.SharedGameId)
    .OnDelete(DeleteBehavior.Cascade);
builder.HasIndex(e => e.SharedGameId);
```

- [ ] **Step 5: Sincronizzare `MeepleAiDbContextModelSnapshot.cs` (reconciliation di Task 2)**

**Context:** Task 2 migration è stata hand-written — il ModelSnapshot NON è stato aggiornato. Qui si riconcilia.

Approach:
1. Run da `apps/api/src/Api/`: `dotnet ef migrations add _SnapshotSync --output-dir Infrastructure/Migrations`
2. Il comando scaffolda una migration e rigenera lo snapshot. La migration `*_SnapshotSync.cs` conterrà le operazioni che EF crede necessarie (drop GameId, add SharedGameId FK…) — duplicate di Task 2.
3. **Inspection**: aprire `*_SnapshotSync.cs`. Verificare che contenga SOLO drop `GameId` / add `SharedGameId` FK + indici su `pdf_documents` e `document_collections` (cioè le stesse operazioni di Task 2).
4. **Delete** il file `*_SnapshotSync.cs` + `*_SnapshotSync.Designer.cs` — NON lo vogliamo eseguire perché Task 2 già copre queste operazioni.
5. **Keep** il nuovo `MeepleAiDbContextModelSnapshot.cs` rigenerato — ora allineato con le entity post-cleanup.

```bash
# From apps/api/src/Api/
dotnet ef migrations add _SnapshotSync --output-dir Infrastructure/Migrations
# Inspect generated file — verify it only drops GameId + adds SharedGameId FK
# Then delete the spurious migration:
rm Infrastructure/Migrations/*_SnapshotSync.cs
rm Infrastructure/Migrations/*_SnapshotSync.Designer.cs
# Snapshot stays updated. Verify diff:
git diff Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs | head -60
```

Se il file `_SnapshotSync` contiene operazioni INASPETTATE (diverse da drop GameId / add SharedGameId), STOP e riportare — significa che c'è un disallineamento extra.

- [ ] **Step 6: Build**

Run: `dotnet build apps/api/src/Api`
Expected: errori di compilazione in consumer (`collection.GameId`, `pdfDoc.GameId`) → fix sequenziale Task 4+. IL BUILD DELLE ENTITY + SNAPSHOT DEVE PASSARE — errori solo nei consumer.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/PdfDocumentEntity.cs \
        apps/api/src/Api/Infrastructure/Entities/DocumentProcessing/DocumentCollectionEntity.cs \
        apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/PdfDocumentEntityConfiguration.cs \
        apps/api/src/Api/Infrastructure/EntityConfigurations/DocumentProcessing/DocumentCollectionEntityConfiguration.cs \
        apps/api/src/Api/Infrastructure/Migrations/MeepleAiDbContextModelSnapshot.cs
git commit -m "refactor(pdf): remove GameId from PdfDocument+DocumentCollection, add SharedGame FK with Cascade"
```

---

## Task 4: Storage bucket refactor (C2=B) + rebucket script

**Context:** Pre-migration, lo storage S3/local usa `{gameId}.ToString("N")` come bucket key. Post-migration passa a `{pdf.Id}.ToString("N")` — disaccoppiamento storage/game, nessuna dipendenza dal keyspace game.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs` (bucket computation)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ExtractPdfTextCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/DownloadPdfQueryHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetPdfPageImageQueryHandler.cs`
- Create: `scripts/rebucket-pdfs.ps1` (local filesystem)
- Create: `scripts/rebucket-pdfs-s3.sh` (S3/R2)

- [ ] **Step 1: Definire helper centralizzato per bucket key**

Aggiungere a `PdfProcessingPipelineService.cs` o creare nuovo `IPdfStorageKeyResolver`:

```csharp
public static class PdfStorageKey
{
    /// <summary>
    /// Bucket key for PDF storage. Uses pdf.Id to decouple from game lifecycle.
    /// Pre-migration PDFs stored under gameId bucket must be rebucket-ed by scripts/rebucket-pdfs.*
    /// </summary>
    public static string ForPdf(Guid pdfId) => pdfId.ToString("N");
}
```

- [ ] **Step 2: Write test rosso — bucket key indipendente da GameId**

```csharp
[Fact]
public void ForPdf_UsesPdfIdNotGameId()
{
    var pdfId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    PdfStorageKey.ForPdf(pdfId).Should().Be("11111111111111111111111111111111");
}
```

Run: `dotnet test --filter "FullyQualifiedName~PdfStorageKeyTests"` → FAIL

- [ ] **Step 3: Implementare helper + aggiornare tutti i call sites**

In ogni file:
```csharp
// BEFORE
(pdfDoc.PrivateGameId ?? pdfDoc.GameId)?.ToString() ?? string.Empty
// AFTER
PdfStorageKey.ForPdf(pdfDoc.Id)
```

Run test: PASS.

- [ ] **Step 4: Creare script rebucket local filesystem `scripts/rebucket-pdfs.ps1`**

```powershell
#!/usr/bin/env pwsh
# Rebucket script: pdf_documents storage from {gameId} to {pdfId} bucket.
# Usage: ./scripts/rebucket-pdfs.ps1 -StorageRoot "apps/api/src/Api/storage/pdfs" -DryRun

param(
    [Parameter(Mandatory=$true)][string]$StorageRoot,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Query DB for all PDFs with current expected bucket (pre-migration = gameId, post-migration = pdfId)
$pdfs = docker exec meepleai-postgres psql -U meeple -d meepleai -t -A -F "," -c `
    "SELECT ""Id""::text, COALESCE(""PrivateGameId""::text, ""SharedGameId""::text) FROM pdf_documents WHERE ""FilePath"" IS NOT NULL;"

$moved = 0; $skipped = 0; $missing = 0

foreach ($row in ($pdfs -split "`n" | Where-Object { $_.Trim() })) {
    $parts = $row -split ","
    $pdfId = $parts[0].Replace("-","")
    $gameId = if ($parts[1]) { $parts[1].Replace("-","") } else { $null }

    if (-not $gameId) { $skipped++; continue }

    $oldPath = Join-Path $StorageRoot "$gameId/$pdfId.pdf"
    $newPath = Join-Path $StorageRoot "$pdfId/$pdfId.pdf"

    if (-not (Test-Path $oldPath)) { $missing++; continue }
    if (Test-Path $newPath) { $skipped++; continue }

    if ($DryRun) {
        Write-Host "[DRY] Move: $oldPath -> $newPath"
    } else {
        New-Item -ItemType Directory -Path (Split-Path $newPath) -Force | Out-Null
        Move-Item $oldPath $newPath
    }
    $moved++
}

Write-Host "Moved: $moved | Skipped: $skipped | Missing: $missing"
```

- [ ] **Step 5: Creare script rebucket S3 `scripts/rebucket-pdfs-s3.sh`**

```bash
#!/usr/bin/env bash
# Rebucket script for S3/R2: copies pdfs/{gameId}/{pdfId}.pdf -> pdfs/{pdfId}/{pdfId}.pdf
# Usage: BUCKET=my-bucket ./scripts/rebucket-pdfs-s3.sh [--dry-run]

set -euo pipefail
DRY_RUN="${1:-}"

docker exec meepleai-postgres psql -U meeple -d meepleai -t -A -F "," \
  -c 'SELECT "Id"::text, COALESCE("PrivateGameId"::text, "SharedGameId"::text) FROM pdf_documents WHERE "FilePath" IS NOT NULL;' \
  | while IFS=, read -r pdfId gameId; do
    [[ -z "$gameId" ]] && continue
    pdfIdN="${pdfId//-/}"
    gameIdN="${gameId//-/}"
    src="s3://${BUCKET}/pdfs/${gameIdN}/${pdfIdN}.pdf"
    dst="s3://${BUCKET}/pdfs/${pdfIdN}/${pdfIdN}.pdf"

    if aws s3 ls "$dst" >/dev/null 2>&1; then continue; fi
    if ! aws s3 ls "$src" >/dev/null 2>&1; then continue; fi

    if [[ "$DRY_RUN" == "--dry-run" ]]; then
        echo "[DRY] $src -> $dst"
    else
        aws s3 cp "$src" "$dst" --only-show-errors
    fi
done

echo "Rebucket complete. Verify then run cleanup (s3 rm) separately."
```

- [ ] **Step 6: Documentare in spec appendix**

Aggiungere a `2026-04-19-legacy-games-removal-design.md`:
```markdown
### Storage Rebucket (sub-project #1, C2=B)

- Bucket key: `{pdf.Id}.ToString("N")` (disaccoppiato da game)
- Script local: `scripts/rebucket-pdfs.ps1`
- Script S3: `scripts/rebucket-pdfs-s3.sh`
- **Run order**: apply migration → run rebucket script → verify count → deploy app
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/ \
        scripts/rebucket-pdfs.ps1 scripts/rebucket-pdfs-s3.sh \
        tests/Api.Tests/BoundedContexts/DocumentProcessing/PdfStorageKeyTests.cs \
        docs/superpowers/specs/2026-04-19-legacy-games-removal-design.md
git commit -m "feat(storage): decouple pdf bucket from gameId (use pdf.Id) + rebucket scripts"
```

---

## Task 5: Fix Mechanic Extractor bug — `GetAllPdfsQueryHandler`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetAllPdfsQueryHandler.cs`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/page.tsx` (I3 — path corretto)
- Create/Modify: `tests/Api.Tests/BoundedContexts/DocumentProcessing/GetAllPdfsQueryHandlerTests.cs`

- [ ] **Step 1: Test rosso — status mapping `Ready` → `"completed"` + filter per `SharedGameId`**

```csharp
[Fact]
public async Task Handle_WhenPdfIsReady_MapsStatusToCompleted_AndFiltersBySharedGameId()
{
    using var ctx = CreateDbContext();
    var sharedGameId = Guid.NewGuid();
    ctx.SharedGames.Add(new SharedGameEntity { Id = sharedGameId, Title = "Test Game" });
    ctx.PdfDocuments.Add(new PdfDocumentEntity
    {
        Id = Guid.NewGuid(),
        SharedGameId = sharedGameId,
        FileName = "test.pdf",
        FilePath = "/tmp/test.pdf",
        UploadedByUserId = Guid.NewGuid(),
        ProcessingState = "Ready"
    });
    await ctx.SaveChangesAsync();

    var handler = new GetAllPdfsQueryHandler(ctx);
    var result = await handler.Handle(
        new GetAllPdfsQuery(Page: 1, PageSize: 10, Status: "completed", GameId: sharedGameId),
        default);

    result.Items.Should().HaveCount(1);
    result.Items[0].Status.Should().Be("completed");
    result.Items[0].GameName.Should().Be("Test Game");
}
```

Run: FAIL.

- [ ] **Step 2: Fix status filter**

```csharp
if (!string.IsNullOrWhiteSpace(query.Status))
{
    pdfsQuery = query.Status.ToLowerInvariant() switch
    {
        "completed" => pdfsQuery.Where(p => p.ProcessingState == "Ready"),
        "failed"    => pdfsQuery.Where(p => p.ProcessingState == "Failed"),
        "pending"   => pdfsQuery.Where(p => p.ProcessingState == "Pending"),
        "processing" => pdfsQuery.Where(p =>
            p.ProcessingState == "Uploading" ||
            p.ProcessingState == "Extracting" ||
            p.ProcessingState == "Chunking" ||
            p.ProcessingState == "Embedding" ||
            p.ProcessingState == "Indexing"),
        _ => pdfsQuery
    };
}
```

- [ ] **Step 3: Fix game filter (usa SharedGameId)**

```csharp
if (query.GameId.HasValue)
{
    pdfsQuery = pdfsQuery.Where(p => p.SharedGameId == query.GameId.Value);
}
```

- [ ] **Step 4: Fix projection — Join esplicito (I2), NO correlated subquery**

```csharp
var results = await (
    from p in pdfsQuery
    join sg in ctx.SharedGames on p.SharedGameId equals sg.Id into sgj
    from sg in sgj.DefaultIfEmpty()
    orderby p.UploadedAt descending
    select new PdfListItemDto
    {
        Id = p.Id,
        GameId = p.SharedGameId,
        GameName = sg != null ? sg.Title : null,
        FileName = p.FileName,
        Status = p.ProcessingState == "Ready" ? "completed"
               : p.ProcessingState == "Failed" ? "failed"
               : p.ProcessingState == "Pending" ? "pending"
               : "processing",
        UploadedAt = p.UploadedAt
        // ... altri campi esistenti
    }
).Skip((query.Page - 1) * query.PageSize)
 .Take(query.PageSize)
 .ToListAsync(ct);
```

- [ ] **Step 5: Run test — PASS**

- [ ] **Step 6: Fix frontend status filter**

Edit `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/page.tsx`:
- Trovare costante o hardcoded `'Completed'` → cambiare in `'completed'` (lowercase, coerente con API contract).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetAllPdfsQueryHandler.cs \
        apps/web/src/app/admin/\(dashboard\)/knowledge-base/mechanic-extractor/page.tsx \
        tests/Api.Tests/BoundedContexts/DocumentProcessing/GetAllPdfsQueryHandlerTests.cs
git commit -m "fix(mechanic-extractor): Ready→completed status, filter+join by SharedGameId"
```

---

## Task 6a: DocumentProcessing commands — batch #1 (upload/chunked/index/extract)

**Files:**
- Modify: `UploadPdfCommandHandler.cs` + `.Processing.cs`
- Modify: `CompleteChunkedUploadCommandHandler.cs`
- Modify: `IndexPdfCommandHandler.cs`
- Modify: `ExtractPdfTextCommandHandler.cs`

Regole sostituzione:
- `pdfDoc.GameId` / `pdf.GameId` → `pdfDoc.SharedGameId` / `pdf.SharedGameId`
- `pdf.PrivateGameId ?? pdf.GameId ?? pdf.SharedGameId` → `pdf.PrivateGameId ?? pdf.SharedGameId`
- Storage bucket: già refactored in Task 4 (`PdfStorageKey.ForPdf(pdf.Id)`) — verificare call sites

- [ ] **Step 1: `UploadPdfCommandHandler.cs` + `.Processing.cs`**

Sostituzioni + verifica che `CreateKbCardEntityLinkSafelyAsync` usi `SharedGameId`.

- [ ] **Step 2: `CompleteChunkedUploadCommandHandler.cs`** — sostituzioni

- [ ] **Step 3: `IndexPdfCommandHandler.cs`** — sostituzioni

- [ ] **Step 4: `ExtractPdfTextCommandHandler.cs`** — sostituzioni

- [ ] **Step 5: Build intermedio**

Run: `dotnet build apps/api/src/Api`
Expected: errori residui solo su file non ancora toccati → OK procedere.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Upload* \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Complete* \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Index* \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Extract*
git commit -m "refactor(pdf): migrate upload/chunked/index/extract commands to SharedGameId"
```

---

## Task 6b: DocumentProcessing commands — batch #2 (delete/collection/pipeline)

**Files:**
- Modify: `DeletePdfCommandHandler.cs`
- Modify: `CreateDocumentCollectionCommandHandler.cs`
- Modify: `AddDocumentToCollectionCommandHandler.cs`
- Modify: `PdfProcessingPipelineService.cs`

- [ ] **Step 1: `DeletePdfCommandHandler.cs`** — `var gameId = pdfDoc.SharedGameId;`

- [ ] **Step 2: `CreateDocumentCollectionCommandHandler.cs`** — 3 riferimenti

```csharp
// BEFORE
if (pdfDoc.GameId != command.GameId) { ... }
collection.GameId = command.GameId;

// AFTER
if (pdfDoc.SharedGameId != command.GameId) { ... }
collection.SharedGameId = command.GameId;
```

Il nome del command field `GameId` resta invariato (contract FE) ma la semantica è `SharedGameId`.

- [ ] **Step 3: `AddDocumentToCollectionCommandHandler.cs`** —

```csharp
// BEFORE
if (pdfDoc.GameId != collection.GameId)
// AFTER
if (pdfDoc.SharedGameId != collection.SharedGameId)
```

- [ ] **Step 4: `PdfProcessingPipelineService.cs`** — 6 righe

Tutte le occorrenze `pdfDoc.GameId` → `pdfDoc.SharedGameId`. Semplificare chain `PrivateGameId ?? GameId ?? SharedGameId` → `PrivateGameId ?? SharedGameId`.

- [ ] **Step 5: Build + test DocumentProcessing**

```bash
dotnet build apps/api/src/Api
dotnet test tests/Api.Tests --filter "BoundedContext=DocumentProcessing&Category=Unit"
```

Expected: PASS (fix eventuali regressioni prima di proseguire).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Delete* \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Create* \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/Add* \
        apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs
git commit -m "refactor(pdf): migrate delete/collection/pipeline to SharedGameId"
```

---

## Task 7: DocumentProcessing — queries refactor

**Files:**
- Modify: `GetCollectionsByUserQueryHandler.cs`
- Modify: `GetCollectionByIdQueryHandler.cs`
- Modify: `GetCollectionByGameQueryHandler.cs`
- Modify: `Queue/GetProcessingQueueQueryHandler.cs`
- Modify: `DownloadPdfQueryHandler.cs`
- Modify: `GetPdfPageImageQueryHandler.cs`

- [ ] **Step 1: Collection queries** — `collection.GameId` → `collection.SharedGameId`; projection DTO `GameId` popolato da `SharedGameId`. Commit.

- [ ] **Step 2: `GetProcessingQueueQueryHandler.cs`** — rimuovere OR cross-FK (righe 52-54):

```csharp
pdfsQuery = pdfsQuery.Where(j => j.PdfDocument.SharedGameId == query.GameId.Value);
```

Rimuovere commento legacy. Commit.

- [ ] **Step 3: `DownloadPdfQueryHandler.cs`** —

```csharp
bool isSharedGamePdf = pdf.SharedGameId != null && pdf.PrivateGameId == null;
// Storage già refactored (Task 4): usa PdfStorageKey.ForPdf(pdf.Id)
await storage.RetrieveAsync(PdfStorageKey.ForPdf(pdf.Id), ct);
```

Commit.

- [ ] **Step 4: `GetPdfPageImageQueryHandler.cs`** —

```csharp
// Usa helper Task 4
var bucket = PdfStorageKey.ForPdf(pdfDoc.Id);
```

Commit.

- [ ] **Step 5: Build + test DocumentProcessing full**

Run: `dotnet test tests/Api.Tests --filter "BoundedContext=DocumentProcessing"`
Expected: PASS.

---

## Task 8: KnowledgeBase — `VectorDocumentRepository` + proiezioni + regression test (I1)

**Files:**
- Modify: `BoundedContexts/KnowledgeBase/Infrastructure/Persistence/VectorDocumentRepository.cs`
- Modify: `BoundedContexts/KnowledgeBase/Application/Queries/GetPdfChunksPreviewQueryHandler.cs`
- Modify: `BoundedContexts/KnowledgeBase/Infrastructure/Projections/CopyrightDataProjection.cs`
- Create/Modify: `tests/Api.Tests/BoundedContexts/KnowledgeBase/VectorDocumentRepositoryTests.cs` (I1)

- [ ] **Step 1: Test regressione `VectorDocumentRepository` (I1)**

Scenario critico: rimuovendo il bridge legacy, dobbiamo garantire che la risoluzione per `sharedGameId` funzioni direttamente e che PDF privati siano ancora trovabili.

```csharp
[Fact]
public async Task GetDocumentsByGameId_WithSharedGameId_ReturnsSharedAndPrivate()
{
    using var ctx = CreateDbContext();
    var sharedGameId = Guid.NewGuid();
    ctx.SharedGames.Add(new SharedGameEntity { Id = sharedGameId, Title = "Test" });

    var sharedPdf = new PdfDocumentEntity
    {
        Id = Guid.NewGuid(),
        SharedGameId = sharedGameId,
        FileName = "shared.pdf",
        FilePath = "/tmp/s.pdf",
        UploadedByUserId = Guid.NewGuid(),
        ProcessingState = "Ready"
    };
    var privatePdf = new PdfDocumentEntity
    {
        Id = Guid.NewGuid(),
        PrivateGameId = sharedGameId,  // coincidenza UUID
        FileName = "private.pdf",
        FilePath = "/tmp/p.pdf",
        UploadedByUserId = Guid.NewGuid(),
        ProcessingState = "Ready"
    };
    ctx.PdfDocuments.AddRange(sharedPdf, privatePdf);
    await ctx.SaveChangesAsync();

    var repo = new VectorDocumentRepository(ctx, Substitute.For<IVectorStore>());
    var result = await repo.GetDocumentIdsByGameAsync(sharedGameId, default);

    result.Should().HaveCount(2);
    result.Should().Contain(new[] { sharedPdf.Id, privatePdf.Id });
}

[Fact]
public async Task GetDocumentsByGameId_NoLegacyBridge_DoesNotResolveViaGamesTable()
{
    // Assicura che la risoluzione NON tocchi più la tabella games
    // (se lo facesse, dopo la rimozione di games.Id sub-project #3 romperebbe).
    using var ctx = CreateDbContext();
    var sharedGameId = Guid.NewGuid();
    ctx.SharedGames.Add(new SharedGameEntity { Id = sharedGameId, Title = "Test" });
    await ctx.SaveChangesAsync();

    var repo = new VectorDocumentRepository(ctx, Substitute.For<IVectorStore>());
    var result = await repo.GetDocumentIdsByGameAsync(sharedGameId, default);

    result.Should().BeEmpty();  // Nessun PDF, nessuna eccezione JOIN su games
}
```

Run: FAIL (bridge legacy ancora presente).

- [ ] **Step 2: Fix `VectorDocumentRepository.cs`**

```csharp
// BEFORE (righe 152, 173-175, 204)
.Where(pdf => pdf.PrivateGameId == gameId || pdf.GameId == gameId)
// + bridge commentato con JOIN su games per risolvere SharedGameId legacy

// AFTER (semantica diretta)
.Where(pdf => pdf.PrivateGameId == sharedGameId || pdf.SharedGameId == sharedGameId)
```

Rimuovere commento bridge (righe 173-175):
```csharp
// REMOVE
// GameEntity.Id (games table) before saving PdfDocument.GameId.
// So PdfDocument.GameId = internalId != sharedGameId.
// Resolve via: GameEntity.SharedGameId == gameId → GameEntity.Id → PdfDocument.GameId.
```

Rimuovere blocco `internalGameIds` resolution (righe ~200-204).

Rinominare parametro `gameId` → `sharedGameId` in firme metodo per chiarezza semantica.

Run test: PASS.

- [ ] **Step 3: `GetPdfChunksPreviewQueryHandler.cs`** — projection `pdfDoc.GameId` → `pdfDoc.SharedGameId`. Commit.

- [ ] **Step 4: `CopyrightDataProjection.cs`** — projection + query internal. Commit.

- [ ] **Step 5: Build + test KnowledgeBase**

Run: `dotnet test tests/Api.Tests --filter "BoundedContext=KnowledgeBase"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/ \
        tests/Api.Tests/BoundedContexts/KnowledgeBase/VectorDocumentRepositoryTests.cs
git commit -m "refactor(kb): migrate VectorDocumentRepository to direct SharedGameId resolution"
```

---

## Task 9: Altri BC (GameManagement, Administration, SharedGameCatalog, UserLibrary)

**Files:** (come Task 8 precedente nel plan originale)
- `GenerateRuleSpecFromPdfCommandHandler.cs`
- `GetGamesWithKbQueryHandler.cs` (solo commento)
- `LaunchAdminPdfProcessingCommandHandler.cs`
- `RagExportService.cs`
- `AdminStatsService.cs`
- `ExtractGameMetadataFromPdfByPdfIdQueryHandler.cs`
- Verify: `GetGamePdfsQueryHandler.cs`, `UserLibraryRepository.cs`, `GetGameDetailQueryHandler.cs`

- [ ] **Step 1: `GenerateRuleSpecFromPdfCommandHandler.cs`** — `pdf.SharedGameId ?? Guid.Empty`. Commit.

- [ ] **Step 2: `GetGamesWithKbQueryHandler.cs`** — aggiornare commento. Commit.

- [ ] **Step 3: `LaunchAdminPdfProcessingCommandHandler.cs`** —

```csharp
p => p.Id == command.PdfDocumentId && p.SharedGameId == resolvedGameId
```

Commit.

- [ ] **Step 4: `RagExportService.cs`** — projection `GameId: pdfDoc.SharedGameId`. Commit.

- [ ] **Step 5: `AdminStatsService.cs`** — `query.Where(pdf => pdf.SharedGameId == gameGuid)`. Commit.

- [ ] **Step 6: `ExtractGameMetadataFromPdfByPdfIdQueryHandler.cs`** — `pdfDoc.SharedGameId.ToString()`. Commit.

- [ ] **Step 7: Verify UserLibrary** — aprire i 3 file; sostituire `pdf.GameId` / `pdfDoc.GameId` se presenti. Commit solo se modificato.

- [ ] **Step 8: Build + test BC toccati**

```bash
dotnet build apps/api/src/Api
dotnet test tests/Api.Tests --filter "BoundedContext=GameManagement|BoundedContext=Administration|BoundedContext=SharedGameCatalog|BoundedContext=UserLibrary"
```

Expected: PASS.

---

## Task 10: Seeders + contracts + routing

**Files:**
- Modify: `Infrastructure/Seeders/Catalog/PdfSeeder.cs`
- Modify: `Models/Contracts.cs` (commento solo)
- Modify/Verify: `Routing/AdminGameWizardEndpoints.cs`

- [ ] **Step 1: `PdfSeeder.cs`** — `GameId = ...` → `SharedGameId = ...` in seed data. Se seeder crea sia legacy game che shared, popolare solo `SharedGameId`. Stesso fix per eventuali `DocumentCollection` seeded. Commit.

- [ ] **Step 2: `Contracts.cs`** — campo DTO `GameId` conservato per retro-compat FE. Aggiungere commento:
```csharp
// Note: populated from PdfDocument.SharedGameId after 2026-04-19 migration (sub-project #1).
public Guid? GameId { get; init; }
```
Commit.

- [ ] **Step 3: `AdminGameWizardEndpoints.cs`** (riga ~112) — verificare che `result.GameId` venga popolato dal `SharedGameId`. Se già così, no change. Commit se modificato.

- [ ] **Step 4: Build + test completo**

Run: `dotnet test tests/Api.Tests --filter "Category=Unit"`
Expected: PASS.

- [ ] **Step 5: Commit**

---

## Task 11: Apply migration + run rebucket script + integration test

**Files:**
- DB state update
- Storage state update (rebucket)

- [ ] **Step 1: Stop dev stack**

Run: `cd infra && make dev-down`

- [ ] **Step 2: Start only DB**

Run: `docker compose --env-file infra/.env -f infra/docker-compose.yml up -d postgres`

- [ ] **Step 3: Apply migration**

Run (da `apps/api/src/Api/`): `dotnet ef database update`
Expected: migration applicata; logs mostrano backfill.

- [ ] **Step 4: Verificare stato DB**

```bash
MSYS_NO_PATHCONV=1 docker exec meepleai-postgres psql -U meeple -d meepleai -c "\d pdf_documents"
MSYS_NO_PATHCONV=1 docker exec meepleai-postgres psql -U meeple -d meepleai -c "\d document_collections"
```

Expected: colonna `GameId` **assente** in entrambe; colonna `SharedGameId` presente con FK a `shared_games`; nuovi indici presenti.

- [ ] **Step 5: Run rebucket script (dry-run poi apply)**

```bash
./scripts/rebucket-pdfs.ps1 -StorageRoot "apps/api/src/Api/storage/pdfs" -DryRun
./scripts/rebucket-pdfs.ps1 -StorageRoot "apps/api/src/Api/storage/pdfs"
```

Expected: tutti i PDF riallocati sotto bucket `pdfId` invece di `gameId`.

- [ ] **Step 6: Start full dev stack**

Run: `cd infra && make dev`

- [ ] **Step 7: Full integration test run**

Run: `dotnet test tests/Api.Tests`
Expected: PASS completo. Fix regressioni prima del merge.

- [ ] **Step 8: Smoke test manuale frontend**

Aprire http://localhost:3000/admin/knowledge-base/mechanic-extractor → verificare lista PDF popolata con status "completed" (prima vuota per bug status case).

Test aggiuntivo: upload nuovo PDF → verifica che il bucket key sia `pdfId` (ispezione `apps/api/src/Api/storage/pdfs/`).

Test download: scaricare un PDF pre-migration (post-rebucket) → verifica che sia raggiungibile.

- [ ] **Step 9: Commit marker**

```bash
git commit --allow-empty -m "test(integration): verify pdf+collection migration + rebucket end-to-end"
```

---

## Task 12: PR + code review + merge + cleanup

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/pdf-sharedgame-migration
```

- [ ] **Step 2: Detect parent branch**

```bash
git config branch.feature/pdf-sharedgame-migration.parent main-dev
```

- [ ] **Step 3: Create PR**

```bash
gh pr create --base main-dev --title "refactor(pdf): drop GameId from PdfDocument+DocumentCollection, migrate to SharedGameId" --body "$(cat <<'EOF'
## Summary
Executes sub-project #1 of spec `2026-04-19-legacy-games-removal-design.md`.

**DB migration:**
- Backfills `pdf_documents.SharedGameId` and `document_collections.SharedGameId` from `games.SharedGameId`
- Drops `GameId` column + FK + indices from both tables
- Hard-cuts orphans per spec decision D (Q2=D)
- New FK `document_collections.SharedGameId → shared_games(id)` with Cascade delete (Q5=A)

**Storage refactor (C2=B):**
- Decouples bucket key from game: `{pdf.Id}.ToString("N")` instead of `{gameId}.ToString("N")`
- Rebucket scripts: `scripts/rebucket-pdfs.ps1` (local), `scripts/rebucket-pdfs-s3.sh` (S3/R2)

**Code refactor (~30 consumers):**
- `DocumentProcessing`, `KnowledgeBase`, `GameManagement`, `Administration`, `SharedGameCatalog`, `UserLibrary`
- Removes legacy bridge in `VectorDocumentRepository` (direct `SharedGameId` resolution)

**Bug fix (Mechanic Extractor):**
- Status mapping `Ready` → `"completed"` in API response
- Filter uses `SharedGameId` (not legacy `GameId`)
- Frontend status constant `'Completed'` → `'completed'`

## Test plan
- [x] Unit tests — `dotnet test --filter Category=Unit`
- [x] Integration tests — `dotnet test`
- [x] Migration applied on dev DB — schema verified
- [x] Rebucket script dry-run + apply verified
- [x] Smoke: Mechanic Extractor list populated with Ready→completed
- [x] Smoke: Upload + Download new PDF (bucket = pdfId)
- [x] Regression: RAG chat works post `VectorDocumentRepository` semantic change

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Dispatch code-reviewer subagent**

Review focus: (1) migration correctness both tables, (2) tutti i `p.GameId` / `collection.GameId` sostituiti, (3) rebucket script safety, (4) test coverage `GetAllPdfsQueryHandler` + `VectorDocumentRepository`, (5) FK/indici orfani.

- [ ] **Step 5: CI green**

Run: `gh pr checks`

- [ ] **Step 6: Merge squash**

```bash
gh pr merge --squash --delete-branch
```

- [ ] **Step 7: Cleanup**

```bash
git checkout main-dev && git pull
git branch -D feature/pdf-sharedgame-migration
git remote prune origin
```

- [ ] **Step 8: Aggiornare tracker issue #2**

Marcare sub-project #1 completato nello spec appendix con data merge.

---

## Post-merge

1. Sub-project #2 (Audit legacy games table) parte con PDF+Collection già disaccoppiati.
2. Sub-project #3 (big-bang FK migration) ha meno dipendenze circolari.
3. Sub-project #4 (workflow unification) non ha più storage bucket pendente.
