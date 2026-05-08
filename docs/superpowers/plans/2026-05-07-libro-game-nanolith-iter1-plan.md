# Libro Game Nanolith — Iter 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare la demo dogfooding Nanolith (4 user goal N1-N4 dello spec `2026-05-07-libro-game-nanolith-demo-design.md`) in 2 PR sequenziali — Iter 1.A (N3 photo translate-on-the-fly) + Iter 1.B (N4 resume cross-day + glossary) — riusando 70% asset esistenti (`KnowledgeBase` BC, `useTranslateParagraph` hook, mockup SP6 A/C/D/E/G/H).

**Architecture:** Estensione di `SessionTracking` BC con 2 nuovi aggregates (`GamebookCampaignSession` root + `TranslatedParagraph` 1-N + `GamebookPhotoArtifact` transitorio). 2 endpoint REST resource-oriented (`/photo-segments` + `/photo-segments/{id}/translations`). Frontend page composition di primitive v2 esistenti (FREEZE-compliant, zero nuovi component v2). Resilience patterns (circuit breaker Polly + bulkhead + 9 metriche Prometheus). Persistence Postgres con 1 EF migration (3 tabelle nuove, soft-delete + optimistic concurrency). LLM via chat-stream esistente (OpenRouter chain).

**Tech Stack:** .NET 9 + ASP.NET Minimal API + MediatR + EF Core + PostgreSQL 16 (jsonb) | Next.js 16 App Router + React 19 + TanStack Query + Zod + Vitest + Playwright | Polly (resilience) + Prometheus (.NET OpenTelemetry exporter) | smoldocling-service (riuso) + chat-stream LLM (riuso)

**Spec reference:** [`docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md`](../specs/2026-05-07-libro-game-nanolith-demo-design.md) — 1192 righe, sc:spec-panel reviewed, 23 fix applicati. Quando il plan dice "vedi spec §X", riferirsi a quella sezione per dettagli completi che non sono duplicati qui (DRY).

**Mockup reference:** SP6 famiglia (A index, C play-session, D translation-viewer, E quota-credits, G resume-state, H glossary-editor) tutti committati in `admin-mockups/design_files/`. FREEZE-compliant verificato.

**Granularità task scelta:** Task di 30-90 min di lavoro effettivo (non 2-5 min di azione singola). Ogni Task contiene 4-7 sub-step `- [ ]` per TDD discipline (test fail → impl → test pass → commit). Per pattern ripetitivi (es. EF entity configuration di 3 tabelle, scenari Gherkin di N task simili) viene mostrato 1 esempio completo + reference a "stesso pattern per gli altri".

**Stima totale:** ~10-14 giorni di lavoro effettivo, distribuiti su 2 settimane calendario. Iter 1.A ~5-7 giorni, Iter 1.B ~5-7 giorni.

**Bundle budget (frontend):** ≤ +120 KB per Iter 1 totale. Phase 1.A ~+60 KB (page route + viewer + camera flow), Phase 1.B ~+60 KB (resume + glossary editor + multi-campaign).

---

## Top-level Acceptance Criteria

Hoisted ACs cross-phase, validati end-to-end al merge finale:

- **AC-1** (N1+N2 baseline): Sprint 0 valida che Q&A su Press Start KB + Rules KB funziona via chat-stream esistente con citation accuracy ≥ 90% manualmente verificata su 5 query sample
- **AC-2** (N3 happy path): foto pagina A4 → photo-segments → user choice § → translate streaming visibile con primo token cumulato P95 < 8 sec da scatto
- **AC-3** (N3 segmentation): paragraph segmentation accuracy ≥ 90% su test fixture 20 foto storybook (mock OCR)
- **AC-4** (N3 privacy): EXIF GPS location stripping client-side verificato su unit test fixture JPEG con GPS coordinates
- **AC-5** (N4 resume): chiusura app + ripristino dopo Y giorni (Y ∈ [1, 7, 30, 90]) → 100% paragrafi cached restano consultabili senza re-LLM
- **AC-6** (N4 consistency): glossary consistency rate ≥ 0.95 su sample 20 paragrafi con terms (vedi spec §0.2 operational definition)
- **AC-7** (Resilience): circuit breaker LLM chain testato (5 failures → Open → 60s → Half-Open), bulkhead 5 concurrent + 11 queue + 16 reject testato
- **AC-8** (FREEZE): zero nuovi component v2 con `hsl(*, 89%, 48%)` hardcoded — `apps/web/src/components/v2/gamebook/*` resta com'è, nuove UI sono page composition o legacy `ui/*` paths
- **AC-9** (Bundle): bundle delta frontend ≤ +120 KB per Iter 1 (verificato `pnpm build` size-limit gate)
- **AC-10** (Dogfood readiness): pre-condition checklist spec §8.1 tutta ✓ + Aaron può aprire `/library/games/nanolith/play` e completare 1 sessione mock 30 min E2E
- **AC-11** (Seed automation): `make seed-nanolith-demo` su DB pulito → 4 precondizioni Sprint 0 SO.1 PASS senza intervento manuale (G8+G9+G10+G11)
- **AC-12** (Legacy cleanup): zero referenze attive a `/library/games/:gameId/translate` e `/gamebook/[gameId]/play/_components/TranslationViewer` post-merge Phase 1.A.9 (G13+G14)
- **AC-13** (Mockup admin coverage): `admin-mockups/design_files/sp5-kb-upload-flow.{html,jsx}` presenti + FREEZE-compliance grep zero match (G12)

---

## Pre-flight

Verifiche state prima di iniziare:

- [ ] **PF-1**: branch hygiene
  ```bash
  git checkout main-dev
  git pull --ff-only
  git remote prune origin
  git branch --show-current  # MUST: main-dev
  git status                  # MUST: clean
  ```

- [ ] **PF-2**: verifica spec + mockup committati
  ```bash
  ls docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md
  ls admin-mockups/design_files/sp6-libro-game-{index,photo-upload,play-session,translation-viewer,quota-credits,resume-state,glossary-editor}.{html,jsx} | wc -l
  # Expected: 14 (7 mockup × 2 estensioni)
  ```

- [ ] **PF-3**: verifica worktree commit `aa72b1b70` accessibile (Nanolith translate paragraph demo cherry-pickable)
  ```bash
  git log --all --oneline aa72b1b70 -1
  # Expected: "feat(demo): Nanolith translate paragraph scaffolding"
  ```

- [ ] **PF-4**: verifica datasource Nanolith presente
  ```bash
  ls -la "data/rulebook/nanolith_datasource/"
  # Expected: Nanolith Rules ENG.pdf (~101MB) + Nanolith Press Start ENG.pdf (~36MB)
  ```

- [ ] **PF-5**: apri umbrella issue + 2 child issues
  ```bash
  gh issue create --title "[Iter 1 dogfood] Nanolith libro game demo umbrella" \
    --body "Implementa demo dogfooding Aaron per Nanolith. 2 PR sequenziali: Iter 1.A (N3 photo-translate, ~5-7gg) + Iter 1.B (N4 resume cross-day, ~5-7gg). Refs design doc \`docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md\`." \
    --label "enhancement,area/backend,area/frontend"
  # Save number as $ITER1_UMBRELLA

  gh issue create --title "[Iter 1.A] N3 photo translate-on-the-fly + N1/N2 baseline" \
    --body "Backend: photo-segments + translations endpoints + ParagraphSegmentationService + circuit breaker LLM + EF migration tab gamebook_photo_artifacts. Frontend: useTranslateParagraph cherry-pick adattato single-shot + page route paragraph viewer + glossary pill display. Sub-issue di #\$ITER1_UMBRELLA." \
    --label "enhancement,area/backend,area/frontend"
  # Save as $ITER1A

  gh issue create --title "[Iter 1.B] N4 resume cross-day + glossary persistence" \
    --body "Backend: GamebookCampaignSession + TranslatedParagraph aggregates + EF migration + PhotoArtifactPurgeJob + glossary extraction prompt. Frontend: page route resume entry + ResumeCard + MultiCampaignList + StaleWarningCard + GlossaryPillEditor wired. Sub-issue di #\$ITER1_UMBRELLA." \
    --label "enhancement,area/backend,area/frontend"
  # Save as $ITER1B
  ```

---

## File Structure

### Backend (.NET 9)

**Domain layer** (`apps/api/src/Api/BoundedContexts/SessionTracking/Domain/`):
- `Aggregates/GamebookCampaignSession.cs` — NEW root aggregate (campaign metadata, last_paragraph_id, party, scoring)
- `Aggregates/TranslatedParagraph.cs` — NEW separate aggregate (1-N relationship con session, paragraph-level granularity)
- `Aggregates/GamebookPhotoArtifact.cs` — NEW transitorio (S3 ref, OCR confidence, detected paragraphs, expires_at)
- `Services/IParagraphSegmentationService.cs` — NEW interface (regex-based + fallback)
- `ValueObjects/SegmentationMatchQuality.cs` — NEW enum (Exact/Partial/Fallback)
- `ValueObjects/ParagraphId.cs` — NEW value object (validated `§N` format)
- `Exceptions/CampaignSessionNotFoundException.cs` — NEW
- `Exceptions/PhotoArtifactExpiredException.cs` — NEW

**Application layer** (`apps/api/src/Api/BoundedContexts/SessionTracking/Application/`):
- `Commands/IngestPhotoCommand.cs` + `IngestPhotoHandler.cs`
- `Commands/TranslateParagraphCommand.cs` + `TranslateParagraphHandler.cs`
- `Commands/MarkParagraphReadCommand.cs` + `MarkParagraphReadHandler.cs`
- `Commands/UpdateGlossaryTermCommand.cs` + `UpdateGlossaryTermHandler.cs`
- `Commands/CreateCampaignSessionCommand.cs` + `CreateCampaignSessionHandler.cs`
- `Commands/ArchiveCampaignSessionCommand.cs` + `ArchiveCampaignSessionHandler.cs` (soft-delete N4.5)
- `Queries/GetCampaignSessionQuery.cs` + handler
- `Queries/ListUserCampaignsQuery.cs` + handler (N4.4 multi-campaign)
- `Queries/GetTranslatedParagraphQuery.cs` + handler (cache hit Lvl 2)
- `Services/ParagraphSegmentationService.cs` (impl)
- `Services/GlossaryExtractionPromptService.cs` (LLM piggy-back)
- `Validators/IngestPhotoCommandValidator.cs`
- `Validators/TranslateParagraphCommandValidator.cs`

**Infrastructure layer** (`apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/`):
- `Persistence/GamebookCampaignSessionConfiguration.cs` (EF mapping)
- `Persistence/TranslatedParagraphConfiguration.cs`
- `Persistence/GamebookPhotoArtifactConfiguration.cs`
- `Repositories/IGamebookCampaignSessionRepository.cs` + impl
- `Repositories/ITranslatedParagraphRepository.cs` + impl
- `Repositories/IGamebookPhotoArtifactRepository.cs` + impl
- `External/SmoldoclingClientAdapter.cs` (riuso `ISmoldoclingClient` da DocumentProcessing BC)
- `External/ChatStreamLlmAdapter.cs` (riuso chat-stream LLM existing)
- `BackgroundJobs/PhotoArtifactPurgeJob.cs` — NEW HostedService cron 0 */6 * * *

**Routing** (`apps/api/src/Api/Routing/`):
- `GamebookEndpoints.cs` — NEW `MapGamebookEndpoints()` extension method

**EF Migrations** (`apps/api/src/Api/Migrations/`):
- `20260507_AddGamebookSessionAggregate.cs` — NEW (3 tabelle: gamebook_campaign_sessions + gamebook_translated_paragraphs + gamebook_photo_artifacts + index + FK + soft-delete)

**Resilience** (`apps/api/src/Api/Infrastructure/Resilience/`):
- `LlmCircuitBreakerPolicy.cs` — NEW Polly policy (5 failures/30s → Open, 60s → Half-Open)
- `LlmBulkheadPolicy.cs` — NEW Polly policy (5 concurrent, 10 queue, 16+ reject)
- `MeepleAiMetrics.Translation.cs` — NEW metric definitions (9 metriche translation_*, ocr_*, glossary_*, llm_*)

**Tests** (`apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/`):
- `Domain/GamebookCampaignSessionTests.cs`
- `Domain/TranslatedParagraphTests.cs`
- `Domain/GamebookPhotoArtifactTests.cs`
- `Domain/ParagraphIdTests.cs`
- `Application/Commands/IngestPhotoHandlerTests.cs`
- `Application/Commands/TranslateParagraphHandlerTests.cs`
- `Application/Commands/MarkParagraphReadHandlerTests.cs`
- `Application/Commands/UpdateGlossaryTermHandlerTests.cs`
- `Application/Services/ParagraphSegmentationServiceTests.cs`
- `Application/Services/GlossaryExtractionPromptServiceTests.cs`
- `Infrastructure/Resilience/LlmCircuitBreakerPolicyTests.cs`
- `Infrastructure/Resilience/LlmBulkheadPolicyTests.cs`
- `Infrastructure/BackgroundJobs/PhotoArtifactPurgeJobTests.cs`
- `Integration/GamebookCampaignSessionRepositoryIntegrationTests.cs` (Testcontainers Postgres)
- `Integration/TranslatedParagraphRepositoryIntegrationTests.cs`
- `Integration/IngestPhotoIntegrationTests.cs`
- `Integration/GlossaryConsistencyIntegrationTest.cs` (M12 fix dello spec)

### Frontend (Next.js 16)

**Page routes** (`apps/web/src/app/(authenticated)/library/games/[gameId]/play/`):
- `page.tsx` — NEW Iter 1.B entry point (riuso mockup G logic)
- `_components/ResumeHero.tsx` — NEW state-02-single-resume composition
- `_components/MultiCampaignList.tsx` — NEW state-03-multi-campaign
- `_components/StaleWarningCard.tsx` — NEW state-04-stale-warning
- `_components/EmptyFirstTime.tsx` — NEW state-01-first-time
- `paragraph/[num]/page.tsx` — NEW Iter 1.A entry point (riuso mockup C+D)
- `paragraph/[num]/_components/PhotoSegmentsList.tsx` — NEW segmentation result UI
- `paragraph/[num]/_components/TranslationViewer.tsx` — NEW page composition viewer fullscreen (riuso mockup D spec, primitives v2 esistenti)
- `paragraph/[num]/_components/GlossaryPillEditor.tsx` — NEW modal H wired (riuso mockup H spec)
- `paragraph/[num]/_components/QaSidePanel.tsx` — NEW drawer riuso `useAgentChatStream` per N2

**Hooks** (`apps/web/src/lib/gamebook/hooks/`):
- `useTranslateParagraph.ts` — cherry-pick from worktree `aa72b1b70`, adapt single-shot pattern
- `useTranslateParagraph.test.ts` — 22 tests cherry-pick + 5 nuovi
- `useCampaignSession.ts` — NEW (TanStack `useQuery` + `useMutation`)
- `useCampaignSession.test.ts`
- `useUserCampaigns.ts` — NEW (list multi-campaign)
- `useUserCampaigns.test.ts`
- `useGlossary.ts` — NEW (read + update terms)
- `useGlossary.test.ts`
- `usePhotoSegmentation.ts` — NEW (POST photo + parse response)
- `usePhotoSegmentation.test.ts`
- `clientExifStripper.ts` — NEW (privacy/EXIF location stripping)
- `clientExifStripper.test.ts`

**API client** (`apps/web/src/lib/api/`):
- `gamebook.ts` — NEW (Zod schemas + fetch wrappers per /photo-segments, /translations, /campaigns)

**E2E tests** (`apps/web/tests/e2e/gamebook/`):
- `N3.1a-photo-segments.spec.ts`
- `N3.1b-translate-streaming.spec.ts`
- `N3.1c-viewer-rendering.spec.ts`
- `N3.2-low-confidence-fallback.spec.ts`
- `N3.4-offline-retry.spec.ts`
- `N3.5-glossary-pill-edit.spec.ts`
- `N3.8-segmentation-threshold-alert.spec.ts`
- `N3.9-exif-privacy.spec.ts`
- `N3.10-e2e-day-orchestration.spec.ts` (slow, manual run)
- `N4.1-resume-cross-day.spec.ts`
- `N4.4-multi-campaign.spec.ts`
- `N4.5-stale-warning.spec.ts`
- `latency-budget.spec.ts`

