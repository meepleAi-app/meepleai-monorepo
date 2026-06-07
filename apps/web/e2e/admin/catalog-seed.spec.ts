/**
 * Admin catalog seed queue E2E smoke tests (#1903 M8.5)
 *
 * Covers:
 * 1. Admin reaches /admin/catalog/seed-queue → page renders + KPI hero visible
 * 2. Mock API: bulk paste 3 BGG IDs → POST /bulk invoked → 3 entries in queue
 * 3. Filter chip "Pending" → list refetches with status=Pending param
 *
 * Auth pattern: page.route() mock for /api/v1/auth/me (same as
 * catalog-ingestion-reskin.spec.ts and admin-mockups specs).
 *
 * API mocks: /api/v1/admin/catalog/seeds endpoints + SSE stream
 *
 * NOTE: E2E tests require a running Next.js dev/prod server. Skipped in
 * Backend Fast unit-test runs (we already cover the components with Vitest).
 */

import { test, expect, type Page } from '../fixtures';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ─── Mock helpers ─────────────────────────────────────────────────────────────

interface SeedListState {
  items: Record<string, unknown>[];
  bulkRequestBody: unknown;
  bulkCalled: number;
  lastListStatusFilter: string | null;
}

async function setupCatalogSeedMocks(page: Page, state: SeedListState): Promise<void> {
  // Admin auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'admin-test-id',
          email: 'admin@meepleai.dev',
          displayName: 'Test Admin',
          role: 'Admin',
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // SSE stream — keep open and emit nothing so the EventSource opens cleanly
  await page.route(`**/api/v1/admin/catalog/seeds/stream`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'text/event-stream',
      body: ':ok\n\n',
    });
  });

  // Bulk enqueue
  await page.route(`**/api/v1/admin/catalog/seeds/bulk`, async route => {
    state.bulkCalled++;
    state.bulkRequestBody = JSON.parse(route.request().postData() ?? '{}');
    const bggIds = (state.bulkRequestBody as { bggIds: number[] }).bggIds;
    const newDrafts = bggIds.map((bgg, idx) => ({
      id: `seed-${state.items.length + idx + 1}`,
      bggId: bgg,
      wikidataQid: null,
      searchTermInput: null,
      status: 'Pending',
      errorMessage: null,
      resultingSharedGameId: null,
      createdByUserId: 'admin-test-id',
      createdAt: new Date().toISOString(),
      fetchedAt: null,
      approvedAt: null,
      approvedByUserId: null,
    }));
    state.items.push(...newDrafts);
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        total: bggIds.length,
        enqueued: bggIds.length,
        duplicates: 0,
        newDraftIds: newDrafts.map(d => d.id),
      }),
    });
  });

  // List endpoint — honour the status filter to support test #3
  await page.route(`**/api/v1/admin/catalog/seeds*`, async route => {
    if (route.request().method() !== 'GET') return route.fallback();
    const url = new URL(route.request().url());
    const statusFilter = url.searchParams.get('status');
    state.lastListStatusFilter = statusFilter;
    const items = statusFilter ? state.items.filter(i => i.status === statusFilter) : state.items;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        total: items.length,
        skip: Number(url.searchParams.get('skip') ?? 0),
        take: Number(url.searchParams.get('take') ?? 50),
        items,
      }),
    });
  });
}

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Admin catalog seed queue (#1903)', () => {
  test('page renders header + KPI hero + queue list + log stream', async ({ page }) => {
    const state: SeedListState = {
      items: [],
      bulkRequestBody: null,
      bulkCalled: 0,
      lastListStatusFilter: null,
    };
    await setupCatalogSeedMocks(page, state);
    await page.goto('/admin/catalog/seed-queue');
    await page.waitForLoadState('networkidle');

    // Page header
    await expect(
      page.getByRole('heading', { name: /Catalog seed queue/i, level: 1 })
    ).toBeVisible();
    await expect(page.getByText(/Admin · Catalog · Seed pipeline/i)).toBeVisible();

    // KPI cards
    await expect(page.getByTestId('seed-status-card-pending')).toBeVisible();
    await expect(page.getByTestId('seed-status-card-fetched')).toBeVisible();
    await expect(page.getByTestId('seed-status-card-approved')).toBeVisible();
    await expect(page.getByTestId('seed-status-card-rejected')).toBeVisible();

    // Input column forms (region role from <section aria-label>)
    await expect(page.getByRole('region', { name: /Bulk paste BGG IDs/i })).toBeVisible();
    await expect(page.getByRole('region', { name: /Single seed add/i })).toBeVisible();
    await expect(page.getByRole('region', { name: /Wikidata search/i })).toBeVisible();

    // Queue + log stream
    await expect(page.getByRole('region', { name: /Catalog seed queue list/i })).toBeVisible();
    await expect(page.getByRole('region', { name: /Catalog seed live stream/i })).toBeVisible();
  });

  test('bulk paste 3 BGG IDs invokes POST /bulk', async ({ page }) => {
    const state: SeedListState = {
      items: [],
      bulkRequestBody: null,
      bulkCalled: 0,
      lastListStatusFilter: null,
    };
    await setupCatalogSeedMocks(page, state);
    await page.goto('/admin/catalog/seed-queue');
    await page.waitForLoadState('networkidle');

    await page.getByLabel(/BGG IDs textarea/i).fill('13\n30549\n167791');
    await page.getByRole('button', { name: /Enqueue 3 IDs/i }).click();

    await expect.poll(() => state.bulkCalled).toBeGreaterThanOrEqual(1);
    expect(state.bulkRequestBody).toEqual({ bggIds: [13, 30549, 167791] });

    // After mutation the list refetches and shows the 3 new rows
    await expect(page.getByText(/BGG:13/)).toBeVisible();
    await expect(page.getByText(/BGG:30549/)).toBeVisible();
    await expect(page.getByText(/BGG:167791/)).toBeVisible();
  });

  test('clicking the Pending filter chip refetches the list with status=Pending', async ({
    page,
  }) => {
    const state: SeedListState = {
      items: [],
      bulkRequestBody: null,
      bulkCalled: 0,
      lastListStatusFilter: null,
    };
    await setupCatalogSeedMocks(page, state);
    await page.goto('/admin/catalog/seed-queue');
    await page.waitForLoadState('networkidle');

    // Initial load uses no status filter
    expect(state.lastListStatusFilter).toBeNull();

    await page.getByRole('button', { name: 'Pending', exact: true }).click();
    await expect.poll(() => state.lastListStatusFilter).toBe('Pending');
  });
});
