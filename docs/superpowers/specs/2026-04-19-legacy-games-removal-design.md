# Legacy Games Table Removal — Design Spec

**Date**: 2026-04-19
**Status**: Design approved
**Author**: Claude + user collaboration (brainstorming session)
**Related issues**: TBD (creare issue GitHub parent + 4 child per i sotto-progetti)

## Context

L'indagine sul bug del Mechanic Extractor ha rivelato un mismatch architetturale: la tabella legacy `games` coesiste con `shared_games` (catalogo community). I PDF, chat, session, e ~17 altre entità linkano via FK al legacy `games.Id`, ma il frontend più recente popola i dropdown da `shared_games`. I due namespace UUID non si intersecano direttamente — solo via la colonna opzionale `games.SharedGameId`.

Il legacy `games` table ha anche semantica propria: conserva il workflow di approval (`ApprovalStatus`, `IsPublished`, `PublishedAt`) per game submission pre-pubblicazione in community.

**Decisione utente**: eliminare completamente il legacy `games` table, unificando il workflow in `shared_games`.

## Goals

- Eliminare `games` table dal DB
- Unificare il workflow di approval in `shared_games`
- Ripuntare tutte le FK (~20 entità) a `shared_games.id`
- Sbloccare il Mechanic Extractor per testing manuale come primo tassello concreto

## Non-goals

- Preservare dati orfani (hard cut = cascade delete)
- Rilascio zero-downtime (big-bang con maintenance window accettabile)
- Ristrutturazione schema `shared_games` oltre le colonne workflow aggiuntive

## Blast radius

- **46 file backend** referenziano legacy `games` (entità, handler, repository, seeder)
- **32 file** toccano `shared_games` (molti già allineati)
- **3 seeder** (`GameSeeder`, `PdfSeeder`, `CatalogSeeder`)
- **3 manifest yml** (`dev.yml`, `staging.yml`, `prod.yml`)
- **1 migration** `20260410113220_Initial` è il punto di partenza della catena

## Design decisions

### D1 — Workflow approval: unificato in `shared_games` (Q1-A)
`ApprovalStatus`, `IsPublished` (computed), `PublishedAt` si spostano da `games` a `shared_games`. Tutti i game (anche draft) vivono in `shared_games`; filtri per status sostituiscono la separazione di tabella.

### D2 — Orfani legacy: hard delete (Q2-D)
Legacy `games` senza `SharedGameId` popolato vengono eliminati via cascade delete. Entità dipendenti (PDF, chat, session, ecc.) che puntano a orfani vengono eliminate insieme. Nessun archivio.

### D3 — Release strategy: big-bang (Q3-A)
Una singola migration + PR per ogni sotto-progetto, non sprint-per-sprint. Accettabile maintenance window per staging/prod.

### D4 — Visibility Draft: per-ruolo (Q4-B)
Draft/PendingReview/Rejected visibili solo a `CreatedBy` + Admin. Query filter globale su `SharedGameEntity`:
```csharp
g => g.ApprovalStatus == Approved
     || g.CreatedBy == currentUserId
     || currentUserIsAdmin
```

### D5 — Cascade policy: cascade ovunque (Q5-A)
Quando legacy games orfani vengono eliminati, `ON DELETE CASCADE` elimina tutte le entità dipendenti (PDF Ready incluse, chat con messaggi, session user-owned). Coerente con hard cut.

### D6 — Quick fix scope: anticipa migrazione PDF (Q6-B)
Il quick fix per sbloccare il Mechanic Extractor NON è un hack throw-away, ma il primo tassello concreto del refactor: migra `PdfDocumentEntity.GameId → SharedGameId`.

## Decomposition — 4 sub-projects

Ogni sotto-progetto è un PR merge-ready isolato. Ordine: #1 → #2 → #3 → #4.

### Sub-project #1 — Quick fix Mechanic Extractor (PDF FK migration)

**Goal**: sbloccare test manuale + primo tassello del refactor.

**Scope**:
- Migration EF Core: rinomina `pdf_documents.GameId → SharedGameId`
- Backfill SQL: `UPDATE pdf_documents SET SharedGameId = g.SharedGameId FROM games g WHERE g.Id = pdf_documents.GameId`
- Cascade delete orfani: `DELETE FROM pdf_documents WHERE SharedGameId IS NULL` (cascades: `text_chunks`, `vector_documents`, `chunked_upload_sessions`)
- Codice C# aggiornato:
  - `PdfDocumentEntity` (rinomina property + FK config)
  - `GetAllPdfsQueryHandler` (filtro per `SharedGameId`)
  - `UploadPdfCommandHandler` (crea PDF con `SharedGameId`)
  - `GetKbReadinessQueryHandler`
  - Qualsiasi query/repository che proietta `p.GameId`
- Frontend: fix casing bug `'Completed' → 'completed'` già editato in `mechanic-extractor/page.tsx:97`, da committare
- Test: unit per handler + integration upload→query + manual E2E Mechanic Extractor browser

**Deliverable**: PR separato, Mechanic Extractor funzionante.

### Sub-project #2 — Audit & pre-flight