**Fixtures** (`apps/web/tests/e2e/fixtures/`):
- `nanolith-e2e-seed.ts` — NEW deterministic seed (vedi spec §8.3)
- `storybook-page-fixture.jpg` — NEW pre-OCR'd mock photo
- `storybook-page-with-gps-exif.jpg` — NEW privacy test fixture
- `storybook-page-multi-paragraph.jpg` — NEW segmentation test (3 paragrafi)
- `storybook-page-no-numbering.jpg` — NEW fallback test (no §)

---

## Phase 0 — Generate SP5 Admin KB Upload Mockup (parallel)

> **Goal**: copertura design del flusso admin di upload+indicizzazione PDF, prerequisito UX per il seed automation (Sprint -1) e per qualsiasi futuro miglioramento dell'admin KB ingestion. Modalità di generazione **identica a SP6** (spec design `2026-05-07` §13bis.3): sessione fresca `claude.ai/design/` + tokens.css + components.css + brief + mockup di riferimento + grep gate FREEZE-compliance.

**Quando**: parallelo a Sprint 0 (non blocca Iter 1.A). Output committato su branch dedicato `docs/sp5-kb-upload-mockup` e mergiato indipendentemente.

### Task 0.1: Brief SP5 KB upload flow

**Files:**
- Modify: `admin-mockups/briefs/SP5-admin-tools.md` — aggiungi sezione "KB Upload Flow"

**Coverage**: i prerequisiti del seed automation (G10 indicizzazione 2 PDF Nanolith) hanno bisogno di una UX di reference per `/admin/(dashboard)/knowledge-base/upload/page.tsx`. Oggi non esiste mockup di confronto (SP5 admin batch è "pending Claude Design production resumption post 2026-05-10" per `v2-migration-matrix.md`).

**States to include** (5 stati canonici per upload flow):
- `state-01-empty` — drop zone vuota, "Trascina PDF qui o clicca per scegliere"
- `state-02-uploading` — progress bar + filename + size + cancel button
- `state-03-processing` — multi-stage progressive: parsing → chunking → embedding con percentuale per stage
- `state-04-complete` — preview chunk count + average confidence score + first 3 chunks expandable
- `state-05-error` — variants: parse error / upload failure (network) / quota exceeded / file too large / not a PDF

- [x] Step 1: brief scritto come sezione **A5b** in `SP5-admin-tools.md` (tra A5 e A6). Coverage: 5 stati FSM (empty/uploading/processing/complete/error) + 2 sub-stati idempotency-guard + persona admin desktop + accessibility notes WCAG/aria-live/role + 5 component v2 da progettare allineati ai 4 component reali esistenti (`KbIdempotencyGuard`/`UploadZone`/`UploadSettings`/`ProcessingQueue`).
- [x] Step 2: commit incluso in branch `chore/sp5-kb-upload-brief`

### Task 0.2: Generate mockup via claude.ai/design (modalità SP6 §13bis.3)

**Files:**
- Create: `admin-mockups/design_files/sp5-kb-upload-flow.html`
- Create: `admin-mockups/design_files/sp5-kb-upload-flow.jsx`

- [ ] **Step 1**: apri sessione fresca `claude.ai/design/` e riallegare:
  - `admin-mockups/design_files/tokens.css`
  - `admin-mockups/design_files/components.css`
  - `admin-mockups/briefs/SP5-admin-tools.md` (con sezione Task 0.1)
  - `admin-mockups/design_files/sp4-kb-detail.html` come baseline visuale admin
  - Optional reference: `admin-mockups/design_files/sp6-libro-game-photo-upload.html` (pattern di upload UX)

- [ ] **Step 2**: brief specifico nel prompt:
  ```
  Genera mockup hi-fi sp5-kb-upload-flow.html + .jsx (file 1500-2500 righe, pattern A-E SP6).
  5 stati elencati nel brief SP5-admin-tools.md sezione "KB Upload Flow".
  Persona: admin superadmin desktop (sidebar menu admin attiva).
  Token semantic only — usa var(--c-*), zero hsl(*, 89%, 48%) hardcoded.
  Layout: drop zone centrale + sidebar admin (riuso pattern sp4-kb-detail).
  ```

- [ ] **Step 3**: salva output in `admin-mockups/design_files/`:
  ```bash
  ls admin-mockups/design_files/sp5-kb-upload-flow.{html,jsx}
  # Expected: 2 file
  ```

- [ ] **Step 4**: FREEZE-compliance grep gate
  ```bash
  grep -E "hsl\([0-9]+,?\s*89%,\s*48%\)" admin-mockups/design_files/sp5-kb-upload-flow.html
  # Expected: zero match (exit code 1 = OK)
  ```

- [ ] **Step 5**: commit `docs(sp5): mockup sp5-kb-upload-flow (admin KB ingestion, 5 states)`

### Task 0.3: Update v2-migration-matrix.md

**Files:**
- Modify: `docs/for-developers/frontend/v2-migration-matrix.md`

- [ ] **Step 1**: aggiungi row in sezione SP5 admin batch:
  | Mockup | Component path | Route | Status | PR |
  |---|---|---|---|---|
  | `sp5-kb-upload-flow` | `apps/web/src/app/admin/(dashboard)/knowledge-base/upload/_components/KbUploadFlow.tsx` | `/admin/knowledge-base/upload` | `pending` | — |

- [ ] **Step 2**: nota esplicita: row resta `pending` finché token redesign #807 Fase 2 non sblocca SP5 batch (FREEZE #808). Mockup è asset di design, NOT actionable per implementazione finché lift-criteria #808 not met.

- [ ] **Step 3**: commit `docs(v2-migration): add sp5-kb-upload-flow row (pending under FREEZE #808)`

### Task 0.4: Update MEMORY

**Files:**
- Create: `memory/project_sp5_kb_upload_mockup.md`
- Modify: `memory/MEMORY.md`

Mirror del pattern `project_sp6_libro_game_mockups_wip.md`. Una riga in MEMORY.md sotto sezione progetti attivi.

- [ ] **Step 1**: scrivi memory file (frontmatter type=project, why=copertura design admin KB ingestion, how to apply=quando si tocca `/admin/.../knowledge-base/upload`)
- [ ] **Step 2**: 1 riga in `MEMORY.md`
- [ ] **Step 3**: commit `docs(memory): track SP5 KB upload mockup`

---

## Sprint -1 — Demo Seed Automation

> **Goal**: automatizzare le 4 precondizioni di Sprint 0 SO.1 (G8 account + G9 game + G10 PDF indicizzati + G11 agent) eliminando il fallback manuale "create via admin UI". Riusa 4 asset esistenti per minimizzare codice nuovo.

**Asset esistenti riusabili** (verificato):
- `infra/scripts/seed-all-games-staging.sh` — pattern cookie jar + curl al `API_BASE` + iterazione su `games-metadata.json`
- `infra/scripts/games-metadata.json` — schema `{title, year, ...}` per ogni gioco
- `data/rulebook/nanolith_datasource/Nanolith Rules ENG.pdf` + `Press Start ENG.pdf` (verifica PF-4)
- `infra/scripts/seed-index-wait.sh` — polling `processing_jobs` fino a terminale
- Pattern bake/consume da `.docs-archive/superpowers/plans/archived/2026-04-10-seed-pdf-pre-indexed.md`

**Quando**: prima di Sprint 0 (sblocca SO.1 → SO.2 → SO.3 senza intervento manuale).

### Task S-1.1: Create seed-nanolith-demo.sh

**Files:**
- Create: `infra/scripts/seed-nanolith-demo.sh`
- Modify (se Nanolith assente): `infra/scripts/games-metadata.json`

**Pattern**: estende `seed-all-games-staging.sh` ma scoped a Nanolith + adds account/agent seeding.

- [ ] **Step 1**: shebang + safety + variabili
  ```bash
  #!/usr/bin/env bash
  # seed-nanolith-demo.sh — Idempotent seed for Nanolith libro game demo dogfooding.
  # Prerequisites: API stack up (`make dev` or staging tunnel), curl, jq, python3.
  # Usage: ./seed-nanolith-demo.sh [--target=local|staging]

  set -euo pipefail

  TARGET="${1:-local}"
  case "$TARGET" in
    local)   API_BASE="http://localhost:8080/api/v1" ;;
    staging) API_BASE="https://meepleai.app/api/v1" ;;
    *) echo "ERROR: TARGET must be local|staging"; exit 1 ;;
  esac

  COOKIE_JAR="/tmp/meepleai-${TARGET}-cookies.txt"
  RESULTS_FILE="/tmp/nanolith-demo-seed-results.csv"
  SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  RULEBOOK_DIR="$SCRIPT_DIR/../../data/rulebook/nanolith_datasource"
  ```

- [ ] **Step 2**: precondition — verifica i 2 PDF presenti (PF-4 reuse)
  ```bash
  for pdf in "Nanolith Rules ENG.pdf" "Nanolith Press Start ENG.pdf"; do
    [[ -f "$RULEBOOK_DIR/$pdf" ]] || { echo "ERROR: $pdf missing in $RULEBOOK_DIR"; exit 2; }
  done
  ```

- [ ] **Step 3**: ensure account `badsworm@gmail.com` con role=SuperAdmin (idempotent)
  - Per **local**: SQL diretto via psql `meepleai` DB se possibile, fallback `POST /api/v1/admin/users` con admin bootstrap token
  - Per **staging**: `POST /api/v1/admin/users` con cookie superadmin esistente
  - Password seed: `TestNanolith2026!` (hash via API endpoint, no DB writes raw)
  - Risultato salvato in `$RESULTS_FILE` con `account_id`

- [ ] **Step 4**: ensure Nanolith in `shared_games` (idempotent)
  - Check via `GET /api/v1/shared-games?search=nanolith`
  - Se assente: `POST /api/v1/shared-games` con payload `{title:"Nanolith", yearPublished:2024, status:"Published"}` (riusa schema da `games-metadata.json`)
  - Salva `game_id` in $RESULTS_FILE

- [ ] **Step 5**: upload 2 PDF (idempotent — skip se già `embedding_status=complete`)
  - Riusa pattern `seed-all-games-staging.sh` curl multipart
  - Endpoint: `POST /api/v1/admin/knowledge-base/upload?gameId=$game_id`
  - 2 chiamate sequenziali (Rules + Press Start)
  - Salva `pdf_id_rules`, `pdf_id_press_start` in $RESULTS_FILE

- [ ] **Step 6**: poll `processing_jobs` fino a `embedding_status=complete` per entrambi
  - Riusa `seed-index-wait.sh` con timeout 30 min totali (15 min per PDF)
  - Verifica confidence ≥ 0.85 (spec N1.1 precondition)
  - Se timeout o confidence < 0.85: log error + exit 3

- [ ] **Step 7**: ensure agent "Nanolith Tutor" linkato ai 2 KB (idempotent)
  - Check via `GET /api/v1/admin/agent-definitions?name=Nanolith%20Tutor`
  - Se assente: `POST /api/v1/admin/agent-definitions` con `{name:"Nanolith Tutor", isActive:true, kbLinks:[pdf_id_rules, pdf_id_press_start]}`
  - Salva `agent_id` in $RESULTS_FILE

- [ ] **Step 8**: final state CSV `/tmp/nanolith-demo-seed-results.csv`:
  ```csv
  step,resource,id,status
  account,badsworm@gmail.com,$account_id,OK
  game,Nanolith,$game_id,OK
  pdf_rules,Nanolith Rules ENG.pdf,$pdf_id_rules,COMPLETE
  pdf_press_start,Nanolith Press Start ENG.pdf,$pdf_id_press_start,COMPLETE
  agent,Nanolith Tutor,$agent_id,ACTIVE
  ```

- [x] **Step 9**: chmod +x + commit (eseguito su branch `feature/nanolith-iter1-plan-extension`)

### Task S-1.2: Add Makefile target

**Files:**
- Modify: `infra/Makefile`

- [x] **Step 1**: target aggiunto in `infra/Makefile` (vicino a `seed-games`):
  ```makefile
  seed-nanolith-demo: ## Sprint -1: seed Nanolith libro game demo
  	bash scripts/seed-nanolith-demo.sh local

  seed-nanolith-demo-staging: ## Same, against staging tunnel (requires SSH key + secrets)
  	bash scripts/seed-nanolith-demo.sh staging
  ```

- [x] **Step 2**: `make help` raccoglie i nuovi target dalla docstring `##`
- [ ] **Step 3**: smoke test su local stack pulito (richiede `make dev` attivo): `make seed-nanolith-demo`
- [x] **Step 4**: commit incluso in `feature/nanolith-iter1-plan-extension`

### Task S-1.3: bats test del seed script

**Files:**
- Create: `infra/scripts/tests/seed-nanolith-demo.bats`

Riusa pattern `snapshot-verify.bats`. Verifica che dopo `make seed-nanolith-demo` su DB pulito le 4 precondizioni Sprint 0 SO.1 passino (account, game, 2 PDF complete, agent active).

- [x] **Step 1**: 6 test case scritti in `infra/scripts/tests/seed-nanolith-demo.bats`:
  - `exit 1 on invalid TARGET argument`
  - `fail when Nanolith Rules PDF is missing`
  - `fail when Nanolith Press Start PDF is missing`
  - `fail when admin credentials are not configured`
  - `credentials sourced from infra/secrets/admin.secret when env vars empty`
  - `syntax: bash -n parses the script`

  > **Scope nota**: i test coprono solo le pre-flight failure paths (dipendenze dichiarate, PDF presenti, credenziali admin). Il flusso live (login → create user → create game → upload PDF → poll → create agent) richiede stack attivo ed è validato manualmente via `make seed-nanolith-demo` con `make dev` running.

