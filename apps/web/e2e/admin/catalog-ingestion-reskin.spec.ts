/**
 * Catalog ingestion re-skin E2E smoke tests (#1835)
 *
 * Covers:
 * 1. Page renders: hero with chip + stats + timeline empty-state + 2 placeholder panels + Export catalog CTA
 * 2. Provider switching shows/hides BGG-specific config controls
 * 3. CSV provider + Run sync now opens the CSV/Excel import modal
 *
 * Auth pattern: page.route() mock for /api/v1/auth/me (same as batch-jobs.spec.ts + AdminHelper)
 * API mocks: /api/v1/admin/catalog-ingestion/status + /api/v1/admin/catalog-ingestion/runs
 *
 * NOTE: E2E tests require a running Next.js dev/prod server (pnpm dev or pnpm build && pnpm start).
 * If the server is not running locally, tests will fail with a connection error — this is expected
 * and the spec is designed to pass in CI where `pnpm build && pnpm start` is always executed first.
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ─── Mock helpers ─────────────────────────────────────────────────────────────

async function setupCatalogIngestionMocks(page: Page): Promise<void> {
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

  // Catalog sync status — idle state with cumulative stats
  await page.route(`**/api/v1/admin/catalog-ingestion/status`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        status: 'idle',
        lastRun: {
          id: 'run-1',
          provider: 'BggApi',
          status: 'Success',
          title: 'BGG API sync',
          createdAt: new Date(Date.now() - 6 * 60 * 60 * 1000).toISOString(),
          startedAt: new Date(Date.now() - 6 * 60 * 60 * 1000).toISOString(),
          completedAt: new Date(Date.now() - 5 * 60 * 60 * 1000).toISOString(),
          duration: '00:58:22',
          itemsAdded: 12,
          itemsUpdated: 3,
          itemsFailed: 0,
          errorCode: null,
          errorDetail: null,
          triggeredByUserId: null,
        },
        currentRun: null,
        cumulative: { gamesTotal: 1234 },
        nextScheduled: 'in 5h 2m',
      }),
    });
  });

  // Catalog sync runs — empty list (shows empty-state "Sync history" heading)
  await page.route(`**/api/v1/admin/catalog-ingestion/runs**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [],
        total: 0,
        page: 1,
        pageSize: 12,
        hasMore: false,
      }),
    });
  });

  // Catch-all for other admin API calls
  await page.route(`${API_BASE}/api/v1/admin/**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ data: [] }),
    });
  });
}

// ─── Tests ───────────────────────────────────────────────────────────────────

test.describe('Catalog ingestion re-skin (#1835)', () => {
  test.beforeEach(async ({ page }) => {
    await setupCatalogIngestionMocks(page);
    await page.goto('/admin/catalog-ingestion');
    // Wait for the SyncStatusHero to render (it shows a pulse skeleton while data === undefined)
    await page.waitForLoadState('networkidle');
  });

  test('page renders hero with chip + stats + timeline + 2 placeholder panels', async ({
    page,
  }) => {
    // Page heading
    await expect(page.getByRole('heading', { name: /Catalog ingestion/i })).toBeVisible();

    // Hero chip — idle state maps to "Idle"
    await expect(page.getByText(/Idle|Running|Last sync failed|Setup/)).toBeVisible();

    // Stats — "Giochi importati totali:" is shown in SyncStatusHero
    await expect(page.getByText(/Giochi importati totali/i)).toBeVisible();

    // Provider dropdown
    await expect(page.getByLabel(/Provider/i)).toBeVisible();

    // Run sync now button
    await expect(page.getByRole('button', { name: /Run sync now/i })).toBeVisible();

    // Timeline empty-state heading
    await expect(page.getByText(/Sync history/i)).toBeVisible();

    // QueuePendingPanel placeholder
    await expect(page.getByText(/Queue pending/i).first()).toBeVisible();

    // FailedItemsPanel placeholder
    await expect(page.getByText(/Failed items/i).first()).toBeVisible();

    // Export catalog CTA (renamed from old "Export history")
    await expect(page.getByRole('link', { name: /Export catalog/i })).toBeVisible();

    // OLD label must NOT be present (regression guard)
    await expect(page.getByText(/Export history/i)).not.toBeVisible();
  });

  test('Provider switching shows/hides BGG config controls', async ({ page }) => {
    // Default provider is BggApi — Batch size input should be visible
    await expect(page.getByLabel(/Batch size/i)).toBeVisible();

    // Switch to CsvImport — BGG-specific controls should disappear
    await page.getByLabel(/Provider/i).selectOption('CsvImport');
    await expect(page.getByLabel(/Batch size/i)).not.toBeVisible();

    // Switch to Manual — BGG-specific controls still hidden
    await page.getByLabel(/Provider/i).selectOption('Manual');
    await expect(page.getByLabel(/Batch size/i)).not.toBeVisible();

    // Switch back to BggApi — controls reappear
    await page.getByLabel(/Provider/i).selectOption('BggApi');
    await expect(page.getByLabel(/Batch size/i)).toBeVisible();
  });

  test('CSV provider + Run sync now opens CSV/Excel import modal', async ({ page }) => {
    // Switch to CsvImport provider
    await page.getByLabel(/Provider/i).selectOption('CsvImport');

    // Click Run sync now — should open the CsvImportModal instead of triggering API
    await page.getByRole('button', { name: /Run sync now/i }).click();

    // Modal title must be visible
    await expect(page.getByText(/CSV \/ Excel import/i)).toBeVisible();
  });
});
