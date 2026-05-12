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
| GameManagement | Catalog, sessions, FAQs, specs |
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

## AI Assistant Rules

### 🔒 Active Freezes

**Design System De-versioning FREEZE** (active 2026-05-11, umbrella #1023 / Stage 1 audit #1024)

Until Stage 2 path-migration PR lands, **no new files** may be added under:
- `apps/web/src/components/v2/**`
- `apps/web/src/components/ui/v2/**`

Implementations of `missing` components (per [audit report](./docs/for-developers/audits/2026-05-11-mockup-conformity.md)) MUST wait for the post-Stage-2 canonical paths:
- Feature compositions → `apps/web/src/components/features/<feature>/`
- Primitives → `apps/web/src/components/ui/<primitive>/`

Stage 2 codemod (atomic rename + import fix) is the unblocker. Exceptions require explicit user sign-off in the PR description. Reference: [`docs/for-developers/specs/2026-05-11-design-system-deversioning.md`](./docs/for-developers/specs/2026-05-11-design-system-deversioning.md).

> **Historical**: SP6 v2 expansion FREEZE (issued 2026-05-06 per [#808](https://github.com/meepleAi-app/meepleai-monorepo/issues/808), tied to A11y audit [#807](https://github.com/meepleAi-app/meepleai-monorepo/issues/807)) was **lifted on 2026-05-10** by PR #876 (token redesign — AA-compliant CSS vars + entity Tailwind utilities). Issues #807 and #808 are both CLOSED. Restore A11y CI job (`Frontend - A11y E2E`) to blocking (`continue-on-error: false`) when verified green on `main-dev`.

**Token Canonicalization** — Tier 1+2+3+4 complete, 0 project-wide violations (2026-05-12, spec [`2026-05-12-token-canonicalization.md`](./docs/for-developers/specs/2026-05-12-token-canonicalization.md)).

The runtime imports `admin-mockups/design_files/tokens.css` as `apps/web/src/styles/design-tokens-canonical.css`. Legacy v1 names (`--bg-base`, `--gaming-bg-*`, `--nh-bg-*`, `--e-*`) are still aliased via `token-bridge.css` because ~120 CSS-side consumers reference them directly via `var(--*)` literals. The bridge will be removed in **DS-16** (CSS variable migration codemod), separate from this token-class migration.

Theming uses `[data-theme="light|dark"]` (next-themes applies both `class="dark"` AND `data-theme="dark"`). **Default theme is light** (mockup cream `#f7f3ee`), dark accessible via user toggle.

When writing new components:
- ✅ Use semantic tokens: `bg-background`, `bg-card`, `bg-muted`, `text-foreground`, `text-muted-foreground`, `border-border`, `border-border-strong`.
- ✅ Use entity utilities: `bg-entity-game`, `text-entity-session`, `ring-entity-event/30`, etc.
- ❌ Forbidden by ESLint rule `local/no-hardcoded-color-utility` (mode: **error** since DS-15): `bg-white`, `bg-slate-*`, `text-gray-*`, `border-zinc-*`, etc. (full neutral palette).

Exemption: `text-white` / `border-white` / `ring-white` ARE allowed when the same className declares a colored bg (entity utility, gradient, arbitrary `bg-[hsl(…)]`, hue palette, semantic `bg-primary/secondary/accent`). This is the mockup `.e-bg` pattern.

Run `pnpm lint:tokens` to regenerate the inventory in `audits/2026-05-12-token-violations.md`.

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