- [ ] **Step 2**: opzionale aggiunta a CI matrix (richiede `bats-core` installato sull'agent)
- [x] **Step 3**: commit incluso in `feature/nanolith-iter1-plan-extension`

### Task S-1.4: Update Sprint 0 SO.1 step "If missing: create via admin UI"

**Files:**
- Modify (in questo plan, sezione Sprint 0 SO.1)

- [ ] **Step 1**: sostituisci 4 occorrenze di `# If missing: create via admin UI ...` con:
  ```
  # If missing: run `make seed-nanolith-demo` (Sprint -1 Task S-1.1) — automatizza tutte le 4 precondizioni
  ```
- [ ] **Step 2**: commit `docs(plan): point Sprint 0 SO.1 at seed-nanolith-demo automation`

---

## Sprint 0 — Pre-condition Validation

**Goal**: validare che N1 (Q&A setup Press Start) e N2 (Q&A regole Rules) funzionino su KB Nanolith pre-indicizzato + agente preesistente. Riuso 100%, zero nuovo codice. Se Sprint 0 fallisce, Iter 1.A è bloccato (significa che la baseline LLM/RAG non funziona).

### Sprint 0 Task SO.1: Verifica account + game catalog + KB indexing

**Files:** N/A (DB query + admin UI checks)

- [ ] **Step 1**: account superadmin esiste
  ```bash
  cd apps/api/src/Api
  dotnet ef database update  # ensure latest migrations
  ```
  Then via psql or DB tool:
  ```sql
  SELECT id, email, role, is_active FROM auth_users WHERE email = 'badsworm@gmail.com';
  -- Expected: 1 row, role='SuperAdmin', is_active=true
  ```
  If missing: run `make seed-nanolith-demo` (Sprint -1 Task S-1.1) — automatizza tutte le 4 precondizioni. Block Iter 1.A until present.

- [ ] **Step 2**: Nanolith in `shared_games` catalog
  ```sql
  SELECT id, name, status FROM shared_games WHERE LOWER(name) = 'nanolith';
  -- Expected: 1 row, status=Published
  ```
  If missing: run `make seed-nanolith-demo` (Sprint -1) — fallback manuale: admin UI `/admin/shared-games` (NEW Game button).

- [ ] **Step 3**: Press Start KB + Rules KB indicizzati con confidence ≥ 0.85
  ```sql
  SELECT id, file_name, embedding_status, chunk_count
  FROM pdf_documents
  WHERE shared_game_id = (SELECT id FROM shared_games WHERE LOWER(name)='nanolith');
  -- Expected: 2 rows, embedding_status='complete', chunk_count > 0
  ```
  If missing or status != complete: run `make seed-nanolith-demo` (Sprint -1) — fallback manuale: admin UI `/admin/knowledge-base` upload + wait embedding.

- [ ] **Step 4**: AgentDefinition `Nanolith Tutor` linked + active
  ```sql
  SELECT id, name, is_active, kb_links FROM agent_definitions
  WHERE LOWER(name) LIKE '%nanolith%' AND is_active = true;
  -- Expected: 1 row, kb_links contains both PDF document ids
  ```
  If missing: run `make seed-nanolith-demo` (Sprint -1) — fallback manuale: admin UI `/admin/agents/builder` → New Agent → KB Nanolith → Activate.

- [ ] **Step 5**: documenta esiti su Google Sheet `nanolith-dogfood-eval.gsheet` foglio "Sprint 0"
  - Colonne: precondition_id, status (PASS/FAIL), notes, owner, date

### Sprint 0 Task SO.2: Validate N1 (Press Start Q&A baseline)

**Files:** N/A (manual smoke test)

- [ ] **Step 1**: avvia stack locale
  ```bash
  cd infra && make dev-core  # senza AI extra
  # wait for healthchecks
  ```

- [ ] **Step 2**: login come `badsworm@gmail.com`, naviga a `/chat` o `/library/games/nanolith/chat`

- [ ] **Step 3**: pone 5 query setup tipiche e annota risposte:
  | # | Query | Expected pattern |
  |---|---|---|
  | 1 | "come si setupa Nanolith per 4 giocatori?" | Step list ≥ 5 con citation Press Start pag. X |
  | 2 | "quali componenti servono per iniziare?" | Lista componenti specifici |
  | 3 | "come si decide chi inizia?" | Procedure 1-3 frasi con pag |
  | 4 | "qual è la prima azione di gioco?" | Step opening con pag |
  | 5 | "cosa fanno i dadi neri?" | Spiegazione meccanica con pag |

- [ ] **Step 4**: per ogni query annota su sheet:
  - `actionable` (bool, criteri spec §0.1: nominal specificity + quantitative + self-contained)
  - `citation_correct` (bool, manuale verifica vs PDF Press Start)
  - `confidence_score_visible` (bool)
  - `latency_ms` (P95 deve essere < 8 sec rilassato setup)

- [ ] **Step 5**: AC-1 met se ≥ 4/5 actionable + 5/5 citation_correct + 5/5 confidence_visible

### Sprint 0 Task SO.3: Validate N2 (Rules Q&A baseline)

**Files:** N/A

- [ ] **Step 1**: stesso stack locale + login

- [ ] **Step 2**: pone 20 query regole tipiche durante un mock playthrough Nanolith e annota:
  ```
  Esempi query:
  - "quanti dadi tira il mago per il fuoco?"
  - "come funziona il combat?"
  - "cosa succede se tiro un 1 critico?"
  - "i goblin possono colpire più volte?"
  - "le ferite si curano dormendo?"
  ...
  ```

- [ ] **Step 3**: per ogni query annota:
  - `useful` (bool, Aaron-side judgment "OK / sbagliata / non lo so")
  - `citation_correct` (bool, manuale)
  - `latency_ms` (P95 deve essere < 5 sec stretto, vedi spec N2 SMART)
  - `confidence_score`

- [ ] **Step 4**: AC-1 met se ≥ 17/20 useful + ≥ 18/20 in < 5 sec + hallucination 0% su confidence > 0.85

- [ ] **Step 5**: se Sprint 0 PASS → procedi Iter 1.A. Se FAIL → triage bug LLM provider chain o KB indexing prima di iniziare codice nuovo

---

## Iter 1.A — N3 Photo Translate-on-the-fly

### Phase 1.A.1 — Branch + Domain layer foundations

#### Task 1.A.1.1: Crea branch da `main-dev`

**Files:** N/A

- [ ] **Step 1**: switch a main-dev pulito + create feature branch
  ```bash
  git checkout main-dev
  git pull --ff-only
  git status  # MUST: clean
  git branch --show-current  # MUST: main-dev
  git checkout -b feature/issue-$ITER1A-nanolith-iter1a-photo-translate
  git config branch.feature/issue-$ITER1A-nanolith-iter1a-photo-translate.parent main-dev
  ```

- [ ] **Step 2**: verifica branch creato
  ```bash
  git branch --show-current
  # Expected: feature/issue-$ITER1A-nanolith-iter1a-photo-translate
  ```

#### Task 1.A.1.2: ParagraphId value object (TDD)

**Files:**
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/ParagraphIdTests.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/ParagraphId.cs`

- [ ] **Step 1**: scrivi test failing per `ParagraphId.Create("§147")`

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/ParagraphIdTests.cs
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

public class ParagraphIdTests
{
    [Theory]
    [InlineData("§147", "§147")]
    [InlineData("§ 147", "§147")]   // tolerate space after section symbol
    [InlineData("§1", "§1")]
    [InlineData("§9999", "§9999")]
    public void Create_ValidFormat_ReturnsNormalizedId(string input, string expected)
    {
        var result = ParagraphId.Create(input);
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("147")]              // no §
    [InlineData("§abc")]             // not numeric
    [InlineData("§")]                // empty number
    [InlineData("")]                 // empty string
    [InlineData(null)]               // null
    public void Create_InvalidFormat_ThrowsArgumentException(string? input)
    {
        Action act = () => ParagraphId.Create(input!);
        act.Should().Throw<ArgumentException>();
    }
}
```

- [ ] **Step 2**: run test, verify FAIL
  ```bash
  cd apps/api/tests/Api.Tests
  dotnet test --filter "FullyQualifiedName~ParagraphIdTests" -v normal
  # Expected: FAIL with "ParagraphId not defined"
  ```

- [ ] **Step 3**: scrivi minimal implementation

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/ParagraphId.cs
using System.Text.RegularExpressions;

namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

public sealed record ParagraphId
{
    private static readonly Regex Pattern = new(@"^§\s*(\d+)$", RegexOptions.Compiled);

    public string Value { get; }

    private ParagraphId(string value) => Value = value;

    public static ParagraphId Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("ParagraphId cannot be empty", nameof(input));

        var match = Pattern.Match(input);
        if (!match.Success)
            throw new ArgumentException($"Invalid ParagraphId format: '{input}' (expected §N where N is integer)", nameof(input));

        return new ParagraphId($"§{match.Groups[1].Value}");
    }

    public override string ToString() => Value;
}
```

- [ ] **Step 4**: run test, verify PASS
  ```bash
  dotnet test --filter "FullyQualifiedName~ParagraphIdTests" -v normal
  # Expected: PASS
  ```

- [ ] **Step 5**: commit
  ```bash
  git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/ParagraphId.cs \
          apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/ParagraphIdTests.cs
  git commit -m "feat(session-tracking): add ParagraphId value object"
  ```

#### Task 1.A.1.3: SegmentationMatchQuality enum (TDD)

**Files:**
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/SegmentationMatchQualityTests.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/ValueObjects/SegmentationMatchQuality.cs`

- [ ] **Step 1**: test fail per parsing string → enum
```csharp
// SegmentationMatchQualityTests.cs
[Theory]
[InlineData("exact", SegmentationMatchQuality.Exact)]
[InlineData("partial", SegmentationMatchQuality.Partial)]
[InlineData("fallback", SegmentationMatchQuality.Fallback)]
public void Parse_ValidString_ReturnsEnum(string input, SegmentationMatchQuality expected)
{
    SegmentationMatchQuality.Parse(input).Should().Be(expected);
}

[Fact]
public void Parse_InvalidString_Throws()
{
    Action act = () => SegmentationMatchQuality.Parse("invalid");
    act.Should().Throw<ArgumentException>();
}
```

- [ ] **Step 2**: run, verify FAIL

- [ ] **Step 3**: implementation
```csharp
// SegmentationMatchQuality.cs
namespace Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

public enum SegmentationMatchQuality
{
    Exact = 0,      // regex perfect match §N
    Partial = 1,    // match with OCR noise tolerated
    Fallback = 2    // no match, whole page as 1 paragraph
}

public static class SegmentationMatchQualityExtensions
{
    public static SegmentationMatchQuality Parse(string value) => value.ToLowerInvariant() switch
    {
        "exact" => SegmentationMatchQuality.Exact,
        "partial" => SegmentationMatchQuality.Partial,
        "fallback" => SegmentationMatchQuality.Fallback,
        _ => throw new ArgumentException($"Invalid SegmentationMatchQuality: {value}")
    };

    public static string ToWireString(this SegmentationMatchQuality q) => q switch
    {
        SegmentationMatchQuality.Exact => "exact",
        SegmentationMatchQuality.Partial => "partial",
        SegmentationMatchQuality.Fallback => "fallback",
        _ => throw new ArgumentOutOfRangeException(nameof(q))
    };
}
```

- [ ] **Step 4**: run, verify PASS

- [ ] **Step 5**: commit `feat(session-tracking): add SegmentationMatchQuality enum`

#### Task 1.A.1.4: Domain exceptions

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Exceptions/CampaignSessionNotFoundException.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Exceptions/PhotoArtifactExpiredException.cs`

- [ ] **Step 1**: create both exception files (no test needed, simple inheritance)
```csharp
// CampaignSessionNotFoundException.cs
using Api.Shared.Exceptions;
namespace Api.BoundedContexts.SessionTracking.Domain.Exceptions;
public sealed class CampaignSessionNotFoundException(Guid id)
    : NotFoundException($"Campaign session {id} not found");
```

```csharp
// PhotoArtifactExpiredException.cs
using Api.Shared.Exceptions;
namespace Api.BoundedContexts.SessionTracking.Domain.Exceptions;
public sealed class PhotoArtifactExpiredException(Guid id, DateTimeOffset expiredAt)
    : ConflictException($"Photo artifact {id} expired at {expiredAt:O}");
```

- [ ] **Step 2**: build verify
  ```bash
  cd apps/api/src/Api
  dotnet build
  # Expected: Build succeeded
  ```

- [ ] **Step 3**: commit `feat(session-tracking): add domain exceptions for gamebook session`

### Phase 1.A.2 — IParagraphSegmentationService

#### Task 1.A.2.1: Service interface + tests scaffolding

**Files:**
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Services/ParagraphSegmentationServiceTests.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Services/IParagraphSegmentationService.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/ParagraphSegmentationService.cs`

- [ ] **Step 1**: scrivi tests failing per 6 scenari segmentation
```csharp
// ParagraphSegmentationServiceTests.cs
using Api.BoundedContexts.SessionTracking.Application.Services;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Services;

public class ParagraphSegmentationServiceTests
{
    private readonly ParagraphSegmentationService _sut = new();

    [Fact]
    public void Segment_TextWithThreeParagraphs_ReturnsThreeSegments()
    {
        var text = """
            §146 First paragraph text continues here for several lines.
            §147 Second paragraph that follows on the same page with different content.
            §148 Third paragraph wrapping the section nicely.
            """;
        var result = _sut.Segment(text);
        result.Should().HaveCount(3);
        result[0].Number.Value.Should().Be("§146");
        result[1].Number.Value.Should().Be("§147");
        result[2].Number.Value.Should().Be("§148");
        result.Should().AllSatisfy(p => p.Quality.Should().Be(SegmentationMatchQuality.Exact));
    }

    [Fact]
    public void Segment_TextWithSpaceAfterSection_NormalizesId()
    {
        var text = "§ 147 Text after symbol with space.";
        var result = _sut.Segment(text);
        result.Should().HaveCount(1);
        result[0].Number.Value.Should().Be("§147");
        result[0].Quality.Should().Be(SegmentationMatchQuality.Partial);
    }

    [Fact]
    public void Segment_TextWithoutSectionSymbols_FallsBackToWholePage()
    {
        var text = "This is a continuous paragraph without any numbering markers at all.";
        var result = _sut.Segment(text);
        result.Should().HaveCount(1);
        result[0].Number.Should().BeNull();
        result[0].Quality.Should().Be(SegmentationMatchQuality.Fallback);
        result[0].SnippetEn.Should().Be(text);
    }

    [Fact]
    public void Segment_EmptyText_ReturnsEmptySegment()
    {
        var result = _sut.Segment("");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Segment_FragmentEndsMidSentence_FlagsContinuation()
    {
        // §147 senza punto/!/? finale → likely multi-page (M4 fix spec)
        var text = "§147 The sentence continues but does not end here";
        var result = _sut.Segment(text);
        result.Should().HaveCount(1);
        result[0].LikelyContinuesNextPage.Should().BeTrue();
    }

    [Fact]
    public void Segment_ParagraphWithLetterSuffix_TreatedAsSeparate()
    {
        // §147a is uncommon but should be handled
        var text = "§147a First sub-paragraph. §147b Second sub-paragraph.";
        var result = _sut.Segment(text);
        result.Should().HaveCount(2);
    }
}
```

- [ ] **Step 2**: run, verify FAIL (compile error)

- [ ] **Step 3**: scrivi interface + impl
```csharp
// IParagraphSegmentationService.cs
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
namespace Api.BoundedContexts.SessionTracking.Domain.Services;

public interface IParagraphSegmentationService
{
    IReadOnlyList<DetectedParagraph> Segment(string rawText);
}

public sealed record DetectedParagraph(
    ParagraphId? Number,
    string SnippetEn,
    SegmentationMatchQuality Quality,
    bool LikelyContinuesNextPage = false);
```

```csharp
// ParagraphSegmentationService.cs
using System.Text.RegularExpressions;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;

namespace Api.BoundedContexts.SessionTracking.Application.Services;

public sealed class ParagraphSegmentationService : IParagraphSegmentationService
{
    // Match §N or § N or §Na (letter suffix uncommon)
    private static readonly Regex SectionPattern =
        new(@"§\s*(\d+[a-z]?)\b(.*?)(?=§\s*\d+[a-z]?\b|$)",
            RegexOptions.Compiled | RegexOptions.Singleline);

    public IReadOnlyList<DetectedParagraph> Segment(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return Array.Empty<DetectedParagraph>();

        var matches = SectionPattern.Matches(rawText);

        if (matches.Count == 0)
        {
            // Fallback: whole text as 1 paragraph
            return new[]
            {
                new DetectedParagraph(
                    Number: null,
                    SnippetEn: rawText.Trim(),
                    Quality: SegmentationMatchQuality.Fallback)
            };
        }

        var result = new List<DetectedParagraph>();
        foreach (Match match in matches)
        {
            var rawId = $"§{match.Groups[1].Value}";
            var content = match.Groups[2].Value.Trim();

            var hasSpaceInOriginal = match.Value.Contains("§ ");
            var quality = hasSpaceInOriginal
                ? SegmentationMatchQuality.Partial
                : SegmentationMatchQuality.Exact;

            // M4 fix: detect mid-sentence ending (no terminal punctuation)
            var likelyContinues = !string.IsNullOrEmpty(content)
                && !".?!".Contains(content[^1]);

            result.Add(new DetectedParagraph(
                Number: ParagraphId.Create(rawId),
                SnippetEn: content,
                Quality: quality,
                LikelyContinuesNextPage: likelyContinues));
        }

        return result;
    }
}
```

- [ ] **Step 4**: run, verify all 6 tests PASS
  ```bash
  dotnet test --filter "FullyQualifiedName~ParagraphSegmentationServiceTests"
  # Expected: 6/6 PASS
  ```

- [ ] **Step 5**: commit `feat(session-tracking): add ParagraphSegmentationService with regex + fallback`

### Phase 1.A.3 — Resilience patterns (Polly)

#### Task 1.A.3.1: LlmCircuitBreakerPolicy + tests

**Files:**
- Test: `apps/api/tests/Api.Tests/Infrastructure/Resilience/LlmCircuitBreakerPolicyTests.cs`
- Create: `apps/api/src/Api/Infrastructure/Resilience/LlmCircuitBreakerPolicy.cs`

- [ ] **Step 1**: install Polly se non già presente
  ```bash
  cd apps/api/src/Api
  dotnet list package | grep Polly
  # If absent:
  dotnet add package Polly --version 8.4.0
  dotnet add package Polly.Extensions --version 8.4.0
  ```

