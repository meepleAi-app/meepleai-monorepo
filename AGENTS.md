# Repository Guidelines

## Project Structure & Module Organization
Backend code lives in `apps/api/src/Api` with feature folders and `Program.cs` wiring, and xUnit suites sit in `apps/api/tests`. The Next.js frontend is in `apps/web/src`, routes in `pages/`, shared helpers in `lib/`, Jest specs in `__tests__/`, and Playwright suites in `apps/web/e2e`. Supporting materials are under `infra/` for Docker and env templates, `docs/` for architecture detail, and `tools/` for automation scripts.

## Build, Test, and Development Commands
- `cd apps/api && dotnet build` compiles the backend; follow with `dotnet test` or `/p:CollectCoverage=true` for validation.
- `cd apps/web && pnpm dev` runs the web app, while `pnpm build` produces the production bundle.
- `pnpm lint`, `pnpm lint:fix`, and `pnpm typecheck` enforce ESLint and TypeScript strictness before commits.
- `cd infra && docker compose up -d postgres qdrant redis n8n` launches the local data stack powering both apps.
- `pwsh tools/measure-coverage.ps1 -Project api` (or `-GenerateHtml`) aggregates coverage across projects.

## Coding Style & Naming Conventions
Follow Microsoft C# guidelines with four-space indentation, nullable reference types, async-first services, and structured logging via `ILogger<T>`. TypeScript uses strict mode, two-space indentation, functional React components, and Airbnb lint rules; reserve `pnpm lint:fix` for focused cleanups. Tests adopt BDD method names (`Method_WhenCondition_ThenExpectation`) with fixtures near the suite. Install `pre-commit` to run formatting, lint, and secret scans automatically.

## Testing Guidelines
xUnit and Moq cover the API, while Jest, Testing Library, and Playwright (plus `pnpm test:a11y` and `pnpm audit:a11y`) exercise the web app. Maintain ≥80% backend and ≥90% frontend coverage—CI blocks merges below target. Work outside-in: write the failing BDD test, implement the fix, then rerun `dotnet test`, `pnpm test`, and targeted suites when dialing in failures.

## Commit & Pull Request Guidelines
Create branches as `feature/<issue-id>-<slug>` or `fix/<issue-id>-<slug>` and keep them rebased on `main`. Use Conventional Commits—`feat(api): add semantic search scoring tweaks`—to keep history and automation tidy. Before opening a PR, run lint, type check, and test commands, record results in the template, link the issue (`Closes #123`), and attach UI screenshots when relevant. Request a review and avoid force-pushing during discussion so feedback threads remain intact.

## Security & Configuration Tips
Copy environment templates from `infra/env/*.example` before starting services and keep secrets like `OPENROUTER_API_KEY`, `ConnectionStrings__Postgres`, and `NEXT_PUBLIC_API_BASE` out of Git. Review `SECURITY.md` for rotation and disclosure expectations, and let pre-commit and GitHub scanning block accidental exposures.
