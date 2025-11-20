# Repository Guidelines

## Project Structure & Module Organization
MeepleAI is a DDD monorepo rooted in `apps/meepleai-api/src/meepleai-api` (bounded contexts under `BoundedContexts/*`), `apps/meepleai-web` (feature folders inside `src/app`, shared UI in `src/components`), and workers in `apps/meepleai-unstructured`, `apps/smoldocling-service`, and `apps/meepleai-embedding`. Platform scaffolding stays in `infra` (Compose stacks, secrets) and `tools` (automation scripts such as `tools/secrets/init-secrets.sh`). Reference material lives in `docs`, fixtures in `tests`, and generated secrets inside `infra/secrets/*.txt`.

## Build, Test, and Development Commands
- `cd tools/secrets && ./init-secrets.sh`: generate local secret placeholders once.
- `cd infra && docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis meepleai-n8n`: boot mandatory services.
- API loop: `cd apps/api && dotnet restore && dotnet build && dotnet test`; apply schema changes with `dotnet ef database update --project src/Api`.
- Web loop: `cd apps/web && pnpm install && pnpm dev`; ship bundles via `pnpm build && pnpm start`.
- Quality gates: `pnpm lint`, `pnpm lint:fix`, `pnpm typecheck`, `pnpm test:coverage`, `pnpm test:e2e`, `pnpm test:a11y`, `pnpm test:performance`, plus `pwsh tools/measure-coverage.ps1 -Project api`.

## Coding Style & Naming Conventions
`.editorconfig` enforces UTF-8, final newlines, and four-space indents for `.cs`. Preserve the Domain → Application → Infrastructure → Http layering, keep types `PascalCase`, locals `camelCase`, and reserve `SCREAMING_SNAKE_CASE` for shared constants. React components remain `PascalCase`, hooks start with `use*`, feature routes live under `src/app/<feature>`, and imports stay sorted. Analyzer warnings (`CA2000`, nullable) fail builds, so run `pnpm lint` before committing.

## Testing Guidelines
Backend specs reside in `apps/api/tests` (xUnit + Testcontainers); follow the `Given_When_Should` naming convention and share fixtures via `[Collection("Context Scenario")]`. Frontend unit tests live in `apps/web/__tests__`, and Playwright suites in `apps/web/e2e` (preview with `pnpm test:e2e:ui`). CI requires ≥90 % coverage for API and web; watch regressions with `tools/coverage-trends.ps1` or `.sh`.

## Commit & Pull Request Guidelines
Commits use Conventional Commits with scope and issue reference, for example `feat(api): add ingestion hook (#1421)`. Branches follow `feature/<issue>-<slug>` or `fix/<issue>-<slug>` and stay rebased on `main`. Every PR follows `PR_DESCRIPTION.md`, covering Summary, Key Achievements, Metrics (coverage deltas), and Testing evidence (commands + screenshots). Link the GitHub issue, attach UI screenshots for visual work, and wait for lint/unit/Playwright checks before requesting review.

## Security & Configuration Tips
Never commit secrets; rely on the init script and keep `.env` files local. Prefer service URLs defined in `infra/docker-compose.*.yml`, install `pre-commit` hooks for secret scanning, and audit dependencies with `dotnet list package --vulnerable` plus `pnpm audit`. Keep Compose services updated to avoid drift across environments.
