# Repository Guidelines

## Project Structure & Module Organization
MeepleAI is a DDD-oriented monorepo. Core roots include `apps/meepleai-api/src/meepleai-api` (ASP.NET Core service with bounded contexts under `BoundedContexts/*`), `apps/meepleai-web` (Next.js 16 UI with feature folders in `src/app` and shared components in `src/components`), and the ingestion workers in `apps/meepleai-unstructured`, `apps/smoldocling-service`, and `apps/meepleai-embedding`. Platform services live in `infra` (Docker Compose, env files) and `tools` (e.g., `tools/secrets/init-secrets.sh` for secret scaffolding). Shared fixtures and scenario data live under `/tests`, while detailed references land in `docs/` (architecture, testing, security).

## Build, Test, and Development Commands
- Initialize secrets once: `cd tools/secrets && ./init-secrets.sh` (writes to `infra/secrets/*.txt`).
- Bring up dependencies: `cd infra && docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis meepleai-n8n`.
- API cycle: `cd apps/api && dotnet restore && dotnet build && dotnet test`; run `dotnet ef database update --project src/Api` after schema changes.
- Web cycle: `cd apps/web && pnpm install && pnpm dev`; bundle via `pnpm build && pnpm start`.
- Coverage & reports: `pwsh tools/measure-coverage.ps1 -Project api` (or `-GenerateHtml`) and `pnpm test:coverage`; Playwright flows run through `pnpm test:e2e`, with `pnpm test:e2e:report` to inspect artifacts.

## Coding Style & Naming Conventions
`.editorconfig` enforces UTF-8, final newlines, and four-space indents for `.cs` plus strict analyzer severities (e.g., `CA2000`, nullable warnings). Keep files aligned to the `Domain → Application → Infrastructure → Http` layering and use `PascalCase` for types, `camelCase` variables, and `SCREAMING_SNAKE_CASE` only for shared constants such as `TestConstants`. The frontend relies on ESLint + TypeScript—run `pnpm lint`, `pnpm lint:fix`, and `pnpm typecheck` before committing. React components stay `PascalCase` in `src/components`, hooks start with `use*`, and route segments follow feature-based folder names under `src/app`. Adopt the BDD naming noted in `CONTRIBUTING.md` for tests (`Given_When_Should` phrasing) and keep imports sorted.

## Testing Guidelines
CI enforces ≥90% coverage for both API and web. Backend suites reside in `apps/api/tests` using xUnit + Testcontainers; tag fixtures with descriptive `[Collection("Context Scenario")]` attributes and favor shared builders over inline setup (see `docs/02-development/testing`). Frontend unit tests live in `apps/web/__tests__`, while Playwright specs live in `apps/web/e2e` and can be previewed with `pnpm test:e2e:ui`. Accessibility and performance checks run through `pnpm test:a11y`, `pnpm audit:a11y`, and `pnpm test:performance`. Use `pwsh tools/coverage-trends.ps1` or `bash tools/coverage-trends.sh` before merging to catch regressions early.

## Commit & Pull Request Guidelines
Follow Conventional Commits with scoped types plus the issue reference, mirroring history like `refactor(frontend): reorganize components root (#1355)`. Branches should follow `feature/<issue>-<slug>` / `fix/<issue>-<slug>` naming and stay rebased on `main`. PRs must fill out the template from `PR_DESCRIPTION.md`: include Summary, Key Achievements, Metrics (coverage deltas), and Testing (commands/screenshots). Link the GitHub issue, attach UI screenshots for visual changes, and confirm CI (lint, unit, Playwright) before requesting review.

## Security & Configuration Tips
Secrets never enter git—use the init script to populate Docker secrets, keep `.env` files local, and reference settings via `infra/docker-compose.*.yml`. Install the safeguards with `pre-commit install` so secret scanning and formatting hooks run automatically. Audit dependencies regularly with `dotnet list package --vulnerable` and `pnpm audit`, and prefer service URLs from `infra` compose files rather than ad hoc ports to avoid configuration drift.