**Goal**: quantificare data loss previsto + proposta soglia bloccante.

**Scope**:
- Script SQL in `scripts/migrations/2026-04-19-audit-legacy-games.sql` che per ogni FK a `games.Id` conta:
  - Record totali
  - Record con game NON orfano (mantenuti)
  - Record con game orfano (cascade-deleted in #3)
- Output report: `docs/migrations/2026-04-19-legacy-games-audit.md`
- Soglia: se >10% di chat/session user-owned verrebbero eliminati, flag bloccante + decisione esplicita utente richiesta prima di #3

**Deliverable**: solo documentazione + SQL scripts, no codice production.

### Sub-project #3 — Big-bang FK migration

**Goal**: rinominare tutte le ~19 FK restanti a `shared_games.id` in un colpo.

**Scope**:
- Singola migration EF Core con:
  - Rename colonne `GameId → SharedGameId` su: `ChatEntity`, `ChatSessionEntity`, `ChatThreadEntity`, `AgentSessionEntity`, `TextChunkEntity` (se presente), `VectorDocumentEntity`, `GameSessionEntity`, `LiveGameSessionEntity`, `PlayRecordEntity`, `GameStrategyEntity`, `GameReviewEntity`, `RuleSpecEntity`, `RuleSpecCommentEntity`, `RaptorSummaryEntity`, `DocumentCollectionEntity`, `RuleConflictFAQEntity`, `GamePhaseTemplateEntity`, `GameNightPlaylistEntity`, `GameEntityRelationEntity`, `GameToolkitEntity`
  - Backfill via hop `games.SharedGameId`
  - Cascade delete orfani per ogni tabella
  - Drop FK vecchie, add FK nuove a `shared_games.id`
- Aggiornamento tutte le entità + handler + repository
- Aggiornamento seeder `GameSeeder`, `PdfSeeder`, `CatalogSeeder` + 3 manifest yml
- Regression: full test suite backend, tolleranza zero ai fallimenti

**Deliverable**: PR grosso ma isolato. Legacy `games` ancora presente (drop in #4).

### Sub-project #4 — Workflow unification + drop table

**Goal**: spostare workflow in `shared_games`, droppare `games`.

**Scope**:
- Migration:
  - Add columns a `shared_games`: `ApprovalStatus int NOT NULL DEFAULT 0`, `IsPublished bool GENERATED ALWAYS AS (ApprovalStatus = 2) STORED`, `PublishedAt timestamptz NULL`, `CreatedBy uuid NULL` (FK a `users.Id`)
  - Indici: btree su `(ApprovalStatus)`, `(CreatedBy)`
  - Backfill: copia valori da legacy `games` dove esiste `SharedGameId`
  - Drop table `games` + cascade
- `ICurrentUserContext` scoped service (verificare se esiste, altrimenti creare)
- Global query filter su `SharedGameEntity` (pattern D4)
- Nuovo endpoint/handler `CreateSharedGameDraft` (sostituisce creazione legacy in upload flow)
- Remove: `GameEntity`, `GameRepository`, `GameSeeder` legacy code paths
- Aggiornamento BC `GameManagement` che usava `GameEntity` → ora usa `SharedGameEntity`

**Deliverable**: PR finale, `games` table rimossa, workflow unificato.

## Data flow (post-migration)

### Upload PDF
```
1. User POST /api/v1/pdfs + gameName
2. UploadPdfCommandHandler:
   - sharedGame = SharedGame.FindByName(name) ?? SharedGame.Create(name, CreatedBy=user, ApprovalStatus=Draft)
   - pdf = PdfDocument.Create(SharedGameId = sharedGame.Id, UploadedBy = user)
3. Processing pipeline invariata (chunks, embeddings keyed by SharedGameId)
```

### Mechanic Extractor
```
1. Admin GET /api/v1/sharedgames → dropdown (query filter applica: ApprovalStatus=Approved OR own Drafts)
2. Admin GET /api/v1/pdfs?sharedGameId={id}&status=completed → PDF Ready associati
3. Admin crea MechanicDraft → Edit → Complete → Activate → RulebookAnalysis
```

### Admin review (nuovo)
```
1. Admin GET /api/v1/sharedgames?status=PendingReview → list pending
2. Admin POST /api/v1/sharedgames/{id}/approve → ApprovalStatus=Approved, PublishedAt=now
```

## Schema changes summary

### Tabella `shared_games` (post #4)

| Column | Type | Notes |
|---|---|---|
| `id` | uuid | PK (invariato) |
| `title` | text | invariato |
| … altre colonne esistenti | … | invariato |
| `ApprovalStatus` | int | **NEW** — 0=Draft, 1=PendingReview, 2=Approved, 3=Rejected |
| `IsPublished` | bool | **NEW** — GENERATED ALWAYS AS (ApprovalStatus = 2) STORED |
| `PublishedAt` | timestamptz NULL | **NEW** |
| `CreatedBy` | uuid NULL | **NEW** — FK a `users.Id` |

Indici nuovi: `idx_shared_games_approval_status`, `idx_shared_games_created_by`

### Tabelle con FK rinominata

`GameId → SharedGameId`, FK punta a `shared_games.id` invece di `games.Id`. Cascade delete per tutte.

Lista entità: PdfDocumentEntity (in #1), ChatEntity, ChatSessionEntity, ChatThreadEntity, AgentSessionEntity, VectorDocumentEntity, GameSessionEntity, LiveGameSessionEntity, PlayRecordEntity, GameStrategyEntity, GameReviewEntity, RuleSpecEntity, RuleSpecCommentEntity, RaptorSummaryEntity, DocumentCollectionEntity, RuleConflictFAQEntity, GamePhaseTemplateEntity, GameNightPlaylistEntity, GameEntityRelationEntity, GameToolkitEntity, TextChunkEntity (se ha FK diretta).

### Tabella `games` (post #4)
**DROPPED**.

## Testing strategy

### #1
- **Unit**: `GetAllPdfsQueryHandler` con filtro `SharedGameId` (happy path + combinazioni di filtri: status, gameId, range)
- **Integration**: upload PDF → query by sharedGameId → verify Ready PDFs listed
- **Manual**: flusso completo Mechanic Extractor end-to-end in browser (obiettivo originale: sbloccare test)

### #2
- Nessun test (solo doc + SQL idempotenti)

### #3
- **Migration test Testcontainers**: seed DB con stato pre-migrazione realistico → apply migration → verify FK rinominate, orfani eliminati, conteggi pre/post
- **Update test esistenti**: ogni test che usa `game.Id` come FK dipendente aggiornato a `SharedGame.id`
- **Regression**: full test suite backend (13k+ test), zero tolleranza

### #4
- **Unit**: query filter `SharedGameEntity` con 3 contesti (anonymous, owner, admin)
- **Integration**: user A crea Draft → user B NON lo vede → admin lo vede → approve → user B lo vede
- **Security**: user A non può vedere/modificare Draft di user B (tentativi diretti via API)

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| Perdita dati user (PDF/chat/session) in cascade | #2 audit con soglie + report pre-migrazione, approval esplicita richiesta se soglia superata |
| Break feature non toccate direttamente | Full test suite + smoke test staging prima di prod |
| Query filter perf degradation | Index su `(ApprovalStatus, CreatedBy)`, benchmark pre/post #4 |
| Seeder rotti | Aggiornati in stessa PR di #3/#4, verificati con `make dev` |
| Rollback complesso big-bang | Backup DB pre-migration, script `rollback.sql` preparato |
| `ICurrentUserContext` non esiste | Verificare in #4; se assente, creare come scoped service iniettato nel DbContext |

## Open questions

- Nessuna. Tutte le decisioni sono state prese durante il brainstorming (Q1-Q6).

## Dependencies

- Nessuna esterna. Tutto il lavoro è self-contained nel monorepo.

## References

- `apps/api/src/Api/Infrastructure/Entities/GameManagement/GameEntity.cs` (entità legacy)
- `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/SharedGameEntity.cs` (target)
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/GetAllPdfsQueryHandler.cs` (punto di partenza bug)
- `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/page.tsx` (UI consumer bloccato)

## Migration Baseline (2026-04-19)

**Baseline snapshot — main-dev SHA**: `117d3e5af` (`fix(seed): add badsworm demo user seeded from SEED_BADSWORM_PASSWORD secret (#410)`)

**DB**: `meepleai_staging` (user `meepleai`) — active dev DB from Docker compose dev stack.

**Note**: The DB in use is the staging snapshot (`make dev-from-snapshot`). Steps 2–3 return zeros because all `pdf_documents` already have `SharedGameId` populated (Step 1 confirms 113/113 with_shared=113, missing_shared=0), meaning no legacy `GameId`-only rows remain to join on in Step 2; and `document_collections` has 0 rows.

### Step 1 — `pdf_documents` mapping counts

```
 total | with_shared | missing_shared | only_private | orphan
-------+-------------+----------------+--------------+--------
   113 |         113 |              0 |            0 |      0
(1 row)
```

**Interpretation**: All 113 `pdf_documents` rows already have `SharedGameId` set. Zero rows need backfill. Zero orphans.

### Step 2 — Backfill source (`pdf_documents` with `GameId` but no `SharedGameId`)

```
 distinct_legacy_games | mappable | unmappable
-----------------------+----------+------------
                     0 |        0 |          0
(1 row)
```

**Interpretation**: No rows in `pdf_documents` have `GameId IS NOT NULL AND SharedGameId IS NULL`. Backfill is a no-op. `unmappable = 0` — satisfies Q2=D hard-cut policy.

### Step 3 — `document_collections` audit

```
 total | distinct_games | mappable | unmappable
-------+----------------+----------+------------
     0 |              0 |        0 |          0
(1 row)
```

**Interpretation**: `document_collections` table is empty. No collection rows need migration.

### Step 4 — Local storage bucket baseline

```
0
```

**Interpretation**: `apps/api/src/Api/storage/pdfs/` directory does not exist (or is empty). No local PDF blobs to rebucket in Task 4. Storage is likely on S3/R2 in this environment.