- [ ] **Step 2**: scrivi 4 test per circuit breaker behavior
```csharp
// LlmCircuitBreakerPolicyTests.cs
using Api.Infrastructure.Resilience;
using FluentAssertions;
using Polly;
using Polly.CircuitBreaker;
using Xunit;

namespace Api.Tests.Infrastructure.Resilience;

public class LlmCircuitBreakerPolicyTests
{
    [Fact]
    public async Task FiveConsecutiveFailures_OpensCircuit()
    {
        var policy = LlmCircuitBreakerPolicy.Create("test-provider");
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(
                () => policy.ExecuteAsync(() => throw new HttpRequestException("fail")));
        }
        // 6th call: circuit open
        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => policy.ExecuteAsync(() => Task.FromResult("ok")));
    }

    [Fact]
    public async Task SuccessAfterHalfOpen_ClosesCircuit()
    {
        var policy = LlmCircuitBreakerPolicy.Create("test-provider", breakDuration: TimeSpan.FromMilliseconds(100));

        // Open circuit
        for (var i = 0; i < 5; i++)
            try { await policy.ExecuteAsync(() => throw new HttpRequestException("fail")); } catch {}

        // Wait Half-Open
        await Task.Delay(150);

        // Single success → Closed
        var result = await policy.ExecuteAsync(() => Task.FromResult("ok"));
        result.Should().Be("ok");

        // Closed: subsequent calls work
        var result2 = await policy.ExecuteAsync(() => Task.FromResult("ok2"));
        result2.Should().Be("ok2");
    }

    [Fact]
    public async Task FailureDuringHalfOpen_ReopensWithCooldown()
    {
        var policy = LlmCircuitBreakerPolicy.Create("test-provider", breakDuration: TimeSpan.FromMilliseconds(100));
        for (var i = 0; i < 5; i++)
            try { await policy.ExecuteAsync(() => throw new HttpRequestException("fail")); } catch {}

        await Task.Delay(150);

        // Half-Open probe fails → Open con cooldown raddoppiato
        await Assert.ThrowsAsync<HttpRequestException>(
            () => policy.ExecuteAsync(() => throw new HttpRequestException("fail again")));

        // Cooldown should have doubled — circuit still open immediately
        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => policy.ExecuteAsync(() => Task.FromResult("ok")));
    }

    [Fact]
    public async Task NonHttpException_DoesNotTriggerBreaker()
    {
        var policy = LlmCircuitBreakerPolicy.Create("test-provider");
        for (var i = 0; i < 10; i++)
            try { await policy.ExecuteAsync(() => throw new InvalidOperationException("not http")); } catch {}

        // Non-HTTP exceptions don't count toward breaker
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => policy.ExecuteAsync(() => throw new InvalidOperationException("still not http")));
        // Circuit not open
    }
}
```

- [ ] **Step 3**: run, verify FAIL

- [ ] **Step 4**: implementation
```csharp
// LlmCircuitBreakerPolicy.cs
using Polly;
using Polly.CircuitBreaker;

namespace Api.Infrastructure.Resilience;

public static class LlmCircuitBreakerPolicy
{
    public static IAsyncPolicy<T> Create<T>(
        string providerName,
        int handledEventsAllowedBeforeBreaking = 5,
        TimeSpan? breakDuration = null,
        TimeSpan? observationWindow = null)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: handledEventsAllowedBeforeBreaking,
                durationOfBreak: breakDuration ?? TimeSpan.FromSeconds(60),
                onBreak: (ex, ts) =>
                {
                    // Telemetry: emit metric llm_circuit_breaker_state{provider} = open (state=1)
                    MeepleAiMetrics.LlmCircuitBreakerState
                        .WithLabels(providerName, "open")
                        .Set(1);
                },
                onReset: () =>
                {
                    MeepleAiMetrics.LlmCircuitBreakerState
                        .WithLabels(providerName, "closed")
                        .Set(0);
                },
                onHalfOpen: () =>
                {
                    MeepleAiMetrics.LlmCircuitBreakerState
                        .WithLabels(providerName, "half_open")
                        .Set(0.5);
                });
    }

    public static IAsyncPolicy Create(
        string providerName,
        int handledEventsAllowedBeforeBreaking = 5,
        TimeSpan? breakDuration = null) =>
        Create<object>(providerName, handledEventsAllowedBeforeBreaking, breakDuration);
}
```

- [ ] **Step 5**: run, verify 4/4 PASS
- [ ] **Step 6**: commit `feat(resilience): add LlmCircuitBreakerPolicy with Polly`

#### Task 1.A.3.2: LlmBulkheadPolicy + tests

**Files:**
- Test: `apps/api/tests/Api.Tests/Infrastructure/Resilience/LlmBulkheadPolicyTests.cs`
- Create: `apps/api/src/Api/Infrastructure/Resilience/LlmBulkheadPolicy.cs`

- [ ] **Step 1-5**: stesso pattern di 1.A.3.1, con tests:
```csharp
[Fact]
public async Task SixteenthConcurrent_RejectedWith503()
{
    var policy = LlmBulkheadPolicy.Create("translation-pool", maxParallelization: 5, maxQueuingActions: 10);
    var tasks = Enumerable.Range(0, 16)
        .Select(i => Task.Run(async () =>
            await policy.ExecuteAsync(async () => { await Task.Delay(500); return i; })))
        .ToList();
    var rejected = 0;
    foreach (var t in tasks)
    {
        try { await t; }
        catch (Polly.Bulkhead.BulkheadRejectedException) { rejected++; }
    }
    rejected.Should().Be(1); // 5 running + 10 queue + 1 rejected
}
```

Implementation:
```csharp
// LlmBulkheadPolicy.cs
using Polly;
using Polly.Bulkhead;

namespace Api.Infrastructure.Resilience;

public static class LlmBulkheadPolicy
{
    public static IAsyncPolicy<T> Create<T>(
        string poolName,
        int maxParallelization = 5,
        int maxQueuingActions = 10)
    {
        return Policy.BulkheadAsync<T>(
            maxParallelization: maxParallelization,
            maxQueuingActions: maxQueuingActions,
            onBulkheadRejectedAsync: (ctx) =>
            {
                MeepleAiMetrics.LlmChainSaturation
                    .WithLabels(poolName)
                    .Set(1.0); // saturated
                return Task.CompletedTask;
            });
    }
}
```

- [ ] commit `feat(resilience): add LlmBulkheadPolicy with isolation pool`

#### Task 1.A.3.3: MeepleAiMetrics.Translation extension

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Resilience/MeepleAiMetrics.Translation.cs`

- [ ] **Step 1**: aggiungi 9 metriche translation/OCR/glossary/LLM coerenti con spec §6.4.5

```csharp
// MeepleAiMetrics.Translation.cs
using Prometheus;

namespace Api.Infrastructure.Resilience;

public static partial class MeepleAiMetrics
{
    public static class Translation
    {
        public static readonly Counter SuccessTotal = Metrics.CreateCounter(
            "translation_success_total",
            "Total successful paragraph translations",
            new[] { "provider", "confidence_bin" });

        public static readonly Counter FailureTotal = Metrics.CreateCounter(
            "translation_failure_total",
            "Total failed paragraph translations",
            new[] { "provider", "reason" });

        public static readonly Histogram OcrConfidence = Metrics.CreateHistogram(
            "ocr_confidence",
            "OCR confidence per page (avg from smoldocling)",
            new HistogramConfiguration { Buckets = Histogram.LinearBuckets(0.0, 0.1, 11) });

        public static readonly Gauge GlossaryConsistencyRate = Metrics.CreateGauge(
            "glossary_consistency_rate",
            "Rolling glossary consistency rate per campaign",
            new[] { "campaign_id" });

        public static readonly Counter SegmentationAccuracyBelowThresholdTotal =
            Metrics.CreateCounter(
                "segmentation_accuracy_below_threshold_total",
                "Count of segmentations where rolling accuracy < 0.90");

        public static readonly Gauge DailyCostEur = Metrics.CreateGauge(
            "daily_cost_eur",
            "Daily LLM cost in EUR per user",
            new[] { "user_id" });

        public static readonly Gauge PhotoPurgeBacklogTotal = Metrics.CreateGauge(
            "photo_purge_backlog_total",
            "Photo artifacts past expires_at not yet purged");

        public static readonly Counter RetryRatePerEndpoint = Metrics.CreateCounter(
            "retry_rate_per_endpoint_total",
            "HTTP retries per endpoint",
            new[] { "endpoint", "reason" });
    }

    // Reusable LLM-level metrics
    public static readonly Gauge LlmCircuitBreakerState = Metrics.CreateGauge(
        "llm_circuit_breaker_state",
        "Circuit breaker state per provider (0=closed, 0.5=half_open, 1=open)",
        new[] { "provider", "state" });

    public static readonly Gauge LlmChainSaturation = Metrics.CreateGauge(
        "llm_chain_saturation",
        "Bulkhead saturation per pool (0..1)",
        new[] { "pool" });
}
```

- [ ] **Step 2**: verify build
- [ ] **Step 3**: commit `feat(metrics): add translation + LLM Prometheus metrics`

### Phase 1.A.4 — Endpoint #1: POST /photo-segments

#### Task 1.A.4.1: IngestPhotoCommand + Validator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/IngestPhotoCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Validators/IngestPhotoCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/IngestPhotoCommandTests.cs`

- [ ] **Step 1**: definisci command
```csharp
// IngestPhotoCommand.cs
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record IngestPhotoCommand(
    Guid GameId,
    Guid? CampaignSessionId,           // null = stateless N3 (no resume yet)
    Stream PhotoStream,
    string ContentType,                 // image/jpeg | image/png | image/heic
    long ContentLength,
    string SourceLanguage,              // "en"
    string TargetLanguage,              // "it"
    string? CurrentParagraph,           // hint per UI fallback
    Guid? IdempotencyKey                // {sessionId}:{ts} hash
) : IRequest<IngestPhotoResult>;

public sealed record IngestPhotoResult(
    Guid PhotoId,
    IReadOnlyList<DetectedParagraphDto> Paragraphs,
    decimal OcrConfidenceAvg);

public sealed record DetectedParagraphDto(
    string? Number,
    string SnippetEn,
    string SegmentationMatchQuality,
    bool LikelyContinuesNextPage);
```

- [ ] **Step 2**: scrivi validator (FluentValidation)
```csharp
// IngestPhotoCommandValidator.cs
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Validators;

public sealed class IngestPhotoCommandValidator : AbstractValidator<IngestPhotoCommand>
{
    private const long MaxPhotoSizeBytes = 10 * 1024 * 1024; // 10 MB
    private static readonly string[] AllowedContentTypes =
        { "image/jpeg", "image/png", "image/heic" };

    public IngestPhotoCommandValidator()
    {
        RuleFor(x => x.GameId).NotEmpty();
        RuleFor(x => x.PhotoStream).NotNull();
        RuleFor(x => x.ContentType)
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage($"ContentType must be one of: {string.Join(", ", AllowedContentTypes)}");
        RuleFor(x => x.ContentLength)
            .GreaterThan(0)
            .LessThanOrEqualTo(MaxPhotoSizeBytes)
            .WithMessage($"Photo size must be between 1 byte and {MaxPhotoSizeBytes} bytes");
        RuleFor(x => x.SourceLanguage).NotEmpty().Length(2);
        RuleFor(x => x.TargetLanguage).NotEmpty().Length(2);
    }
}
```

- [ ] **Step 3**: test validator (4 cases happy + 4 cases fail)
```csharp
[Fact] public void Validate_ValidCommand_Passes() { /* ... */ }
[Fact] public void Validate_PhotoTooLarge_Fails() { /* ... */ }
[Fact] public void Validate_InvalidContentType_Fails() { /* ... */ }
[Fact] public void Validate_EmptyGameId_Fails() { /* ... */ }
```

- [ ] **Step 4**: run + commit `feat(session-tracking): add IngestPhotoCommand + validator`

#### Task 1.A.4.2: IngestPhotoHandler + tests

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/IngestPhotoHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/IngestPhotoHandlerTests.cs`

- [ ] **Step 1**: scrivi tests (5 scenari: happy, OCR fail, S3 fail, unauthorized game, idempotency cache hit)

```csharp
public class IngestPhotoHandlerTests
{
    private readonly Mock<ISmoldoclingClient> _ocrMock = new();
    private readonly Mock<IS3StorageClient> _s3Mock = new();
    private readonly Mock<IGamebookPhotoArtifactRepository> _photoRepoMock = new();
    private readonly Mock<IParagraphSegmentationService> _segMock = new();
    private readonly IngestPhotoHandler _sut;

    [Fact]
    public async Task Handle_HappyPath_Returns3SegmentsWithConfidence()
    {
        // Arrange: mock OCR returns text + confidence 0.92
        _ocrMock.Setup(c => c.ExtractTextAsync(It.IsAny<Stream>(), default))
            .ReturnsAsync(new OcrResult("§146 first §147 second §148 third", 0.92m));
        _segMock.Setup(s => s.Segment("§146 first §147 second §148 third"))
            .Returns(new[]
            {
                new DetectedParagraph(ParagraphId.Create("§146"), "first", SegmentationMatchQuality.Exact),
                new DetectedParagraph(ParagraphId.Create("§147"), "second", SegmentationMatchQuality.Exact),
                new DetectedParagraph(ParagraphId.Create("§148"), "third", SegmentationMatchQuality.Exact),
            });
        var cmd = ValidIngestCommand();

        // Act
        var result = await _sut.Handle(cmd, default);

        // Assert
        result.Paragraphs.Should().HaveCount(3);
        result.OcrConfidenceAvg.Should().Be(0.92m);
        _photoRepoMock.Verify(r => r.AddAsync(It.IsAny<GamebookPhotoArtifact>(), default), Times.Once);
    }

    [Fact] public async Task Handle_OcrFails_ThrowsInfrastructureException() { /* ... */ }
    [Fact] public async Task Handle_S3UploadFails_ThrowsAndLogsRetryable() { /* ... */ }
    [Fact] public async Task Handle_GameNotOwnedByUser_ThrowsForbidden() { /* ... */ }
    [Fact] public async Task Handle_IdempotencyKeyHits_ReturnsCachedResult() { /* ... */ }
}
```

- [ ] **Step 2**: run FAIL

- [ ] **Step 3**: implementation handler (riferimento spec §4.2 frame 4-7)
```csharp
// IngestPhotoHandler.cs
using MediatR;
using Api.BoundedContexts.SessionTracking.Domain.Aggregates;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Repositories;
using Api.Shared.Storage;
using Api.Infrastructure.Resilience;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed class IngestPhotoHandler(
    ISmoldoclingClient ocr,
    IS3StorageClient s3,
    IGamebookPhotoArtifactRepository photoRepo,
    IParagraphSegmentationService segmentation,
    ILogger<IngestPhotoHandler> log
) : IRequestHandler<IngestPhotoCommand, IngestPhotoResult>
{
    public async Task<IngestPhotoResult> Handle(IngestPhotoCommand cmd, CancellationToken ct)
    {
        // 1. Idempotency check (skipped for brevity, requires IIdempotencyStore lookup by key)

        // 2. Persist photo to S3 (R2)
        var photoId = Guid.NewGuid();
        var s3Key = $"gamebook-photos/{cmd.GameId}/{photoId}.jpg";
        await s3.UploadAsync(s3Key, cmd.PhotoStream, cmd.ContentType, ct);

        // 3. OCR via smoldocling
        cmd.PhotoStream.Position = 0; // rewind for re-read
        var ocrResult = await ocr.ExtractTextAsync(cmd.PhotoStream, ct);

        Translation.OcrConfidence.Observe((double)ocrResult.ConfidenceAvg);

        // 4. Segmentation
        var detected = segmentation.Segment(ocrResult.Text);

        // 5. Persist artifact
        var artifact = GamebookPhotoArtifact.Create(
            id: photoId,
            campaignSessionId: cmd.CampaignSessionId,
            s3Key: s3Key,
            ocrConfidenceAvg: ocrResult.ConfidenceAvg,
            detectedParagraphs: detected,
            expiresAt: DateTimeOffset.UtcNow.AddHours(24));

        await photoRepo.AddAsync(artifact, ct);

        // 6. Return DTO
        return new IngestPhotoResult(
            PhotoId: photoId,
            Paragraphs: detected.Select(p => new DetectedParagraphDto(
                Number: p.Number?.Value,
                SnippetEn: p.SnippetEn,
                SegmentationMatchQuality: p.Quality.ToWireString(),
                LikelyContinuesNextPage: p.LikelyContinuesNextPage)).ToList(),
            OcrConfidenceAvg: ocrResult.ConfidenceAvg);
    }
}
```

- [ ] **Step 4**: run, verify 5/5 PASS
- [ ] **Step 5**: commit `feat(session-tracking): add IngestPhotoHandler with OCR + segmentation`

#### Task 1.A.4.3: Endpoint registration

**Files:**
- Create: `apps/api/src/Api/Routing/GamebookEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs` (add `app.MapGamebookEndpoints();`)

- [ ] **Step 1**: scrivi endpoint registration
```csharp
// GamebookEndpoints.cs
using Api.BoundedContexts.SessionTracking.Application.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

