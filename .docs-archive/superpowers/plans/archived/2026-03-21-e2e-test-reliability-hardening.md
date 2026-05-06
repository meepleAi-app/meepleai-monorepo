# E2E Test Reliability Hardening — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Harden E2E test reliability by eliminating false positives (500=pass), silent error swallowing, brittle timing, and adding CI-essential infrastructure (tagging, screenshots, health checks).

**Architecture:** 4 phases ordered by priority. P0 fixes false positives and silent failures. P1 adds CI infrastructure (tagging, screenshots, flow health gates). P2 replaces brittle timing with proper Playwright waits. P3 adds contract testing and full-stack CI stage.

**Tech Stack:** Playwright (TypeScript), xUnit + FluentAssertions (.NET 9), Testcontainers

**Source:** Spec panel analysis from 2026-03-21 session.

---

## File Map

| Phase | Files | Action |
|-------|-------|--------|
| P0-a | `apps/api/tests/Api.Tests/E2E/KnowledgeBase/ChatE2ETests.cs` | Remove `InternalServerError` from accepted status codes |
| P0-a | `apps/api/tests/Api.Tests/E2E/KnowledgeBase/ArbitroAgentE2ETests.cs` | Same |
| P0-a | `apps/api/tests/Api.Tests/E2E/SharedGameCatalog/AdminGameCreationJourneyE2ETests.cs` | Same |
| P0-a | `apps/api/tests/Api.Tests/E2E/SharedGameCatalog/ShareRequestE2ETests.cs` | Same |
| P0-a | `apps/api/tests/Api.Tests/E2E/UserLibrary/UserLibraryE2ETests.cs` | Same |
| P0-a | `apps/api/tests/Api.Tests/E2E/UserNotifications/NotificationsE2ETests.cs` | Same |
| P0-a | `apps/api/tests/Api.Tests/E2E/DocumentProcessing/DocumentProcessingE2ETests.cs` | Same |
| P0-a | `apps/api/tests/Api.Tests/E2E/Infrastructure/E2ETestBase.cs` | Add `AssertSuccessOrSkip()` helper |
| P0-b | `apps/web/e2e/flows/admin-user-onboarding.spec.ts` | Replace `.catch(() => {})` with logging catches |
| P0-b | `apps/web/e2e/flows/admin-embedding-flow.spec.ts` | Same |
| P0-b | `apps/web/e2e/helpers/dismiss-cookie.ts` | Create shared cookie dismiss helper |
| P1-a | `apps/web/e2e/helpers/test-tags.ts` | Create tag constants + annotation utility |
| P1-b | `apps/web/playwright.config.ts` | Add `screenshot: 'only-on-failure'` + `trace: 'retain-on-failure'` |
| P1-c | `apps/web/e2e/helpers/flow-health-gate.ts` | Create pre-flow health check for external services |
| P1-c | `apps/web/e2e/flows/admin-user-onboarding.spec.ts` | Add health gate `test.beforeAll` |
| P1-c | `apps/web/e2e/flows/admin-embedding-flow.spec.ts` | Add health gate `test.beforeAll` |
| P2-a | `apps/web/e2e/flows/admin-user-onboarding.spec.ts` | Replace `waitForTimeout` with proper waits |
| P2-a | `apps/web/e2e/flows/admin-embedding-flow.spec.ts` | Same |
| P2-b | `apps/web/e2e/helpers/dismiss-cookie.ts` | Add `waitForResponse` pattern for cookie dismiss |

---

## Phase P0: Eliminate False Positives & Silent Failures

### Task 1: Add `AssertSuccessOrSkip` helper to E2E base class

**Files:**
- Modify: `apps/api/tests/Api.Tests/E2E/Infrastructure/E2ETestBase.cs`

The pattern `BeOneOf(OK, InternalServerError)` occurs **44 times** across 7 E2E files. Instead of editing each assertion individually, add a base class helper that tests can use to assert "success or skip if service unavailable".

- [ ] **Step 1: Add helper method to `E2ETestBase`**

Add after `ClearAuthentication()` method (~line 191):

