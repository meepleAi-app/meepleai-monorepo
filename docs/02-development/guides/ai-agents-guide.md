# Repository Guidelines

## Project Structure & Module Organization
Backend services live in `apps/api/src/Api`, split by feature folders with cross-cutting plumbing wired through `Program.cs` and `Infrastructure/`. API-focused xUnit suites are under `apps/api/tests`. The Next.js frontend resides in `apps/web/src`, where routes live in `app/` (App Router), shared utilities in `lib/`, React components under `components/`, and Jest specs in `__tests__/`; browser automation sits in `apps/web/e2e`. Operational collateral is grouped into `infra/` for Docker + environment assets, `docs/` for deep architecture notes, and `tools/` for helper scripts.

## Build, Test, and Development Commands
- `cd apps/api && dotnet build` compiles backend services; append `dotnet test /p:CollectCoverage=true` to validate and gather coverage.
- `cd apps/web && pnpm dev` runs the Next.js dev server, while `pnpm build` emits the production bundle.
- `pnpm lint`, `pnpm lint:fix`, and `pnpm typecheck` enforce Airbnb ESLint rules and strict TypeScript before commits land.
- `cd infra && docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis meepleai-n8n` provisions the shared data stack powering both apps.
- `pwsh tools/measure-coverage.ps1 -Project api -GenerateHtml` aggregates multi-project coverage when you need a reportable artifact.

## Coding Style & Naming Conventions
Follow Microsoft C# guidelines: four-space indentation, nullable reference types enabled, async-first services, and structured logging via `ILogger<T>`. Lean on explicit DTOs and keep controllers thin. TypeScript runs in strict mode with two-space indentation, functional React components, and hook-based state. Tests use BDD-style names (`Method_WhenCondition_ThenExpectation`). Branches follow `feature/<issue-id>-slug` or `fix/<issue-id>-slug`, and Conventional Commits such as `feat(api): add semantic search scoring` keep history machine-readable. Install and run `pre-commit` so formatting, lint, and secret scans match CI.

## Testing Guidelines
xUnit and Moq cover backend units and integrations; Jest, Testing Library, and Playwright validate the frontend, with `pnpm test:a11y` and `pnpm audit:a11y` guarding accessibility. Maintain ≥80% coverage on the API and ≥90% on the web app—CI blocks regressions. Work outside-in: add the failing spec, implement the fix, then rerun `dotnet test`, `pnpm test`, and targeted Playwright suites before pushing.

## Commit & Pull Request Guidelines
Keep branches rebased on `main` and reference issues directly (e.g., `Closes #123`). Every PR should describe behavior changes, list the commands you ran (lint/type/tests), and attach screenshots or API samples when UI or surface area changes occur. Avoid force-pushing once reviews start so discussion threads remain intact, and respond to feedback with follow-up commits instead of amend + push.

## Security & Configuration Tips
Copy environment templates from `infra/env/*.example` before running services and keep secrets such as `OPENROUTER_API_KEY`, `ConnectionStrings__Postgres`, and `NEXT_PUBLIC_API_BASE` out of source control. Consult `SECURITY.md` for rotation timelines and disclosure steps. Prefer sealed secrets or local user secrets instead of `.env` commits, and let GitHub Advanced Security plus `pre-commit` scanners run before every push to prevent accidental exposure.

