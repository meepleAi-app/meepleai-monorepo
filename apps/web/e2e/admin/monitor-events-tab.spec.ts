/**
 * Admin Monitor — Events tab smoke (F4.1, Issue #1718)
 *
 * Scope: structural smoke only.
 *   - Admin auth mock (no real backend needed)
 *   - Navigate to /admin/monitor?tab=events
 *   - Verify "Events" tab is aria-selected
 *   - Verify LiveEventLog section renders (header h3 or region aria-label)
 *   - Verify Pause/Resume button is present
 *
 * Deliberately skips:
 *   - Live SSE data assertions (require seeded DB + running server)
 *   - Filter chip interactions (covered by LiveEventLog unit tests)
 *   - Export ndjson button click (placeholder, no-op in Phase 4)
 *
 * Pattern mirrors apps/web/e2e/admin/kb-doc-actions.spec.ts (AdminHelper fixture).
 */

import { expect, Page, test as base } from '@playwright/test';

import { AdminHelper } from '../pages';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Fixture: adminPage
// Sets up /auth/me mock + catch-all /api/v1/admin/** stub.
// Each test navigates explicitly (skipNavigation = true).
// ---------------------------------------------------------------------------

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);
    await adminHelper.setupAdminAuth(true);

    // Stub the events backfill endpoint (useLiveEvents calls GET on mount)
    await page.route(`${apiBase}/api/v1/admin/events**`, async route => {
      // SSE stream: return an empty text/event-stream body so the browser
      // connects but receives no events (safe for smoke assertions).
      if (
        route.request().method() === 'GET' &&
        route.request().headers()['accept']?.includes('text/event-stream')
      ) {
        await route.fulfill({
          status: 200,
          headers: {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
          },
          body: '',
        });
        return;
      }
      // Backfill JSON endpoint
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: [], totalCount: 0, nextCursor: null }),
      });
    });

    await use(page);
  },
});

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('Monitor — Events tab (F4.1 #1718)', () => {
  test('Events tab is active when tab=events', async ({ adminPage: page }) => {
    await page.goto('/admin/monitor?tab=events', { waitUntil: 'domcontentloaded' });

    // The AdminHubTabBar renders each tab as role="tab" with aria-selected.
    // The active tab link has aria-selected="true".
    const eventsTab = page.getByRole('tab', { name: /events/i });
    await expect(eventsTab).toBeVisible({ timeout: 8_000 });
    await expect(eventsTab).toHaveAttribute('aria-selected', 'true');
  });

  test('LiveEventLog section renders with header and Pause button', async ({ adminPage: page }) => {
    await page.goto('/admin/monitor?tab=events', { waitUntil: 'domcontentloaded' });

    // LiveEventLog renders as <section role="region" aria-label="Live event stream">
    const liveLogRegion = page.getByRole('region', { name: /live event stream/i });
    await expect(liveLogRegion).toBeVisible({ timeout: 8_000 });

    // h3 header inside the panel
    const header = liveLogRegion.locator('h3').filter({ hasText: /live event stream/i });
    await expect(header).toBeVisible({ timeout: 5_000 });

    // Pause/Resume button — aria-label changes based on streaming state.
    // In smoke context (SSE returns empty body), the hook starts streaming,
    // so the button should be labelled "Pause stream".
    // Guard: accept either label to be resilient to hook init timing.
    const pauseOrResume = page
      .getByRole('button', { name: /pause stream/i })
      .or(page.getByRole('button', { name: /resume/i }));
    await expect(pauseOrResume.first()).toBeVisible({ timeout: 5_000 });
  });
});