```csharp
/// <summary>
/// Asserts the response is successful (2xx). If the endpoint returned 500
/// due to unconfigured external services (embedding, LLM, Qdrant), the test
/// is skipped with a descriptive message instead of silently passing.
/// Uses xUnit v3's Assert.Skip() for dynamic test skipping.
/// </summary>
protected static async Task AssertSuccessOrSkipIfServiceUnavailable(
    HttpResponseMessage response,
    string context = "")
{
    if (response.IsSuccessStatusCode)
        return;

    if (response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Skip(
            $"Skipped: {context} returned 500 (external service likely unavailable). " +
            $"Body: {body[..Math.Min(body.Length, 200)]}");
    }

    // For non-500 failures, fail the test properly
    response.EnsureSuccessStatusCode();
}
```

> **Note:** Uses `Assert.Skip()` from xUnit v3 (confirmed: project uses `xunit.v3` version 3.2.1).
> The method is `async Task` to avoid `.GetAwaiter().GetResult()` blocking calls.
> Call sites **must `await`** this helper.

- [ ] **Step 2: Verify it compiles**

Run: `cd apps/api/tests/Api.Tests && dotnet build --no-restore 2>&1 | tail -5`

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/E2E/Infrastructure/E2ETestBase.cs
git commit -m "feat(e2e): add AssertSuccessOrSkipIfServiceUnavailable helper to E2ETestBase"
```

---

### Task 2: Fix `ChatE2ETests` — remove 500 from accepted status codes

**Files:**
- Modify: `apps/api/tests/Api.Tests/E2E/KnowledgeBase/ChatE2ETests.cs`

This file has **11 instances** of `HttpStatusCode.InternalServerError` in `BeOneOf()` assertions.

- [ ] **Step 1: Replace all `BeOneOf` patterns that include `InternalServerError`**

For each test method, replace the pattern:

```csharp
// BEFORE:
response.StatusCode.Should().BeOneOf(
    HttpStatusCode.OK,
    HttpStatusCode.Created,
    HttpStatusCode.InternalServerError,
    HttpStatusCode.BadRequest);

// AFTER (option A — if the test SHOULD succeed):
await AssertSuccessOrSkipIfServiceUnavailable(response, "CreateChatThread");

// AFTER (option B — if BadRequest is also valid, e.g. validation):
if (response.StatusCode == HttpStatusCode.InternalServerError)
    Assert.Skip("External service unavailable");
response.StatusCode.Should().BeOneOf(
    HttpStatusCode.OK,
    HttpStatusCode.Created,
    HttpStatusCode.BadRequest);
```

Decision rule:
- If the test is a **happy path** (valid credentials, valid data) → use `await AssertSuccessOrSkipIfServiceUnavailable`
- If the test explicitly tests **validation/auth** → keep `BadRequest`/`Unauthorized` but remove `InternalServerError`

> **Important:** The helper is `async Task` — all call sites **must use `await`**.

- [ ] **Step 2: Run the E2E tests to verify compilation**

Run: `cd apps/api/tests/Api.Tests && dotnet build --no-restore 2>&1 | tail -5`

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/E2E/KnowledgeBase/ChatE2ETests.cs
git commit -m "fix(e2e): remove InternalServerError as accepted status in ChatE2ETests

500 errors now skip instead of silently passing. Prevents false positives
when external services (LLM, embedding) are unavailable in test environment."
```

---

### Task 3: Fix remaining backend E2E files — remove 500 from accepted codes

**Files:**
- Modify: `apps/api/tests/Api.Tests/E2E/KnowledgeBase/ArbitroAgentE2ETests.cs` (7 instances)
- Modify: `apps/api/tests/Api.Tests/E2E/SharedGameCatalog/AdminGameCreationJourneyE2ETests.cs` (25 instances)
- Modify: `apps/api/tests/Api.Tests/E2E/SharedGameCatalog/ShareRequestE2ETests.cs` (2 instances)
- Modify: `apps/api/tests/Api.Tests/E2E/UserLibrary/UserLibraryE2ETests.cs` (1 instance)
- Modify: `apps/api/tests/Api.Tests/E2E/UserNotifications/NotificationsE2ETests.cs` (2 instances)
- Modify: `apps/api/tests/Api.Tests/E2E/DocumentProcessing/DocumentProcessingE2ETests.cs` (1 instance)

