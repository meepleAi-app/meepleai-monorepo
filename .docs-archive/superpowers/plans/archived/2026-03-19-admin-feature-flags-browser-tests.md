# Admin Feature Flags & Tier Limits — Browser Test Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Write 6 P0 Playwright browser tests validating that admin feature flags and tier limits management is fully functional via UI.

**Architecture:** Mock-first approach using `page.context().route()` for API interception (no backend required). Tests follow existing project patterns: `mockAdminAuth()` → mock endpoints → navigate → assert. Critical flag confirmation uses `page.on('dialog')` for `window.confirm()`.

**Tech Stack:** Playwright, TypeScript, page.context().route() mocking, window.confirm dialog handling

---

## File Structure

| File | Responsibility |
|------|----------------|
| `apps/web/e2e/admin/admin-feature-flags.spec.ts` | FF-01, FF-03, FF-06: Feature flag toggle, tier enable, critical dialog |
| `apps/web/e2e/admin/admin-tier-management.spec.ts` | TL-01, TL-02, TL-04: View tiers, edit limits, validation |

Both files are independent and can run in parallel.

---

## Mock Data Reference

### SystemConfigurationDto shape (for feature flags)
```typescript
{
  id: string (uuid),
  key: string (e.g. "Features:PdfUpload"),
  value: "true" | "false",
  valueType: "bool",
  description: string | null,
  category: "FeatureFlag",
  isActive: boolean,
  requiresRestart: boolean,
  environment: "Development",
  version: number,
  previousValue: string | null,
  createdAt: ISO datetime,
  updatedAt: ISO datetime,
  createdByUserId: string (uuid),
  updatedByUserId: string | null,
  lastToggledAt: string | null,
  tierFree?: boolean,
  tierNormal?: boolean,
  tierPremium?: boolean,
}
```

### API called by FeatureFlagsWrapper
```
GET /api/v1/admin/configurations?activeOnly=true&page=1&pageSize=100
→ Returns: { items: SystemConfigurationDto[], total, page, pageSize }
```

### API called by FeatureFlagsTab toggle
```
PUT /api/v1/admin/configurations/{id}
Body: { value: "true"|"false" }
```

### API called by tier toggle
```
POST /api/v1/admin/feature-flags/{featureKey}/tier/{tier}/enable
POST /api/v1/admin/feature-flags/{featureKey}/tier/{tier}/disable
```

### TierDefinition shape
```typescript
{
  id: string (uuid),
  name: string,
  displayName: string,
  description: string | null,
  monthlyPriceEur: number,
  isActive: boolean,
  limits: Array<{ key: string, value: number }>,
}
```

### API called by tiers page
```
GET  /api/v1/admin/tiers → TierDefinition[]
PUT  /api/v1/admin/tiers/{name} → TierDefinition
POST /api/v1/admin/tiers → TierDefinition
```

---

## Task 1: Feature Flags Browser Tests (FF-01, FF-03, FF-06)

**Files:**
- Create: `apps/web/e2e/admin/admin-feature-flags.spec.ts`

- [ ] **Step 1: Create the spec file with mock data and auth setup**

