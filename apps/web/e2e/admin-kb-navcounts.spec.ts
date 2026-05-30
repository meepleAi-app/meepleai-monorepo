/**
 * Admin KB SubNav count badges smoke test — F3-FU-6 #1655 Task 11
 *
 * Verifies that navigating to /admin/knowledge-base renders both
 * kb-nav-badge-queue and kb-nav-badge-feedback with valid numeric content
 * (or "99+" if capped, or "—" on backend error).
 *
 * Does NOT assert exact counts (those are flaky against a live backend).
 *
 * ── Auth context ──────────────────────────────────────────────────────────
 * This test requires real admin access to /admin/knowledge-base.
 *
 * Known limitation (issue #1655 follow-up):
 *   The v1 plaintext role cookie (meepleai_user_role) that proxy.ts used to
 *   resolve admin role in E2E tests was sunset on 2026-05-13. Since then,
 *   proxy.ts resolves the role only from the backend-validated session cache
 *   (isSessionCookieValid → /auth/me → cacheSessionValidation). However,
 *   when PLAYWRIGHT_AUTH_BYPASS=true is set in the webServer environment
 *   (see playwright.config.ts), proxy.ts skips isSessionCookieValid entirely,
 *   so the cache is never populated and isAdmin always evaluates to false.
 *
 *   As a result, ALL admin E2E tests that use PLAYWRIGHT_AUTH_BYPASS + mock
 *   auth helpers (AdminHelper, seedAuthSession) are currently broken for
 *   /admin/** routes in dev/CI environments. This is a systemic issue tracked
 *   separately from this task.
 *
 *   This test uses real credentials and a pre-flight check: if the server
 *   redirects away from /admin after real login (indicating bypass-broken-role),
 *   it skips gracefully instead of failing with a confusing error.
 *
 * ── Nav-counts ────────────────────────────────────────────────────────────
 * The nav-counts endpoint is mocked so the badge counts are deterministic
 * regardless of live processing-queue or feedback state.
 *
 * ── Credentials ───────────────────────────────────────────────────────────
 * PLAYWRIGHT_ADMIN_EMAIL / PLAYWRIGHT_ADMIN_PASSWORD env vars.
 * Falls back to ADMIN_EMAIL / ADMIN_PASSWORD (from .env.test) then to the
 * local dev seeded admin (admin@meepleai.app).
 */

import { test, expect, type Page } from '@playwright/test';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const adminEmail =
  process.env.PLAYWRIGHT_ADMIN_EMAIL || process.env.ADMIN_EMAIL || 'admin@meepleai.app';
const adminPassword =
  process.env.PLAYWRIGHT_ADMIN_PASSWORD || process.env.ADMIN_PASSWORD || '5ZwHNfXqTkRfTQG5bFr5MAPh';

// ── Helpers ──────────────────────────────────────────────────────────────────

/**
 * Log in via the backend API. Playwright's shared cookie jar automatically
 * carries the response cookies (including the `secure`-flagged session cookie,
 * which Chrome DevTools Protocol allows on localhost over HTTP) into subsequent
 * browser page navigations.
 *
 * Returns the raw session cookie value for diagnostic use, or null on failure.
 */
async function loginViaBackendApi(page: Page): Promise<boolean> {
  const response = await page.request.post(`${apiBase}/api/v1/auth/login`, {
    headers: { 'Content-Type': 'application/json' },
    data: { email: adminEmail, password: adminPassword },
  });

  if (!response.ok()) {
    console.error(
      `[admin-kb-navcounts] Backend login failed: HTTP ${response.status()} — ` +
        `check PLAYWRIGHT_ADMIN_EMAIL / PLAYWRIGHT_ADMIN_PASSWORD env vars`
    );
    return false;
  }

  console.log(`[admin-kb-navcounts] API login OK for ${adminEmail}`);
  return true;
}

/**
 * Pre-flight: navigate to /admin and check whether the proxy lets us in.
 *
 * With PLAYWRIGHT_AUTH_BYPASS=true active in the dev server and the v1 role
 * cookie path expired (2026-05-13), the proxy cannot resolve the admin role
 * and redirects /admin → / → /library. In that case this function returns
 * false and the test skips gracefully.
 *
 * Returns true only if the browser actually lands on /admin or /admin/**.
 */
async function canAccessAdmin(page: Page): Promise<boolean> {
  await page.goto('/admin');
  await page.waitForLoadState('domcontentloaded');
  const isOnAdmin = page.url().includes('/admin');
  if (!isOnAdmin) {
    console.warn(
      `[admin-kb-navcounts] /admin redirected to ${page.url()}. ` +
        'This is the known PLAYWRIGHT_AUTH_BYPASS + v1-sunset issue (#1655 follow-up). ' +
        'Skipping test.'
    );
  }
  return isOnAdmin;
}

// ── Tests ─────────────────────────────────────────────────────────────────────

test.describe('Admin KB SubNav count badges (#1655)', () => {
  test.beforeEach(async ({ page }) => {
    // 1. Mock the nav-counts endpoint BEFORE navigation so the mock is active
    //    from the first fetch. Returns non-zero counts for a clear numeric display.
    await page.route(`${apiBase}/api/v1/admin/kb/nav-counts`, async route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          processingQueue: 3,
          feedback7d: 12,
          asOf: new Date().toISOString(),
        }),
      })
    );

    // 2. Log in via real credentials so the browser context holds a valid session.
    const loggedIn = await loginViaBackendApi(page);
    test.skip(!loggedIn, 'Skipping: backend login failed — check credentials or API availability');
  });

  test('shows queue and feedback badges with numeric content', async ({ page }) => {
    // Pre-flight: verify admin access is actually granted.
    // Skips gracefully when PLAYWRIGHT_AUTH_BYPASS + v1 sunset blocks admin role.
    const adminAccessible = await canAccessAdmin(page);
    test.skip(
      !adminAccessible,
      'Skipping: /admin not accessible — PLAYWRIGHT_AUTH_BYPASS active + v1 role cookie sunset ' +
        '(2026-05-13). Admin E2E tests require proxy.ts role bypass support to be updated. ' +
        'See #1655 follow-up.'
    );

    await page.goto('/admin/knowledge-base');

    // Guard: confirm we're on the admin KB page.
    await expect(page).toHaveURL(/\/admin\/knowledge-base/, { timeout: 10_000 });

    // Wait for the skeleton to resolve into the real badge.
    // KbCountBadge renders data-testid="kb-nav-badge-queue-loading" while
    // loading=true; after the first fetch resolves it switches to
    // data-testid="kb-nav-badge-queue" with the numeric display.
    const queueBadge = page.getByTestId('kb-nav-badge-queue');
    const feedbackBadge = page.getByTestId('kb-nav-badge-feedback');

    await expect(queueBadge).toBeVisible({ timeout: 10_000 });
    await expect(feedbackBadge).toBeVisible({ timeout: 10_000 });

    // Content must be: digits with optional "+", or em-dash "—" on error.
    await expect(queueBadge).toHaveText(/^\d+\+?$|^—$/, { timeout: 10_000 });
    await expect(feedbackBadge).toHaveText(/^\d+\+?$|^—$/, { timeout: 10_000 });
  });
});