Apply the same pattern from Task 2 to each file. This is a bulk operation — can be parallelized across files.

- [ ] **Step 1: Apply `AssertSuccessOrSkipIfServiceUnavailable` or remove `InternalServerError` in each file**

Same decision rule as Task 2:
- Happy path tests → `await AssertSuccessOrSkipIfServiceUnavailable(response, "contextName")`
- Validation/auth tests → Remove `InternalServerError` from `BeOneOf`, keep other valid codes

> **Verify actual instance counts per file before starting** — grep confirmed:
> ArbitroAgent (7), AdminGameCreation (22), ShareRequest (2), UserLibrary (1), UserNotifications (2), DocumentProcessing (1).

- [ ] **Step 2: Build verification**

Run: `cd apps/api/tests/Api.Tests && dotnet build --no-restore 2>&1 | tail -5`

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/E2E/
git commit -m "fix(e2e): remove InternalServerError as accepted status across all backend E2E tests

Affected: ArbitroAgent, AdminGameCreation, ShareRequest, UserLibrary,
UserNotifications, DocumentProcessing. 500 now skips, not passes."
```

---

### Task 4: Create shared cookie dismiss helper for flow tests

**Files:**
- Create: `apps/web/e2e/helpers/dismiss-cookie.ts`

The `.catch(() => {})` pattern for cookie consent appears 5+ times across flow files. Extract into a reusable helper that logs instead of silencing.

- [ ] **Step 1: Create `dismiss-cookie.ts`**

```typescript
import { Page } from '@playwright/test';

/**
 * Dismiss cookie consent banner if present.
 * Logs outcome instead of silently swallowing errors.
 *
 * @param page - Playwright Page instance
 * @param label - Test step label for debugging (e.g., '[T3]')
 */