public static class GamebookEndpoints
{
    public static IEndpointRouteBuilder MapGamebookEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/v1/gamebook").RequireAuthorization();

        group.MapPost("/{gameId:guid}/photo-segments", IngestPhotoEndpoint)
            .WithName("IngestPhoto")
            .WithSummary("Upload a photo and receive paragraph segmentation")
            .Produces<IngestPhotoResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status413PayloadTooLarge)
            .DisableAntiforgery(); // multipart upload

        return routes;
    }

    private static async Task<IResult> IngestPhotoEndpoint(
        [FromRoute] Guid gameId,
        [FromForm] IFormFile photo,
        [FromForm] string sourceLang,
        [FromForm] string targetLang,
        [FromForm] Guid? campaignSessionId,
        [FromForm] string? currentParagraph,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        IMediator mediator,
        CancellationToken ct)
    {
        await using var stream = photo.OpenReadStream();
        var cmd = new IngestPhotoCommand(
            GameId: gameId,
            CampaignSessionId: campaignSessionId,
            PhotoStream: stream,
            ContentType: photo.ContentType,
            ContentLength: photo.Length,
            SourceLanguage: sourceLang,
            TargetLanguage: targetLang,
            CurrentParagraph: currentParagraph,
            IdempotencyKey: TryParseGuid(idempotencyKey));

        var result = await mediator.Send(cmd, ct);
        return Results.Ok(result);
    }

    private static Guid? TryParseGuid(string? input) =>
        Guid.TryParse(input, out var g) ? g : null;
}
```

- [ ] **Step 2**: aggiungi `app.MapGamebookEndpoints();` in `Program.cs` dopo le altre `Map*Endpoints` calls

- [ ] **Step 3**: smoke test endpoint via curl
```bash
cd infra && make dev
# wait
curl -X POST "http://localhost:8080/api/v1/gamebook/<NANOLITH_GAME_ID>/photo-segments" \
  -H "Cookie: <SESSION_COOKIE>" \
  -F "photo=@apps/web/tests/e2e/fixtures/storybook-page-multi-paragraph.jpg" \
  -F "sourceLang=en" \
  -F "targetLang=it"
# Expected: 200 OK with {photoId, paragraphs[], ocrConfidenceAvg}
```

- [ ] **Step 4**: commit `feat(routing): register POST /api/v1/gamebook/{gameId}/photo-segments`

### Phase 1.A.5 — Endpoint #2: POST /photo-segments/{photoId}/translations

#### Task 1.A.5.1: TranslateParagraphCommand + Validator + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/TranslateParagraphCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/TranslateParagraphHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Validators/TranslateParagraphCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Commands/TranslateParagraphHandlerTests.cs`

- [ ] **Step 1**: command + result types
```csharp
public sealed record TranslateParagraphCommand(
    Guid GameId,
    Guid PhotoId,
    string ParagraphNumber,
    Guid? CampaignSessionId,
    IReadOnlyList<GlossaryEntryDto> GlossarySnapshot
) : IRequest<TranslateParagraphResult>;

public sealed record TranslateParagraphResult(
    string TextIt,
    string LlmProvider,           // "openrouter:anthropic-haiku-3"
    decimal LlmCostEur,
    decimal Confidence,
    IReadOnlyList<GlossaryEntryDto> GlossarySnapshotUsed,
    IReadOnlyList<GlossaryEntryDto> NewGlossaryTerms);  // piggy-back extraction

public sealed record GlossaryEntryDto(
    string TermEn,
    string TermIt,
    string? FirstSeenParagraph,
    bool UserEdited);
```

- [ ] **Step 2**: handler con LLM via chat-stream + glossary snapshot semantics (M11 fix)
```csharp
public sealed class TranslateParagraphHandler(
    IGamebookPhotoArtifactRepository photoRepo,
    ITranslatedParagraphRepository txRepo,
    IChatStreamLlmAdapter llm,
    IGlossaryExtractionPromptService glossary,
    IAsyncPolicy<TranslateParagraphResult> circuitBreakerPolicy,
    IAsyncPolicy bulkheadPolicy,
    ILogger<TranslateParagraphHandler> log
) : IRequestHandler<TranslateParagraphCommand, TranslateParagraphResult>
{
    public async Task<TranslateParagraphResult> Handle(TranslateParagraphCommand cmd, CancellationToken ct)
    {
        // 1. Cache hit check (Lvl 2 server-side)
        if (cmd.CampaignSessionId is { } sid)
        {
            var cached = await txRepo.GetByParagraphAsync(sid, cmd.ParagraphNumber, ct);
            if (cached != null)
            {
                Translation.SuccessTotal.WithLabels("cache", "high").Inc();
                return new TranslateParagraphResult(
                    TextIt: cached.TextIt,
                    LlmProvider: cached.LlmProvider + " (cached)",
                    LlmCostEur: 0m,
                    Confidence: cached.Confidence,
                    GlossarySnapshotUsed: cached.GlossarySnapshot,
                    NewGlossaryTerms: Array.Empty<GlossaryEntryDto>());
            }
        }

        // 2. Load photo artifact + verify expires
        var artifact = await photoRepo.GetByIdAsync(cmd.PhotoId, ct)
            ?? throw new NotFoundException($"Photo artifact {cmd.PhotoId} not found");
        if (artifact.ExpiresAt < DateTimeOffset.UtcNow)
            throw new PhotoArtifactExpiredException(cmd.PhotoId, artifact.ExpiresAt);

        // 3. Find target paragraph EN text
        var targetParagraph = artifact.DetectedParagraphs
            .FirstOrDefault(p => p.Number == cmd.ParagraphNumber);
        if (targetParagraph == null)
            throw new BadRequestException($"Paragraph {cmd.ParagraphNumber} not in photo {cmd.PhotoId}");

        // 4. LLM translate via chat-stream (resilience-wrapped)
        var prompt = BuildNarrativeTranslationPrompt(
            sourceText: targetParagraph.SnippetEn,
            sourceLang: "en",
            targetLang: "it",
            glossary: cmd.GlossarySnapshot);

        var llmResponse = await bulkheadPolicy.ExecuteAsync(async () =>
            await circuitBreakerPolicy.ExecuteAsync(async () =>
                await llm.StreamCompletionAsync(prompt, ct)));

        // 5. Glossary extraction piggy-back
        var newTerms = await glossary.ExtractTermsAsync(
            sourceEn: targetParagraph.SnippetEn,
            translationIt: llmResponse.Text,
            existingGlossary: cmd.GlossarySnapshot,
            ct);

        Translation.SuccessTotal.WithLabels(llmResponse.Provider, ConfidenceBin(llmResponse.Confidence)).Inc();

        return new TranslateParagraphResult(
            TextIt: llmResponse.Text,
            LlmProvider: llmResponse.Provider,
            LlmCostEur: llmResponse.CostEur,
            Confidence: llmResponse.Confidence,
            GlossarySnapshotUsed: cmd.GlossarySnapshot,
            NewGlossaryTerms: newTerms);
    }

    private string BuildNarrativeTranslationPrompt(
        string sourceText, string sourceLang, string targetLang,
        IReadOnlyList<GlossaryEntryDto> glossary)
    {
        var glossaryBlock = glossary.Count > 0
            ? $"\nGLOSSARY (use exactly):\n" + string.Join("\n", glossary.Select(g => $"- {g.TermEn} → {g.TermIt}"))
            : "";

        return $$"""
            You are a literary translator for tabletop RPG narrative gamebooks.
            Translate from {{sourceLang}} to {{targetLang}}.
            Preserve narrative tone, dialogue formatting, and dramatic pacing.
            DO NOT summarize. Translate every sentence completely.
            DO NOT add commentary. Output the translation ONLY.
            {{glossaryBlock}}

            SOURCE:
            {{sourceText}}

            TRANSLATION:
            """;
    }

    private static string ConfidenceBin(decimal c) => c switch
    {
        >= 0.85m => "high",
        >= 0.70m => "medium",
        _ => "low"
    };
}
```

- [ ] **Step 3**: tests (4-6 scenari): happy path streaming, cache hit, glossary used inline, photo expired, paragraph not in photo, LLM circuit breaker open

- [ ] **Step 4**: commit `feat(session-tracking): add TranslateParagraphHandler with chat-stream LLM + resilience`

#### Task 1.A.5.2: GlossaryExtractionPromptService

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/GlossaryExtractionPromptService.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Application/Services/GlossaryExtractionPromptServiceTests.cs`

- [ ] **Step 1-5**: service che fa 1 LLM call combinato per extract new terms da (source_en, translation_it). Usa stesso `IChatStreamLlmAdapter` ma prompt template diverso. Deduplica case-insensitive. Skippa terms già in `existingGlossary` (preserva user_edited).

```csharp
// GlossaryExtractionPromptService.cs
public sealed class GlossaryExtractionPromptService(
    IChatStreamLlmAdapter llm,
    ILogger<GlossaryExtractionPromptService> log
) : IGlossaryExtractionPromptService
{
    public async Task<IReadOnlyList<GlossaryEntryDto>> ExtractTermsAsync(
        string sourceEn, string translationIt,
        IReadOnlyList<GlossaryEntryDto> existingGlossary,
        CancellationToken ct)
    {
        var prompt = $$"""
            Extract 0-5 game-specific proper nouns or unique concepts from this paragraph
            translation. Focus on: character names, item names, location names, faction names,
            magical/sci-fi concepts. Skip common nouns and verbs.

            Output JSON only:
            {"terms": [{"term_en": "...", "term_it": "..."}, ...]}

            EN: {{sourceEn}}
            IT: {{translationIt}}

            JSON:
            """;
        var response = await llm.GetCompletionAsync(prompt, ct);
        var parsed = ParseJson(response.Text);
        // Dedup case-insensitive vs existing
        var existing = existingGlossary
            .Select(g => g.TermEn.ToLowerInvariant()).ToHashSet();
        return parsed
            .Where(t => !existing.Contains(t.TermEn.ToLowerInvariant()))
            .Select(t => new GlossaryEntryDto(t.TermEn, t.TermIt, FirstSeenParagraph: null, UserEdited: false))
            .ToList();
    }

    private static IEnumerable<(string TermEn, string TermIt)> ParseJson(string text) { /* ... */ }
}
```

- [ ] commit `feat(session-tracking): add GlossaryExtractionPromptService for LLM piggy-back`

#### Task 1.A.5.3: Endpoint registration

- [ ] estendi `GamebookEndpoints.cs` con `POST /{gameId:guid}/photo-segments/{photoId:guid}/translations`
- [ ] commit `feat(routing): register POST /photo-segments/{photoId}/translations`

### Phase 1.A.6 — Frontend hooks + page composition

#### Task 1.A.6.1: Cherry-pick useTranslateParagraph

**Files:**
- Cherry-pick from worktree commit `aa72b1b70`:
  - `apps/web/src/lib/gamebook/hooks/useTranslateParagraph.ts`
  - `apps/web/src/lib/gamebook/hooks/__tests__/useTranslateParagraph.test.ts`

- [ ] **Step 1**: cherry-pick
  ```bash
  git cherry-pick aa72b1b70 --strategy=recursive -X theirs
  # If conflict: investigate. Commit aa72b1b70 only adds new files.
  ```

- [ ] **Step 2**: run tests
  ```bash
  cd apps/web
  pnpm test src/lib/gamebook/hooks/__tests__/useTranslateParagraph.test.ts --run
  # Expected: 22/22 PASS
  ```

- [ ] **Step 3**: adapt single-shot pattern (vedi spec §4.3)
  - Modifica `useTranslateParagraph` per usare `POST /photo-segments/{photoId}/translations` invece del chat-stream prompt-only workaround
  - Aggiunge `glossarySnapshot` parameter
  - Add 5 nuovi tests per single-shot pattern

- [ ] **Step 4**: commit `feat(gamebook): adapt useTranslateParagraph to single-shot photo translate API`

#### Task 1.A.6.2: usePhotoSegmentation hook

**Files:**
- Create: `apps/web/src/lib/gamebook/hooks/usePhotoSegmentation.ts`
- Test: `apps/web/src/lib/gamebook/hooks/usePhotoSegmentation.test.ts`

- [ ] **Step 1**: hook con TanStack `useMutation` per POST `/photo-segments` + idempotency-key generation client-side

```typescript
// usePhotoSegmentation.ts
import { useMutation } from '@tanstack/react-query';
import { z } from 'zod';

const DetectedParagraphSchema = z.object({
  number: z.string().nullable(),
  snippetEn: z.string(),
  segmentationMatchQuality: z.enum(['exact', 'partial', 'fallback']),
  likelyContinuesNextPage: z.boolean(),
});

const IngestPhotoResultSchema = z.object({
  photoId: z.string().uuid(),
  paragraphs: z.array(DetectedParagraphSchema),
  ocrConfidenceAvg: z.number().min(0).max(1),
});

export type DetectedParagraph = z.infer<typeof DetectedParagraphSchema>;
export type IngestPhotoResult = z.infer<typeof IngestPhotoResultSchema>;

export interface UsePhotoSegmentationOptions {
  gameId: string;
  campaignSessionId?: string;
  currentParagraph?: string;
}

export function usePhotoSegmentation({ gameId, campaignSessionId, currentParagraph }: UsePhotoSegmentationOptions) {
  return useMutation({
    mutationFn: async (photo: File): Promise<IngestPhotoResult> => {
      // Strip EXIF location client-side (privacy fix N3.9)
      const stripped = await stripExifLocation(photo);

      const idempotencyKey = `${campaignSessionId ?? 'anon'}:${Date.now()}`;
      const fd = new FormData();
      fd.append('photo', stripped);
      fd.append('sourceLang', 'en');
      fd.append('targetLang', 'it');
      if (campaignSessionId) fd.append('campaignSessionId', campaignSessionId);
      if (currentParagraph) fd.append('currentParagraph', currentParagraph);

      const res = await fetch(`/api/v1/gamebook/${gameId}/photo-segments`, {
        method: 'POST',
        headers: { 'Idempotency-Key': idempotencyKey },
        body: fd,
        credentials: 'same-origin',
      });
      if (!res.ok) throw new Error(`Photo segmentation failed: ${res.status}`);
      const json = await res.json();
      return IngestPhotoResultSchema.parse(json);
    },
  });
}
```

- [ ] **Step 2**: test (5+ scenari): happy path, OCR confidence binning, S3 upload error, network error retry, segmentation no match
- [ ] **Step 3**: commit `feat(gamebook): add usePhotoSegmentation hook with EXIF strip + idempotency`

#### Task 1.A.6.3: clientExifStripper utility

**Files:**
- Create: `apps/web/src/lib/gamebook/utils/clientExifStripper.ts`
- Test: `apps/web/src/lib/gamebook/utils/clientExifStripper.test.ts`

- [ ] **Step 1**: install dependency
  ```bash
  cd apps/web
  pnpm add piexifjs
  pnpm add -D @types/piexifjs
  ```

- [ ] **Step 2**: implementation
```typescript
// clientExifStripper.ts
import piexif from 'piexifjs';

export async function stripExifLocation(file: File): Promise<File> {
  if (!file.type.startsWith('image/jpeg')) return file; // only JPEG has EXIF

  const dataUrl = await fileToDataUrl(file);
  const exifObj = piexif.load(dataUrl);

  // Remove GPS IFD entirely (lat, lon, timestamp)
  delete exifObj['GPS'];

  const newDataUrl = piexif.insert(piexif.dump(exifObj), dataUrl);
  return dataUrlToFile(newDataUrl, file.name, file.type);
}

async function fileToDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

