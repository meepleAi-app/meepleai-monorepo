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

### DDD Bounded Contexts (21)

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
| WorkflowIntegration | n8n, webhooks, logging |

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

**Policy**: PRs MUST NOT grow the unit-test fail count above baseline (zero). Future regressions: fix the root cause OR skip with `[Trait("Skip", "<issue#>")]` and add a row here in the same PR.

## AI Assistant Rules

### 🔒 Active Freezes

**BGG user-side asset ban — 2026-06-10** ([#2123](https://github.com/meepleAi-app/meepleai-monorepo/issues/2123)) — Hard ban on browser requests to `cf.geekdo-images.com`, `*.boardgamegeek.com`, `images.geekdo.com`, `geekdo-images.com`. Three-layer enforcement: (1) data plane (seed manifests + `SeedManifestGame` stripped of image props + nullify migration `20260610152201`); (2) resolution plane (`SharedGameDto.CoverUrl` single FE source, placeholder fallback via `cover-utils.ts`); (3) network plane (`next.config.js` explicit allowlist, no `**` catch-all + ESLint `local/no-bgg-host` + `pnpm lint:bgg` gate). Prometheus `meepleai_bgg_url_attempted_render_total` SLO=0; any nonzero = P1. See [ADR-059 §5](./docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md). Admin server-to-server BGG paths (`apps/web/src/app/admin/**`, `components/admin/**`) remain legitimate per ADR-059 §2.

> A11y AA: any axe color-contrast/ARIA fail = real regression (gate is blocking) — investigate, never skip.

### Domain Model — GameNight / Session

**Reference (source of truth)**: [`2026-06-04-gamenight-session-domain-model.md`](./docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md) — 20 invariants, backend semantic mapping (demo term ↔ `GameNightEvent`/`Session` aggregates). Consult it when touching **`SessionTracking`** or **`GameManagement`** (GameNight sub-aggregate): GameNight 1→N Session; 3 Session timestamps (createdAt always, startedAt/completedAt nullable); state machine planned → in-progress → completed; player identity mix (User-linked + guest); max 1 live per GameNight; sidebar Library (personale) + Games (catalogo, Discover default tab).

**🔴 Nav rule (#1977 + #2158)**: `AppTopBar` is the **single source-of-truth for primary desktop navigation** (5-id `TOP_BAR_NAV_IDS` in `apps/web/src/config/navigation.ts` + Altro overflow). The persistent desktop `MainSidebar` was rolled back/deleted — `MainSidebar` now mounts ONLY inside the mobile hamburger `SideDrawer` (`<lg`). Do NOT re-introduce a persistent desktop sidebar.

**Live-session scoring** (epic #2354): polymorphic ScoreType (Points/BinaryWin/Objectives/Ranking) is the current pattern; write scores via `useUpdateSessionScores`, never the store directly (ESLint `local/no-store-scores-direct` = **error**). Shipped-implementation diaries (Asse A–D, Session-live G1/G5a): [claude-md-history.md](./docs/for-claude/claude-md-history.md#gamenight--session-asse-ad--shipped-implementation-diaries).

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

---

**Last Updated**: 2026-07-31 | **License**: Proprietary
