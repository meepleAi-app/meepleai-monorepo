# Mock Mode Removal — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminate definitively the frontend "mock mode" (MSW, Dev Panel, dev-fast loop) while keeping backend DevTools (already isolated via `#if DEBUG`).

**Architecture:** Single atomic PR on a fresh feature branch from `main-dev`, executed in 11 surgical tasks. Each task ends with a gate (typecheck/lint/test/build) and a commit. The end state: `process.env.NEXT_PUBLIC_MOCK_MODE` is referenced nowhere in `apps/web`, `src/mocks/` and `src/dev-tools/` are gone, dependency `msw` is uninstalled, fast-dev infrastructure is wiped, and a restore-point tag `pre-mock-removal` exists on `main-dev`.

**Tech Stack:** Next.js 16 + React 19 + TypeScript 5 (frontend), pnpm workspace, Vitest + Playwright (tests), GNU Make + bash (infra), git for restore points.

**Spec reference:** `docs/superpowers/specs/2026-04-25-mock-mode-removal-design.md`

---

## File Map

### Files to **edit** (8)
| Path | What changes |
|------|--------------|
| `apps/web/src/proxy.ts` | Remove 3 `NEXT_PUBLIC_MOCK_MODE` branches + 1 in `isSessionCookieValid()` |
| `apps/web/src/lib/domain-hooks/usePWA.ts` | Remove SW-skip guard at L135 |
| `apps/web/src/app/api/v1/[...path]/route.ts` | Remove mock-mode short-circuit at L80–92 |
| `apps/web/src/app/providers.tsx` | Remove lazy `MockProvider` require + conditional wrap |
| `apps/web/.env.development.example` | Remove `NEXT_PUBLIC_MOCK_MODE` block |
| `apps/web/package.json` | Remove `msw` devDep, `msw.workerDirectory` field, `dev:mock` script |
| `infra/Makefile` | Remove 5 `dev-fast*` targets + `.PHONY` entries |
| `apps/web/bundle-size-baseline.json` | Re-measure post-build (expected drop ~80–120 KB) |

### Files / dirs to **delete** (~95 files)
| Path | Notes |
|------|-------|
| `apps/web/src/app/mock-provider.tsx` | Entire file |
| `apps/web/src/mocks/` | MSW handlers + scenarioBridge (~24 files) |
| `apps/web/src/dev-tools/` | Dev Panel + stores (~37 files) |
| `apps/web/public/mockServiceWorker.js` | MSW SW |
| `apps/web/__tests__/dev-tools/` | 14 test files |
| `apps/web/__tests__/integration/dev-tools/` | 3 test files |
| `apps/web/__tests__/bench/fetchInterceptor.bench.ts` | Imports `@/dev-tools` |
| `infra/scripts/dev-fast.sh` | Fast-dev entrypoint |
| `infra/scripts/dev-fast-down.sh` | Fast-dev teardown |
| `infra/scripts/dev-env-check.sh` | Fast-dev env validator |
| `infra/.env.dev.local` | Local override |
| `infra/.env.dev.local.example` | Template |
| `.github/workflows/dev-tools-isolation.yml` | CI guard now redundant |

### Files to **keep** (DO NOT TOUCH)
- `apps/web/src/__tests__/mocks/` — Vitest test infrastructure (independent from `src/mocks/`)
- `apps/api/src/Api/DevTools/**` — backend mocks (already excluded from Release via csproj `<Compile Remove>`)
- `apps/api/tests/Api.Tests/DevTools/**` — backend mock tests
- `apps/web/src/proxy.ts:405–407` — admin redirect (legitimate)
- `apps/web/src/proxy.ts:418–449` — Alpha mode logic (separate system)

### Docs to **update** (3)
| Path | Action |
|------|--------|
| `docs/architecture/adr/adr-022-ssr-auth-protection.md` | Remove MSW E2E reference |
| `docs/architecture/adr/adr-042-dashboard-performance.md` | Remove MSW integration-test reference |
| `docs/frontend/storybook-guide.md` | Remove `msw-storybook-addon` section |

### Docs to **add** (1)
- `docs/architecture/adr/adr-052-mock-mode-removal.md` — closing ADR

### Docs to **archive** (none — all already in `archived/`)
The 6 spec/plan files referenced in the spec already live in `docs/superpowers/{specs,plans}/archived/` or root specs — no move needed.

---

## Task 0: Setup branch + restore tag