function dataUrlToFile(dataUrl: string, name: string, type: string): File {
  const arr = dataUrl.split(',');
  const bstr = atob(arr[1]);
  const u8 = new Uint8Array(bstr.length);
  for (let i = 0; i < bstr.length; i++) u8[i] = bstr.charCodeAt(i);
  return new File([u8], name, { type });
}
```

- [ ] **Step 3**: test fixture JPEG con GPS EXIF
  ```bash
  # Place fixture: apps/web/tests/fixtures/storybook-page-with-gps-exif.jpg
  ```

- [ ] **Step 4**: tests
```typescript
// clientExifStripper.test.ts
import { describe, it, expect } from 'vitest';
import { stripExifLocation } from './clientExifStripper';
import piexif from 'piexifjs';
import { readFileSync } from 'fs';

describe('stripExifLocation', () => {
  it('removes GPS IFD from JPEG', async () => {
    const buf = readFileSync('tests/fixtures/storybook-page-with-gps-exif.jpg');
    const file = new File([buf], 'test.jpg', { type: 'image/jpeg' });

    const stripped = await stripExifLocation(file);

    const strippedBuf = await stripped.arrayBuffer();
    const dataUrl = bufToDataUrl(strippedBuf, 'image/jpeg');
    const exif = piexif.load(dataUrl);
    expect(exif['GPS']).toBeUndefined();
  });

  it('passes through PNG unchanged', async () => {
    const file = new File([new Uint8Array([0x89, 0x50])], 'test.png', { type: 'image/png' });
    const result = await stripExifLocation(file);
    expect(result).toBe(file);
  });
});
```

- [ ] **Step 5**: commit `feat(gamebook): add EXIF location stripping for privacy compliance`

#### Task 1.A.6.4: Page route paragraph viewer

**Files:**
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/paragraph/[num]/page.tsx`
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/paragraph/[num]/_components/TranslationViewer.tsx`
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/paragraph/[num]/_components/PhotoSegmentsList.tsx`
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/paragraph/[num]/_components/CameraButton.tsx`

- [ ] **Step 1**: page.tsx come server component sottile + client island viewer
```tsx
// page.tsx
import { TranslationViewerIsland } from './_components/TranslationViewer';

interface PageProps {
  params: Promise<{ gameId: string; num: string }>;
}

export default async function Page({ params }: PageProps) {
  const { gameId, num } = await params;
  return <TranslationViewerIsland gameId={gameId} initialParagraph={num} />;
}
```

- [ ] **Step 2**: TranslationViewer client island riusando primitive v2 esistenti (NO nuovi component v2 con HSL pattern, riferimento mockup SP6-D)
```tsx
// _components/TranslationViewer.tsx
'use client';
import { useState } from 'react';
import { CameraViewfinder } from '@/components/v2/gamebook/CameraViewfinder';
import { OfflineBanner } from '@/components/v2/gamebook/OfflineBanner';
import { ConfidenceBadge } from '@/components/v2/gamebook/ConfidenceBadge';
import { usePhotoSegmentation } from '@/lib/gamebook/hooks/usePhotoSegmentation';
import { useTranslateParagraph } from '@/lib/gamebook/hooks/useTranslateParagraph';
import { PhotoSegmentsList } from './PhotoSegmentsList';

export function TranslationViewerIsland({ gameId, initialParagraph }: Props) {
  const [photoResult, setPhotoResult] = useState<IngestPhotoResult | null>(null);
  const [selectedParagraph, setSelectedParagraph] = useState<string | null>(null);

  const segmentation = usePhotoSegmentation({ gameId, currentParagraph: initialParagraph });
  const translate = useTranslateParagraph({
    gameId,
    photoId: photoResult?.photoId ?? null,
  });

  const handlePhotoCapture = async (file: File) => {
    const result = await segmentation.mutateAsync(file);
    setPhotoResult(result);
  };

  const handleParagraphSelect = (paragraphNumber: string) => {
    setSelectedParagraph(paragraphNumber);
    translate.mutate({ paragraphNumber });
  };

  // Composition: 3 stati visibili (capture / segmentation list / translation viewer)
  return (
    <main className="reading-mode" aria-label="Storybook translation viewer">
      {/* state: pre-capture */}
      {!photoResult && !selectedParagraph && (
        <CameraButton onCapture={handlePhotoCapture} loading={segmentation.isPending} />
      )}

      {/* state: segments listed */}
      {photoResult && !selectedParagraph && (
        <PhotoSegmentsList
          photo={photoResult}
          onSelect={handleParagraphSelect}
        />
      )}

      {/* state: translation visible (mockup SP6-D) */}
      {selectedParagraph && (
        <article className="reading-body" aria-live="polite">
          <h1 className="paragraph-id">{selectedParagraph}</h1>
          <p style={{ fontSize: '26px', lineHeight: 1.6, maxWidth: '60ch' }}>
            {translate.data?.textIt ?? 'Sto traducendo…'}
          </p>
          {translate.data && (
            <ConfidenceBadge value={translate.data.confidence} />
          )}
        </article>
      )}
    </main>
  );
}
```

- [ ] **Step 3**: integration manual test in browser dev
- [ ] **Step 4**: commit `feat(gamebook): add /play/paragraph/[num] page composition`

### Phase 1.A.7 — DI registration + endpoint smoke test

#### Task 1.A.7.1: Wire DI

**Files:**
- Modify: `apps/api/src/Api/Program.cs`

- [ ] **Step 1**: registra services
```csharp
// Program.cs (additions)
builder.Services.AddScoped<IParagraphSegmentationService, ParagraphSegmentationService>();
builder.Services.AddScoped<IGlossaryExtractionPromptService, GlossaryExtractionPromptService>();
builder.Services.AddScoped<IGamebookPhotoArtifactRepository, GamebookPhotoArtifactRepository>();
builder.Services.AddScoped<IRequestHandler<IngestPhotoCommand, IngestPhotoResult>, IngestPhotoHandler>();
builder.Services.AddScoped<IRequestHandler<TranslateParagraphCommand, TranslateParagraphResult>, TranslateParagraphHandler>();
builder.Services.AddSingleton(LlmCircuitBreakerPolicy.Create<TranslateParagraphResult>("openrouter"));
builder.Services.AddSingleton(LlmBulkheadPolicy.Create<TranslateParagraphResult>("translation-pool"));

// In MapEndpoints section:
app.MapGamebookEndpoints();
```

- [ ] **Step 2**: build + smoke test
- [ ] **Step 3**: commit `feat(api): wire DI for gamebook endpoints + resilience policies`

### Phase 1.A.8 — E2E + DoD validation Iter 1.A

#### Task 1.A.8.1: E2E happy path N3.1

**Files:**
- Create: `apps/web/tests/e2e/gamebook/N3.1a-photo-segments.spec.ts`
- Create: `apps/web/tests/e2e/gamebook/N3.1b-translate-streaming.spec.ts`
- Create: `apps/web/tests/e2e/gamebook/N3.1c-viewer-rendering.spec.ts`

- [ ] **Step 1**: scrivi test E2E per N3.1a-c (vedi spec §3 N3.1a/b/c Gherkin scenarios)
```typescript
// N3.1a-photo-segments.spec.ts
import { test, expect } from '@playwright/test';
import { setupNanolithSeed } from '../fixtures/nanolith-e2e-seed';

test.describe('N3.1a Photo Segments — Happy Path', () => {
  test.beforeEach(async ({ page, context }) => {
    await setupNanolithSeed(context);
    await page.goto('/library/games/nanolith/play/paragraph/§147');
  });

  test('foto pagina A4 → 3 paragrafi separabili in ≤ 4.5s', async ({ page }) => {
    // Mock backend response
    await page.route('**/api/v1/gamebook/*/photo-segments', async (route) => {
      const start = Date.now();
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          photoId: 'fixture-photo-id',
          paragraphs: [
            { number: '§146', snippetEn: 'First paragraph...', segmentationMatchQuality: 'exact', likelyContinuesNextPage: false },
            { number: '§147', snippetEn: 'Second paragraph...', segmentationMatchQuality: 'exact', likelyContinuesNextPage: false },
            { number: '§148', snippetEn: 'Third paragraph...', segmentationMatchQuality: 'exact', likelyContinuesNextPage: false },
          ],
          ocrConfidenceAvg: 0.92,
        }),
      });
    });

    await page.locator('[data-testid="camera-button"]').click();
    await page.locator('[data-testid="camera-input"]').setInputFiles('tests/fixtures/storybook-page-multi-paragraph.jpg');

    await expect(page.locator('[data-testid="paragraph-segment-§146"]')).toBeVisible({ timeout: 4500 });
    await expect(page.locator('[data-testid="paragraph-segment-§147"]')).toBeVisible();
    await expect(page.locator('[data-testid="paragraph-segment-§148"]')).toBeVisible();
  });
});
```

- [ ] **Step 2**: scrivi N3.1b e N3.1c (split per spec)

- [ ] **Step 3**: run E2E
  ```bash
  cd apps/web
  pnpm test:e2e -- --grep "N3.1"
  # Expected: PASS
  ```

- [ ] **Step 4**: commit `test(gamebook): add E2E for N3.1a/b/c happy path`

#### Task 1.A.8.2: E2E edge + error scenarios N3.2 N3.4 N3.7 N3.8 N3.9

- [ ] scrivi tests per:
  - N3.2 low confidence fallback (3 azioni banner)
  - N3.4 offline retry (backoff 31s)
  - N3.7 CJK script error
  - N3.8 segmentation accuracy below threshold (alert visible)
  - N3.9 EXIF privacy (assert no GPS in upload payload)
- [ ] commit `test(gamebook): add E2E edge + error scenarios for N3`

#### Task 1.A.8.3: latency-budget E2E

- [ ] scrivi `latency-budget.spec.ts` con assertion P95 < 8s end-to-end (mocked backend latency injection)
- [ ] commit `test(gamebook): add latency budget P95 < 8s assertion`

#### Task 1.A.8.4: PR Iter 1.A

- [ ] **Step 1**: push branch
  ```bash
  git push -u origin feature/issue-$ITER1A-nanolith-iter1a-photo-translate
  ```

- [ ] **Step 2**: open PR
  ```bash
  gh pr create --base main-dev \
    --title "feat(gamebook): Iter 1.A N3 photo translate-on-the-fly (#$ITER1A)" \
    --body "$(cat <<'EOF'
  ## Summary
  - Implements N3 photo translate-on-the-fly per spec 2026-05-07
  - 2 endpoint REST: POST /photo-segments + POST /photo-segments/{id}/translations
  - ParagraphSegmentationService (regex multi-paragraph + fallback)
  - LLM circuit breaker (Polly) + bulkhead pool
  - 9 Prometheus metrics for translation/OCR/glossary
  - Frontend: useTranslateParagraph cherry-pick + adapt + usePhotoSegmentation + EXIF stripper
  - Page route /library/games/[gameId]/play/paragraph/[num] (page composition, FREEZE-compliant)

  ## Resolves
  Closes #$ITER1A
  Refs #$ITER1_UMBRELLA

  ## Test plan
  - [x] Backend unit (target 90%+ coverage)
  - [x] Backend integration (Testcontainers Postgres + minio S3 mock)
  - [x] Frontend unit (Vitest)
  - [x] Frontend E2E (Playwright N3.1a/b/c, N3.2, N3.4, N3.7, N3.8, N3.9, latency)
  - [x] Manual smoke test su `/api/v1/gamebook/{gameId}/photo-segments` curl
  - [x] FREEZE compliance grep zero match
  - [x] Bundle size delta ≤ +60 KB

  Co-authored-by: Claude Opus 4.7 <noreply@anthropic.com>
  EOF
  )"
  ```

- [ ] **Step 3**: code review + CI green → merge
- [ ] **Step 4**: dopo merge, switch a main-dev + pull
  ```bash
  git checkout main-dev
  git pull --ff-only
  git branch -D feature/issue-$ITER1A-nanolith-iter1a-photo-translate
  ```

### Phase 1.A.9 — Legacy routing cleanup (consolidamento)

> **Goal**: deprecare le 2 implementazioni parallele di "play/translate" non integrate (audit gap G13+G14). Eseguito **dopo merge Iter 1.A** in modo che la nuova route `/library/games/[gameId]/play/paragraph/[num]` sia già su `main-dev` e i redirect siano sicuri.

**Quando**: branch separato `chore/gamebook-legacy-cleanup`, PR piccola post-merge Iter 1.A. Non blocca Iter 1.B.

#### Task 1.A.9.1: Deprecate /library/games/[gameId]/translate/page.tsx (DEMO-ONLY)

**Files:**
- Delete: `apps/web/src/app/(authenticated)/library/games/[gameId]/translate/page.tsx`
- Delete (se presente): `apps/web/src/app/(authenticated)/library/games/[gameId]/translate/__tests__/`
- Modify: `apps/web/src/components/v2/gamebook/TranslateParagraphDemo.tsx` — valuta se mantenere come dev fixture o rimuovere

Razionale: la pagina è marcata "DEMO-ONLY" con `DEMO_AGENT_LOOKUP: Record<string, string> = {}` hardcoded. Richiede edit codice prima di ogni demo. Superseded da `/library/games/[gameId]/play/paragraph/[num]/page.tsx` che risolve `agentId` server-side via `useCampaignSession`/agent lookup runtime.

- [ ] **Step 1**: branch
  ```bash
  git checkout main-dev && git pull --ff-only
  git checkout -b chore/gamebook-legacy-cleanup
  ```

- [ ] **Step 2**: grep cross-references prima di delete
  ```bash
  grep -rn "DEMO_AGENT_LOOKUP" apps/web/src/ tests/
  grep -rn "library/games.*translate" apps/web/src/ docs/
  # Update or remove all references
  ```

- [ ] **Step 3**: delete page + tests
  ```bash
  rm -rf apps/web/src/app/\(authenticated\)/library/games/\[gameId\]/translate/
  ```

- [ ] **Step 4**: optional 301 redirect in `next.config.ts` per back-compat URL temporanea (2 settimane)
  ```ts
  async redirects() {
    return [
      {
        source: '/library/games/:gameId/translate',
        destination: '/library/games/:gameId/play',
        permanent: false, // 307 temp, rimuovi dopo 2 settimane
      },
    ];
  }
  ```

- [ ] **Step 5**: `pnpm build` verde + `pnpm test` verde
- [ ] **Step 6**: commit `chore(gamebook): remove DEMO-ONLY translate page (superseded by /play/paragraph/[num])`

#### Task 1.A.9.2: Deprecate orphan TranslationViewer in /gamebook/[gameId]/play/

**Files:**
- Delete: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/_components/TranslationViewer.tsx`
- Delete: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/__tests__/TranslationViewer.test.tsx`
- Delete: `apps/web/src/app/(authenticated)/gamebook/[gameId]/play/` (intera directory — non c'è `page.tsx`, è orfana)

Razionale: il componente `TranslationViewer.tsx` esiste sotto `/gamebook/[gameId]/play/_components/` ma non c'è `page.tsx` corrispondente — quindi è non-routable, testato solo in unit, mai esposto come pagina. Codice morto. La nuova `/library/games/[gameId]/play/paragraph/[num]/_components/TranslationViewer.tsx` (Task 1.A.6.4) lo sostituisce.

- [ ] **Step 1**: grep zero import del vecchio TranslationViewer
  ```bash
  grep -rn "from.*gamebook/\[gameId\]/play/_components/TranslationViewer" apps/web/src/
  # Expected: zero match (orfano confermato)
  ```

- [ ] **Step 2**: delete directory
  ```bash
  rm -rf apps/web/src/app/\(authenticated\)/gamebook/\[gameId\]/play/
  ```

- [ ] **Step 3**: verifica nessun routing punta lì
  ```bash
  grep -rn "/gamebook/.*play" apps/web/src/ docs/
  # Expected: solo riferimenti SP6 spec (storici, OK), nessun routing attivo
  ```

- [ ] **Step 4**: `pnpm build` verde + `pnpm test` verde
- [ ] **Step 5**: commit `chore(gamebook): remove orphan TranslationViewer in /gamebook/[gameId]/play (no page.tsx, dead code)`

#### Task 1.A.9.3: Mantenere /gamebook/page.tsx + /gamebook/upload/page.tsx (SP6 Phase B)

> **Decisione esplicita**: questi NON sono orfani — sono "I tuoi manuali" index + upload (issue #788, Wave D.3 blueprint). Servono use case differenti dalla user story Nanolith dogfooding (ingestion fotografica generica vs play+translate session-based).

- [ ] **Step 1**: aggiungi commento in `/gamebook/page.tsx` JSDoc:
  ```tsx
  /**
   * Gamebook Index Page — `/gamebook` (SP6 Phase B Tier M, Issue #788).
   * 
   * NOTE: distinct from `/library/games/[gameId]/play` (Iter 1 Nanolith dogfooding,
   * spec 2026-05-07). This index is for user-uploaded photo gamebooks (G1 vision);
   * the /library/games/.../play route is for session-based KB-indexed gamebooks.
   * No consolidation planned — they serve different user goals.
   */
  ```

- [ ] **Step 2**: commit `docs(gamebook): clarify scope distinction /gamebook vs /library/games/[gameId]/play`

#### Task 1.A.9.4: PR cleanup

- [ ] **Step 1**: push + PR (small, fast review)
  ```bash
  git push -u origin chore/gamebook-legacy-cleanup
  gh pr create --base main-dev \
    --title "chore(gamebook): legacy routing cleanup (post Iter 1.A)" \
    --body "$(cat <<'EOF'
  ## Summary
  - Remove DEMO-ONLY /library/games/[gameId]/translate (superseded by /play/paragraph/[num])
  - Remove orphan TranslationViewer.tsx in /gamebook/[gameId]/play (no page.tsx, dead code)
  - Clarify /gamebook scope distinction (SP6 Phase B kept)

  ## Resolves
  Audit gaps G13 + G14 from spec-panel review of nanolith demo design.

  ## Test plan
  - [x] grep zero references to removed paths
  - [x] pnpm build + pnpm test green
  - [x] 307 redirect in next.config (2-week back-compat for /library/games/:id/translate)

  Co-authored-by: Claude Opus 4.7 <noreply@anthropic.com>
  EOF
  )"
  ```

- [ ] **Step 2**: merge dopo CI green + 1 review

---

## Iter 1.B — N4 Resume Cross-day + Glossary

### Phase 1.B.1 — Branch + Aggregates persistence

#### Task 1.B.1.1: Create branch for Iter 1.B

```bash
git checkout main-dev && git pull --ff-only
git checkout -b feature/issue-$ITER1B-nanolith-iter1b-resume
git config branch.feature/issue-$ITER1B-nanolith-iter1b-resume.parent main-dev
```

#### Task 1.B.1.2: GamebookCampaignSession aggregate (TDD)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Aggregates/GamebookCampaignSession.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/GamebookCampaignSessionTests.cs`

