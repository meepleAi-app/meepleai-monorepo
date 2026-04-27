# ADR-052 — Frontend Mock Mode Removal

**Status:** Accepted
**Date:** 2026-04-25
**Deciders:** @badsworm
**Supersedes:** ADR-022 §MSW reference, ADR-042 §MSW reference

## Context

The Next.js frontend supported a "mock mode" controlled by the `NEXT_PUBLIC_MOCK_MODE`
environment variable. When enabled it activated:

1. **MSW** (Mock Service Worker) handlers in `apps/web/src/mocks/` intercepting all fetches
2. The **Dev Panel** UI in `apps/web/src/dev-tools/` (scenario switching, request inspector,
   backend toggles) — MeepleDev Phase 2
3. The **fast-dev** infrastructure in `infra/` (`make dev-fast*` targets, scripts,
   `.env.dev.local*`) — MeepleDev Phase 1
4. The **`MockProvider`** wrapper in `apps/web/src/app/mock-provider.tsx`
5. Multiple branches in middleware (`apps/web/src/proxy.ts`) bypassing real auth
6. A CI workflow (`dev-tools-isolation.yml`) enforcing tree-shaking of `dev-tools/`
   from production builds

## Problem

Symptom: visiting `http://localhost:3000` with `NEXT_PUBLIC_MOCK_MODE=true` forced
`isAuthenticated=true` and read `NEXT_PUBLIC_DEV_AS_ROLE=admin`, redirecting `/` → `/admin`.

Beyond the symptom, the mock-mode surface had grown to ~95 frontend files plus 5 infra
files plus 1 CI workflow plus 8 documentation references. The maintenance cost outweighed
the development-velocity benefit, especially since:

- Backend `Api.DevTools` (mock LLM, BGG, S3, etc.) is independently isolated from Release
  builds via `Api.csproj` `<Compile Remove Configuration='Release'>` and `#if DEBUG`
  directives — so backend mocking still works for tests and local dev without any
  frontend mocking.
- Engineers consistently chose `make dev` (real stack) over `make dev-fast*` for any
  non-trivial work.
- MSW handlers diverged from real backend contracts, surfacing bugs only at integration
  time anyway.

## Decision

Remove the entire frontend mock-mode surface. Keep the backend `Api.DevTools` intact.

**In scope (removed):**
- `apps/web/src/mocks/` (MSW handlers + scenarioBridge)
- `apps/web/src/dev-tools/` (Dev Panel UI + stores)
- `apps/web/src/app/mock-provider.tsx`
- `apps/web/public/mockServiceWorker.js`
- `apps/web/__tests__/dev-tools/`, `__tests__/integration/dev-tools/`, `__tests__/bench/fetchInterceptor.bench.ts`
- `msw` devDependency from `apps/web/package.json`
- `dev:mock` script and `msw.workerDirectory` config
- `NEXT_PUBLIC_MOCK_MODE` and `NEXT_PUBLIC_DEV_AS_ROLE` references in `proxy.ts`,
  `usePWA.ts`, `app/api/v1/[...path]/route.ts`, `app/providers.tsx`
- `infra/scripts/dev-fast*.sh`, `infra/scripts/dev-env-check.sh`, `infra/.env.dev.local*`
- `infra/Makefile` targets `dev-fast`, `dev-fast-api`, `dev-fast-full`, `dev-fast-down`,
  `dev-fast-check`
- `.github/workflows/dev-tools-isolation.yml` (now redundant)

**Out of scope (kept):**
- `apps/api/src/Api/DevTools/**` — backend mock services (already Release-excluded)
- `apps/api/tests/Api.Tests/DevTools/**` — backend mock tests
- `apps/web/src/__tests__/mocks/**` — Vitest test infrastructure (independent)
- `make dev`, `make dev-core`, `make alpha` — standard workflows
- Alpha mode logic (`NEXT_PUBLIC_ALPHA_MODE`) — separate system

## Consequences

**Positive:**
- ~95 frontend files + 5 infra files + 1 CI workflow deleted
- Bundle size reduction (post-removal baseline re-measured in PR)
- One less environment variable for new contributors to learn
- `proxy.ts` middleware simpler, single auth path (real backend or `PLAYWRIGHT_AUTH_BYPASS`
  for E2E only)
- No MSW/handler-vs-backend drift surface

**Negative:**
- No more "frontend-only" dev loop. All local dev requires `make dev` (real Postgres + API
  + Redis stack). Mitigated by: stack startup is ~30 s on warm Docker.
- E2E tests cannot mock at the network layer via MSW. They must use Playwright's
  `page.route()` (already the convention for `apps/web/e2e/`) or `PLAYWRIGHT_AUTH_BYPASS=true`.
- Storybook stories that depended on MSW handlers must mock at the component-prop or
  React-Query level instead. (No story currently breaks — verified during removal.)

## Restore Point

Tag `pre-mock-removal` exists on `main-dev` immediately before the removal commit. To
restore the mock-mode surface temporarily for debugging, `git checkout pre-mock-removal`
into a throwaway branch.

## Implementation

PR: feature/mock-mode-removal → main-dev
Spec: `docs/superpowers/specs/2026-04-25-mock-mode-removal-design.md`
Plan: `docs/superpowers/plans/2026-04-25-mock-mode-removal.md`