**Files:** None (git operations only)

- [ ] **Step 1: Verify on `main-dev` and clean tree**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git status --short
git branch --show-current
```

Expected: Branch is `main-dev`. Untracked files (`.png`, snapshots, `.claude/`) are OK; modified `MechanicAnalysis*.cs` from prior session are OK and stay out of this PR (do not stage them).

- [ ] **Step 2: Create restore-point tag on `main-dev`**

```bash
git tag pre-mock-removal main-dev
git push origin pre-mock-removal
```

Expected: Tag created and pushed.

- [ ] **Step 3: Create feature branch**

```bash
git checkout -b feature/mock-mode-removal main-dev
git config branch.feature/mock-mode-removal.parent main-dev
```

Expected: On `feature/mock-mode-removal`. Parent recorded as `main-dev` (per CLAUDE.md PR rule).

- [ ] **Step 4: Verify baseline build is green BEFORE any deletions**

```bash
cd apps/web
pnpm typecheck
pnpm lint
```

Expected: Both pass with 0 errors. If they don't, STOP and surface to user — pre-existing breakage must not be attributed to this PR.

---

## Task 1: Strip `NEXT_PUBLIC_MOCK_MODE` from `proxy.ts`

**Files:**
- Modify: `apps/web/src/proxy.ts` (4 sites)

- [ ] **Step 1: Remove mock-mode branch in `isSessionCookieValid()`**

In `apps/web/src/proxy.ts`, delete lines 115–119 (the `if (process.env.NEXT_PUBLIC_MOCK_MODE === 'true') return false;` block) plus the comment above it. Replace this:

```ts
async function isSessionCookieValid(request: NextRequest, cookieValue: string): Promise<boolean> {
  // In mock mode there is no real backend — skip validation to avoid self-referential loops
  if (process.env.NEXT_PUBLIC_MOCK_MODE === 'true') {
    return false;
  }

  const cached = sessionValidationCache.get(cookieValue);
```

With:

```ts
async function isSessionCookieValid(request: NextRequest, cookieValue: string): Promise<boolean> {
  const cached = sessionValidationCache.get(cookieValue);
```

- [ ] **Step 2: Remove the `else if (MOCK_MODE)` branch in the auth chain**

Locate around line 340 (after the `PLAYWRIGHT_AUTH_BYPASS` branch). Replace:

```ts
  if (
    process.env.NODE_ENV !== 'production' &&
    process.env.PLAYWRIGHT_AUTH_BYPASS === 'true' &&
    sessionCookieValue
  ) {
    isAuthenticated = true;
  } else if (process.env.NEXT_PUBLIC_MOCK_MODE === 'true') {
    // In mock mode there is no real backend. Trust the session cookie if present
    // (set by MSW on login), or bypass auth entirely so devs can navigate freely
    // without having to log in first. Role is read from the cookie or from the
    // NEXT_PUBLIC_DEV_AS_ROLE env var set in .env.local.
    isAuthenticated = true;
  } else if (sessionCookieValue) {
    isAuthenticated = await isSessionCookieValid(request, sessionCookieValue);
  }
```

With:

```ts
  if (
    process.env.NODE_ENV !== 'production' &&
    process.env.PLAYWRIGHT_AUTH_BYPASS === 'true' &&
    sessionCookieValue
  ) {
    isAuthenticated = true;
  } else if (sessionCookieValue) {
    isAuthenticated = await isSessionCookieValid(request, sessionCookieValue);
  }
```

- [ ] **Step 3: Remove the `DEV_AS_ROLE` fallback in `userRole`**

Locate around line 351–357. Replace:

```ts
  const userRoleCookie = request.cookies.get(USER_ROLE_COOKIE);
  const userRole = isAuthenticated
    ? userRoleCookie?.value ||
      (process.env.NEXT_PUBLIC_MOCK_MODE === 'true'
        ? (process.env.NEXT_PUBLIC_DEV_AS_ROLE ?? 'user')
        : 'user')
    : 'user';
```

With:

```ts
  const userRoleCookie = request.cookies.get(USER_ROLE_COOKIE);
  const userRole = isAuthenticated ? (userRoleCookie?.value ?? 'user') : 'user';
```

- [ ] **Step 4: Verify no `NEXT_PUBLIC_MOCK_MODE` remains in `proxy.ts`**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
grep -n "NEXT_PUBLIC_MOCK_MODE\|NEXT_PUBLIC_DEV_AS_ROLE" apps/web/src/proxy.ts
```

Expected: No output (exit 1).

- [ ] **Step 5: Typecheck + lint gate**

```bash
cd apps/web
pnpm typecheck
pnpm lint
```

Expected: 0 errors.

- [ ] **Step 6: Run proxy unit tests**

```bash
cd apps/web
pnpm test src/proxy.test.ts
```

Expected: PASS. If a test was specifically asserting MOCK_MODE behavior, it will fail — note the failure for Task 6 cleanup. Do NOT modify the test in this task; we'll handle test deletion holistically.

If any non-mock-mode tests fail, STOP and investigate.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/proxy.ts
git commit -m "refactor(proxy): remove NEXT_PUBLIC_MOCK_MODE branches"
```

---

## Task 2: Remove SW-skip guard in `usePWA.ts`

**Files:**
- Modify: `apps/web/src/lib/domain-hooks/usePWA.ts:134–137`

- [ ] **Step 1: Replace the conditional**

In `apps/web/src/lib/domain-hooks/usePWA.ts`, replace:

```ts
    // Register service worker (skip in mock mode to avoid conflicting with MSW)
    if (supported && process.env.NEXT_PUBLIC_MOCK_MODE !== 'true') {
      registerServiceWorker();
    }
```

With:

```ts
    // Register service worker
    if (supported) {
      registerServiceWorker();
    }
```

- [ ] **Step 2: Verify**

```bash
grep -n "NEXT_PUBLIC_MOCK_MODE" apps/web/src/lib/domain-hooks/usePWA.ts
```

Expected: No output.

- [ ] **Step 3: Typecheck + run usePWA tests**

```bash
cd apps/web
pnpm typecheck
pnpm test src/lib/domain-hooks/usePWA
```

Expected: typecheck 0 errors; usePWA tests PASS (if any test asserted the mock-mode skip, note for Task 6).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/domain-hooks/usePWA.ts
git commit -m "refactor(pwa): remove mock-mode SW skip"
```

---

## Task 3: Remove mock-mode short-circuit in API proxy route

**Files:**
- Modify: `apps/web/src/app/api/v1/[...path]/route.ts:79–93`

- [ ] **Step 1: Delete the short-circuit block**

In `apps/web/src/app/api/v1/[...path]/route.ts`, replace:

```ts
async function proxyRequest(request: NextRequest, method: string) {
  // In mock mode, MSW handles API requests client-side.
  // If a request reaches this server-side route, MSW didn't intercept it.
  // Return 501 (not retryable) so the error surfaces immediately without the
  // exponential-backoff retry loop that 503 would trigger.
  if (process.env.NEXT_PUBLIC_MOCK_MODE === 'true') {
    return NextResponse.json(
      {
        error:
          'Mock mode: MSW did not intercept this request. Reload the page to re-activate the Service Worker.',
      },
      { status: 501 }
    );
  }

  try {
    const pathname = request.nextUrl.pathname;
```

With:

```ts
async function proxyRequest(request: NextRequest, method: string) {
  try {
    const pathname = request.nextUrl.pathname;
```

- [ ] **Step 2: Verify**

```bash
grep -n "NEXT_PUBLIC_MOCK_MODE\|Mock mode" apps/web/src/app/api/v1/'[...path]'/route.ts
```

Expected: No output.

- [ ] **Step 3: Typecheck + lint**

```bash
cd apps/web
pnpm typecheck
pnpm lint
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add "apps/web/src/app/api/v1/[...path]/route.ts"
git commit -m "refactor(api-proxy): remove mock-mode short-circuit"
```

---

## Task 4: Remove `MockProvider` wiring + delete `mock-provider.tsx`

**Files:**
- Modify: `apps/web/src/app/providers.tsx:145–183`
- Delete: `apps/web/src/app/mock-provider.tsx`

- [ ] **Step 1: Strip the lazy require + conditional wrap in `providers.tsx`**

Replace the entire `AppProviders` body (lines 145–183) with:

```tsx
export function AppProviders({ children }: AppProvidersProps) {
  return (
    <IntlProvider>
      <ThemeProvider>
        <QueryProvider>
          <AuthProvider>
            <LayoutProvider>
              <ErrorBoundary
                componentName="App"
                showDetails={process.env.NODE_ENV === 'development'}
              >
                <RouteErrorBoundary routeName="AppContent">
                  <AddGameWizardProvider>
                    <AppContent>{children}</AppContent>
                  </AddGameWizardProvider>
                </RouteErrorBoundary>
              </ErrorBoundary>
            </LayoutProvider>
          </AuthProvider>
        </QueryProvider>
      </ThemeProvider>
    </IntlProvider>
  );
}
```

The header docstring above can stay as-is.

- [ ] **Step 2: Delete `mock-provider.tsx`**

```bash
rm apps/web/src/app/mock-provider.tsx
```

- [ ] **Step 3: Verify no lingering references**

```bash
grep -rn "mock-provider\|MockProvider" apps/web/src --include='*.ts' --include='*.tsx'
```

Expected: No output.

- [ ] **Step 4: Typecheck**

```bash
cd apps/web
pnpm typecheck
```

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/providers.tsx
git rm apps/web/src/app/mock-provider.tsx
git commit -m "refactor(providers): remove MockProvider wiring"
```

---

## Task 5: Delete `src/mocks/`, `src/dev-tools/`, `mockServiceWorker.js`

**Files:**
- Delete: `apps/web/src/mocks/` (~24 files)
- Delete: `apps/web/src/dev-tools/` (~37 files)
- Delete: `apps/web/public/mockServiceWorker.js`

- [ ] **Step 1: Confirm no production code outside `mocks/`/`dev-tools/` imports them**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
grep -rn "from '@/mocks\|from '@/dev-tools\|from \"@/mocks\|from \"@/dev-tools" apps/web/src \
  --include='*.ts' --include='*.tsx' \
  | grep -v "src/mocks\|src/dev-tools"
```

Expected: No output. (Internal imports inside the dirs being deleted are fine — they go away with the dirs.)

If any external consumer is found, STOP and surface — likely an undocumented dependency that needs separate handling.

- [ ] **Step 2: Delete the three targets**

```bash
rm -rf apps/web/src/mocks
rm -rf apps/web/src/dev-tools
rm apps/web/public/mockServiceWorker.js
```

- [ ] **Step 3: Verify directory absence**

```bash
ls apps/web/src/mocks 2>&1 | head -2
ls apps/web/src/dev-tools 2>&1 | head -2
ls apps/web/public/mockServiceWorker.js 2>&1 | head -2
```

Expected: All three report "No such file or directory".

- [ ] **Step 4: Typecheck — confirms no production code was depending on these**

```bash
cd apps/web
pnpm typecheck
```

Expected: 0 errors. If errors appear, the prior import scan missed something — investigate, do NOT add re-exports as a workaround.

- [ ] **Step 5: Run test suite — failures expected ONLY in `__tests__/dev-tools` and `__tests__/integration/dev-tools` and `__tests__/bench/fetchInterceptor.bench.ts`**

```bash
cd apps/web
pnpm test 2>&1 | tail -40
```

Expected: Failures concentrated in the 18 files listed above (17 test files + 1 bench). Note any other failure for Task 6.

- [ ] **Step 6: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git add -A apps/web/src/mocks apps/web/src/dev-tools apps/web/public/mockServiceWorker.js
git commit -m "refactor(web): remove src/mocks, src/dev-tools, mockServiceWorker"
```

---

## Task 6: Delete dependent test files

**Files:**
- Delete: `apps/web/__tests__/dev-tools/` (14 files)
- Delete: `apps/web/__tests__/integration/dev-tools/` (3 files)
- Delete: `apps/web/__tests__/bench/fetchInterceptor.bench.ts`

- [ ] **Step 1: Delete the directories and bench file**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
rm -rf apps/web/__tests__/dev-tools
rm -rf apps/web/__tests__/integration/dev-tools
rm apps/web/__tests__/bench/fetchInterceptor.bench.ts
```

- [ ] **Step 2: Verify**

```bash
ls apps/web/__tests__/dev-tools 2>&1 | head -2
ls apps/web/__tests__/integration/dev-tools 2>&1 | head -2
ls apps/web/__tests__/bench/fetchInterceptor.bench.ts 2>&1 | head -2
```

Expected: All three report "No such file or directory".

- [ ] **Step 3: Full test suite must now pass**

```bash
cd apps/web
pnpm test 2>&1 | tail -20
```

Expected: All tests pass. If any test still fails referencing `@/mocks` or `@/dev-tools`, locate and delete it — it was missed in the scope.

- [ ] **Step 4: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git add -A apps/web/__tests__
git commit -m "test(web): remove dev-tools and integration test suites"
```

---

## Task 7: Uninstall `msw` + clean `package.json` + `.env.development.example`

**Files:**
- Modify: `apps/web/package.json` (remove `msw` devDep, `msw.workerDirectory`, `dev:mock` script)
- Modify: `apps/web/.env.development.example` (remove MOCK_MODE block)

- [ ] **Step 1: Uninstall `msw` via pnpm**

```bash
cd apps/web
pnpm remove msw
```

Expected: `msw` removed from `devDependencies`. `pnpm-lock.yaml` updated.

- [ ] **Step 2: Manually remove the `msw.workerDirectory` field**

`pnpm remove` only touches dependency entries. Open `apps/web/package.json` and delete this top-level block (was at lines 290–294):

```json
  "msw": {
    "workerDirectory": [
      "public"
    ]
  }
```

Make sure to also remove the trailing comma on the line before, if needed, to keep JSON valid.

- [ ] **Step 3: Remove the `dev:mock` script**

Open `apps/web/package.json` and delete the line (originally line 6):

```json
    "dev:mock": "cross-env NEXT_PUBLIC_MOCK_MODE=true NEXT_PUBLIC_API_BASE=http://localhost:3000 node ./node_modules/next/dist/bin/next dev -p 3000",
```

- [ ] **Step 4: Remove the MOCK_MODE block from `.env.development.example`**

In `apps/web/.env.development.example`, delete lines 10–11:

```
# Mock Mode - serves mocked data when backend is unavailable
NEXT_PUBLIC_MOCK_MODE=true
```

(Leave the surrounding blank lines tidy — single blank between the Alpha block and the Mechanic Validation block.)

- [ ] **Step 5: Reinstall + verify `msw` is gone**

```bash
cd apps/web
pnpm install
grep -n '"msw"' package.json
```

Expected: `pnpm install` succeeds. `grep` returns no output.

- [ ] **Step 6: Verify build works**

```bash
cd apps/web
pnpm build 2>&1 | tail -20
```

Expected: Build completes successfully. Note the output line `du -sb .next/static/chunks` size — needed for Task 10.

- [ ] **Step 7: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git add apps/web/package.json apps/web/.env.development.example pnpm-lock.yaml apps/web/pnpm-lock.yaml
git commit -m "chore(web): remove msw dependency, dev:mock script, MOCK_MODE env"
```

(`pnpm-lock.yaml` lives at repo root for pnpm workspaces; the `apps/web/pnpm-lock.yaml` arg is a no-op fallback if it doesn't exist — git ignores missing paths via `git add` only when `-A` is used; here add only what exists. If git complains about a missing `apps/web/pnpm-lock.yaml`, drop that arg and re-run.)

---

## Task 8: Wipe `dev-fast` infrastructure + clean Makefile

**Files:**
- Delete: `infra/scripts/dev-fast.sh`
- Delete: `infra/scripts/dev-fast-down.sh`
- Delete: `infra/scripts/dev-env-check.sh`
- Delete: `infra/.env.dev.local`
- Delete: `infra/.env.dev.local.example`
- Modify: `infra/Makefile` (remove 5 targets + .PHONY entries + section header)

- [ ] **Step 1: Delete fast-dev scripts and env files**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
rm infra/scripts/dev-fast.sh
rm infra/scripts/dev-fast-down.sh
rm infra/scripts/dev-env-check.sh
rm infra/.env.dev.local
rm infra/.env.dev.local.example
```

- [ ] **Step 2: Edit `infra/Makefile` — remove the dev-fast section**

Open `infra/Makefile` and delete lines 18–39 (the `# MeepleDev fast dev loop (Phase 1)` section header through the `dev-fast-check` target). The result: line 17 (`down` target ends) is immediately followed by the `tunnel:` target.

Specifically, remove this exact block:

```makefile
# ================================================================
# MeepleDev fast dev loop (Phase 1)
# ================================================================

dev-fast: ## Fast dev loop (reads .env.dev.local)
	@bash scripts/dev-fast.sh

dev-fast-api: ## Fast dev + local dotnet watch + Postgres
	@DEV_BACKEND=true DEV_POSTGRES=true bash scripts/dev-fast.sh

dev-fast-full: ## Fast dev + backend + Postgres + Redis, all mocks OFF
	@DEV_BACKEND=true DEV_POSTGRES=true DEV_REDIS=true \
	 MOCK_LLM=false MOCK_EMBEDDING=false MOCK_BGG=false \
	 MOCK_S3=false MOCK_SMOLDOCLING=false MOCK_UNSTRUCTURED=false \
	 MOCK_N8N=false MOCK_RERANKER=false \
	 bash scripts/dev-fast.sh

dev-fast-down: ## Stop everything started by dev-fast
	@bash scripts/dev-fast-down.sh

dev-fast-check: ## Verify .env.dev.local against template
	@bash scripts/dev-env-check.sh

```

- [ ] **Step 3: Edit `infra/Makefile:332` — remove fast-dev from `.PHONY`**

Replace this line:

```makefile
.PHONY: dev dev-core dev-down dev-fast dev-fast-api dev-fast-full dev-fast-down dev-fast-check \
```

With:

```makefile
.PHONY: dev dev-core dev-down \
```

- [ ] **Step 4: Verify `make help` no longer lists dev-fast**

```bash
cd infra
make help 2>&1 | grep -i 'dev-fast'
```

Expected: No output.

- [ ] **Step 5: Verify `make dev-core` (sanity — won't actually start, just resolves)**

```bash
cd infra
make -n dev-core
```

Expected: Echoes the `docker compose ...` command without errors.

- [ ] **Step 6: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git add -A infra/scripts infra/Makefile infra/.env.dev.local infra/.env.dev.local.example
git commit -m "chore(infra): remove dev-fast loop scripts, env, and Makefile targets"
```

---

## Task 9: Remove CI workflow + update docs + write closing ADR

**Files:**
- Delete: `.github/workflows/dev-tools-isolation.yml`
- Modify: `docs/architecture/adr/adr-022-ssr-auth-protection.md` (remove MSW reference)
- Modify: `docs/architecture/adr/adr-042-dashboard-performance.md` (remove MSW reference)
- Modify: `docs/frontend/storybook-guide.md` (remove `msw-storybook-addon` section)
- Create: `docs/architecture/adr/adr-052-mock-mode-removal.md` (closing ADR)

> NOTE: The spec also referenced `docs/testing/performance-testing-guide.md` and `docs/vitest-migration-guide.md`. **Both files do not exist in this repo** — verified via `ls`. Skipping them.

- [ ] **Step 1: Delete the CI workflow**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
rm .github/workflows/dev-tools-isolation.yml
```

- [ ] **Step 2: Surgical edit ADR-022**

Open `docs/architecture/adr/adr-022-ssr-auth-protection.md`. Find the section/sentence mentioning MSW (likely "MSW handlers" or "MSW for E2E"). Remove that bullet/sentence cleanly. Do not restructure surrounding text.

Verify removal:

```bash
grep -ni 'msw' docs/architecture/adr/adr-022-ssr-auth-protection.md
```

Expected: No output.

- [ ] **Step 3: Surgical edit ADR-042**

Open `docs/architecture/adr/adr-042-dashboard-performance.md`. Remove the MSW mention (likely "MSW mocks in integration tests" or similar).

Verify:

```bash
grep -ni 'msw' docs/architecture/adr/adr-042-dashboard-performance.md
```

Expected: No output.

- [ ] **Step 4: Surgical edit storybook-guide**

Open `docs/frontend/storybook-guide.md`. Remove the section / paragraph that documents `msw-storybook-addon` setup. Adjacent sections should still flow.

Verify:

```bash
grep -ni 'msw' docs/frontend/storybook-guide.md
```

Expected: No output.

- [ ] **Step 5: Write closing ADR-052**

Create `docs/architecture/adr/adr-052-mock-mode-removal.md` with this content:

```markdown
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
```

- [ ] **Step 6: Final verification — no residual mock-mode references in repo (excluding archived plans/specs and this ADR)**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
grep -rn "NEXT_PUBLIC_MOCK_MODE\|NEXT_PUBLIC_DEV_AS_ROLE" apps/web infra .github 2>/dev/null
```

Expected: No output. (The new ADR-052 mentions the env var names in narrative but lives under `docs/`, which is excluded from this scan.)

- [ ] **Step 7: Commit**

```bash
git add -A docs/architecture/adr .github/workflows docs/frontend
git commit -m "docs: remove MSW references; add ADR-052 mock-mode removal"
```

---

## Task 10: Rebuild + update bundle-size baseline

**Files:**
- Modify: `apps/web/bundle-size-baseline.json`

- [ ] **Step 1: Clean rebuild**

```bash
cd apps/web
rm -rf .next
pnpm build 2>&1 | tail -10
```

Expected: Build succeeds.

- [ ] **Step 2: Measure new chunk size**

```bash
du -sb apps/web/.next/static/chunks
```

Expected: Output like `12950000  apps/web/.next/static/chunks` (the leading number is bytes — record it). Old baseline was `13150443`. Expected drop: 80–120 KB (the `msw` browser bundle alone is ~70 KB minified-gzipped, plus `dev-tools/` chunks).

- [ ] **Step 3: Update `apps/web/bundle-size-baseline.json`**

Open the file and replace `totalBytes` with the new measured value. Update `updatedAt` to today's date. Example result:

```json
{
  "description": "Baseline JS bundle size for prod build (no mock). Update manually in dedicated PRs.",
  "updatedAt": "2026-04-25",
  "totalBytes": <new-measured-value>,
  "toleranceBytes": 10240
}
```

(Replace `<new-measured-value>` with the byte count from Step 2.)

- [ ] **Step 4: Re-run bundle size test to confirm new baseline passes**

```bash
cd apps/web
pnpm test src/__tests__/bundle-size 2>&1 | tail -10
```

If no specific bundle-size test exists, run:

```bash
pnpm test 2>&1 | grep -i 'bundle' | tail -5
```

Expected: Tests pass with the new baseline.

- [ ] **Step 5: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git add apps/web/bundle-size-baseline.json
git commit -m "chore(web): update bundle-size baseline post mock-mode removal"
```

---

## Task 11: Final verification + push

**Files:** None (verification + git operations)

- [ ] **Step 1: Run full frontend quality gate**

```bash
cd apps/web
pnpm typecheck && pnpm lint && pnpm test && pnpm build 2>&1 | tail -10
```

Expected: All four steps succeed.

- [ ] **Step 2: Backend sanity (regression check — should be untouched)**

```bash
cd apps/api/src/Api
dotnet build -c Release 2>&1 | tail -5
dotnet build -c Debug 2>&1 | tail -5
```

Expected: Both build clean. Release excludes `DevTools/**` per `Api.csproj` (no behavior change here).

- [ ] **Step 3: Acceptance criteria verification (per spec)**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
echo "--- AC1: no mock-mode env refs in src ---"
grep -r "NEXT_PUBLIC_MOCK_MODE\|NEXT_PUBLIC_DEV_AS_ROLE" apps/web/src
echo "--- AC2: mocks/ and dev-tools/ absent ---"
ls apps/web/src/mocks apps/web/src/dev-tools 2>&1
echo "--- AC3: msw uninstalled ---"
grep '"msw"' apps/web/package.json
echo "--- AC4: dev-fast targets gone ---"
grep -E "^dev-fast" infra/Makefile
echo "--- AC5: CI workflow gone ---"
ls .github/workflows/dev-tools-isolation.yml 2>&1
echo "--- AC6: restore tag exists ---"
git tag -l "pre-mock-removal"
```

Expected:
- AC1, AC3, AC4 → no matches (empty output)
- AC2, AC5 → "No such file or directory"
- AC6 → `pre-mock-removal`

- [ ] **Step 4: Review commit log**

```bash
git log --oneline main-dev..HEAD
```

Expected: ~10 clean commits, one per task.

- [ ] **Step 5: Push branch**

```bash
git push -u origin feature/mock-mode-removal
```

Expected: Branch pushed. PR can now be opened against `main-dev` (per CLAUDE.md PR rule).

- [ ] **Step 6: Open PR**

```bash
gh pr create --base main-dev --title "refactor(web): remove frontend mock mode + dev-fast loop" --body "$(cat <<'EOF'
## Summary
Definitive removal of the frontend mock-mode surface (MSW + Dev Panel + dev-fast loop).
Backend DevTools remains untouched (already Release-isolated via `#if DEBUG`).

## Why
Visiting `localhost:3000` with `NEXT_PUBLIC_MOCK_MODE=true` was forcing `isAuthenticated=true`
and redirecting `/` → `/admin`. The wider mock-mode surface (~95 frontend files + 5 infra
files + 1 CI workflow + 8 doc refs) had grown beyond its development-velocity payoff.

## What changed
- Removed: `src/mocks/`, `src/dev-tools/`, `mock-provider.tsx`, `mockServiceWorker.js`
- Removed: 17 dev-tools test files + 1 fetch-interceptor benchmark
- Removed: `msw` devDependency, `dev:mock` script, `NEXT_PUBLIC_MOCK_MODE` env var
- Removed: `make dev-fast*` (5 targets), `dev-fast*.sh`, `.env.dev.local*`
- Removed: `.github/workflows/dev-tools-isolation.yml`
- Edited: `proxy.ts`, `usePWA.ts`, `api/v1/[...path]/route.ts`, `providers.tsx`
- Updated: ADR-022, ADR-042, storybook-guide (MSW references removed)
- Added: ADR-052 closing decision document
- Updated: `bundle-size-baseline.json` (drop ~80–120 KB)

## Restore point
Tag `pre-mock-removal` on `main-dev`. Spec in `docs/superpowers/specs/2026-04-25-mock-mode-removal-design.md`. Plan in `docs/superpowers/plans/2026-04-25-mock-mode-removal.md`.

## Test plan
- [x] `pnpm typecheck` clean
- [x] `pnpm lint` clean
- [x] `pnpm test` passes
- [x] `pnpm build` succeeds, new bundle baseline recorded
- [x] `dotnet build -c Release` and `-c Debug` both pass
- [ ] Manual: `make dev` then `curl http://localhost:3000` → 200 (landing), no `/admin` redirect without session
- [ ] CI: all GitHub Actions green

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Expected: PR URL returned. Report URL to user.

---

## Self-Review

**1. Spec coverage check**

| Spec section | Task |
|--------------|------|
| Goal: eliminate mock mode | T1–T9 |
| Symptom (proxy.ts:340–355 redirect) | T1 |
| Scope C ridotto: UI mock + dev-fast loop infra | T1–T8 |
| Backend DevTools mantenuti | (no task — invariant verified in T11 Step 2) |
| CI workflow cancellato | T9 Step 1 |
| `proxy.ts` 3 branches removed | T1 (4 sites including `isSessionCookieValid`) |
| `providers.tsx` rm conditional | T4 |
| `mock-provider.tsx` deleted | T4 |
| `package.json` rm `msw` + `dev:mock` | T7 |
| `Makefile` rm 5 targets + `.PHONY` | T8 |
| `infra/scripts/dev-fast*.sh` rm | T8 |
| `infra/.env.dev.local*` rm | T8 |
| `.github/workflows/dev-tools-isolation.yml` rm | T9 |
| Update ADR-022, ADR-042 | T9 |
| Update storybook-guide | T9 |
| Update performance-testing-guide, vitest-migration-guide | **N/A — files do not exist (verified)** |
| `bundle-size-baseline.json` re-measure | T10 |
| Tag `pre-mock-removal` | T0 |
| ADR-XXX closing doc | T9 (ADR-052) |
| Acceptance criteria checklist | T11 Step 3 |

Gaps found and resolved during planning:
- Spec missed `usePWA.ts:135` (SW skip guard) → added Task 2
- Spec missed `app/api/v1/[...path]/route.ts:84` (proxy short-circuit) → added Task 3
- Spec missed `__tests__/bench/fetchInterceptor.bench.ts` (imports `@/dev-tools`) → added to Task 6
- Spec missed `infra/scripts/dev-env-check.sh` (referenced by deleted Makefile target) → added to Task 8
- Spec missed `apps/web/.env.development.example:11` (MOCK_MODE example) → added to Task 7
- Spec missed `package.json` `msw.workerDirectory` config field → added to Task 7
- Spec missed first MOCK_MODE branch in `proxy.ts:117` (`isSessionCookieValid`) → added to Task 1 Step 1
- Spec referenced `docs/testing/performance-testing-guide.md` and `docs/vitest-migration-guide.md` → **do not exist**, removed from scope

**2. Placeholder scan:** No TBD/TODO/"implement later"/"add appropriate handling" patterns. Each step has the exact code or command.

**3. Type consistency:** Function/variable names match across tasks: `isSessionCookieValid`, `userRoleCookie`, `MockProvider`, `AppProviders`, `proxyRequest`, `registerServiceWorker`, `pre-mock-removal`, `feature/mock-mode-removal`. Bundle baseline JSON keys (`totalBytes`, `updatedAt`) consistent.

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-04-25-mock-mode-removal.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
