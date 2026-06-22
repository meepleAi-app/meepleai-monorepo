/**
 * Auth Redirect Safety E2E Tests — Issue #2168
 *
 * Verifies that the `?from=` redirect parameter on /login is sanitised
 * by `isSafeRelativeLink` so open-redirect attack vectors are silently
 * dropped in favour of the safe fallback (/library).
 *
 * Also verifies that the Referrer-Policy: strict-origin header is present
 * on /login responses (Task E middleware).
 *
 * Tests (8 total):
 *   – 6 × unsafe ?from= vectors → fallback to /library
 *   – 1 × valid relative ?from=/sessions → preserved
 *   – 1 × Referrer-Policy header check on /login
 *
 * Credentials: sourced from PLAYWRIGHT_TEST_USER_EMAIL +
 * PLAYWRIGHT_TEST_USER_PASSWORD env vars (CI provides via secrets store;
 * local devs source `infra/secrets/admin.secret` before running).
 * Falls back to placeholder strings so the spec compiles without env
 * configured — tests will fail-fast at login if credentials are missing.
 *
 * @see apps/web/src/lib/url-safety.ts — isSafeRelativeLink helper
 * @see apps/web/src/middleware.ts — Referrer-Policy injection
 */

import { test, expect, type Page } from '@playwright/test';

// ---------------------------------------------------------------------------
// Credentials — sourced from env (CI secrets / local infra/secrets/admin.secret)
// ---------------------------------------------------------------------------
const TEST_USER = {
  email: process.env.PLAYWRIGHT_TEST_USER_EMAIL ?? 'test@meepleai.com',
  password: process.env.PLAYWRIGHT_TEST_USER_PASSWORD ?? '',
};

// ---------------------------------------------------------------------------
// Attack vectors — representative subset of isSafeRelativeLink coverage
// ---------------------------------------------------------------------------
const UNSAFE_FROM_INPUTS = [
  'https://evil.com',
  'http://evil.com/path',
  '//evil.com',
  'javascript:alert(1)',
  'data:text/html,<script>',
  '%2F%2Fevil.com',
] as const;

// ---------------------------------------------------------------------------
// Helper: dismiss cookie-consent banner if present (best-effort, non-fatal)
// ---------------------------------------------------------------------------
async function dismissCookieBanner(page: Page): Promise<void> {
  try {
    await page.getByRole('button', { name: 'Solo essenziali' }).click({ timeout: 1500 });
  } catch {
    // Banner not present — continue normally
  }
}

// ---------------------------------------------------------------------------
// Helper: fill and submit login form, wait for navigation to settle
// ---------------------------------------------------------------------------
async function loginAs(page: Page, email: string, password: string): Promise<void> {
  // Use data-testid on the form to scope selectors precisely
  const form = page.locator('[data-testid="login-form"]');

  // Fill email — label is "Email" (same in it/en locale)
  await form.getByLabel(/^email$/i).fill(email);

  // Fill password — label is "Password"
  await form.getByLabel(/^password$/i).fill(password);

  // Submit — the Btn renders as type="submit"; text is "Accedi" (it) / "Login" (en)
  // We match any submit button inside the form to be locale-independent
  await form.locator('button[type="submit"]').click();
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('Auth redirect safety (#2168)', () => {
  test.beforeEach(async ({ context }) => {
    // Start every test with a clean, unauthenticated state
    await context.clearCookies();
  });

  // -- Unsafe ?from= vectors -------------------------------------------------

  for (const unsafeFrom of UNSAFE_FROM_INPUTS) {
    test(`rejects ?from=${unsafeFrom.slice(0, 40)} and falls back to /library`, async ({
      page,
    }) => {
      await page.goto(`/login?from=${encodeURIComponent(unsafeFrom)}`);

      await dismissCookieBanner(page);

      await loginAs(page, TEST_USER.email, TEST_USER.password);

      // Should land on /library (the safe fallback), NOT on the attacker domain
      await expect(page).toHaveURL(/\/library(\?.*)?$/, { timeout: 5000 });

      // Negative assertions
      expect(page.url()).not.toContain('evil.com');
      expect(page.url()).not.toContain('javascript:');
      expect(page.url()).not.toContain('data:');
    });
  }

  // -- Valid relative path ---------------------------------------------------

  test('preserves valid relative ?from path on successful login', async ({ page }) => {
    await page.goto('/login?from=/sessions');

    await dismissCookieBanner(page);

    await loginAs(page, TEST_USER.email, TEST_USER.password);

    // Should land on /sessions (the allowed relative path)
    await expect(page).toHaveURL(/\/sessions(\?.*)?$/, { timeout: 5000 });
  });

  // -- Referrer-Policy header ------------------------------------------------

  test('Referrer-Policy: strict-origin header present on /login response', async ({ page }) => {
    const response = await page.goto('/login');

    // HTTP/2 header names are lower-cased; Playwright normalises them too
    expect(response?.headers()['referrer-policy']).toBe('strict-origin');
  });
});
