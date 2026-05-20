# Game Entity Reset — Consolidate `Game` → `SharedGame` | `PrivateGame`

**Date**: 2026-05-19
**Status**: Draft (post review v2)
**Owner**: TBD
**Tracking issue**: [#1320](https://github.com/meepleAi-app/meepleai-monorepo/issues/1320)
**Type**: Schema reset + domain refactor (no data migration — wipe relational, preserve vectors)

---

## 1. Context

Today three aggregate roots model the same real-world concept ("a board game"):

| Aggregate | BC | Visibility | Purpose |
|---|---|---|---|
| `Game` | GameManagement | internal | Legacy thin wrapper, holds `SharedGameId` FK |
| `SharedGame` | SharedGameCatalog | public | Rich community catalog (mechanics, FAQs, contributors, approval workflow) |
| `PrivateGame` | UserLibrary | public | User-owned snapshot (homebrew, regional, or BGG-imported) |

Field overlap is **~70%** (title, year, players, playtime, image, BGG link, etc.). `Game` is effectively redundant since `SharedGame` superseded it (issue #2373 attempted integration via `SharedGameId`).

### Why now

- **No production data to preserve** beyond PDF embeddings — token cost is already sunk.
- Three aggregates ⇒ triple maintenance, divergent validation rules, growing schema drift.
- Cross-BC references (sessions, KB, FAQs) point to `Game.Id` ambiguously — should be `SharedGame.Id` or `PrivateGame.Id` based on visibility.

### Non-goals

- Migrate or merge existing relational rows (we drop them).
- Re-generate any embedding (token waste). Vectors stay byte-identical.
- Touch storage backend for PDFs (R2/S3/local — already disaccoppiato).

---

## 2. What to preserve vs. drop

### ✅ PRESERVE (3 tables + PDF storage; `__EFMigrationsHistory` schema-only)

| Table | Mode | Reason |
|---|---|---|
| `pgvector_embeddings` | full preserve | The 768d `nomic-embed-text` vectors — **the only asset worth keeping**. Re-embedding costs tokens. Created via raw SQL by `PgVectorStoreAdapter.EnsureTableExistsAsync()` → **survives EF schema reset automatically** (not managed by migrations). |
| `vector_documents` | full preserve | Tracking metadata linking embeddings to PDF documents. |
| `pdf_documents` | full preserve | File metadata (path, sha256, page_count, extracted text). Already FK-free from `games` since migration `20260419155458_DropPdfAndCollectionGameId`. |
| `__EFMigrationsHistory` | **schema-preserve, content-reset** | Table structure kept by Postgres, rows wiped (`DELETE FROM`) so EF treats the new initial migration as the first applied. |
| PDF blob storage | full preserve | R2/S3/local — orthogonal to the relational reset. |

### ❌ DROP (everything else relational + custom enum types)

Rather than enumerate manually (risk of stale list), the **authoritative DROP list is generated at execution time** (see §4 Step 2 prep query). Examples of in-scope tables, non-exhaustive:

- Core: `games`, `shared_games`, `private_games`, `game_sessions`, `game_reviews`, `game_strategies`
- Library: `user_library_entries`, `user_collection_entries`, `wishlist_items`
- Events: `game_night_events`, `game_night_invitations`, `game_night_rsvps`, `game_night_sessions`
- KB / processing: `kb_reindex_jobs`, `text_chunks`, `agent_definitions` (see M-6 note below)
- Catalog: FAQs, mechanics, designers, publishers, categories, contributors, badges, achievements, errata
- Cross-cutting: audit log, entity_links, gamebook_*, photo ingestion tables, etc.

**Custom PostgreSQL enum types** (e.g. `approval_status`, `game_status`, `game_night_status`) are **not** dropped by `DROP TABLE CASCADE` — they linger as orphan types. Step 2 includes an explicit cleanup pass.

> **Rationale for non-enumerated list**: tables change frequently (recent migrations add new ones every week). A snapshot list would go stale. The generated query at execution time guarantees freshness.

### ⚠️ ORPHANING after drop

`pgvector_embeddings.game_id` becomes a dangling Guid (no row in `games` anymore). Two options:

- **Option α** (recommended): keep the column populated with the **old** Guid as a stable identity. After re-seeding new games, run a **re-link script** that updates `game_id` from the mapping CSV (see §4 step 3).
- **Option β**: null out `game_id` post-drop, rely on `vector_document_id → pdf_document_id` chain to recover game identity. Cleaner but loses direct filter; HNSW index on `game_id` becomes useless until re-link.

**Decision**: go with α. Keep old Guids → re-link to new game IDs deterministically.

---

## 3. Target schema (post-reset)

### Aggregates kept

```
SharedGame          (community catalog, approval workflow, rich metadata)
PrivateGame         (user-owned, no approval, BGG-import or manual)
```

### Aggregate eliminated

```
Game                (legacy thin wrapper) — DELETED
```

### Cross-BC reference: `GameRef` discriminated value object

Replace every `Guid GameId` field in dependent BCs (sessions, KB, FAQs, …) with:

```csharp
public sealed record GameRef(GameRefKind Kind, Guid Id)
{
    public static GameRef Shared(Guid id) => new(GameRefKind.Shared, id);
    public static GameRef Private(Guid id) => new(GameRefKind.Private, id);
}

public enum GameRefKind { Shared = 0, Private = 1 }
```

EF mapping: two nullable FK columns + check constraint that exactly one is populated, OR a single Guid + discriminator column. **Recommendation**: discriminator column for query simplicity:

```sql
game_ref_id   uuid    NOT NULL
game_ref_kind smallint NOT NULL  -- 0=Shared, 1=Private
```

> ⚠️ **Trade-off accepted**: the discriminator approach **gives up DB-side referential integrity**. PostgreSQL cannot enforce that `game_ref_id` points to a real row in `shared_games` (when `Kind=Shared`) or `private_games` (when `Kind=Private`). Integrity is enforced at the application layer only.
>
> **Mitigation options** (pick one at implementation time):
> 1. Application-layer validation in command handlers + integration tests covering FK semantics.
> 2. PostgreSQL trigger that re-validates on INSERT/UPDATE (heavier, catches direct SQL writes).
> 3. Materialized "GameRef view" `UNION ALL` over both tables + `FOREIGN KEY ... REFERENCES game_ref_view(id) DEFERRABLE` — not portable.
>
> Default choice: **option 1** (app-layer + tests). Cheapest, sufficient because all writes go through MediatR command handlers.

### Shared Kernel value object `GameCoreData`

Extract the 70% overlap into a single VO used by both aggregates:

```csharp
public sealed record GameCoreData(
    GameTitle Title,
    YearPublished? YearPublished,
    PlayerCount? PlayerCount,
    PlayTime? PlayTime,
    string? Description,
    string? ImageUrl,
    string? ThumbnailUrl,
    int? BggId,
    decimal? ComplexityRating);
```

Both `SharedGame` and `PrivateGame` hold one `GameCoreData` + their specific concerns:
- `SharedGame` adds: contributors, mechanics, FAQs, designers, publishers, categories, approval status, RAG visibility.
- `PrivateGame` adds: `OwnerId`, `Source` (BGG vs manual), `BggSyncedAt`.

### Promotion path

> **Out of scope for this reset** — the promotion API surface is sketched here only to validate that `GameCoreData` enables it cleanly. **Do not implement** as part of #1320; it belongs to a follow-up issue.
>
> ```csharp
> // Future API surface, NOT delivered by this issue:
> SharedGame.CreateFromPrivate(PrivateGame source, Guid promotedBy)
>   → copies CoreData, sets ApprovalStatus.PendingReview, audit trail
> ```

---

## 4. Migration plan

### Step 0 — Pre-flight backup (safety net)

```bash
# Setup: create the backups directory and add to .gitignore (snapshots may contain PII)
mkdir -p infra/backups
echo "infra/backups/*.dump" >> .gitignore
echo "infra/backups/*.csv"  >> .gitignore
# Only the README of infra/backups is committed; actual dump/CSV files stay local.

# Full DB snapshot before drop
pg_dump $DATABASE_URL -Fc -f infra/backups/2026-05-19-pre-game-reset.dump

# Record the vector count for the §5 verification gate
psql $DATABASE_URL -c "SELECT COUNT(*) FROM pgvector_embeddings;" \
  > infra/backups/2026-05-19-vector-count-pre.txt
```

Required gate: backup file exists and is non-empty before any drop. Rollback procedure (Step 6) **rehearsed** at least once.

### Step 1 — Export mapping CSV (covers all three game tables)

Before dropping anything, materialize the bridge between old and future world. **Critical**: `vector_documents` can reference `Game.Id` **or** `SharedGameId` (cross-BC ref added by issue #4921), and embeddings may also exist for `PrivateGame` rows. The CSV must cover all three sources or we lose re-link information.

```sql
\copy (
  -- Source A: legacy Game references
  SELECT
    'game'                       AS source_kind,
    vd.game_id                   AS old_game_id,
    g.title                      AS game_title,
    g.bgg_id,
    vd.id                        AS vector_document_id,
    vd.pdf_document_id,
    pd.file_name                 AS pdf_filename,
    pd.file_path                 AS pdf_path,
    pd.metadata->>'sha256'       AS pdf_sha256
  FROM vector_documents vd
  LEFT JOIN games g          ON g.id = vd.game_id
  LEFT JOIN pdf_documents pd ON pd.id = vd.pdf_document_id
  WHERE vd.game_id IS NOT NULL

  UNION ALL

  -- Source B: SharedGame references (issue #4921)
  SELECT
    'shared'                     AS source_kind,
    vd.shared_game_id            AS old_game_id,
    sg.title,
    sg.bgg_id,
    vd.id,
    vd.pdf_document_id,
    pd.file_name,
    pd.file_path,
    pd.metadata->>'sha256'
  FROM vector_documents vd
  LEFT JOIN shared_games sg  ON sg.id = vd.shared_game_id
  LEFT JOIN pdf_documents pd ON pd.id = vd.pdf_document_id
  WHERE vd.shared_game_id IS NOT NULL

  UNION ALL

  -- Source C: orphan vectors (game_id present in pgvector_embeddings but no vector_documents row)
  SELECT
    'orphan'                     AS source_kind,
    pe.game_id                   AS old_game_id,
    NULL                         AS game_title,
    NULL                         AS bgg_id,
    NULL                         AS vector_document_id,
    NULL                         AS pdf_document_id,
    NULL, NULL, NULL
  FROM pgvector_embeddings pe
  LEFT JOIN vector_documents vd ON vd.game_id = pe.game_id OR vd.shared_game_id = pe.game_id
  WHERE vd.id IS NULL
  GROUP BY pe.game_id
) TO 'infra/backups/2026-05-19-game-mapping.csv' WITH CSV HEADER;
```

**Pre-export sanity check** (must pass before proceeding):

```sql
-- Count of distinct game_id values in vectors that won't appear in the CSV
SELECT COUNT(DISTINCT pe.game_id)
FROM pgvector_embeddings pe
WHERE pe.game_id NOT IN (
    SELECT vd.game_id FROM vector_documents vd WHERE vd.game_id IS NOT NULL
    UNION
    SELECT vd.shared_game_id FROM vector_documents vd WHERE vd.shared_game_id IS NOT NULL
);
-- Expected: 0. If > 0, the CSV will not be able to re-link those vectors.
-- Either extend the CSV query, or accept the orphaned vectors will be deleted in Step 5.
```

### Step 2 — Drop relational tables, keep vectors

**Order matters**: drop FK constraints from preserve-tables **first**, then drop tables with `RESTRICT` (fail-fast) instead of `CASCADE` (silent propagation), then clean up enum types and migrations history.

#### 2a — Generate the authoritative DROP list

```sql
-- Output: copy to a .sql file, review, then execute in 2c
SELECT 'DROP TABLE IF EXISTS public.' || quote_ident(tablename) || ' CASCADE;'
FROM pg_tables
WHERE schemaname = 'public'
  AND tablename NOT IN (
    'pgvector_embeddings',
    'vector_documents',
    'pdf_documents',
    '__EFMigrationsHistory'
  )
ORDER BY tablename;
```

Review the generated list manually — abort if anything in the preserve list appears, or if unexpected tables are missing.

#### 2b — Drop FK constraints on preserve-tables (before any DROP TABLE)

```sql
BEGIN;

-- Strip outbound FKs from vector_documents → games / shared_games / private_games
-- (Their target tables will be dropped in 2c. Constraint names may vary — discover via:
--   SELECT conname FROM pg_constraint WHERE conrelid = 'vector_documents'::regclass;)
ALTER TABLE vector_documents DROP CONSTRAINT IF EXISTS fk_vector_documents_games;
ALTER TABLE vector_documents DROP CONSTRAINT IF EXISTS fk_vector_documents_shared_games;
ALTER TABLE vector_documents DROP CONSTRAINT IF EXISTS fk_vector_documents_private_games;

-- pdf_documents has no FK to games as of migration 20260419155458, but verify:
SELECT conname FROM pg_constraint WHERE conrelid = 'pdf_documents'::regclass;
-- If any FK to games/shared_games/private_games appears, drop it here.

-- pgvector_embeddings is created by raw SQL (PgVectorStoreAdapter) — typically no FK.
-- Verify and drop any that exist:
SELECT conname FROM pg_constraint WHERE conrelid = 'pgvector_embeddings'::regclass;

COMMIT;
```

#### 2c — Drop everything else (use the list from 2a)

```sql
BEGIN;

-- Paste the output of 2a here, e.g.:
DROP TABLE IF EXISTS public.games CASCADE;
DROP TABLE IF EXISTS public.shared_games CASCADE;
DROP TABLE IF EXISTS public.private_games CASCADE;
-- ... (all tables from 2a)

-- Drop orphan custom enum types (not removed by DROP TABLE CASCADE)
DO $$
DECLARE
  t text;
BEGIN
  FOR t IN
    SELECT typname FROM pg_type
    WHERE typtype = 'e'
      AND typnamespace = (SELECT oid FROM pg_namespace WHERE nspname = 'public')
  LOOP
    EXECUTE 'DROP TYPE IF EXISTS public.' || quote_ident(t) || ' CASCADE';
  END LOOP;
END $$;

-- Reset EF migrations history: keep table schema, wipe rows
-- (EF will re-apply the new initial migration as if first run)
DELETE FROM "__EFMigrationsHistory";

COMMIT;
```

#### 2d — Post-drop verification

```sql
-- Preserve list must still exist
SELECT tablename FROM pg_tables
WHERE schemaname='public'
  AND tablename IN ('pgvector_embeddings', 'vector_documents', 'pdf_documents', '__EFMigrationsHistory');
-- Expected: 4 rows

-- Vector count unchanged
SELECT COUNT(*) FROM pgvector_embeddings;
-- Expected: identical to pre-drop count (record this number in §0 backup notes)
```

### Step 3 — Apply new EF migrations

Code-side work:

1. Delete `BoundedContexts/GameManagement/Domain/Entities/Game.cs` + all references.
2. Implement `GameCoreData` VO in SharedKernel.
3. Refactor `SharedGame` and `PrivateGame` to hold `GameCoreData`.
4. Introduce `GameRef` discriminator everywhere `Game.Id` was referenced (sessions, KB, FAQs, agent definitions, …).
5. Generate fresh initial migration `dotnet ef migrations add ResetGameSchema_Issue1320`.
6. `dotnet ef database update` → rebuilds dropped tables with new shape.

### Step 4 — Re-link vectors to new IDs

After re-creating games (via seed or fresh upload), build a one-shot staging table that bridges old Guids → new Guids, then bulk-update.

#### 4a — Promotion decision rule

Every row in the mapping CSV becomes a `SharedGame` by default. Rationale: the legacy `Game` aggregate had no `OwnerId` concept, so it cannot be mapped to `PrivateGame` (which requires ownership). Explicit re-classification to `PrivateGame` is a manual post-step if needed.

> **Out of scope**: detecting which games should become `PrivateGame` based on heuristics (e.g. RAG visibility flag). Defer to a follow-up.

#### 4b — Populate staging table

```sql
CREATE TABLE staging_game_mapping (
  old_game_id        uuid PRIMARY KEY,    -- from CSV
  game_title         text,                -- from CSV (nullable for 'orphan' rows)
  pdf_sha256         text,                -- from CSV
  new_kind           text NOT NULL,       -- 'shared' (default) or 'private'
  new_game_id        uuid                 -- assigned after re-seed
);

-- Load CSV
\copy staging_game_mapping (old_game_id, game_title, pdf_sha256, new_kind, new_game_id)
  FROM 'infra/backups/2026-05-19-game-mapping-staging.csv' WITH CSV HEADER;
-- The staging CSV is derived from 2026-05-19-game-mapping.csv by:
--   (a) collapsing UNION sources by old_game_id
--   (b) defaulting new_kind='shared'
--   (c) leaving new_game_id NULL initially
```

#### 4c — Re-seed and assign new IDs

For each row with `new_game_id IS NULL`, run the re-seed (manual or scripted via admin API) that creates the corresponding `SharedGame`. Capture the new Guid and update the staging row:

```sql
UPDATE staging_game_mapping
SET new_game_id = '<new-shared-game-guid>'
WHERE old_game_id = '<old-guid>';
```

Gate: `SELECT COUNT(*) FROM staging_game_mapping WHERE new_game_id IS NULL` must return **0** before 4d.

#### 4d — Bulk re-link vectors (idempotent)

```sql
BEGIN;

UPDATE pgvector_embeddings pe
SET game_id = m.new_game_id
FROM staging_game_mapping m
WHERE pe.game_id = m.old_game_id
  AND m.new_kind = 'shared'
  AND pe.game_id <> m.new_game_id;  -- idempotency: skip already-updated rows

UPDATE vector_documents vd
SET shared_game_id = m.new_game_id, game_id = NULL
FROM staging_game_mapping m
WHERE vd.game_id = m.old_game_id
  AND m.new_kind = 'shared';

COMMIT;
```

#### 4e — Cleanup

```sql
DROP TABLE staging_game_mapping;
```

### Step 5 — Verification gates

- [ ] `SELECT COUNT(*) FROM pgvector_embeddings WHERE game_id NOT IN (SELECT id FROM shared_games UNION SELECT id FROM private_games)` returns **0**
- [ ] `SELECT COUNT(*) FROM pgvector_embeddings` is identical pre/post (no vector lost) — value recorded in §0 backup notes
- [ ] HNSW index on `pgvector_embeddings.vector` is `valid`: `SELECT indexname, indisvalid FROM pg_indexes JOIN pg_class ON pg_class.relname = indexname JOIN pg_index USING (indexrelid) WHERE indexname LIKE '%pgvector_embeddings%';`
- [ ] Sample RAG query returns expected chunks for known game
- [ ] No `Game` references remain (`grep -r "BoundedContexts.GameManagement.Domain.Entities.Game" apps/` returns empty)
- [ ] All `vector_documents` rows have `shared_game_id` populated and `game_id` NULL

### Step 6 — Rollback procedure

If verification gates fail OR the code refactor (Step 3) produces unrecoverable issues, restore from the pre-flight snapshot:

```bash
# 1. Stop the API and any service holding DB connections
docker compose -f infra/docker-compose.yml stop api

# 2. Drop the current (broken) DB
psql $DATABASE_URL_ADMIN -c "DROP DATABASE meepleai_dev;"
psql $DATABASE_URL_ADMIN -c "CREATE DATABASE meepleai_dev;"

# 3. Restore from snapshot
pg_restore -d $DATABASE_URL --no-owner --no-acl \
  infra/backups/2026-05-19-pre-game-reset.dump

# 4. Verify table count matches pre-reset state
psql $DATABASE_URL -c "SELECT COUNT(*) FROM pg_tables WHERE schemaname='public';"

# 5. Roll back code changes (revert PR / checkout main-dev)
git checkout main-dev
```

Required before declaring success: rollback **rehearsed** at least once on a non-prod environment before executing Step 2 on the real DB.

---

## 5. Decisions log

| # | Decision | Choice | Rationale |
|---|---|---|---|
| D-1 | Eliminate `Game` aggregate? | **Yes** | Legacy wrapper, ~70% overlap with `SharedGame`, no migration cost now |
| D-2 | Keep `SharedGame` vs `PrivateGame` split? | **Yes** | Different invariants: approval workflow vs ownership-only |
| D-3 | Cross-BC reference style | `GameRef` discriminator | Explicit, single field, queryable |
| D-4 | Vector store engine | **Stay on pgvector** | Already integrated, HNSW index built, no reason to migrate to Qdrant/AgentDB |
| D-5 | Backup strategy pre-drop | `pg_dump -Fc` snapshot | Cheap insurance, restorable in minutes |
| D-6 | Orphan strategy | **α** (keep old Guid + re-link) | Preserves filter index, deterministic remap |

---

## 6. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| HNSW index invalidated by CASCADE drop on referenced table | HIGH | Verify `pgvector_embeddings` has no FK to `games` before drop; if so, rebuild HNSW after re-link |
| Mapping CSV missing rows (some embeddings not linked to a game) | MEDIUM | Pre-check: `SELECT COUNT(*) FROM pgvector_embeddings WHERE game_id NOT IN (SELECT id FROM games)` |
| `vector_documents.game_id` FK fails during drop | LOW | Step 2.2 explicitly drops the constraint |
| Re-link script run twice → double-update | LOW | Make script idempotent (`WHERE pe.game_id = m.old_game_id` only matches once) |
| EF migration history reset causes drift with code | MEDIUM | Step 3.5 regenerates initial migration; CI must pass on fresh DB |

---

## 7. Definition of Done

- [ ] Pre-drop backup stored locally in `infra/backups/` (gitignored; verified non-empty)
- [ ] Mapping CSV exported locally (gitignored; sanity-check query returned 0)
- [ ] Rollback procedure (Step 6) rehearsed on a disposable DB at least once
- [ ] All references to `Game` aggregate removed from codebase (grep returns empty)
- [ ] `GameCoreData` VO + `GameRef` discriminator implemented in SharedKernel
- [ ] `SharedGame` and `PrivateGame` refactored to use `GameCoreData`
- [ ] Application-layer validation enforces `GameRef.Kind ↔ table` consistency (covered by integration tests)
- [ ] Fresh EF migration `ResetGameSchema_Issue1320` applied successfully
- [ ] All 6 verification gates (§4 step 5) pass
- [ ] Integration test: end-to-end PDF upload → embed → query returns correct chunks
- [ ] Documentation updated in `CLAUDE.md` (remove `Game` from bounded context table)
- [ ] PR merged to `main-dev` (auto-delete branch is enabled at repo level)

---

## 8. Out of scope (deferred)

- Promotion workflow `PrivateGame.PromoteToShared()` — separate issue
- Replacing `nomic-embed-text` (768d) with a different model — would force re-embedding, exactly what we're avoiding
- Frontend changes (TypeScript types, UI for `GameRef`) — separate follow-up issue
- Heuristic re-classification of legacy `Game` rows as `PrivateGame` vs `SharedGame` (everything defaults to `SharedGame` per §4a)
- **AgentDefinition redesign (issue #4228)**: `agent_definitions` table is **dropped** by Step 2 and the entity is re-implemented from scratch as part of #4228, which depends on this reset landing first. No data preservation for agent definitions in this reset.

---

## 9. Implementation phases

The work is split into **3 sequential phases**, each a separate PR with its own review gate:

| Phase | Scope | Risk | Reversible? | PR target |
|---|---|---|---|---|
| **Phase 1** — Backup infrastructure | Scripts, gitignore setup, rollback rehearsal on disposable DB. No production touch. | LOW | Fully | `main-dev` |
| **Phase 2** — Code refactor (big bang) | Delete `Game`, add `GameCoreData` + `GameRef`, update all ~40 consumer files in one PR. Build red until complete. | HIGH | Via revert | `main-dev` |
| **Phase 3** — Execute reset | Run Phase 1 scripts on dev → staging → prod; apply Phase 2 migration; re-link vectors; verify gates. | HIGH | Via Phase 1 rollback | Coordinated deploy |

Each phase has its own implementation plan in `docs/superpowers/plans/`.

## 10. Environment scope

The reset targets **all three environments** sequentially:

1. **Dev (local Docker)**: first execution, full rehearsal of Phase 3 procedure
2. **Staging** (server `meepleai-staging`): second execution after dev sign-off
3. **Production** (server `meepleai-prod`): third execution after staging sign-off

Each environment requires its own pre-flight backup (`pg_dump`) and its own rollback rehearsal. The mapping CSV from Step 1 is environment-specific (different game IDs in each).

**Deploy sequence per environment**: pause traffic → backup → run reset SQL (Steps 1-2) → deploy Phase 2 code (which carries the new EF migration) → re-link vectors (Step 4) → verify gates (Step 5) → resume traffic.

## 11. Refactor strategy: big bang

Phase 2 uses a **big bang rip-and-replace** approach: in a single PR, delete `Game` aggregate and update all ~40 consumer files to use `SharedGame` directly (via `GameRef` or specific aggregate references depending on context).

**Justification**:
- No production data lock-in: schema is being reset anyway, no migration debt to manage in parallel
- A deprecated `[Obsolete]` shim would still require touching every consumer eventually
- Build red during refactor is acceptable because Phase 2 lands as a single PR
- CI catches missing updates before merge

**Tactical mitigations during the big bang**:
- Start refactor from the leaves (repositories, queries) and work upward to handlers and routing
- Land Phase 2 PR with all consumer updates in one commit; do not split — that would extend the build-red window
- Phase 2 PR is gated by green CI on `apps/api/**` + `tests/Api.Tests/**`
- Pair-program / co-review the PR — single reviewer cannot reliably catch a 40-file refactor

**Known consumer surface** (preliminary inventory — full list materialized in Phase 2 plan):
- `GameManagement` BC: 30+ files (repositories, handlers, mappers, queries, event handlers)
- `KnowledgeBase` BC: AskArbiterCommandHandler, LinkExistingKbToGameCommandHandler, ArbitroAgentService, GetKbReadiness, GetKnowledgeBaseStatus, GoldenDatasetLoader
- `DocumentProcessing` BC: AddRulebookCommandHandler, UploadPdfCommandHandler, PdfGameIdResolver, PdfDocumentRepository
- `Administration` BC: GameWizard, ImportRagData, GetGameKbStatuses, ReportGeneratorService.*
- `SharedGameCatalog` BC: BulkImportGamesCommandHandler/Validator
- `SessionTracking` BC: SessionTrackingServiceExtensions
- `Infrastructure`: MeepleAiDbContext (DbSet<Game>)

## 12. References

- Spec-panel review (this session, 2026-05-19): identified `Game` as redundant
- Issue #2373: `Game.SharedGameId` integration (incomplete consolidation)
- Issue #3481: `ApprovalStatus` publication workflow
- Issue #3662: `PrivateGame` introduction
- Issue #4228: SharedGame/PrivateGame → AgentDefinition relationship
- Migration `20260419155458_DropPdfAndCollectionGameId`: PDF/game decoupling precedent
- Phase plans: `docs/superpowers/plans/2026-05-19-game-entity-reset-phase1.md` (others added per phase)