- [ ] **Step 1**: scrivi tests per factory + invariants + soft-delete + concurrency
```csharp
public class GamebookCampaignSessionTests
{
    [Fact]
    public void Create_ValidInputs_ReturnsAggregate()
    {
        var session = GamebookCampaignSession.Create(
            userId: Guid.NewGuid(),
            sharedGameId: Guid.NewGuid(),
            title: "Campagna 1",
            party: PartyJson.FromMembers([new("Aelfric", "mage", 12)]));
        session.Should().NotBeNull();
        session.LastParagraphId.Should().BeNull();
        session.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void MarkParagraphRead_UpdatesLastParagraph()
    {
        var session = ValidSession();
        session.MarkParagraphRead("§147");
        session.LastParagraphId.Should().Be("§147");
        session.UpdatedAt.Should().BeAfter(session.CreatedAt);
    }

    [Fact] public void SoftDelete_SetsIsDeletedAndDeletedAt() { /* ... */ }
    [Fact] public void Archive_SoftDeletesAndPreservesData() { /* ... */ }
}
```

- [ ] **Step 2**: implementation con private setters + factory + audit
```csharp
// GamebookCampaignSession.cs
public sealed class GamebookCampaignSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SharedGameId { get; private set; }
    public string Title { get; private set; }
    public PartyJson Party { get; private set; }
    public string? LastParagraphId { get; private set; }
    public ScoringJson? Scoring { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    [Timestamp] public byte[] RowVersion { get; private set; } = null!;

    public bool IsDeleted => DeletedAt.HasValue;

    private GamebookCampaignSession() { } // EF

    public static GamebookCampaignSession Create(/*...*/) { /*...*/ }
    public void MarkParagraphRead(string paragraphId) { /*...*/ }
    public void Archive() { /*...*/ }
    public void UpdateParty(PartyJson newParty) { /*...*/ }
}
```

- [ ] **Step 3-5**: tests pass + commit

#### Task 1.B.1.3: TranslatedParagraph aggregate

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Aggregates/TranslatedParagraph.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/TranslatedParagraphTests.cs`

- [ ] **Step 1**: scrivi tests per factory + concurrency + soft-delete granulare
```csharp
public class TranslatedParagraphTests
{
    [Fact]
    public void Create_ValidInputs_ReturnsAggregate()
    {
        var glossary = new[] { new GlossaryEntryDto("Voidstone", "Pietra del Vuoto", "§147", false) };
        var p = TranslatedParagraph.Create(
            campaignSessionId: Guid.NewGuid(),
            paragraphId: ParagraphId.Create("§147"),
            textIt: "Il portatore della Pietra del Vuoto avanza...",
            glossarySnapshot: glossary,
            ocrConfidence: 0.92m,
            segmentationMatchQuality: SegmentationMatchQuality.Exact,
            llmProvider: "openrouter:anthropic-haiku-3",
            llmCostEur: 0.03m);
        p.IsDeleted.Should().BeFalse();
        p.GlossarySnapshot.Should().HaveCount(1);
    }

