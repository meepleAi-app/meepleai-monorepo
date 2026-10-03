# MeepleAI Monorepo - Developer Guide

**AI board game assistant: RAG, multi-agent, living docs**

> Operational guide (rules + current state). Resolved history and shipped-implementation
> diaries live in [`docs/for-claude/claude-md-history.md`](./docs/for-claude/claude-md-history.md).

## Quick Reference

| Task | Command | Dir |
|------|---------|-----|
| Start Dev (full) | `make dev` | `infra/` |
| Start Dev (core) | `make dev-core` | `infra/` |
| Dev from Snapshot | `make dev-from-snapshot` | `infra/` — [guide](./docs/for-developers/workflows/snapshot-seed-workflow.md) |
| Bake Snapshot | `make seed-index` | `infra/` — raro, indicizza tutti i PDF |
| Integration | `make tunnel && make integration` | `infra/` — **Git Bash only (Windows)** |
| Deploy Staging | `make staging` | `infra/` (on server) |
| Game Reset (#1320) | `make game-reset-help` | `infra/` — [spec](./docs/for-developers/specs/2026-05-19-game-entity-reset.md) |
| Setup Secrets | `make secrets-setup && make secrets-sync` | `infra/` |
| Stop / Logs | `make dev-down` / `make logs s=api` | `infra/` |
| All commands | `make help` | `infra/` |
| Start API (no Docker) | `dotnet run` | `apps/api/src/Api/` |
| Start Web (no Docker) | `pnpm dev` | `apps/web/` |
| Migration | `dotnet ef migrations add Name` | `apps/api/src/Api/` |
| API Docs | http://localhost:8080/scalar/v1 | Browser |

### Windows Notes

- **Docker commands**: always use `pwsh -c "docker logs meepleai-api --tail=50"` — piping in bash breaks
- **Integration scripts**: run in **Git Bash** (not PowerShell/CMD). Requires SSH key `~/.ssh/meepleai-staging`

### Invite-only Registration

Controlled at runtime via admin toggle (`/admin/config` → General → Registration Mode), backed by `RegistrationMode` config (DB-persisted). When `publicRegistrationEnabled=false`, `/register` shows the request-access popup (`RequestAccessForm`) instead of the standard form. No env var, no redeploy.

## Stack

**Backend** (.NET 9): ASP.NET Minimal APIs + MediatR | PostgreSQL 16 + EF Core (pgvector) + Redis | FluentValidation | xUnit + Testcontainers

**Frontend** (Next.js 16): App Router + React 19 | Tailwind 4 + shadcn/ui | Zustand + React Query | Vitest + Playwright

**AI** (Python): sentence-transformers | cross-encoder | Unstructured | SmolDocling

**Core Features**: RAG (hybrid retrieval) | Multi-agent AI | PDF processing (OCR) | Community game catalog | SSE streaming + SignalR live sessions | CQRS pattern

## Architecture

### 🔴 CQRS Pattern (CRITICAL)

**Rule**: Endpoints use ONLY `IMediator.Send()` — ZERO direct service injection

```csharp
// ✅ CORRECT
app.MapPost("/api/v1/auth/register", async (RegisterCommand cmd, IMediator m) =>
    Results.Ok(await m.Send(cmd)));

// ❌ FORBIDDEN
app.MapPost("/api/v1/auth/register", async (RegisterCommand cmd, IAuthService svc) => ...);
```

### DDD Bounded Contexts (20)

`apps/api/src/Api/BoundedContexts/` — **Layers**: Domain → Application (commands/queries) → Infrastructure

| Context | Responsibility |
|---------|---------------|
| Administration | Users, roles, analytics |
| AgentMemory | House rules, memory notes, guest player claims |
| Authentication | Auth flows, sessions, OAuth, 2FA |
| BusinessSimulations | Ledger entries, cost scenarios, resource forecasts |
| DatabaseSync | DB migrations, tunnel management, sync ops |
| DocumentProcessing | PDF upload, extraction, chunking |
| EntityRelationships | Cross-entity links (EntityLink aggregates) |
| GameManagement | Catalog, sessions, FAQs, specs, game books (multi-role 1..N per game) |
| GameToolbox | Card decks, phases, session tool templates |
| GameToolkit | AI toolkit generation, KB-based suggestions |
| Gamification | Achievements, badges, leaderboards |
| KbQuality | RAG/KB evaluation, cost budgets, quality metrics |
| KnowledgeBase | RAG, AI agents, chat, vector search |
| SecurityAudit | Security audit logging (audit events, audit log) |
| SessionTracking | Session notes, scoring, activity tracking |
| SharedGameCatalog | Community DB w/ soft-delete |
| SystemConfiguration | Runtime config, flags |
| Testing | Test-support endpoints (seed/cleanup test entities) |
| UserLibrary | Collections, wishlist, history |
| UserNotifications | Alerts, email, push |

### Key Data Patterns

| Pattern | Implementation |
|---------|---------------|
| **Soft Delete** | `IsDeleted` + `DeletedAt` + `HasQueryFilter(e => !e.IsDeleted)` |
| **Audit** | `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy` |
| **Concurrency** | Postgres `xmin` system column (EF-backed, no client RowVersion) — see ADR-060 |

### DDD Rules

- ✅ Entities: private setters + factory methods · Value Objects: immutable, validation in factory
- ✅ Repos: interfaces in Domain, implementation in Infrastructure
- ❌ Domain services directly in endpoints · Shared models between commands/queries · Direct service injection (use MediatR)

## Development

### Quick Start

```bash
cd apps/api/src/Api && dotnet restore
cd ../../../web && pnpm install
cd ../../infra && make secrets-setup && make secrets-sync
cd ../apps/web && cp .env.development.example .env.local
cd ../../infra && make dev        # All services (make dev-core = no AI/monitoring)
```

### Secret Management

`.secret` files in `infra/secrets/` — single flat directory. Staging is source of truth.

| Command | Purpose |
|---------|---------|
| `make secrets-setup` | Generate placeholders from `.example` templates |
| `make secrets-sync` | Pull real values from staging (requires SSH) |

**Rule**: Never commit `.secret` files. Only `.secret.example` templates are committed.

**S3 Storage**: Factory pattern via `STORAGE_PROVIDER` env var (`local` default, `s3` for R2/AWS/MinIO). Config in `infra/secrets/storage.secret` — see [Operations Manual](./docs/for-developers/operations/operations-manual.md).

### Git Workflow

> Full rationale in [ADR-054 — DevOps Multi-Branch Strategy](./docs/for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md). Tracking epic [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842).

**Branches**: `main-dev` (dev) | `main-staging` (release) | `main` (prod) | `feature/issue-{n}-{desc}`

**🔴 PR Target Rule**: feature branches MUST merge to their parent branch (typically `main-dev`). Auto-delete on merge is enabled repo-wide.

```bash
git checkout main-dev && git pull
git checkout -b feature/issue-123-desc
git config branch.feature/issue-123-desc.parent main-dev
# work → commit → test → push → PR to main-dev
```

**🔴 Branch Hygiene Rule** (#806): ALWAYS switch to the parent branch BEFORE `git checkout -b`. Never branch while HEAD is on another in-progress `feature/*` — it absorbs that branch's commits into your ancestry (common in concurrent multi-terminal / AI sessions). Pre-creation check:

```bash
git branch --show-current  # MUST print main-dev or main (if feature/… → STOP, checkout main-dev first)
git status                 # MUST be clean
git pull --ff-only         # MUST succeed
```

See [CONTRIBUTING.md § Branch Hygiene](./CONTRIBUTING.md#-branch-hygiene--before-creating-a-feature-branch) for recovery via `git rebase --onto`.

**Commits**: `feat|fix|docs|refactor|test|chore(scope): description`

### Feature Development Flow

```
1. Domain:       Game.MarkAsPlayed() { PlayCount++; }
2. Application:  MarkGameAsPlayedCommand + Validator + Handler
3. Endpoint:     app.MapPut("/games/{id}/mark-played", async (Guid id, IMediator m) => ...)
4. Tests:        Unit (domain) + Integration (DB) + E2E (HTTP)
```

### Migrations

```bash
cd apps/api/src/Api
dotnet ef migrations add DescriptiveName && dotnet ef database update
```

Review SQL, test dev first, never delete old migrations.

## Code Standards

### C# Backend

**Naming**: PascalCase (public) | `_camelCase` (private) | `I` prefix (interfaces)

- **Entity**: private setters + factory method (`Game.Create()`)
- **Value Object**: immutable record + validation in factory (`Email.Create()`)
- **Exception**: domain-specific (`GameNotFoundException`)

### TypeScript Frontend

**Naming**: PascalCase (components/types) | camelCase (functions/vars) | UPPER_SNAKE_CASE (constants)

- **Component**: typed props + explicit `JSX.Element` return · **Store**: Zustand with TS interface

*Full examples: [docs/for-developers/workflows/README.md](./docs/for-developers/workflows/README.md)*

### Card Components

Use `MeepleCard` for all entity displays — **never** the deprecated `GameCard`/`PlayerCard`.

```tsx
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
<MeepleCard entity="game" variant="grid" title={game.title} subtitle={game.publisher}
  imageUrl={game.imageUrl} rating={game.averageRating} ratingMax={10} />
```

Entity types (10, `MeepleEntityType`): `game` · `player` · `session` · `agent` · `kb` · `chat` · `event` · `toolkit` · `tool` · `gameNightEvent`. Variants (6): `grid` (default) · `list` · `compact` · `featured` · `hero` · `focus`. Docs: [meeple-card-design-tokens.md](./docs/for-developers/frontend/meeple-card-design-tokens.md).

> `MeepleCard` is one of **three parallel card families** — see also `ui/shared-games/meeple-card-game.tsx` (`MeepleCardGame`) and `ui/data-display/extra-meeple-card/` (detail/drawer). Consolidation debt tracked in the [MeepleCard/CSS drift audit](./docs/for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md).

### Design System & Tokens

Canonical paths: feature compositions → `apps/web/src/components/features/<feature>/`; primitives → `apps/web/src/components/ui/<primitive>/`. The legacy `components/v2/**` and `ui/v2/**` trees are empty — **do not re-introduce them**.

Theming uses `[data-theme="light|dark"]` (next-themes applies both `class="dark"` AND `data-theme`). **Default is light** (cream `#f7f3ee`).

When writing components:
- ✅ Semantic tokens: `bg-background`, `bg-card`, `bg-muted`, `text-foreground`, `text-muted-foreground`, `border-border`, `border-border-strong`.
- ✅ Entity utilities: `bg-entity-game`, `text-entity-session`, `ring-entity-event/30`, etc.
- ❌ Forbidden by ESLint `local/no-hardcoded-color-utility` (**error**): `bg-white`, `bg-slate-*`, `text-gray-*`, `border-zinc-*`, full neutral palette.
- Exemption: `text-white`/`border-white`/`ring-white` allowed when the same className declares a colored bg (entity utility, gradient, arbitrary `bg-[hsl(…)]`, semantic `bg-primary/secondary/accent`) — the mockup `.e-bg` pattern.

Active anti-drift gates (all blocking in CI): `pnpm lint:tokens` · `lint:tokens:mockups` · `lint:fidelity` · `mockup-annotations:audit` · `lint:bgg` / `lint:bgg-mockups`. Background + completed migrations (De-versioning, Token Canonicalization, Visual Gate removal, DS-17 pattern history): [claude-md-history.md](./docs/for-claude/claude-md-history.md).

## Testing

### Backend (Target: 90%+) — 930+ classes | 13,134+ tests

```bash
cd apps/api/src/Api
dotnet test                                           # All
dotnet test --filter "Category=Unit"                  # Unit only
dotnet test --filter "BoundedContext=GameManagement"  # By context
dotnet test /p:CollectCoverage=true                   # With coverage
```

Patterns: [backend-testing-patterns.md](./docs/for-developers/testing/backend/backend-testing-patterns.md). Suite layout: [tests/README.md](./tests/README.md).

### Frontend (Target: 85%+)

```bash
cd apps/web
pnpm test && pnpm test:coverage   # Unit (Vitest)
pnpm test:e2e                     # E2E (Playwright)
pnpm typecheck && pnpm lint       # Quality
```

## Project Structure

```
apps/
├── api/src/Api/          # .NET 9: BoundedContexts/, Routing/, Infrastructure/
├── web/                  # Next.js: src/app/, components/, lib/, __tests__/
├── embedding-service/    # Python: embeddings
├── reranker-service/     # Python: reranking
└── {smoldocling,unstructured}-service/  # Python: PDF/docs
docs/                     # for-users / for-developers / for-claude (adr/, history)
infra/                    # docker-compose, Makefile, secrets/, monitoring/, scripts/
tests/                    # Api.Tests, k6, api-smoke, llm-eval, fixtures — see tests/README.md
.github/workflows/        # CI/CD pipelines
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Missing secrets | `cd infra && make secrets-setup && make secrets-sync` |
| DB connection | `docker compose logs postgres && dotnet ef database update` |
| Build fails (FE) | `rm -rf .next && pnpm build` |
| Build fails (BE) | `dotnet clean && dotnet build` |
| Testhost blocking | `tasklist \| grep testhost` → `taskkill //PID <PID> //F` |
| Port conflict | `netstat -ano \| findstr :8080` → `taskkill /PID <PID> /F` |
| Snapshot drift | `make seed-index` (rigenera) or `make dev` (fallback) — [workflow](./docs/for-developers/workflows/snapshot-seed-workflow.md#compat-gate--exit-codes) |
| Full ops reference | [operations-manual.md](./docs/for-developers/operations/operations-manual.md) |

## Known Flaky Tests

**Baseline currently clean** (0 known failures on `main-dev`). Resolved-triage history (#1349 → #1422 → #1887 → #2270 → #2266): [claude-md-history.md](./docs/for-claude/claude-md-history.md#known-flaky-tests--resolved-history).

**Intermittenti noti**: nessuno (#3711 chiusa).

**Skippati con tracciamento** ([#4016](https://github.com/meepleAi-app/meepleai-monorepo/issues/4016), dal 2026-10-02): `StoreAsync_ValidFile_ReturnsSuccessWithFileId` e `GetPresignedDownloadUrlAsync_AfterStore_ReturnsValidUrl` (`S3BlobStorageIntegrationTests`), `BggCover_Uploaded_ResolvesViaRawKeyNoSuffix_And200` e `PdfCover_Uploaded_ResolvesViaPreviewSuffix_And200` (`CoverR2ConventionIntegrationTests`). Non sono flaky: non passavano mai, e fino a #3978 **nessuno lo sapeva** perché `minio/minio:latest` era sparita da Docker Hub e il `catch` intorno a `StartAsync` faceva saltare l'intera suite. Riportata l'immagine al mirror GHCR, le due suite girano e il resto dei loro test passa: le tre cause residue (`DisablePayloadSigning` hardcoded nelle pipeline cover — #3846 incompleto · presign SigV2 contro SigV4 atteso · prefisso `pdfs/` contro `pdf_uploads/`) sono in #4016.

> ⚠️ Correzione del 2026-10-02 a una prima versione di questa riga, che diceva «il motivo lo scriveva una `Console.WriteLine` che xUnit non mostra», cioè che i test saltavano **in silenzio**. **Falso**: quelle suite usavano già `Assert.Skip("S3 storage tests require Docker or TEST_S3_ENDPOINT…")`, quindi comparivano come `Skipped` con un motivo. I difetti veri sono due, e sono più istruttivi. (1) Il motivo era **generico e fuorviante**: diceva «richiede Docker» mentre Docker c'era, e la causa vera — l'immagine irraggiungibile — finiva solo nella `Console.WriteLine` del `catch`, che xUnit davvero non mostra. Un motivo di salto generico spiega l'assenza con la causa sbagliata e chiude l'indagine prima che inizi. (2) Lo skip era **osservabile ma non osservato**: nel run `dev-async` 36997819474 i tre shard riportavano `Skipped` 45, 11 e 8, e sedici salti in più su quel fondo non spostano nulla che qualcuno conti. Lezione riusabile: rendere un salto visibile non basta se i salti non hanno una baseline come i fallimenti — vedi [la spec dei livelli di dipendenza](./docs/for-developers/specs/2026-10-02-test-dependency-tiers-and-observable-skips.md).

Prima di rilassare la soglia — o di allargare l'attesa — di un test di timing, stabilisci **perché** la misura scende: potrebbe segnalare un difetto vero. È andata così in [#3686](https://github.com/meepleAi-app/meepleai-monorepo/issues/3686): `DurationTracking_MultipleStageFallback_RecordsCumulativeTime` falliva 1 volta su 2 sullo stesso SHA perché `EnhancedPdfProcessingOrchestrator` fermava il cronometro **prima** dello stage 3 di fallback — il nominale era 100 ms contro una soglia `>= 100`, margine zero. Corretta la misura, la soglia è stata **alzata** a 200.

In [#3711](https://github.com/meepleAi-app/meepleai-monorepo/issues/3711) la stessa domanda ha avuto risposta opposta, e vale come precedente: `SubscribeAsync` è un `IAsyncEnumerable` **lazy**, quindi `pool.TryAdd` non gira all'invocazione ma alla prima `MoveNextAsync` del consumer — un istante deciso dal thread pool, che il `Task.Delay(50)` del test provava a indovinare. Il servizio era corretto (la consegna è garantita solo dopo la registrazione; il resto lo copre Last-Event-ID + replay), quindi il fix è stato **sincronizzare** sullo stato osservabile (`GetConnectionCount`) invece di attendere. Regola pratica: davanti a un'attesa fissa in un test, cerca prima l'osservabile su cui sincronizzarti — allargare il delay è la sconfitta, non il fix.

**Policy**: PRs MUST NOT grow the unit-test fail count above baseline (zero). Future regressions: fix the root cause OR skip with `[Fact(Skip = "#<issue>")]` / `[Theory(Skip = "#<issue>")]` and add a row here in the same PR.

**🔴 Il motivo di ogni salto deve dichiarare chi deve agire** (#4021, gate `SkipReasonClassArchitectureTests`, `Category=Unit`, **blocca in dev-fast**). Quattro classi, e il motivo deve cominciare con una di esse:

| classe | quando | cosa deve contenere |
|---|---|---|
| `PREVISTO:` | servizio opzionale per progetto (L3/L4) | **come** abilitare il test: variabile, comando o chiave |
| `GUASTO:` | un servizio che *dovrebbe* esserci non è sano | l'**errore osservato**, non la condizione generica |
| `DIFETTO:` | il prodotto è rotto, il test è corretto | il **numero della issue** |
| `LIMITE:` | il test non è esprimibile sotto questo harness | la limitazione **e** dove andrebbe spostato |

L'elenco `Exempt` del gate è **vuoto**: non aggiungerci una voce senza la ragione per cui non puoi classificare quel sito *adesso* e la issue che la chiude. Due regole che il gate non può dedurre dal codice:

- **L'assenza di un servizio si ACCERTA con una sonda, non si deduce da una risposta.** Un `500` è anche — e soprattutto — ciò che produce un bug del prodotto: `E2ETestBase.AssertSuccessAsync` lo fa fallire, e il prerequisito si sonda prima con `E2EServiceProbe.SkipUnlessHealthyAsync(Client, Checks.<nome>, "<come abilitarlo>")`. Passare una stringa cruda invece di una costante di `Checks` è bloccato da un gate: un nome che quell'host non registra fa saltare test che funzionano (#4023 — tre test passati sono diventati salti, visibile solo incrociando due run).
- **Un servizio L1** (postgres, redis, minio, mailpit — `L1Services`) **non si salta: fallisce.** Usa `L1Services.FailBecauseUnavailable`. Verificato con un drill: immagine MinIO a un tag inesistente ⇒ le due suite di #3978 vanno da 12 superati / 0 non superati a **0 / 12**.

Spec: [`2026-10-02-test-dependency-tiers-and-observable-skips.md`](./docs/for-developers/specs/2026-10-02-test-dependency-tiers-and-observable-skips.md).

> 🔴 La policy diceva `[Trait("Skip", "<issue#>")]`, e **quel meccanismo è inerte**: un Trait non impedisce l'esecuzione del test, nessun filtro CI lo esclude (`grep -n 'Category!=' .github/workflows/*.yml` elenca Integration/E2E/Performance/Manual/Slow, non Skip), e `#3625` ne aveva rimosso uno proprio perché non faceva nulla — l'unica occorrenza rimasta nel repo è il commento storico in `DashboardEndpointPerformanceTests.cs`. `TestCategoryGateArchitectureTests` legge `FactAttribute`/`TheoryAttribute`.`Skip` via `CustomAttributeData`, non un Trait. Chi seguiva la policy alla lettera scriveva uno skip che non skippava, e il test restava rosso nel conteggio che la policy stessa dice di non far crescere.

## AI Assistant Rules

### 🔒 Active Freezes

**BGG user-side asset ban — 2026-06-10** ([#2123](https://github.com/meepleAi-app/meepleai-monorepo/issues/2123)) — Hard ban on browser requests to `cf.geekdo-images.com`, `*.boardgamegeek.com`, `images.geekdo.com`, `geekdo-images.com`. Three-layer enforcement: (1) data plane (seed manifests + `SeedManifestGame` stripped of image props + nullify migration `20260610152201`); (2) resolution plane (`SharedGameDto.CoverUrl` single FE source, placeholder fallback via `cover-utils.ts`); (3) network plane (`next.config.js` explicit allowlist, no `**` catch-all + ESLint `local/no-bgg-host` + `pnpm lint:bgg` gate). Prometheus `meepleai_bgg_url_attempted_render_total` SLO=0; any nonzero = P1. See [ADR-059 §5](./docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md). Admin server-to-server BGG paths (`apps/web/src/app/admin/**`, `components/admin/**`) remain legitimate per ADR-059 §2.

> A11y AA: any axe color-contrast/ARIA fail = real regression (gate is blocking) — investigate, never skip.

### Domain Model — GameNight / Session

**Reference (source of truth)**: [`2026-06-04-gamenight-session-domain-model.md`](./docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md) — 20 invariants, backend semantic mapping (demo term ↔ `GameNightEvent`/`Session` aggregates). Consult it when touching **`SessionTracking`** or **`GameManagement`** (GameNight sub-aggregate): GameNight 1→N Session; 3 Session timestamps (createdAt always, startedAt/completedAt nullable); state machine planned → in-progress → completed; player identity mix (User-linked + guest); max 1 live per GameNight; sidebar Library (personale) + Games (catalogo, Discover default tab).

**🔴 Nav rule (#1977 + #2158)**: `AppTopBar` is the **single source-of-truth for primary desktop navigation** (5-id `TOP_BAR_NAV_IDS` in `apps/web/src/config/navigation.ts` + Altro overflow). The persistent desktop `MainSidebar` was rolled back/deleted — `MainSidebar` now mounts ONLY inside the mobile hamburger `SideDrawer` (`<lg`). Do NOT re-introduce a persistent desktop sidebar.

**Live-session scoring** (epic #2354): polymorphic ScoreType (Points/BinaryWin/Objectives/Ranking) is the current pattern; write scores via `useUpdateSessionScores`, never the store directly (ESLint `local/no-store-scores-direct` = **error**). Shipped-implementation diaries (Asse A–D, Session-live G1/G5a): [claude-md-history.md](./docs/for-claude/claude-md-history.md#gamenight--session-asse-ad--shipped-implementation-diaries).

**🔴 SSOT sessione + scoring** ([ADR-089](./docs/for-claude/architecture/adr/adr-089-session-scoring-ssot.md), #3395): tre aggregati (`GameSession` = lifecycle/quota/history · `LiveGameSession` = runtime in-play + **scoring in-play SSOT** round-based · `SessionTracking.Session` = companion chat/note/media). I link `TrackingSessionId`/`CorrelatedGameSessionId` (Saga, ADR-083) portano **solo id** per lifecycle/quota/companion — **NON** sincronizzano lo scoring: `RoundScores` ↔ `ScoreData`/`ScoreEntry` non si parlano e non vanno ponticellati. Scegli l'SSOT per contesto, non riconciliare i modelli.

### Known Pitfalls (Issues)

| Issue | Rule |
|-------|------|
| #2567 | Endpoint flow: DTOs → Queries → Commands → Validators → Handlers → Routing |
| #2568 | Exceptions: `ConflictException` (409), `NotFoundException` (404) — never `InvalidOperationException` (500) |
| #2565 | DI: register both `IService` interface and implementation |
| #2593 | Kill testhost before running tests; use culture-independent `$"{val*100:0}%"` |
| #2600 | OAuth: defensive validation + InMemory transaction + manual rollback |
| #2620 | FK constraints: seed dependent entities first; HybridCache needs `IHybridCacheService` for event handlers |
| [ADR-062](./docs/for-claude/architecture/adr/adr-062-config-environment-field-semantics.md) | Config `Environment` field: default to `"All"` for global keys; per-env per-row only when value diverges by design |
| [ADR-060](./docs/for-claude/architecture/adr/adr-060-live-session-persistence.md) | LiveSession is EF-backed. Command handlers calling `_sessionRepository.AddAsync`/`UpdateAsync` MUST also `await _unitOfWork.SaveChangesAsync(ct)`. Domain events dispatch post-SaveChanges. Optimistic concurrency via Postgres `xmin` (#2097 → #2305); same on `game_night_playlists`, `mechanic_drafts` (#2306) |
| [ADR-078](./docs/for-claude/architecture/adr/adr-078-auto-issue-noise-thresholds.md) | Every `.github/workflows/*-monitor.yml` (cron calling GH Issues Search API) MUST declare `concurrency: group: monitor-<type>-${{ github.ref }}` to stay under the 1k req/h rate limit (advisory) |
| [ADR-090](./docs/for-claude/architecture/adr/adr-090-in-session-grounded-answer-ownership.md) | In-session grounded answer: `KnowledgeBase` OWNS it; `SessionTracking` consumes via `IMediator` (never inject KB services), public DTO boundary. The #3390 grounded pipeline is duplicated across `AskGroundedSessionQueryHandler` + `ChatWithSessionAgentCommandHandler` — a correctness/copyright fix must touch both until consolidated on a shared KB service (where Slice 4 enhancements wire) |
| #3737 | 🔴 **`FuseGlobally`: un segnale assente che vale `0` è LOAD-BEARING** — rende la fusione *congiuntiva* (serve evidenza da entrambi i bracci) e tiene giù i match lessicali generici, cioè il difetto di #3735. Non rimuoverlo. I pesi `0.7/0.3` sono tarati contro una query codificata `passage:`: se si corregge il prefisso e5, la taratura **non regge più** e le due cose vanno misurate insieme. Non tarare a mente — 3 ipotesi ragionate con test unit verdi, 3 bocciature del gate (10/11 → 8/11 → 5/11) a ~45 min l'una. Usa l'artifact `rag-fusion-tuning-<run_id>` del gate |
| #3740 | 🔴 **Una colonna assente dalla proiezione non si manifesta come `null` se l'entità ha un default non-null.** Le tre SELECT di lettura di `PgVectorStoreAdapter` omettevano `lang` mentre `Embedding.Language` porta l'inizializzatore `= "en"`: ogni candidato arrivava `"en"` qualunque fosse la lingua vera del chunk. Da lì l'inferenza sbagliata che il corpus del gate fosse monolingua (è **9840 en / 943 it / 107 de**), e il no-op byte-identico della correzione per lingua #3743. **Corretto**: le tre SELECT proiettano `lang`, e la lingua per candidato arriva fino al campo `l` del dump `[RAG-TUNE]`. Nessun consumatore la leggeva ancora sul percorso di ricerca, quindi il fix è abilitante, non un cambio di comportamento. Corpora comunque distinti per granularità: gate 10.890 chunk (Docnet) vs staging 56.367 (heading-aware). Storia: [audit](./docs/for-developers/audits/2026-08-17-e5-prefix-and-cross-lingual-retrieval-audit.md) |
| #4032 | 🔴 **`backend-e2e-tests.yml` non gira sulle PR verso `main-dev`**: il trigger è `pull_request` verso `main`/`main-staging`. Una PR che modifica le suite E2E può essere mergiata con tutti i check verdi **senza che un solo test E2E sia girato**. Per osservarle: `gh workflow run backend-e2e-tests.yml --ref <branch>` (~16 min) — **e un run di CONTROLLO su `main-dev` intatto**, perché il gate porta fallimenti preesistenti invisibili da oltre un mese (ultimo verde: una PR di release del 2026-08-26) e senza il controllo la tua PR sembra la causa di tutto. Confronta i **tre** conteggi, non solo `Failed`: un `Passed` che scende sono test che non girano più — è così che si è visto che tre test passati erano diventati salti in #4023. Gli insiemi di **nomi** falliti (`comm -13`/`comm -23`) sono una prova più forte dei conteggi |
| #4050 | 🔴 **Un test di integrazione che costruisce l'host nel proprio `InitializeAsync` lo paga per OGNI metodo**, perché xUnit istanzia la classe di test una volta per metodo: ~24s a test. Usa `SharedHostPerTestDatabaseFixture` (host per classe + database clonato per test, ~0,1s) o `IntegrationHostFixture` (host **e** database per classe) via `IClassFixture<T>`, con `ExtraConfiguration`/`ConfigureHost` se l'host va personalizzato. **Ma solo da 2 test in su**: la fixture costa ~25,7s per classe, quindi su una classe con un solo test è più lenta di ciò che sostituisce — `PerClassHostFixtureBreakEvenArchitectureTests` lo blocca. Tre trappole che il compilatore non vede: `BeginTestAsync()` DEVE precedere ogni uso di `_factory` (altrimenti il seed va nel database di avvio e le richieste leggono quello per test); un doppio **con stato** registrato in `ConfigureHost` vive quanto la classe, quindi accumula le chiamate di tutti i suoi test; e il prefisso del database sta entro il budget di `TestDatabaseName` (**≤25** caratteri per la fixture per-test), perché oltre i 63 byte Postgres tronca **in silenzio** e il nome tronco rompe solo le query che cercano quel database per nome — sintomo: `55006` a ogni clone |

---

**Last Updated**: 2026-07-31 | **License**: Proprietary