export async function dismissCookieConsent(
  page: Page,
  label = '[cookie]'
): Promise<void> {
  const cookieBtn = page
    .getByRole('button', { name: /essential only|accept all/i })
    .first();

  const visible = await cookieBtn.isVisible({ timeout: 2_000 }).catch(() => false);

  if (visible) {
    await cookieBtn.click();
    console.log(`${label} Cookie consent dismissed`);
    // Wait for banner to disappear — use waitForSelector, not waitForTimeout
    await page
      .locator('[data-testid="cookie-banner"], .cookie-consent')
      .waitFor({ state: 'hidden', timeout: 3_000 })
      .catch(() => console.log(`${label} Cookie banner hide timeout (non-blocking)`));
  } else {
    console.log(`${label} No cookie consent banner found`);
  }
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/e2e/helpers/dismiss-cookie.ts
git commit -m "feat(e2e): add shared dismissCookieConsent helper with logging"
```

---

### Task 5: Replace silent `.catch(() => {})` in onboarding flow

**Files:**
- Modify: `apps/web/e2e/flows/admin-user-onboarding.spec.ts`

Lines with silent catches: 149, 202, 354, 419, 458. All are cookie consent dismissals.

> **DO NOT CHANGE** the following `.catch()` patterns — they are intentional soft checks:
> - Line 83: `.json().catch(() => ({ enabled: false }))` — fallback default for flag parsing
> - Line 155: `.catch(() => null)` — API response interception (replaced with logging below)
> - Line 278: `.isVisible().catch(() => false)` — intentional visibility soft check
> - Line 295: `.isVisible().catch(() => false)` — intentional visibility soft check

- [ ] **Step 1: Add import at top of file**

```typescript
import { dismissCookieConsent } from '../helpers/dismiss-cookie';
```

- [ ] **Step 2: Replace each silent cookie catch with the helper**

Replace each instance of this pattern:
```typescript
await page
  .getByRole('button', { name: /essential only|accept all/i })
  .first()
  .click({ timeout: 5_000 })
  .catch(() => {});
await page.waitForTimeout(500);
```

With:
```typescript
await dismissCookieConsent(page, '[T<N>]');
```

Where `<N>` is the test number (3, 4, 5, 6, 8).

Also replace the `acceptPromise` catch on line 155 (`.catch(() => null)`) with:
```typescript
.catch(e => {
  console.log('[T3] accept-invitation response not intercepted:', e.message);
  return null;
});
```

- [ ] **Step 3: Run lint to verify**

Run: `cd apps/web && npx eslint e2e/flows/admin-user-onboarding.spec.ts --no-error-on-unmatched-pattern`

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/flows/admin-user-onboarding.spec.ts apps/web/e2e/helpers/dismiss-cookie.ts
git commit -m "fix(e2e): replace silent catches with logging in onboarding flow

Uses shared dismissCookieConsent helper. All previously-silent catches
now log context for CI debugging."
```

---

### Task 6: Replace silent catches in embedding flow

**Files:**
- Modify: `apps/web/e2e/flows/admin-embedding-flow.spec.ts`

The embedding flow has fewer silent catches (the `.catch(() => false)` for `isVisible` calls are fine — those are intentional soft checks). Focus on the genuinely silent ones.

- [ ] **Step 1: Add import and replace cookie dismiss in Test 1 (line ~113)**

The `getResp.json().catch(() => ({ enabled: false }))` on line 113 is acceptable — it's a fallback default.

No cookie dismiss in this flow — the admin is already logged in from a fresh context.

- [ ] **Step 2: Add context to the console.warn catches (lines 209, 219, 242, 277)**

These are already logging via `console.warn` — they're acceptable as-is. Review each to ensure the warn message is descriptive enough.

- [ ] **Step 3: Add `state.failureReason` propagation to BOTH flow files**

**3a. Embedding flow** — extend `EmbeddingFlowState` at the top of `admin-embedding-flow.spec.ts`:
```typescript
interface EmbeddingFlowState {
  adminContext: BrowserContext;
  adminPage: Page;
  gameId: string;
  gameName: string;
  jobId: string;
  failureReason?: string;  // NEW: propagate root cause to skip messages
}
```

**3b. Onboarding flow** — extend `OnboardingFlowState` in `apps/web/e2e/fixtures/onboarding-flow.fixture.ts`:
```typescript
export interface OnboardingFlowState {
  // ... existing fields ...
  failureReason?: string;  // NEW: propagate root cause to skip messages
}
```

**3c. In BOTH flows**, update each test's skip check:
```typescript
// BEFORE:
if (!state.adminPage) test.skip(true, 'Requires test 1 to pass');

// AFTER:
if (!state.adminPage)
  test.skip(true, state.failureReason ?? 'Requires test 1 to pass');
```

In Test 1's catch blocks, set the reason:
```typescript
// In any critical failure catch:
state.failureReason = `Test 1 failed: ${e.message}`;
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/flows/admin-embedding-flow.spec.ts
git commit -m "fix(e2e): add failure reason propagation in embedding flow

Downstream test skips now show the root cause, not just 'Requires test 1'."
```

---

## Phase P1: CI Infrastructure

### Task 7: Add global screenshot and trace on failure

**Files:**
- Modify: `apps/web/playwright.config.ts` (line ~105-108, `use` block)

- [ ] **Step 1: Add screenshot and trace settings**

In the `use` block (~line 105), add:
```typescript
use: {
    baseURL: 'http://localhost:3000',
    trace: 'retain-on-failure',         // CHANGED from 'on-first-retry'
    screenshot: 'only-on-failure',      // NEW: always capture on failure
    video: 'retain-on-failure',         // NEW: video on failure for flows
    actionTimeout: 10000,
    navigationTimeout: 60000,
    // ... rest unchanged
},
```

- [ ] **Step 2: Verify config is valid**

Run: `cd apps/web && npx playwright test --list 2>&1 | head -5`

Expected: Lists test files without errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/playwright.config.ts
git commit -m "feat(e2e): enable screenshot + trace + video on failure globally

Improves CI debuggability. Traces retained only on failure to save space."
```

> **CI Storage note:** With 6 browser projects, video files can accumulate. Configure
> CI artifact retention (e.g., `retention-days: 7` in GitHub Actions) to prevent bloat.
> If storage is a concern, limit `video` to desktop-chrome only.

---

### Task 8: Create test tag constants and annotation utility

**Files:**
- Create: `apps/web/e2e/helpers/test-tags.ts`

- [ ] **Step 1: Create tag utility**

```typescript
/**
 * E2E Test Tags for CI filtering.
 *
 * Usage in tests:
 *   test('Login flow @smoke @critical', async () => { ... });
 *
 * CI filtering:
 *   npx playwright test --grep @smoke          # Only smoke tests
 *   npx playwright test --grep @critical       # Only critical path
 *   npx playwright test --grep-invert @slow    # Skip slow tests
 *
 * Tag conventions:
 *   @smoke     - Fast, core functionality (run on every PR)
 *   @critical  - Business-critical paths (run on every PR)
 *   @regression - Full regression suite (nightly)
 *   @slow      - Tests > 60s (exclude from quick CI)
 *   @flow      - Multi-step serial flows (run in dedicated stage)
 *   @admin     - Admin-specific features
 *   @auth      - Authentication flows
 *   @rag       - RAG/AI/Agent tests
 */
export const TAG = {
  SMOKE: '@smoke',
  CRITICAL: '@critical',
  REGRESSION: '@regression',
  SLOW: '@slow',
  FLOW: '@flow',
  ADMIN: '@admin',
  AUTH: '@auth',
  RAG: '@rag',
} as const;
```

- [ ] **Step 2: Tag the two flow tests**

In `admin-user-onboarding.spec.ts`, update the describe:
```typescript
test.describe('Admin-User Onboarding Flow @flow @critical @slow', () => {
```

In `admin-embedding-flow.spec.ts`:
```typescript
test.describe('Admin Embedding Flow @flow @rag @slow', () => {
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/helpers/test-tags.ts apps/web/e2e/flows/
git commit -m "feat(e2e): add test tag system for CI filtering

Tags: @smoke, @critical, @regression, @slow, @flow, @admin, @auth, @rag.
Flow tests tagged as @flow @slow for separate CI stage."
```

---

### Task 9: Create flow health gate for external services

**Files:**
- Create: `apps/web/e2e/helpers/flow-health-gate.ts`
- Modify: `apps/web/e2e/flows/admin-user-onboarding.spec.ts`
- Modify: `apps/web/e2e/flows/admin-embedding-flow.spec.ts`

- [ ] **Step 1: Create health gate helper**

```typescript
import { env } from './onboarding-environment';

interface HealthCheckResult {
  service: string;
  healthy: boolean;
  error?: string;
}

/**
 * Pre-flight health check for external services required by flow tests.
 * Call in test.beforeAll() to skip entire suite if critical services are down.
 *
 * @param services - Which services to check
 * @returns Array of health check results
 */
export async function checkFlowPrerequisites(
  services: ('api' | 'embedding' | 'frontend')[] = ['api', 'frontend']
): Promise<HealthCheckResult[]> {
  const results: HealthCheckResult[] = [];

  for (const service of services) {
    try {
      const url = getHealthUrl(service);
      const resp = await fetch(url, {
        signal: AbortSignal.timeout(10_000),
      });
      results.push({
        service,
        healthy: resp.ok,
        error: resp.ok ? undefined : `HTTP ${resp.status}`,
      });
    } catch (e) {
      results.push({
        service,
        healthy: false,
        error: e instanceof Error ? e.message : String(e),
      });
    }
  }

  return results;
}

/**
 * Resolve health URL per service.
 * Uses env-based URLs — never derive ports via string replacement.
 *
 * Embedding URL defaults:
 *   local  → http://localhost:8000/health
 *   staging → https://api.meepleai.app/health (embedding behind reverse proxy)
 */
function getHealthUrl(service: string): string {
  switch (service) {
    case 'api':
      return `${env.apiURL}/health`;
    case 'embedding':
      return process.env.E2E_EMBEDDING_URL
        ?? (env.name === 'staging'
          ? `${env.apiURL}/health`            // staging: behind reverse proxy
          : 'http://localhost:8000/health');   // local: direct port
    case 'frontend':
      return `${env.baseURL}`;
    default:
      return `http://localhost:3000`;
  }
}

/**
 * Format health results for skip message.
 */
export function formatHealthResults(results: HealthCheckResult[]): string {
  return results
    .filter(r => !r.healthy)
    .map(r => `${r.service}: ${r.error}`)
    .join(', ');
}
```

- [ ] **Step 2: Add health gate to onboarding flow**

In `admin-user-onboarding.spec.ts`, add a `test.beforeAll` and guard **Test 1** (the first test has no existing skip guard):

```typescript
import { checkFlowPrerequisites, formatHealthResults } from '../helpers/flow-health-gate';

test.describe('Admin-User Onboarding Flow @flow @critical @slow', () => {
  const state: Partial<OnboardingFlowState> = {};

  test.beforeAll(async () => {
    const health = await checkFlowPrerequisites(['api', 'frontend']);
    const unhealthy = health.filter(h => !h.healthy);
    if (unhealthy.length > 0) {
      console.error('[HEALTH GATE] Services down:', formatHealthResults(health));
      // Note: Playwright doesn't support test.skip() in beforeAll.
      // Set a state flag that EVERY test checks — including Test 1.
      state.failureReason = `Health gate failed: ${formatHealthResults(health)}`;
    }
  });

  // ... existing afterAll ...

  // ── Test 1: Admin Login ──
  test('1. Admin logs in', async ({ browser }) => {
    // IMPORTANT: Test 1 must also check health gate (it has no prior-test dependency)
    if (state.failureReason) test.skip(true, state.failureReason);

    // ... rest of test 1 unchanged ...
  });

  // Tests 2-8 already have: if (!state.X) test.skip(true, state.failureReason ?? '...');
```

- [ ] **Step 3: Add health gate to embedding flow**

Same pattern in `admin-embedding-flow.spec.ts`, checking `['api', 'frontend', 'embedding']`.

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/helpers/flow-health-gate.ts apps/web/e2e/flows/
git commit -m "feat(e2e): add pre-flight health gate for flow tests

Checks API, frontend, and embedding service health before running flows.
Prevents cascade failures when external services are down."
```

---

## Phase P2: Replace Brittle Timing

### Task 10: Replace `waitForTimeout` in onboarding flow

**Files:**
- Modify: `apps/web/e2e/flows/admin-user-onboarding.spec.ts`

7 instances of `waitForTimeout` at lines: 110, 150, 203, 281, 284, 355, 459.

- [ ] **Step 1: Replace each `waitForTimeout` with appropriate Playwright wait**

| Line | Current | Replacement |
|------|---------|-------------|
| 110 | `waitForTimeout(3000)` after invite send | `await page.waitForResponse(resp => resp.url().includes('invitation') && resp.ok());` or `await expect(page.getByText(/invitation sent/i)).toBeVisible({ timeout: 5_000 });` |
| 150 | `waitForTimeout(500)` after cookie dismiss | Removed — `dismissCookieConsent` helper already waits for banner to hide |
| 203 | `waitForTimeout(500)` after cookie dismiss | Same — replaced by helper in Task 5 |
| 281 | `waitForTimeout(500)` after cookie dismiss + reload | Same — replaced by helper |
| 284 | `waitForTimeout(1000)` after reload | `await page.waitForLoadState('networkidle');` |
| 355 | `waitForTimeout(500)` after cookie dismiss | Same — replaced by helper |
| 459 | `waitForTimeout(500)` after cookie dismiss | Same — replaced by helper |

Most of these are already resolved by the `dismissCookieConsent` helper from Task 5.

For line 110 (post-invite wait), the comment says "Don't use waitForNetworkIdle — admin page has continuous health polling". Use a targeted response wait instead:

```typescript
// BEFORE:
await adminUsersPage.submitInvitation();
await page.waitForTimeout(3000);

// AFTER:
// 1. Set up the response listener BEFORE triggering the action
const inviteResponsePromise = page.waitForResponse(
  resp => resp.url().includes('invitation') && resp.status() < 400,
  { timeout: 10_000 }
);
// 2. Trigger the action
await adminUsersPage.submitInvitation();
// 3. Await the response promise (NOT just .catch — must await resolution)
const inviteResponse = await inviteResponsePromise.catch(e => {
  console.warn('[T2] Invitation API response not intercepted:', e.message);
  return null;
});
if (inviteResponse) {
  console.log(`[T2] Invitation API responded: ${inviteResponse.status()}`);
}
```

For line 284 (post-reload wait):
```typescript
// BEFORE:
await page.reload({ waitUntil: 'networkidle' });
await page.waitForTimeout(1000);

// AFTER:
await page.reload({ waitUntil: 'domcontentloaded' });
await page.waitForLoadState('networkidle').catch(() => {
  console.log('[T5] networkidle timeout after reload (admin polling may prevent idle)');
});
```

- [ ] **Step 2: Run lint**

Run: `cd apps/web && npx eslint e2e/flows/admin-user-onboarding.spec.ts --no-error-on-unmatched-pattern`

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/flows/admin-user-onboarding.spec.ts
git commit -m "fix(e2e): replace waitForTimeout with proper Playwright waits in onboarding flow

Eliminates 7 hard waits. Uses waitForResponse, waitForLoadState, and
dismissCookieConsent helper instead of arbitrary timeouts."
```

---

## Phase P3: Contract Testing & Full-Stack CI (Future)

> **Note:** These tasks are larger initiatives. They are documented here as specifications for future implementation, not immediate actions.

### Task 11: API Contract Testing (specification only)

**Goal:** Validate that frontend API mocks match the real backend contract.

**Approach options:**
1. **Schema-based:** Export OpenAPI spec from backend (`/scalar/v1`), validate frontend API client types against it
2. **Pact-based:** Consumer-driven contract testing — frontend defines expectations, backend verifies

**Recommendation:** Start with option 1 (schema validation) — it's simpler and the API already has Scalar docs.

**Files to create:**
- `apps/web/e2e/contracts/validate-api-schema.ts` — Script that fetches OpenAPI spec and validates against frontend types
- `apps/web/e2e/contracts/api-schema.json` — Cached copy of OpenAPI spec for offline validation

**Not implemented in this plan** — requires separate design session.

---

### Task 12: Full-Stack CI Stage (specification only)

**Goal:** CI stage that runs flow tests against the real Docker stack.

**Approach:**
1. Add GitHub Actions job that:
   - Starts `make dev-core` (postgres, redis, api, web)
   - Waits for health checks
   - Runs `npx playwright test --grep @flow`
   - Uploads artifacts (screenshots, traces, videos)

**Files to create:**
- `.github/workflows/e2e-full-stack.yml` — Dedicated workflow for flow tests

**Not implemented in this plan** — requires CI infrastructure and Docker-in-CI setup.

---

## Execution Summary

| Phase | Tasks | Est. Time | Parallelizable |
|-------|-------|-----------|----------------|
| **P0** | Tasks 1-6 | ~3h | Tasks 2+3 can run in parallel; Task 4 independent |
| **P1** | Tasks 7-9 | ~2h | All 3 independent of each other |
| **P2** | Task 10 | ~1h | Depends on Task 5 (cookie helper) |
| **P3** | Tasks 11-12 | Spec only | Future work |
| **Total** | 10 impl tasks + 2 specs | ~6h | |

### Dependency Graph

```
Task 1 (E2ETestBase helper)
  ├→ Task 2 (ChatE2ETests) ─┐
  └→ Task 3 (remaining E2E) ─┤
                              ├→ Commit P0-a
Task 4 (cookie helper) ──────┤
  ├→ Task 5 (onboarding catches) ─┤
  └→ Task 6 (embedding catches) ──┤
                                   ├→ Commit P0-b
Task 7 (screenshots) ─────────────── Independent
Task 8 (test tags) ────────────────── Independent
Task 9 (health gate) ─────────────── Independent
Task 10 (waitForTimeout) ─────────── Depends on Task 5
```