    [Fact]
    public void SoftDelete_OnlyAffectsThisParagraph()
    {
        var p1 = ValidParagraph("§147");
        var p2 = ValidParagraph("§148");
        p1.SoftDelete();
        p1.IsDeleted.Should().BeTrue();
        p2.IsDeleted.Should().BeFalse(); // granular soft-delete
    }
}
```

- [ ] **Step 2**: implementation
```csharp
// TranslatedParagraph.cs
public sealed class TranslatedParagraph
{
    public Guid Id { get; private set; }
    public Guid CampaignSessionId { get; private set; }
    public string ParagraphId { get; private set; } = "";    // string repr es. "§147"
    public string TextIt { get; private set; } = "";
    public IReadOnlyList<GlossaryEntryDto> GlossarySnapshot { get; private set; } = Array.Empty<GlossaryEntryDto>();
    public decimal OcrConfidence { get; private set; }
    public SegmentationMatchQuality SegmentationMatchQuality { get; private set; }
    public string LlmProvider { get; private set; } = "";
    public decimal LlmCostEur { get; private set; }
    public DateTimeOffset TranslatedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }
    [Timestamp] public byte[] RowVersion { get; private set; } = null!;
    public bool IsDeleted => DeletedAt.HasValue;

    private TranslatedParagraph() { } // EF

    public static TranslatedParagraph Create(
        Guid campaignSessionId, ParagraphId paragraphId, string textIt,
        IReadOnlyList<GlossaryEntryDto> glossarySnapshot,
        decimal ocrConfidence, SegmentationMatchQuality segmentationMatchQuality,
        string llmProvider, decimal llmCostEur)
    {
        // validation: textIt non vuoto, ocrConfidence in [0,1], llmCostEur >= 0
        return new TranslatedParagraph
        {
            Id = Guid.NewGuid(),
            CampaignSessionId = campaignSessionId,
            ParagraphId = paragraphId.Value,
            TextIt = textIt,
            GlossarySnapshot = glossarySnapshot,
            OcrConfidence = ocrConfidence,
            SegmentationMatchQuality = segmentationMatchQuality,
            LlmProvider = llmProvider,
            LlmCostEur = llmCostEur,
            TranslatedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 3-5**: tests pass + commit `feat(session-tracking): add TranslatedParagraph aggregate (1-N with session)`

#### Task 1.B.1.4: GamebookPhotoArtifact aggregate

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Aggregates/GamebookPhotoArtifact.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/GamebookPhotoArtifactTests.cs`

- [ ] **Step 1**: tests
```csharp
[Fact]
public void Create_SetsExpiresAt24HoursFromNow()
{
    var artifact = GamebookPhotoArtifact.Create(
        id: Guid.NewGuid(),
        campaignSessionId: null,
        s3Key: "gamebook-photos/abc/xyz.jpg",
        ocrConfidenceAvg: 0.85m,
        detectedParagraphs: Array.Empty<DetectedParagraph>(),
        expiresAt: DateTimeOffset.UtcNow.AddHours(24));
    artifact.IsExpired.Should().BeFalse();
}

[Fact]
public void IsExpired_ReturnsTrue_WhenExpiresAtPast()
{
    var artifact = GamebookPhotoArtifact.Create(/*...*/, expiresAt: DateTimeOffset.UtcNow.AddHours(-1));
    artifact.IsExpired.Should().BeTrue();
}
```

- [ ] **Step 2**: implementation
```csharp
// GamebookPhotoArtifact.cs
public sealed class GamebookPhotoArtifact
{
    public Guid Id { get; private set; }
    public Guid? CampaignSessionId { get; private set; }
    public string S3Key { get; private set; } = "";
    public decimal OcrConfidenceAvg { get; private set; }
    public IReadOnlyList<DetectedParagraph> DetectedParagraphs { get; private set; } = Array.Empty<DetectedParagraph>();
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsExpired => ExpiresAt < DateTimeOffset.UtcNow;

    private GamebookPhotoArtifact() { }

    public static GamebookPhotoArtifact Create(
        Guid id, Guid? campaignSessionId, string s3Key,
        decimal ocrConfidenceAvg, IReadOnlyList<DetectedParagraph> detectedParagraphs,
        DateTimeOffset expiresAt) =>
        new()
        {
            Id = id,
            CampaignSessionId = campaignSessionId,
            S3Key = s3Key,
            OcrConfidenceAvg = ocrConfidenceAvg,
            DetectedParagraphs = detectedParagraphs,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
        };
}
```

- [ ] **Step 3-5**: tests pass + commit `feat(session-tracking): add GamebookPhotoArtifact transient aggregate`

### Phase 1.B.2 — EF migration + Repository + Configuration

#### Task 1.B.2.1: EntityTypeConfiguration per ogni aggregate

**Files:**
- Create: 3 files in `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Persistence/`

- [ ] esempio per `GamebookCampaignSession`:
```csharp
public sealed class GamebookCampaignSessionConfiguration : IEntityTypeConfiguration<GamebookCampaignSession>
{
    public void Configure(EntityTypeBuilder<GamebookCampaignSession> b)
    {
        b.ToTable("gamebook_campaign_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.SharedGameId).HasColumnName("shared_game_id");
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(120);
        b.Property(x => x.LastParagraphId).HasColumnName("last_paragraph_id").HasMaxLength(20);

        // jsonb mapping for Party + Scoring
        b.Property<string>("_partyJson")
            .HasColumnName("party_json").HasColumnType("jsonb");
        b.Ignore(x => x.Party);
        b.Property<string>("_scoringJson")
            .HasColumnName("scoring_json").HasColumnType("jsonb");
        b.Ignore(x => x.Scoring);

        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.RowVersion).HasColumnName("row_version").IsRowVersion();

        b.HasQueryFilter(x => !x.IsDeleted);
        b.HasIndex(x => new { x.UserId, x.DeletedAt });
        b.HasIndex(x => new { x.SharedGameId, x.DeletedAt });
    }
}
```

- [ ] commit per ogni configuration `feat(persistence): add EF config for {entity}`

#### Task 1.B.2.2: EF Migration

- [ ] **Step 1**: scaffold migration
  ```bash
  cd apps/api/src/Api
  dotnet ef migrations add AddGamebookSessionAggregate
  ```

- [ ] **Step 2**: review SQL generato (verifica index, FK, soft-delete filter)

- [ ] **Step 3**: apply on dev DB
  ```bash
  dotnet ef database update
  ```

- [ ] **Step 4**: commit `feat(migrations): add gamebook session + translated paragraphs + photo artifacts`

#### Task 1.B.2.3: Repository implementations

**Files:** 3 repository files in `Infrastructure/Repositories/`

- [ ] esempio per `GamebookCampaignSessionRepository`:
```csharp
public sealed class GamebookCampaignSessionRepository(MeepleAiDbContext db)
    : IGamebookCampaignSessionRepository
{
    public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Set<GamebookCampaignSession>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<GamebookCampaignSession>> GetActiveByUserAsync(
        Guid userId, CancellationToken ct) =>
        await db.Set<GamebookCampaignSession>()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);

    public Task AddAsync(GamebookCampaignSession s, CancellationToken ct) =>
        db.Set<GamebookCampaignSession>().AddAsync(s, ct).AsTask();
}
```

- [ ] commit per ogni repository

### Phase 1.B.3 — N4 Application layer

> **Pattern reference**: tutti i 6 task seguono lo scaffold canonico mostrato in **Task 1.A.4.1 (IngestPhotoCommand)** e **Task 1.A.5.1 (TranslateParagraphCommand)** della Iter 1.A: definisci `record Command` → `Validator: AbstractValidator<Command>` → `Handler: IRequestHandler<Command, Result>` → 5+ test scenari (happy + 4 fail) → register in DI → commit. Sotto, per ogni task, fornisco solo le **type signatures distintive** del command/result e gli **scenari test specifici**. La struttura interna (validator + handler shape) è identica al pattern canonico.

#### Task 1.B.3.1: CreateCampaignSessionCommand + Handler

**Files:** Application/Commands/CreateCampaignSessionCommand.cs + Handler.cs + Validator.cs + Tests

- [ ] Type signatures:
```csharp
public sealed record CreateCampaignSessionCommand(
    Guid UserId, Guid SharedGameId, string Title,
    PartyJson Party
) : IRequest<CreateCampaignSessionResult>;

public sealed record CreateCampaignSessionResult(Guid CampaignSessionId);
```
- [ ] Test scenari:
  1. Happy: valid inputs → aggregate created + persisted + ID returned
  2. Empty title → validation error (FluentValidation)
  3. Empty party → validation error (almeno 1 member)
  4. SharedGameId not in catalog → ConflictException
  5. Idempotency: same userId + sharedGameId + similar title within 5 min → returns existing ID
- [ ] commit `feat(session-tracking): add CreateCampaignSessionCommand handler`

#### Task 1.B.3.2: GetCampaignSessionQuery + Handler

**Files:** Application/Queries/GetCampaignSessionQuery.cs + Handler.cs + Tests

- [ ] Type signatures:
```csharp
public sealed record GetCampaignSessionQuery(Guid CampaignSessionId, Guid UserId)
    : IRequest<CampaignSessionDto?>;

public sealed record CampaignSessionDto(
    Guid Id, Guid SharedGameId, string Title, PartyJson Party,
    string? LastParagraphId, ScoringJson? Scoring,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    int TranslatedParagraphCount, int GlossaryTermCount);
```
- [ ] Test scenari:
  1. Happy: existing session owned by user → returns DTO
  2. Session not found → returns null (NOT exception per query semantic)
  3. Session belongs to different user → returns null (privacy)
  4. Soft-deleted session → returns null (HasQueryFilter applied)
  5. Counts (TranslatedParagraphCount + GlossaryTermCount) computed correttamente via separate queries
- [ ] commit `feat(session-tracking): add GetCampaignSessionQuery handler`

#### Task 1.B.3.3: ListUserCampaignsQuery + Handler (N4.4 multi-campaign)

**Files:** Application/Queries/ListUserCampaignsQuery.cs + Handler.cs + Tests

- [ ] Type signatures:
```csharp
public sealed record ListUserCampaignsQuery(
    Guid UserId,
    Guid? SharedGameId = null,    // optional filter
    bool IncludeArchived = false   // soft-deleted (N4.5 archive)
) : IRequest<IReadOnlyList<CampaignSummaryDto>>;

public sealed record CampaignSummaryDto(
    Guid Id, Guid SharedGameId, string Title,
    string? LastParagraphId, int GlossaryTermCount, int PartyMemberCount,
    DateTimeOffset UpdatedAt,
    int DaysSinceLastSession,         // computed for stale warning N4.5
    bool IsStale);                     // true if DaysSinceLastSession > 90
```
- [ ] Test scenari:
  1. User with 0 campaigns → empty list (N4.1 first-time state)
  2. User with 1 campaign 7gg ago → 1 DTO (N4.1 single-resume)
  3. User with 2 campaigns parallel → 2 DTOs ordered by UpdatedAt DESC (N4.4 multi-campaign)
  4. Filter by SharedGameId → only campaigns of that game
  5. IsStale flag: campaign 100 giorni fa → IsStale=true (N4.5 stale warning)
- [ ] commit `feat(session-tracking): add ListUserCampaignsQuery handler`

#### Task 1.B.3.4: ArchiveCampaignSessionCommand + Handler (N4.5 stale archive)

**Files:** Application/Commands/ArchiveCampaignSessionCommand.cs + Handler.cs + Validator.cs + Tests

- [ ] Type signatures:
```csharp
public sealed record ArchiveCampaignSessionCommand(
    Guid CampaignSessionId, Guid UserId, string Reason)
    : IRequest;
```
- [ ] Test scenari:
  1. Happy: archive sets `DeletedAt` (soft-delete, NOT hard delete)
  2. Already archived → idempotent no-op (no exception)
  3. Wrong user → ForbiddenException
  4. Session not found → NotFoundException
  5. Reason logged in audit trail
- [ ] commit `feat(session-tracking): add ArchiveCampaignSessionCommand handler`

#### Task 1.B.3.5: MarkParagraphReadCommand + Handler (auto-save trigger)

**Files:** Application/Commands/MarkParagraphReadCommand.cs + Handler.cs + Validator.cs + Tests

- [ ] Type signatures:
```csharp
public sealed record MarkParagraphReadCommand(
    Guid CampaignSessionId, Guid UserId, string ParagraphNumber)
    : IRequest;
```
- [ ] Test scenari:
  1. Happy: updates `last_paragraph_id` + `updated_at`
  2. ParagraphId invalid format → ValidationError
  3. Wrong user → ForbiddenException
  4. Concurrency conflict (2 simultaneous writes) → optimistic retry x3 max
  5. Crash mid-write: transaction atomic (no half-state)
- [ ] commit `feat(session-tracking): add MarkParagraphReadCommand handler`

#### Task 1.B.3.6: UpdateGlossaryTermCommand + Handler (N3.5 inline pill edit)

**Files:** Application/Commands/UpdateGlossaryTermCommand.cs + Handler.cs + Validator.cs + Tests

- [ ] Type signatures:
```csharp
public sealed record UpdateGlossaryTermCommand(
    Guid CampaignSessionId, Guid UserId,
    string TermEn, string NewTermIt,
    bool ConfirmCollisionOverride = false)
    : IRequest<UpdateGlossaryTermResult>;

public sealed record UpdateGlossaryTermResult(
    bool Success,
    bool CollisionDetected,
    string? CollidedExistingTermEn,
    string? CollidedExistingTermIt);
```
- [ ] Test scenari:
  1. Happy first edit: creates new MemoryNote with `user_edited=true`
  2. Update existing term: increments revision, persists
  3. Collision detected (newTermIt already used for different termEn): returns `CollisionDetected=true` without write — UI shows confirmation modal (state-04 mockup H)
  4. Collision + ConfirmCollisionOverride=true: overrides, marks both as `user_edited`
  5. Race condition (M11 fix): glossary edits do NOT propagate to in-flight translations (snapshot semantic preserved)
- [ ] commit `feat(session-tracking): add UpdateGlossaryTermCommand handler with collision detection`

### Phase 1.B.4 — Frontend resume + glossary

#### Task 1.B.4.1: useCampaignSession hook
#### Task 1.B.4.2: useUserCampaigns hook  
#### Task 1.B.4.3: useGlossary hook + inline edit mutation

#### Task 1.B.4.4: Resume page route + components

**Files:**
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/page.tsx`
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/_components/ResumeHero.tsx`
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/_components/MultiCampaignList.tsx`
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/_components/StaleWarningCard.tsx`
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/_components/EmptyFirstTime.tsx`

Reference design: mockup G `admin-mockups/design_files/sp6-libro-game-resume-state.html` + `.jsx`. **NO copia-incolla** del JSX mockup (è demo HTML/CSS standalone) — implementa come page composition con primitive v2 esistenti + tokens.

State → Component mapping (4 stati mockup G):

| Mockup state anchor | Component renderizzato | Triggers |
|---|---|---|
| `state-01-first-time` | `<EmptyFirstTime />` | `useUserCampaigns().data.length === 0` |
| `state-02-single-resume` | `<ResumeHero campaign={...} />` | `data.length === 1 && !data[0].isStale` |
| `state-03-multi-campaign` | `<MultiCampaignList campaigns={data} />` | `data.length >= 2` |
| `state-04-stale-warning` | `<StaleWarningCard campaign={...} />` | `data.length === 1 && data[0].isStale` |

- [ ] **Step 1**: scrivi page.tsx come server component sottile + dispatch
```tsx
// page.tsx
import { ResumeIsland } from './_components/ResumeIsland';

interface PageProps {
  params: Promise<{ gameId: string }>;
}

export default async function Page({ params }: PageProps) {
  const { gameId } = await params;
  return <ResumeIsland gameId={gameId} />;
}
```

- [ ] **Step 2**: ResumeIsland client island con state dispatch
```tsx
// _components/ResumeIsland.tsx
'use client';
import { useUserCampaigns } from '@/lib/gamebook/hooks/useUserCampaigns';
import { EmptyFirstTime } from './EmptyFirstTime';
import { ResumeHero } from './ResumeHero';
import { MultiCampaignList } from './MultiCampaignList';
import { StaleWarningCard } from './StaleWarningCard';

export function ResumeIsland({ gameId }: { gameId: string }) {
  const { data, isLoading, error } = useUserCampaigns({ gameId });

  if (isLoading) return <SkeletonResume />;
  if (error) return <ErrorBoundary error={error} />;

  const campaigns = data ?? [];

  if (campaigns.length === 0) return <EmptyFirstTime gameId={gameId} />;
  if (campaigns.length === 1 && campaigns[0].isStale) return <StaleWarningCard campaign={campaigns[0]} />;
  if (campaigns.length === 1) return <ResumeHero campaign={campaigns[0]} />;
  return <MultiCampaignList campaigns={campaigns} />;
}
```

- [ ] **Step 3**: implementa 4 sub-component riusando primitive v2 esistenti (auth-card, btn, chip, OfflineBanner) — vedi mockup G per dettagli visivi (font sizes, spacing, copy text)

- [ ] **Step 4**: 4+ unit test per dispatch logic (one per state)
```typescript
// ResumeIsland.test.tsx
describe('ResumeIsland', () => {
  it('renders EmptyFirstTime when no campaigns', () => { /* mock useUserCampaigns([]) */ });
  it('renders ResumeHero when 1 active', () => { /* mock 1 with isStale=false */ });
  it('renders StaleWarningCard when 1 stale', () => { /* mock 1 with isStale=true */ });
  it('renders MultiCampaignList when 2+', () => { /* mock 2 */ });
});
```

- [ ] **Step 5**: commit `feat(gamebook): add resume page route + 4 state components (mockup G)`

#### Task 1.B.4.5: GlossaryPillEditor modal

**Files:**
- Create: `apps/web/src/app/(authenticated)/library/games/[gameId]/play/paragraph/[num]/_components/GlossaryPillEditor.tsx`
- Test: corresponding `.test.tsx`

Reference design: mockup H `admin-mockups/design_files/sp6-libro-game-glossary-editor.html` + `.jsx`. Modal popup che si apre sopra `TranslationViewer` quando user tappa una glossary pill.

State → Component state mapping (4 stati mockup H):

| Mockup state anchor | UI state interno | Trigger |
|---|---|---|
| `state-01-edit-pristine` | `pristine` (initial) | Modal opens, input value === existing termIt |
| `state-02-edited` | `dirty` | Input value !== existing termIt, no save attempted yet |
| `state-03-save-error` | `error` | Mutation `updateGlossaryTerm` failed (network, 5xx) |
| `state-04-collision` | `collision` | Mutation returned `collisionDetected: true` |

- [ ] **Step 1**: scrivi component
```tsx
// GlossaryPillEditor.tsx
'use client';
import { useState } from 'react';
import { useGlossary } from '@/lib/gamebook/hooks/useGlossary';

interface Props {
  campaignSessionId: string;
  termEn: string;
  initialTermIt: string;
  firstSeenParagraph: string;
  open: boolean;
  onClose: () => void;
}

export function GlossaryPillEditor({ campaignSessionId, termEn, initialTermIt, firstSeenParagraph, open, onClose }: Props) {
  const [valueIt, setValueIt] = useState(initialTermIt);
  const [confirmOverride, setConfirmOverride] = useState(false);
  const update = useGlossary(campaignSessionId);

  const isDirty = valueIt !== initialTermIt;
  const collision = update.data?.collisionDetected;
  const errorState = update.error;
  const stateAnchor = !isDirty ? 'pristine' : collision ? 'collision' : errorState ? 'error' : 'dirty';

  const handleSave = () => {
    update.mutate({ termEn, newTermIt: valueIt, confirmCollisionOverride: confirmOverride });
  };

  // Render based on stateAnchor (4 cases per mockup H)
  return (
    <Drawer open={open} onClose={onClose} aria-label={`Modifica traduzione ${termEn}`}>
      {/* ... composition primitive v2 esistenti ... */}
    </Drawer>
  );
}
```

- [ ] **Step 2**: 4+ unit test (one per state) + 1 integration con useGlossary mock

- [ ] **Step 3**: integra in TranslationViewer (Task 1.A.6.4): cliccare una pill `<GlossaryPill>` apre il modal con props correctly populated

- [ ] **Step 4**: commit `feat(gamebook): add GlossaryPillEditor modal (mockup H)`

### Phase 1.B.5 — PhotoArtifactPurgeJob + cost cap

#### Task 1.B.5.1: PhotoArtifactPurgeJob HostedService

```csharp
public sealed class PhotoArtifactPurgeJob(
    IServiceProvider sp,
    ILogger<PhotoArtifactPurgeJob> log
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            using var scope = sp.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IGamebookPhotoArtifactRepository>();
            var s3 = scope.ServiceProvider.GetRequiredService<IS3StorageClient>();

            var expired = await repo.GetExpiredAsync(ct);
            foreach (var artifact in expired)
            {
                await s3.DeleteAsync(artifact.S3Key, ct);
                await repo.DeleteAsync(artifact.Id, ct);
            }

            MeepleAiMetrics.Translation.PhotoPurgeBacklogTotal.Set(expired.Count);
            await Task.Delay(TimeSpan.FromHours(6), ct);
        }
    }
}
```

#### Task 1.B.5.2: Daily cost cap superadmin guard

Implementa `ICostCapEnforcer` che intercetta `TranslateParagraphHandler` pre-LLM call: se `daily_cost_eur >= 5.00` per user, throw `RateLimitException` con `Retry-After: <seconds_to_midnight_UTC>`.

### Phase 1.B.6 — Tests + DoD validation Iter 1.B

#### Task 1.B.6.1: GlossaryConsistencyIntegrationTest (M12 fix)

Test che:
1. Seeds glossary 20 entries
2. Translate 20 mock paragraphs containing terms
3. Asserts consistency_rate ≥ 0.95

#### Task 1.B.6.2: E2E N4.1 N4.4 N4.5

**Files:**
- Create: `apps/web/tests/e2e/gamebook/N4.1-resume-cross-day.spec.ts`
- Create: `apps/web/tests/e2e/gamebook/N4.4-multi-campaign.spec.ts`
- Create: `apps/web/tests/e2e/gamebook/N4.5-stale-warning.spec.ts`

- [ ] **N4.1 Resume cross-day** — parametrizzato per Y in [1, 7, 30, 90] giorni (relative time, M1 fix spec):
```typescript
[1, 7, 30, 90].forEach((daysAgo) => {
  test(`resume sessione ${daysAgo} giorni dopo`, async ({ page, context }) => {
    await seedCampaignSessionWithLastSession(context, daysAgo);
    await page.goto('/library/games/nanolith/play');

    await expect(page.locator('[data-testid="resume-card"]')).toBeVisible();
    await expect(page.locator(`text=Ultima sessione ${daysAgo} giorni fa`)).toBeVisible();
    await expect(page.locator('text=§289')).toBeVisible();
    await expect(page.locator('text=12 termini glossario')).toBeVisible();

    // Resume click
    const start = Date.now();
    await page.locator('[data-testid="resume-button"]').click();
    await expect(page).toHaveURL(/paragraph/);
    expect(Date.now() - start).toBeLessThan(2000); // < 2 sec restore
  });
});
```

- [ ] **N4.4 Multi-campaign** — 2 campagne parallele, accent colors diversi:
```typescript
test('2 campagne attive distinte e selezionabili', async ({ page, context }) => {
  await seedTwoActiveCampaigns(context);
  await page.goto('/library/games/nanolith/play');

  const cards = page.locator('[data-testid="campaign-card"]');
  await expect(cards).toHaveCount(2);

  // Accent color distinguishability via data-entity attribute
  await expect(cards.nth(0)).toHaveAttribute('data-entity', 'game');
  await expect(cards.nth(1)).toHaveAttribute('data-entity', 'agent');

  // Selecting one preserves isolation
  await cards.nth(0).locator('[data-testid="resume-button"]').click();
  await expect(page).toHaveURL(/paragraph\/§289/);
});
```

- [ ] **N4.5 Stale warning** — 100 giorni fa con tono rispettoso:
```typescript
test('stale warning con CTA archive (no destructive)', async ({ page, context }) => {
  await seedCampaignSessionWithLastSession(context, 100);
  await page.goto('/library/games/nanolith/play');

  await expect(page.locator('text=100 giorni fa')).toBeVisible();
  await expect(page.locator('text=potrebbe essere disorientante')).toBeVisible();
  await expect(page.locator('[data-testid="resume-button"]')).toBeVisible();
  await expect(page.locator('[data-testid="archive-restart-button"]')).toBeVisible();

  // Archive is soft-delete, not destructive
  await page.locator('[data-testid="archive-restart-button"]').click();
  await expect(page.locator('text=Archivia e ricomincia')).toBeVisible(); // confirmation
});
```

- [ ] **Step 4**: run E2E
  ```bash
  cd apps/web
  pnpm test:e2e -- --grep "N4"
  # Expected: 6 tests PASS (4 N4.1 + 1 N4.4 + 1 N4.5)
  ```

- [ ] **Step 5**: commit `test(gamebook): add E2E for N4.1/N4.4/N4.5 resume scenarios`

#### Task 1.B.6.3: PR Iter 1.B

Same pattern come Iter 1.A.

---

## Final Acceptance — Dogfood Readiness Checklist

Prima della prima sessione reale Aaron:

- [ ] Both PR Iter 1.A + Iter 1.B merged on `main-dev`
- [ ] Phase 1.A.9 cleanup PR merged on `main-dev` (legacy routing deprecated)
- [ ] `make seed-nanolith-demo` runs green su DB pulito (Sprint -1 automation)
- [ ] Sprint 0 pre-condition validation passed (vedi spec §8.1)
- [ ] `make dev` locale: stack up + healthchecks green
- [ ] N1 + N2 manual smoke test: ≥ 4/5 setup queries actionable + ≥ 17/20 in-game queries useful
- [ ] N3 manual smoke: 1 foto storybook fixture → segmentation + translate IT visibile font 26px
- [ ] N4 manual smoke: chiusura browser → riapri → resume card visibile + glossary preserved
- [ ] FREEZE compliance grep su tutto `apps/web/src/app/(authenticated)/library/games/[gameId]/play/`: zero match `hsl(*, 89%, 48%)`
- [ ] Bundle size delta ≤ +120 KB (size-limit gate)
- [ ] Prometheus metrics endpoint `/metrics` espone tutte le 9 translation_*/llm_*/glossary_* metriche
- [ ] Photo retention: PhotoArtifactPurgeJob registered + first execution OK (check log)
- [ ] Daily cost cap configurato a €5.00 per superadmin
- [ ] Google Sheet `nanolith-dogfood-eval.gsheet` creato con foglio per: Sprint 0, Sessione 1, Sessione 2, Glossary review post-sessione

---

## Open Questions Carry-forward Iter 2 (post first dogfood session)

Da prioritizzare dopo dati reali della prima sessione:

1. Encounter Book UX (cheatsheet card vs viewer fullscreen vs sidebar slide-out)
2. Glossario per-game vs cross-game universal terms
3. Glossary auto-edit detection (LLM ignora glossario → flag automatico)
4. Foto multi-pagina batch (snap 5 in fila → process background)
5. Audio TTS read-aloud
6. Mockup F house-rule (vision Phase 1 Sara, generate dopo dogfood se interesse alto)
