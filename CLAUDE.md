# MeepleAI Monorepo - Developer Guide

**AI board game assistant: RAG, multi-agent, living docs**

## Quick Reference

| Task | Command | Dir |
|------|---------|-----|
| Start Dev (full) | `make dev` | `infra/` |
| Start Dev (core) | `make dev-core` | `infra/` |
| Dev from Snapshot | `make dev-from-snapshot` | `infra/` — [guide](./docs/for-developers/workflows/snapshot-seed-workflow.md) |
| Bake Snapshot | `make seed-index` | `infra/` — raro, indicizza tutti i PDF |
| Integration | `make tunnel && make integration` | `infra/` — **Git Bash only (Windows)** |
| Deploy Staging | `make staging` | `infra/` (on server) |
| Game Reset (#1320) | `make game-reset-help` | `infra/` — workflow help, see [spec](./docs/for-developers/specs/2026-05-19-game-entity-reset.md) |
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

**Core Features**: RAG (hybrid retrieval) | Multi-agent AI | PDF processing (OCR) | Community game catalog | SSE streaming | CQRS pattern

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

### DDD Bounded Contexts (18)

| Context | Responsibility |
|---------|---------------|
| Administration | Users, roles, audit, analytics |
| AgentMemory | House rules, memory notes, guest player claims |
| Authentication | Auth flows, sessions, OAuth, 2FA |
| BusinessSimulations | Ledger entries, cost scenarios, resource forecasts |
| DatabaseSync | DB migrations, tunnel management, sync ops |
| DocumentProcessing | PDF upload, extraction, chunking |
| EntityRelationships | Cross-entity links (EntityLink aggregates) |
| Gamification | Achievements, badges, leaderboards |
| GameManagement | Catalog, sessions, FAQs, specs, game books (multi-role 1..N per game) |
| GameToolbox | Card decks, phases, session tool templates |
| GameToolkit | AI toolkit generation, KB-based suggestions |
| KnowledgeBase | RAG, AI agents, chat, vector search |
| SessionTracking | Session notes, scoring, activity tracking |
| SharedGameCatalog | Community DB w/ soft-delete |
| SystemConfiguration | Runtime config, flags |
| UserLibrary | Collections, wishlist, history |
| UserNotifications | Alerts, email, push |
| WorkflowIntegration | n8n, webhooks, logging |

**Layers**: Domain → Application (commands/queries) → Infrastructure

### Key Data Patterns

| Pattern | Implementation |
|---------|---------------|
| **Soft Delete** | `IsDeleted` + `DeletedAt` + `HasQueryFilter(e => !e.IsDeleted)` |
| **Audit** | `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy` |
| **Concurrency** | `[Timestamp] byte[] RowVersion` + catch `DbUpdateConcurrencyException` |

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

> **Reference**: full rationale in [ADR-054 — DevOps Multi-Branch Strategy](./docs/for-claude/architecture/adr/adr-054-devops-multi-branch-strategy.md). Tracking epic: [#842](https://github.com/meepleAi-app/meepleai-monorepo/issues/842).

**Branches**: `main-dev` (dev) | `main-staging` (release) | `main` (prod) | `feature/issue-{n}-{desc}`

**🔴 PR Target Rule**: Feature branches MUST merge to their parent branch (typically `main-dev`)

```bash
git checkout main-dev && git pull
git checkout -b feature/issue-123-desc
git config branch.feature/issue-123-desc.parent main-dev
# work → commit → test → push
git push -u origin feature/issue-123-desc
# PR to main-dev → merge (auto-deletes branch on merge)
```

> **Note**: `frontend-dev` and `backend-dev` were retired on 2026-05-09 (issue #897). All feature branches now target `main-dev` directly. Auto-delete on merge is enabled at repo level — no need to `git branch -D` after PR merge.

**🔴 Branch Hygiene Rule** (issue #806): ALWAYS switch to the parent branch BEFORE creating a feature branch. Never run `git checkout -b feature/...` while HEAD is on another in-progress feature branch — it absorbs the other branch's commits into your new branch's ancestry. Concurrent multi-terminal workflows (incl. AI agentic sessions) are particularly prone to this.

**Pre-creation safety check** — run before `git checkout -b`:

```bash
# Verify HEAD is on the intended parent (main-dev / main),
# NOT on another feature/* branch
git branch --show-current  # MUST print main-dev or main
git status                 # MUST show clean tree
git pull --ff-only         # MUST succeed (no divergence)
git checkout -b feature/issue-{n}-{desc}
```

If `git branch --show-current` prints `feature/...`, STOP. Run `git checkout main-dev && git pull` first.

See also: [CONTRIBUTING.md § Branch Hygiene](./CONTRIBUTING.md#-branch-hygiene--before-creating-a-feature-branch) for the human-facing version (includes opening-PR checklist + recovery via `git rebase --onto`).

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

- **Entity**: Private setters + factory method (`Game.Create()`)
- **Value Object**: Immutable record + validation in factory (`Email.Create()`)
- **Exception**: Domain-specific (`GameNotFoundException`)

### TypeScript Frontend

**Naming**: PascalCase (components/types) | camelCase (functions/vars) | UPPER_SNAKE_CASE (constants)

- **Component**: Typed props + explicit `JSX.Element` return
- **Store**: Zustand with TypeScript interface

*Full examples: [docs/for-developers/workflows/README.md](./docs/for-developers/workflows/README.md)*

### Card Components

Use `MeepleCard` for all entity displays — **never** the deprecated `GameCard` or `PlayerCard`.

```tsx
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
<MeepleCard entity="game" variant="grid" title={game.title} subtitle={game.publisher}
  imageUrl={game.imageUrl} rating={game.averageRating} ratingMax={10} />
```

Entity types: `game` (orange) · `player` (purple) · `collection` (teal) · `event` (rose)
Variants: `grid` (default) · `list` · `compact` · `featured` · `hero`
Docs: [docs/for-developers/frontend/meeple-card-design-tokens.md](./docs/for-developers/frontend/meeple-card-design-tokens.md)

### V2 Migration Components

Phase 0 of the v2 design migration — see [docs/for-developers/specs/2026-04-26-v2-design-migration.md](./docs/for-developers/specs/2026-04-26-v2-design-migration.md) — pre-stubs the 46 feature components introduced by SP4 wave 1+2 mockups under `apps/web/src/components/v2/<feature>/`. The single source of truth for the mapping `<Mockup, Component, Path, Route, AcceptanceCriteria, Status, PR>` is [docs/for-developers/frontend/v2-migration-matrix.md](./docs/for-developers/frontend/v2-migration-matrix.md). Pick `pending` rows from there before implementing v2 features; update `Status` and `PR` in the same PR that lands the implementation.

Path discipline: existing v2 *primitives* live under `apps/web/src/components/ui/v2/` (auth-card, btn, drawer, …); new SP4 *feature compositions* live under `apps/web/src/components/v2/`. Do not collapse the two trees.

## Testing

### Backend (Target: 90%+) — 930+ classes | 13,134+ tests

```bash
cd apps/api/src/Api
dotnet test                                           # All
dotnet test --filter "Category=Unit"                  # Unit only
dotnet test --filter "BoundedContext=GameManagement"  # By context
dotnet test /p:CollectCoverage=true                   # With coverage
```

Patterns: [docs/for-developers/testing/backend/backend-testing-patterns.md](./docs/for-developers/testing/backend/backend-testing-patterns.md)

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
docs/                     # Architecture (adr/), dev guides, API ref, deployment/
infra/                    # docker-compose.yml, secrets/, monitoring/
tests/Api.Tests/          # Backend test suite
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
| Full ops reference | [docs/for-developers/operations/operations-manual.md](./docs/for-developers/operations/operations-manual.md) |

## Known Flaky Tests

Tests confirmed failing on `main-dev` baseline independently of any specific PR.
Triage history: #1349 (closed, Phase 2d carryover) → #1422 (2026-05-21, 12 SharedGameId/PDF cluster resolved) → 2026-05-22 (4 baseline failures cleared; S3Storage entry was stale) → 2026-06-09 (#1887 PdfDocument_SevenStateProgression cleared via PR #2038, baseline now empty).

| Test | File | First observed | Reason | Action |
|---|---|---|---|---|
| _(none — baseline currently clean)_ | | | | |

**Resolved 2026-06-09 (#1887)**: `PdfDocument_SevenStateProgression_ShouldAdvanceThroughAllStates` (fix PR [#2038](https://github.com/meepleAi-app/meepleai-monorepo/pull/2038)) — assertion split into `HaveCount(7)` + typed `OfType<>()` (6 `PdfStateChangedEvent` + 1 `KbDocIndexedEvent` raised on the Ready transition for the activity rail, per BE-3 #1590 B2). The previous baseline entry misattributed the 7th event to PR #1873's `PdfDocumentDeleted` — actual extra event is `KbDocIndexedEvent` from `TransitionTo()` (`PdfDocument.cs:433-443`).

**Resolved 2026-05-22**: 3 documented baseline failures fixed + 1 stale entry removed.
- `Should_Fail_When_GameId_Is_Empty` — fixed by adding `Cascade(CascadeMode.Stop)` to `CreateRuleConflictFaqCommandValidator.RuleFor(x => x.GameId)` so the async `GameExists` check (which calls `GameRef.Shared(Guid.Empty)` → `ArgumentException`) is skipped when `NotEmpty()` already failed.
- `Handle_EmptyGuid_ReturnsNull` (in `GetGameByIdQueryHandlerTests`) — fixed by short-circuiting the handler on `Guid.Empty` before constructing `GameRef.Shared(...)`; the test now also asserts the provider is never consulted. The 4 same-named tests in `DocumentProcessing` were never failing (they mock `repository.GetByIdAsync(Guid)` directly without going through `GameRef`).
- `Handle_WithSearchFilter_ReturnsMatchingGames` — moved to `Integration/GameManagement/GetAllGamesQueryHandlerIntegrationTests.cs` (Testcontainers Postgres), where `EF.Functions.ILike` translates to SQL `ILIKE`. The Unit class retained the non-search scenarios.
- `*_S3Storage_*` (2 tests) — entry was stale: all unit tests in `S3BlobStorageServiceTests` pass; the 11 skipped tests in `S3BlobStorageIntegrationTests` only require Docker.

**Resolved in #1422 (2026-05-21)**: 12 undocumented SharedGameId/PDF cluster failures triaged and cleared.
Root cause: regression from PR #1345/#1347 (Phase 2d delete `GameEntity` + drop `games` table, 2026-05-20). Test fixtures still relied on the dropped `pdf_documents.GameId` column → handlers filtering on `SharedGameId` returned 0 items. Resolution: **11 fixed** via fixture drift correction (add `SharedGameId` to `PdfDocumentEntity`/`TextChunkEntity` setups + `Publisher = "Kosmos"` on `DegradedAgentContext` full-metadata test) + **1 deleted** (`Handle_WithSharedGameId_ResolvesToActualGameId` — Post-Phase 2d the resolver step in `CreateChatThreadCommandHandler:46-54` is a degenerate identity lookup; cross-table resolution no longer exists).

**Policy**: PRs MUST NOT cause the unit-test fail count to grow above this baseline (currently zero). Future regressions: either fix the root cause or skip with `[Trait("Skip", "<issue#>")]` and add a row here in the same PR.

## AI Assistant Rules

### 🔒 Active Freezes

**Design System De-versioning — COMPLETE 2026-05-18** (umbrella #1023 closed, Stage 3 #1026 closed)

All 3 stages shipped (Stage 1 audit #1024 → Stage 2 path-migration #1025/PR #1032 → Stage 3 conformity fixes #1026). Canonical paths are active:
- Feature compositions → `apps/web/src/components/features/<feature>/`
- Primitives → `apps/web/src/components/ui/<primitive>/`

The legacy directories `apps/web/src/components/v2/**` and `apps/web/src/components/ui/v2/**` are empty post-codemod; do not re-introduce them.

Stage 3 conformity fixes shipped per cluster (player-detail, toolkit-detail BE+FE, discover, dashboard REFACTOR-FORWARD, hub/<entity> 3-routes, game-nights runtime) + DetailPageLayout primitive (PR #1112) cross-cutting. Spec: [`docs/for-developers/specs/2026-05-11-design-system-deversioning.md`](./docs/for-developers/specs/2026-05-11-design-system-deversioning.md).

**Visual Gate REMOVED 2026-05-20** — the entire mockup/visual-regression test suite (`apps/web/e2e/visual-conformity/`, `visual-migrated/`, `v2-states/`, `visual-mockups/`) was retired along with the 9 supporting workflows (conformity-* / mockup-* / visual-regression-*) and Playwright projects. False-positive rate (locale drift, font flake, mockup-vs-live divergence) outweighed pickup value; replacement = manual designer review on PRs. Issues #1066 (umbrella WS-C) / #1069 (Phase 3) / #1269 (waiver) closed by the removal PR.

> **Historical**: SP6 v2 expansion FREEZE (issued 2026-05-06 per [#808](https://github.com/meepleAi-app/meepleai-monorepo/issues/808), tied to A11y audit [#807](https://github.com/meepleAi-app/meepleai-monorepo/issues/807)) was **lifted on 2026-05-10** by PR #876 (token redesign — AA-compliant CSS vars + entity Tailwind utilities). Issues #807 and #808 are both CLOSED. **A11y CI restore COMPLETE 2026-05-18** via [#1094](https://github.com/meepleAi-app/meepleai-monorepo/issues/1094) Phase D gate flip: `Frontend - A11y E2E` is now **blocking** (`continue-on-error` removed in `ci.yml`, job in required-jobs list). Final v11 axe run: **0 color-contrast + 0 ARIA violations** across 96 a11y tests (trajectory v4 baseline 103+11 → v11 0, -100% via 17 PRs #1219, #1224-#1260). Companion [#1015](https://github.com/meepleAi-app/meepleai-monorepo/issues/1015) (release-level baseline-diff) also CLOSED COMPLETED 2026-05-18. Original blocker #752 closed 2026-05-12 via #876; supersedes #1179 (duplicate). Audit: [`docs/for-developers/audits/a11y-color-contrast-restoration.md`](./docs/for-developers/audits/a11y-color-contrast-restoration.md). Any axe AA fail now = real regression — investigate, do not skip.

**Token Canonicalization** — Tier 1+2+3+4 complete, 0 project-wide violations (2026-05-12, spec [`2026-05-12-token-canonicalization.md`](./docs/for-developers/specs/2026-05-12-token-canonicalization.md)).

The runtime imports `admin-mockups/design_files/tokens.css` as `apps/web/src/styles/design-tokens-canonical.css`. Legacy v1 names (`--bg-base`, `--gaming-bg-*`, `--nh-bg-*`, `--e-*`) are still aliased via `token-bridge.css` because ~120 CSS-side consumers reference them directly via `var(--*)` literals. The bridge will be removed in **DS-16** (CSS variable migration codemod), separate from this token-class migration.

Theming uses `[data-theme="light|dark"]` (next-themes applies both `class="dark"` AND `data-theme="dark"`). **Default theme is light** (mockup cream `#f7f3ee`), dark accessible via user toggle.

When writing new components:
- ✅ Use semantic tokens: `bg-background`, `bg-card`, `bg-muted`, `text-foreground`, `text-muted-foreground`, `border-border`, `border-border-strong`.
- ✅ Use entity utilities: `bg-entity-game`, `text-entity-session`, `ring-entity-event/30`, etc.
- ❌ Forbidden by ESLint rule `local/no-hardcoded-color-utility` (mode: **error** since DS-15): `bg-white`, `bg-slate-*`, `text-gray-*`, `border-zinc-*`, etc. (full neutral palette).

Exemption: `text-white` / `border-white` / `ring-white` ARE allowed when the same className declares a colored bg (entity utility, gradient, arbitrary `bg-[hsl(…)]`, hue palette, semantic `bg-primary/secondary/accent`). This is the mockup `.e-bg` pattern.

Run `pnpm lint:tokens` to regenerate the inventory in `audits/2026-05-12-token-violations.md`.

**Mockup legacy token guard — DS-17-2 (#2070)** — `pnpm lint:tokens:mockups` scans `admin-mockups/**/*.{html,jsx,css}` for forbidden CSS literals (`var(--bg-base)`, `var(--gaming-*)`, `var(--nh-*)`, `var(--e-*)`) and writes `audits/2026-06-09-mockup-token-violations.{json,md}`. CI runs `--strict --max-baseline 1500` as a whitelist-incremental gate: existing literals are tolerated until DS-16 unwinds the bridge, but a NEW occurrence introduced by a mockup edit fails the build. Use canonical semantic tokens for new mockup CSS (`--background`, `--foreground`, `--card`, `--border`, `--primary`, …). Spec: [`docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md`](./docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md). Umbrella: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063).

**Page-mock story pattern — DS-17-6-v2 (#2092)** — every page-mock migrated to a Storybook story lives side-by-side col Client component (`<ClientComponent>.stories.tsx`) with per-state MSW handler overrides + fixtures from `apps/web/src/__tests__/fixtures/mockup-pilots/`. Phase 2 ships 3 pilot stories (`DashboardClient`, `LibraryContent`, `GameDetailView`) as reference per Phase 3 migration sweep (67 page-mock + 48 component-mock). MSW handlers usano wildcard URL `'*/api/v1/...'` per matchare sia client relativi sia global handlers con `${API_BASE}`. Pattern docs: [`docs/for-developers/frontend/page-mock-story-pattern.md`](./docs/for-developers/frontend/page-mock-story-pattern.md).

**Mockup annotation pattern — DS-17-1 (#2069) + sweep (#2084)** — every user-reachable `apps/web/src/app/.../page.tsx` mapped in `admin-mockups/MOCKUPS_INDEX.md` carries a `@mockup` JSDoc block. Inject via `pnpm mockup-annotations:inject --apply` (idempotent via `MOCKUP-ANNOTATION` marker); audit via `pnpm mockup-annotations:audit --denominator mappable`. CI runs `--denominator mappable --threshold 80` as a **blocking** gate (current state: 100% / 68/68 mappable routes). The `mappable` denominator counts only routes with an INDEX mapping — admin/api/internal routes are excluded because they have no design surface. Don't hand-edit the JSDoc block — fix `MOCKUPS_INDEX.md` and re-run the injector. Pattern docs: [`docs/for-developers/frontend/mockup-annotation-pattern.md`](./docs/for-developers/frontend/mockup-annotation-pattern.md).

**Deferred decisions** (planned for DS-16):
- `--admin-*` token family (admin inline gradients still file-level eslint-disable).
- `--mc-*` MeepleCard palette consolidation.
- CSS variable migration (`var(--bg-base)` → `var(--bg)`) — bridge removal.
- Audit of file-level `eslint-disable local/no-hardcoded-color-utility` directives; convert to line-level or refactor via primitives where feasible.

### DDD Rules

- ✅ Entities: Private setters + factory methods
- ✅ Value Objects: Immutable, validation in factory
- ✅ Repos: Interfaces in Domain, implementation in Infrastructure
- ❌ Domain services directly in endpoints
- ❌ Shared models between commands/queries
- ❌ Direct service injection in endpoints (use MediatR)

### Domain Model — GameNight / Session

**Reference**: [`docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`](./docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md) — 20 invarianti consolidate (9 fatti + 11 derivate), 5 tensioni risolte 2026-06-04. **Vedi anche § Backend Mapping** per la riconciliazione term demo ↔ backend (`GameNightEvent` aggregate, `Session` aggregate, `GameNightRsvp` vs `GameNightInvitation`).

Quando tocchi i bounded context **`SessionTracking`** o **`GameManagement`** (sub-aggregate GameNight `GameNightEvent`), questo spec è la source of truth per:
- Cardinalità GameNight 1→N Session
- 3 timestamp Session distinti (createdAt always, startedAt/completedAt nullable)
- State machine GameNight (planned → in-progress via first Session, → completed manuale)
- Player identity mix (User-linked + guest free)
- Tagging vs RSVP a 5 fasi (tag silente → "Invia inviti" esplicito → pending → confermato)
- Invariante max 1 live per GameNight (parallel play out of scope MVP)
- Sidebar 2 voci game-related: Library (personale) + Games (catalogo, Discover come default tab)

**Asse A v2 implementation** (umbrella #1895 sub-issue #1896): plan TDD in [`docs/superpowers/plans/2026-06-04-asse-a-semantic-alignment.md`](./docs/superpowers/plans/2026-06-04-asse-a-semantic-alignment.md). Plan v2.1 effort ~10.5gg dopo discovery WP4 già shipped upstream (#2053+#1629+#5005). **Stato shipped 2026-06-05 sessione 32**: WP1 (max 1 live) + WP2 (Session.StartedAt+invariante #15+X-Warning-Code+mapping doc) + WP3 (polymorphic ScoreType 4 strategies + UpdateSessionScoresCommand + IDOR guard) + WP4 audit-only + WP5 acceptance. Branch `feature/issue-1896-semantic-alignment` con ~15 commit (12 feat + 2 fix + 3 docs/audit). ~80+ unit test added, 0 regression. Security: 1 HIGH IDOR finding identificato post-merge T10 + fixato in `c1efb4fb6`.

**Backend semantic mapping** (sezione "Backend Mapping" nel domain model spec): demo "GameNight" ↔ backend `GameNightEvent`, demo "tagged player" ↔ `GameNightEvent.PreInvite` (Draft, no event), demo "invited player" ↔ `Publish()` (raises events → email via `GameNightEmailService`), demo `Session.IsLive` ↔ `StartedAt != null && FinalizedAt == null`. Invariante #10 enforcement via `GameNightEvent.StartCurrentSession()` guard → `MaxLiveSessionsExceededException` (HTTP 409 via middleware). Invariante #15 wire via `SessionStartedHandler : INotificationHandler<SessionStartedDomainEvent>`.

Companion: [gap report demo Claude Design](./docs/for-developers/audits/2026-06-04-claude-design-gap-report.md) (38 gap classificati 5-cat: ROUTE/STATE/CTA/ENTITY/TOKEN).

### Asse B — UI Shell + Navigation Pattern (#1897)

**Asse B v2 implementation** (umbrella #1895 sub-issue #1897): plan TDD in [`docs/superpowers/plans/2026-06-04-asse-b-ui-shell-pattern.md`](./docs/superpowers/plans/2026-06-04-asse-b-ui-shell-pattern.md). Plan v2 effort ~6gg dopo discovery (cascade-store + Drawer + sonner già shipped upstream). **Stato shipped 2026-06-05 sessione 33**: WP1 token additions + WP2 MainSidebar 8 voci replicating AdminSidebar pattern + WP3 cascade-store generic DrawerStack semantics + Drawer prefers-reduced-motion + WP4 WizardModal primitive sync/async validate normalize + WP5 StatePreview dev-tool `dynamic({ssr:false})` (tree-shake guaranteed, verified by `apps/web/__tests__/state-preview-tree-shake.test.ts`) + WP6 useNotificationsCounter SSE consumer + WP7 final integration (MainSidebar mounted in `DesktopShell.tsx` `lg+`, StatePreviewProvider wrapped in `app/providers.tsx`). ~120+ unit test, 0 regression. E2E + axe AA gate skeleton in `apps/web/e2e/asse-b-drawer-stack-flow.spec.ts` + `apps/web/__tests__/asse-b-axe.test.tsx`.

### Asse C — Dashboard priority-driven (#1898)

**Asse C v2 implementation** (umbrella #1895 sub-issue #1898): plan TDD in [`docs/superpowers/plans/2026-06-05-asse-c-dashboard-priority-driven.md`](./docs/superpowers/plans/2026-06-05-asse-c-dashboard-priority-driven.md). Effort actual ~6gg (vs v1 stima ~4gg, +2gg post-discovery). **Stato shipped 2026-06-05 sessione 34**: WP1 BE FriendsActivity endpoint (`GET /api/v1/dashboard/friends-activity?limit=10` → `FriendActivityDto[]`) + WP2 ProssimiSection (upcoming GameNights Published+InProgress ASC, "+ Nuova" CTA inline) + WP3 RecentiSection (completed GameNights DESC, MVP/mini-cover thumbnails, "Vedi tutti i completati" footer) + WP4 SuggestedSection (4-6 "Potresti giocare" cards, silent fallback empty/error per MAJ-6 matrix) + WP5 FriendsActivitySection (verbs completed/created/joined, avatar drawer asse-B `openDrawer('player', friendUserId)`) + WP6 GameNightDrawerContent (props-based, asse-B cascade) + WP7 DashboardClient refactor in-place (DEC-1 lockata: 5 entity sections legacy → 4 priority sections in fixed order; DashboardHero + KPI grid preserved). ~75 unit test, 0 regression. E2E skeleton in `apps/web/e2e/dashboard-priority-flow.spec.ts`. Note: Recenti BE endpoint per completed-GN list non yet wired (RecentiSection renderable con empty `null` silent fallback fino al BE wave successivo).

**Asse D follow-up P1 implementation** (umbrella #1895 sub-issue #1899 follow-up): plan TDD in [`docs/superpowers/plans/2026-06-05-asse-d-p1-polymorphic-score-editor.md`](./docs/superpowers/plans/2026-06-05-asse-d-p1-polymorphic-score-editor.md). Polymorphic ScoreType editor primitive (Points/BinaryWin/Objectives/Ranking) wires asse A backend `UpdateSessionScoresCommand`. **Stato shipped 2026-06-05 sessione 35**: T1 types + T2 PointsEditor + BinaryWinEditor + T3 ObjectivesEditor + RankingEditor (`@dnd-kit/sortable`) + T4 dispatcher (tagged `ScoreChangePayload` discriminated union) + T5 `useUpdateSessionScores` mutation hook (`UpdateSessionScoresError` with `kind: 'forbidden' | 'validation' | 'server'`) + T6 wire scores page (backward-compat: `Points` + non-host → legacy `ScoreBoard`; host or non-`Points` → `PolymorphicScoreEditor` + inline `useDebouncedCallback` 500ms autosave) + T7 E2E skeleton in `apps/web/e2e/asse-d-p1-polymorphic-scoring.spec.ts`. ~36 unit test, 0 regression. Known follow-ups: `scoringType` selector + `displayName` field on `PlayerInfo` not yet on `useLiveSessionStore` (T6 hardcodes `'Points'` and adapts `PlayerInfo.name → PlayerOption.displayName`); `MVP_OBJECTIVES_CATALOGUE` is a placeholder array pending game-catalogue wiring.

**Asse D follow-up P2 implementation** (umbrella #1895 sub-issue #1899 follow-up): `/games` hub multi-tab refactor con Discover come default tab (invariante #20 strict). Replaces incondizionato redirect a `/library` (#1521) — risolve broken sidebar link `MainSidebar` `/games?tab=discover`. **Stato shipped 2026-06-05 sessione 36**: DiscoverHub component extracted da `/discover/page.tsx` in `apps/web/src/components/features/discover/DiscoverHub.tsx` (pure render-only, accetta optional `pathnameOverride` per URL writes scoping) + `/games/page.tsx` refactor incondizionato `redirect('/library')` → multi-tab hub orchestrator (DEC-1 lockato: Opt A refactor `/games` come hub multi-tab) con 4 tab (`discover` default / `catalogo` / `trending` / `community`) + 3 ComingSoon placeholder tabs + parseTab fallback su tab invalido (default Discover) + miniNav config 4 tabs strip + `/discover` standalone route preserved per backward compat (existing bookmarks) + unit tests 15 nuovi (11 page hub + 4 DiscoverHub smoke) + E2E skeleton in `apps/web/e2e/asse-d-p2-games-discover-hub.spec.ts` (7 scenari: default route + discover/catalogo/trending/community tabs + invalid fallback + `/discover` backward-compat). 0 regression (159 test discover+games pass). Risolve broken-link issue su `MainSidebar` voice Games → `/games?tab=discover`.

**Asse D follow-up P3 implementation** (umbrella #1895 sub-issue #1899 follow-up): `/onboarding` 3-step generic wizard refactor usando `WizardModal` asse-B (replaces legacy `OnboardingTourClient` 5-step page-flow). Riusa `InterestsStep` + `FirstGameStep` esistenti (Issue #132) come step 1 e 2; step 3 `InviteFriendComingSoonStep` placeholder (feature deferred a sub-issue futura). BGG legal constraint (#1903 ADR): user-side BGG access bloccato per ToS compliance — `FirstGameStep` usa catalog interno (`api.games.getAll`) NON `useSearchBggGames` (admin-only). Invited-user `OnboardingWizard` 5-step token-based (`/accept-invite`) NON toccato. **Stato shipped 2026-06-05 sessione 37**: `OnboardingGenericWizard` orchestrator (gate `validate` su `interestsCompleted` / `firstGameCompleted` flag interno) + `InviteFriendComingSoonStep` skip-only placeholder + `/onboarding/page.tsx` mounts new wizard + deleted deprecated `OnboardingTourClient.tsx` + relativi test. 13 unit test nuovi (9 wizard + 4 placeholder), 91 component test invariati 0 regression. E2E skeleton in `apps/web/e2e/asse-d-p3-onboarding-wizard.spec.ts`.

### Known Pitfalls (Issues)

| Issue | Rule |
|-------|------|
| #2567 | Endpoint flow: DTOs → Queries → Commands → Validators → Handlers → Routing |
| #2568 | Exceptions: `ConflictException` (409), `NotFoundException` (404) — never `InvalidOperationException` (500) |
| #2565 | DI: Register both `IService` interface and implementation |
| #2593 | Kill testhost before running tests; use culture-independent `$"{val*100:0}%"` |
| #2600 | OAuth: Defensive validation + InMemory transaction + manual rollback |
| #2620 | FK constraints: seed dependent entities first; HybridCache needs `IHybridCacheService` for event handlers |

---

**Last Updated**: 2026-04-11 | **License**: Proprietary