```typescript
/**
 * Admin — Feature Flags Management (Browser Tests)
 *
 * FF-01: Toggle global feature flag (active → inactive)
 * FF-03: Enable feature for specific tier (Free)
 * FF-06: Critical flag shows confirmation dialog
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ── Auth mock ───────────────────────────────────────────────────────────────

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'admin-1', email: 'admin@meepleai.dev', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

// ── Mock data ───────────────────────────────────────────────────────────────

const NOW = '2026-03-19T10:00:00.000Z';
const ADMIN_ID = '00000000-0000-0000-0000-000000000001';

const MOCK_FLAGS = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    key: 'Features:PdfUpload',
    value: 'true',
    valueType: 'bool',
    description: 'Enable PDF upload feature',
    category: 'FeatureFlag',
    isActive: true,
    requiresRestart: false,
    environment: 'Development',
    version: 1,
    previousValue: null,
    createdAt: NOW,
    updatedAt: NOW,
    createdByUserId: ADMIN_ID,
    updatedByUserId: null,
    lastToggledAt: null,
    tierFree: true,
    tierNormal: true,
    tierPremium: true,
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    key: 'Features:GameNightV2',
    value: 'true',
    valueType: 'bool',
    description: 'Game Night V2 redesign',
    category: 'FeatureFlag',
    isActive: true,
    requiresRestart: false,
    environment: 'Development',
    version: 1,
    previousValue: null,
    createdAt: NOW,
    updatedAt: NOW,
    createdByUserId: ADMIN_ID,
    updatedByUserId: null,
    lastToggledAt: null,
    tierFree: false,
    tierNormal: true,
    tierPremium: true,
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    key: 'Features:RagCaching',
    value: 'true',
    valueType: 'bool',
    description: 'RAG query caching for performance',
    category: 'FeatureFlag',
    isActive: true,
    requiresRestart: true,
    environment: 'Development',
    version: 2,
    previousValue: 'false',
    createdAt: NOW,
    updatedAt: NOW,
    createdByUserId: ADMIN_ID,
    updatedByUserId: ADMIN_ID,
    lastToggledAt: NOW,
    tierFree: false,
    tierNormal: false,
    tierPremium: true,
  },
];

function buildConfigResponse(flags = MOCK_FLAGS) {
  return { items: flags, total: flags.length, page: 1, pageSize: 100 };
}

// ── Tests ───────────────────────────────────────────────────────────────────

test.describe('ADM — Feature Flags', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);

    // Mock configurations endpoint (used by FeatureFlagsWrapper)
    await page.context().route(
      `${API_BASE}/api/v1/admin/configurations**`,
      (route) => route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(buildConfigResponse()),
      })
    );

    // Mock fallback for other admin endpoints
    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) => {
      const url = route.request().url();
      // Don't intercept already-mocked routes
      if (url.includes('/configurations')) return route.fallback();
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: [] }),
      });
    });
  });

  // ── FF-01: Toggle global feature flag ──────────────────────────────────

  test('FF-01: toggle global feature flag active → inactive', async ({ page }) => {
    // Track the PUT call to verify API interaction
    let toggleCalled = false;
    let toggleBody: Record<string, unknown> = {};

    await page.context().route(
      `${API_BASE}/api/v1/admin/configurations/11111111-1111-1111-1111-111111111111`,
      async (route) => {
        if (route.request().method() === 'PUT') {
          toggleCalled = true;
          toggleBody = JSON.parse(route.request().postData() ?? '{}');
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ ...MOCK_FLAGS[0], value: 'false' }),
          });
        }
        return route.fallback();
      }
    );

    // Navigate to Feature Flags tab
    await page.goto('/admin/config?tab=flags', { waitUntil: 'domcontentloaded' });

    // Wait for flags table to render
    await expect(page.locator('text=PdfUpload')).toBeVisible({ timeout: 10_000 });

    // Verify initial state: PdfUpload shows "Enabled" badge
    const pdfRow = page.locator('tr').filter({ hasText: 'PdfUpload' });
    await expect(pdfRow.getByText('Enabled')).toBeVisible();

    // Find and click the global toggle for PdfUpload (aria-label="Toggle Features:PdfUpload")
    const globalToggle = pdfRow.getByRole('switch', { name: /Toggle Features:PdfUpload/i });
    await expect(globalToggle).toBeVisible();
    await globalToggle.click();

    // Verify API was called with value: "false"
    expect(toggleCalled).toBe(true);
    expect(toggleBody.value).toBe('false');

    // Verify toast success message appears
    await expect(page.getByText(/Feature flag.*PdfUpload.*disabled/i)).toBeVisible({ timeout: 5_000 });
  });

  // ── FF-03: Enable feature for tier ─────────────────────────────────────

  test('FF-03: enable feature for Free tier', async ({ page }) => {
    let tierEnableCalled = false;

    // Note: configClient encodes the featureKey with encodeURIComponent,
    // so "Features:GameNightV2" becomes "Features%3AGameNightV2" in the URL.
    // Use glob pattern for robustness against encoding variations.
    await page.context().route(
      `${API_BASE}/api/v1/admin/feature-flags/*/tier/free/enable`,
      (route) => {
        tierEnableCalled = true;
        return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
      }
    );

    await page.goto('/admin/config?tab=flags', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('text=GameNightV2')).toBeVisible({ timeout: 10_000 });

    // Find the GameNightV2 row
    const gnRow = page.locator('tr').filter({ hasText: 'GameNightV2' });

    // The Free tier toggle for GameNightV2 should be OFF (tierFree: false in mock)
    const freeTierToggle = gnRow.getByRole('switch', { name: /Toggle Free tier for Features:GameNightV2/i });
    await expect(freeTierToggle).toBeVisible();

    // Verify it's currently unchecked (data-state="unchecked")
    await expect(freeTierToggle).toHaveAttribute('data-state', 'unchecked');

    // Click to enable
    await freeTierToggle.click();

    // Verify API was called
    expect(tierEnableCalled).toBe(true);

    // Verify toast success
    await expect(page.getByText(/Free tier enabled/i)).toBeVisible({ timeout: 5_000 });
  });

  // ── FF-06: Critical flag shows confirmation dialog ─────────────────────

  test('FF-06: critical flag (RagCaching) shows confirmation dialog', async ({ page }) => {
    let dialogMessage = '';
    let dialogDismissed = false;

    // Listen for browser dialog (window.confirm)
    page.on('dialog', async (dialog) => {
      dialogMessage = dialog.message();
      // Dismiss (cancel) to verify no API call happens
      dialogDismissed = true;
      await dialog.dismiss();
    });

    let toggleCalled = false;
    await page.context().route(
      `${API_BASE}/api/v1/admin/configurations/33333333-3333-3333-3333-333333333333`,
      (route) => {
        if (route.request().method() === 'PUT') {
          toggleCalled = true;
          return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
        }
        return route.fallback();
      }
    );

    await page.goto('/admin/config?tab=flags', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('text=RagCaching')).toBeVisible({ timeout: 10_000 });

    // Find RagCaching row and its global toggle
    const ragRow = page.locator('tr').filter({ hasText: 'RagCaching' });
    const ragToggle = ragRow.getByRole('switch', { name: /Toggle Features:RagCaching/i });
    await expect(ragToggle).toBeVisible();

    // Verify RagCaching has "Restart" badge (requiresRestart: true)
    await expect(ragRow.getByText('Restart')).toBeVisible();

    // Click toggle → should trigger window.confirm
    await ragToggle.click();

    // Verify dialog appeared with correct message
    expect(dialogDismissed).toBe(true);
    expect(dialogMessage).toContain('RagCaching');
    expect(dialogMessage).toContain('Are you sure');

    // Verify API was NOT called (user dismissed dialog)
    expect(toggleCalled).toBe(false);
  });
});
```

- [ ] **Step 2: Run the test to verify it works**

Run: `cd apps/web && npx playwright test e2e/admin/admin-feature-flags.spec.ts --project=desktop-chrome --headed`

Expected: All 3 tests pass (FF-01, FF-03, FF-06). If any fail, debug by inspecting selectors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/admin/admin-feature-flags.spec.ts
git commit -m "test(e2e): add browser tests for admin feature flags (FF-01, FF-03, FF-06)"
```

---

## Task 2: Tier Management Browser Tests (TL-01, TL-02, TL-04)

**Files:**
- Create: `apps/web/e2e/admin/admin-tier-management.spec.ts`

- [ ] **Step 1: Create the spec file with mock data and auth setup**

```typescript
/**
 * Admin — Tier Management (Browser Tests)
 *
 * TL-01: View all tiers with limits displayed
 * TL-02: Edit tier limits (MaxPrivateGames)
 * TL-04: Negative value validation rejected
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ── Auth mock ───────────────────────────────────────────────────────────────

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, (route) =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: 'admin-1', email: 'admin@meepleai.dev', displayName: 'Admin', role: 'Admin' },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

// ── Mock data ───────────────────────────────────────────────────────────────

const MOCK_TIERS = [
  {
    id: 'aaaa0000-0000-0000-0000-000000000001',
    name: 'free',
    displayName: 'Free Tier',
    description: 'Basic access',
    monthlyPriceEur: 0,
    isActive: true,
    limits: [
      { key: 'MaxPrivateGames', value: 3 },
      { key: 'MaxPdfPerMonth', value: 3 },
      { key: 'MaxAgents', value: 1 },
      { key: 'MaxAgentQueriesPerDay', value: 20 },
      { key: 'MaxSessionQueries', value: 30 },
      { key: 'MaxPlayersPerSession', value: 6 },
      { key: 'MaxPhotosPerSession', value: 5 },
      { key: 'MaxCatalogProposalsPerWeek', value: 1 },
    ],
  },
  {
    id: 'aaaa0000-0000-0000-0000-000000000002',
    name: 'premium',
    displayName: 'Premium Plan',
    description: 'Standard subscription',
    monthlyPriceEur: 9.99,
    isActive: true,
    limits: [
      { key: 'MaxPrivateGames', value: 15 },
      { key: 'MaxPdfPerMonth', value: 15 },
      { key: 'MaxAgents', value: 10 },
      { key: 'MaxAgentQueriesPerDay', value: 200 },
      { key: 'MaxSessionQueries', value: 150 },
      { key: 'MaxPlayersPerSession', value: 12 },
      { key: 'MaxPhotosPerSession', value: 20 },
      { key: 'MaxCatalogProposalsPerWeek', value: 5 },
    ],
  },
  {
    id: 'aaaa0000-0000-0000-0000-000000000003',
    name: 'admin',
    displayName: 'Admin',
    description: 'Unlimited access',
    monthlyPriceEur: 0,
    isActive: true,
    limits: [
      { key: 'MaxPrivateGames', value: 2_147_483_647 },
      { key: 'MaxPdfPerMonth', value: 2_147_483_647 },
      { key: 'MaxAgents', value: 2_147_483_647 },
      { key: 'MaxAgentQueriesPerDay', value: 2_147_483_647 },
      { key: 'MaxSessionQueries', value: 2_147_483_647 },
      { key: 'MaxPlayersPerSession', value: 12 },
      { key: 'MaxPhotosPerSession', value: 2_147_483_647 },
      { key: 'MaxCatalogProposalsPerWeek', value: 2_147_483_647 },
    ],
  },
];

// ── Tests ───────────────────────────────────────────────────────────────────

test.describe('ADM — Tier Management', () => {
  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);

    // Mock tiers endpoint
    await page.context().route(`${API_BASE}/api/v1/admin/tiers**`, (route) => {
      if (route.request().method() === 'GET') {
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_TIERS),
        });
      }
      return route.fallback();
    });

    // Mock fallback for other admin endpoints
    await page.context().route(`${API_BASE}/api/v1/admin/**`, (route) => {
      const url = route.request().url();
      if (url.includes('/tiers')) return route.fallback();
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ data: [] }),
      });
    });
  });

  // ── TL-01: View all tiers with limits ──────────────────────────────────

  test('TL-01: view all tiers with correct limits displayed', async ({ page }) => {
    await page.goto('/admin/config/tiers', { waitUntil: 'domcontentloaded' });

    // Wait for table to load
    const table = page.getByTestId('tiers-table');
    await expect(table).toBeVisible({ timeout: 10_000 });

    // Verify all 3 tiers are displayed
    await expect(page.getByTestId('tier-row-free')).toBeVisible();
    await expect(page.getByTestId('tier-row-premium')).toBeVisible();
    await expect(page.getByTestId('tier-row-admin')).toBeVisible();

    // Verify Free tier row content
    const freeRow = page.getByTestId('tier-row-free');
    await expect(freeRow).toContainText('free');
    await expect(freeRow).toContainText('Free Tier');
    // Price is 0 → shows "—"
    await expect(freeRow).toContainText('—');
    // MaxPrivateGames = 3
    await expect(freeRow.locator('td').nth(3)).toContainText('3');
    // Status badge "Attivo"
    await expect(freeRow).toContainText('Attivo');

    // Verify Premium tier shows price
    const premiumRow = page.getByTestId('tier-row-premium');
    await expect(premiumRow).toContainText('€9.99');
    await expect(premiumRow).toContainText('Premium Plan');
    // MaxPrivateGames = 15
    await expect(premiumRow.locator('td').nth(3)).toContainText('15');

    // Verify Admin tier shows ∞ for unlimited values
    const adminRow = page.getByTestId('tier-row-admin');
    // MaxPrivateGames = 2147483647 → "∞"
    await expect(adminRow.locator('td').nth(3)).toContainText('∞');

    // Verify "Nuovo Tier" create button exists
    await expect(page.getByTestId('btn-create-tier')).toBeVisible();

    // Verify edit buttons exist for each tier
    await expect(page.getByTestId('btn-edit-free')).toBeVisible();
    await expect(page.getByTestId('btn-edit-premium')).toBeVisible();
    await expect(page.getByTestId('btn-edit-admin')).toBeVisible();
  });

  // ── TL-02: Edit tier limits ────────────────────────────────────────────

  test('TL-02: edit MaxPrivateGames for Free tier', async ({ page }) => {
    let updateCalled = false;
    let updateBody: Record<string, unknown> = {};

    // Mock PUT /admin/tiers/free
    await page.context().route(
      `${API_BASE}/api/v1/admin/tiers/free`,
      (route) => {
        if (route.request().method() === 'PUT') {
          updateCalled = true;
          updateBody = JSON.parse(route.request().postData() ?? '{}');

          // Return updated tier
          const updatedTier = {
            ...MOCK_TIERS[0],
            limits: MOCK_TIERS[0].limits.map(l =>
              l.key === 'MaxPrivateGames' ? { key: 'MaxPrivateGames', value: 5 } : l
            ),
          };
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(updatedTier),
          });
        }
        return route.fallback();
      }
    );

    await page.goto('/admin/config/tiers', { waitUntil: 'domcontentloaded' });
    await expect(page.getByTestId('tiers-table')).toBeVisible({ timeout: 10_000 });

    // Click edit button for Free tier
    await page.getByTestId('btn-edit-free').click();

    // Verify dialog opens with correct title
    await expect(page.getByText('Modifica tier: free')).toBeVisible({ timeout: 5_000 });

    // Verify current value of MaxPrivateGames = 3
    const maxGamesInput = page.getByTestId('field-limit-MaxPrivateGames');
    await expect(maxGamesInput).toBeVisible();
    await expect(maxGamesInput).toHaveValue('3');

    // Clear and type new value
    await maxGamesInput.fill('5');
    await expect(maxGamesInput).toHaveValue('5');

    // Click save
    await page.getByTestId('btn-save').click();

    // Verify API was called with correct body
    expect(updateCalled).toBe(true);
    const sentLimits = updateBody.limits as Array<{ key: string; value: number }>;
    const maxGamesLimit = sentLimits?.find((l: { key: string }) => l.key === 'MaxPrivateGames');
    expect(maxGamesLimit?.value).toBe(5);

    // Verify success toast
    await expect(page.getByText(/Tier aggiornato/i)).toBeVisible({ timeout: 5_000 });
  });

  // ── TL-04: Negative value validation ───────────────────────────────────

  test('TL-04: negative limit value is rejected by backend validation', async ({ page }) => {
    // Mock PUT to return 400 validation error for negative values
    await page.context().route(
      `${API_BASE}/api/v1/admin/tiers/free`,
      (route) => {
        if (route.request().method() === 'PUT') {
          const body = JSON.parse(route.request().postData() ?? '{}');
          const limits = body.limits as Array<{ key: string; value: number }> | undefined;
          const hasNegative = limits?.some((l: { value: number }) => l.value < 0);

          if (hasNegative) {
            return route.fulfill({
              status: 400,
              contentType: 'application/json',
              body: JSON.stringify({
                title: 'Validation Error',
                errors: { 'limits[0].value': ['Value must be >= 0'] },
              }),
            });
          }
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(MOCK_TIERS[0]),
          });
        }
        return route.fallback();
      }
    );

    await page.goto('/admin/config/tiers', { waitUntil: 'domcontentloaded' });
    await expect(page.getByTestId('tiers-table')).toBeVisible({ timeout: 10_000 });

    // Open edit dialog for Free tier
    await page.getByTestId('btn-edit-free').click();
    await expect(page.getByText('Modifica tier: free')).toBeVisible({ timeout: 5_000 });

    // Note: The input has min=0, so the browser HTML5 constraint validation
    // should prevent negative values. However, a user can still type/paste negatives.
    // The backend validation is the safety net.

    // Type a negative value for MaxPrivateGames
    const maxGamesInput = page.getByTestId('field-limit-MaxPrivateGames');
    await maxGamesInput.fill('-1');

    // Click save — should either:
    // a) Be blocked by HTML min=0 constraint (input won't submit), OR
    // b) Reach backend which returns 400, showing error toast
    await page.getByTestId('btn-save').click();

    // Wait for either: error toast OR the input still showing invalid state
    // The HTML min=0 attribute prevents form submission with native validation,
    // but Number(-1) still gets sent because the form uses onChange + state.
    // So the backend 400 error should trigger.
    await expect(
      page.getByText(/Errore|Validation Error|Value must be/i)
    ).toBeVisible({ timeout: 5_000 });

    // Verify dialog is still open (save failed, dialog didn't close)
    await expect(page.getByText('Modifica tier: free')).toBeVisible();
  });
});
```

- [ ] **Step 2: Run the test to verify it works**

Run: `cd apps/web && npx playwright test e2e/admin/admin-tier-management.spec.ts --project=desktop-chrome --headed`

Expected: All 3 tests pass (TL-01, TL-02, TL-04). If any fail, debug by inspecting selectors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/admin/admin-tier-management.spec.ts
git commit -m "test(e2e): add browser tests for admin tier management (TL-01, TL-02, TL-04)"
```

---

## Task 3: Run All Tests Together & Fix Issues

- [ ] **Step 1: Run both spec files together**

Run: `cd apps/web && npx playwright test e2e/admin/admin-feature-flags.spec.ts e2e/admin/admin-tier-management.spec.ts --project=desktop-chrome`

Expected: 6 tests pass.

- [ ] **Step 2: Run with the full admin test suite to check no regressions**

Run: `cd apps/web && npx playwright test e2e/admin/ --project=desktop-chrome`

Expected: No regressions in existing tests.

- [ ] **Step 3: Final commit if any fixes were needed**

```bash
git add apps/web/e2e/admin/admin-feature-flags.spec.ts apps/web/e2e/admin/admin-tier-management.spec.ts
git commit -m "test(e2e): fix selectors after live browser validation"
```

---

## Selector Reference (Quick Lookup)

| Element | Selector Strategy |
|---------|-------------------|
| Feature flag row | `page.locator('tr').filter({ hasText: 'FlagName' })` |
| Global toggle | `row.getByRole('switch', { name: /Toggle Features:FlagName/i })` |
| Tier toggle | `row.getByRole('switch', { name: /Toggle Free tier for Features:FlagName/i })` |
| Enabled badge | `row.getByText('Enabled')` |
| Restart badge | `row.getByText('Restart')` |
| Tiers table | `page.getByTestId('tiers-table')` |
| Tier row | `page.getByTestId('tier-row-{name}')` |
| Edit tier button | `page.getByTestId('btn-edit-{name}')` |
| Create tier button | `page.getByTestId('btn-create-tier')` |
| Limit input field | `page.getByTestId('field-limit-{LimitKey}')` |
| Save button | `page.getByTestId('btn-save')` |
| Dialog title | `page.getByText('Modifica tier: {name}')` |
| window.confirm | `page.on('dialog', handler)` |

---

## Mock vs Real Comparison Matrix

| Test | Mock State | Browser Assertion | What validates the US |
|------|-----------|-------------------|----------------------|
| FF-01 | `value: "true"` → toggle → PUT with `"false"` | Badge "Enabled"→toast "disabled" | Admin can toggle flags via UI |
| FF-03 | `tierFree: false` → click → POST enable | Toggle `data-state` unchecked→toast "enabled" | Admin can set tier access via UI |
| FF-06 | Critical flag active → toggle | `window.confirm` dialog appears, dismiss→no API call | Safety dialog prevents accidental disable |
| TL-01 | 3 tiers with known limits | Table rows with correct values, ∞ for admin | All tiers visible with limits |
| TL-02 | `MaxPrivateGames: 3` → fill 5 → PUT | Input value changes, API body has 5, toast "aggiornato" | Admin can edit limits via UI |
| TL-04 | Fill `-1` → PUT returns 400 | Error toast, dialog stays open | Negative values rejected |
