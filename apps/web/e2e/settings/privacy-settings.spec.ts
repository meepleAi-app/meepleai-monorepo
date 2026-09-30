/**
 * SET-03: Privacy Settings — `/settings/privacy`
 * Issue #3082 - P3 Low · rivisto per #3938 (hub impostazioni raggiungibile)
 *
 * What this file used to claim to test, and why it no longer does:
 *
 * - "profile visibility" / "activity visibility": no such control exists in the
 *   web app, and there is no `/api/v1/users/me/privacy` endpoint in the API
 *   either. The old test guarded its whole body behind
 *   `if (await toggle.isVisible())`, so it never asserted anything — it was
 *   green because the body never ran.
 * - "data export": `GET /api/v1/users/me/export` (GDPR Art. 20) exists in the
 *   API, but nothing in the web app calls it — there is no button to find.
 *   Expecting one is a red test for an unbuilt feature, not a regression, so it
 *   is dropped until the UI lands.
 * - the mocks were inert too: in the browser the app calls the Next.js proxy on
 *   its own origin (`http://localhost:3000/api/v1/...`), never the `:8080`
 *   `API_BASE` those `page.route()` patterns were built from — so they bound no
 *   request at all. Origin-agnostic regexes below, per `_helpers/seedAuthSession.ts`.
 * - and none of it reached the hub anyway: `/settings` is in `PROTECTED_ROUTES`,
 *   so `proxy.ts` bounced a cookie-less page to `/login?from=...` before any
 *   React code ran. That is how the loose assertions stayed green — the login
 *   footer carries a "Privacy Policy" link, which `getByText(/privacy/i)`
 *   happily matched.
 *
 * What is worth asserting at this address, and was covered nowhere:
 *
 * `privacy` is not one of the seven ids in `settings-sections.ts`. Until #3938
 * nine `/settings*` redirects in `next.config.js` swallowed this URL with a 308
 * onto `/profile?tab=settings` — an address that, after #3938 cut `/profile`
 * down to three tabs, no longer has a settings tab at all. Now the route
 * resolves: `settings/[section]/page.tsx` degrades an unknown id to
 * `DEFAULT_SECTION` on purpose (the backend emits `/settings/subscription`,
 * which has no section either) and keeps the address the user asked for.
 *
 * The privacy/GDPR controls the product really has live in the `ai-consent`
 * section, so the last test checks that someone who typed `/settings/privacy`
 * can still reach them from the hub sub-nav.
 */

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';
import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

/** `SettingsList` ariaLabel rendered by `SettingsSubNav`. */
const SUB_NAV_LABEL = 'Settings sections';

/**
 * Seed the session the edge gate needs, then mock the two requests the hub
 * actually issues. Origin-agnostic regexes: the browser talks to the Next proxy
 * on :3000, not to the API on :8080.
 */
async function setupSettingsHub(page: Page) {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);

  // SettingsTab → api.auth.getTwoFactorStatus(). The payload must satisfy
  // TwoFactorStatusDtoSchema: a schema-invalid 200 throws in httpClient and
  // retries through TanStack backoff instead of settling.
  await page.route(/\/api\/v1\/users\/me\/2fa\/status(\?.*)?$/, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ isEnabled: false, enabledAt: null, unusedBackupCodesCount: 0 }),
    });
  });

  // AiConsentSection → raw fetch('/api/v1/users/me/ai-consent'); without it the
  // section renders its "Failed to load" fallback instead of the GDPR banner.
  await page.route(/\/api\/v1\/users\/me\/ai-consent(\?.*)?$/, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        userId: 'test-user',
        consentedToAiProcessing: true,
        consentedToExternalProviders: false,
        consentedAt: new Date().toISOString(),
        consentVersion: '1.0.0',
      }),
    });
  });
}

/**
 * Navigate and prove the hub answered at the address we asked for.
 *
 * Status first: a 404 keeps the pathname, so a pathname-only check would go
 * green the day the route disappears again. Pathname second: this is the exact
 * inverse of the 308 that used to live in `next.config.js`, and a returning
 * redirect onto `/profile?tab=settings` would be invisible to an assertion that
 * only looks at the rendered DOM.
 */
async function gotoSettings(page: Page, path: string): Promise<void> {
  const response = await page.goto(path);

  expect(response, `navigation to ${path} produced no response`).not.toBeNull();
  expect(
    response!.status(),
    `${path} returned HTTP ${response!.status()} — the route does not exist`
  ).toBeLessThan(400);

  await page.waitForLoadState('networkidle');

  expect(
    new URL(page.url()).pathname,
    `${path} redirected away — the address the user asked for must be the one they keep`
  ).toBe(path);
}

test.describe('SET-03: Privacy Settings', () => {
  test('should resolve /settings/privacy in place, with no redirect', async ({ page }) => {
    await setupSettingsHub(page);
    await gotoSettings(page, '/settings/privacy');
  });

  test('should degrade the unknown section to the default one', async ({ page }) => {
    await setupSettingsHub(page);
    await gotoSettings(page, '/settings/privacy');

    // Not an error page — the hub itself renders...
    await expect(page.getByRole('heading', { name: 'Impostazioni', level: 1 })).toBeVisible({
      timeout: 5000,
    });

    // ...on DEFAULT_SECTION (`profile`). `SettingsSubNav` marks the active
    // section with aria-current="page" on a `SettingsRow` button — NOT with
    // role="tab", which is what `/profile` used before #3938.
    const subNav = page.getByRole('navigation', { name: SUB_NAV_LABEL });
    await expect(subNav.locator('[aria-current="page"]')).toContainText('Profile');
  });

  test('should reach the AI & data consent section, where the privacy controls live', async ({
    page,
  }) => {
    await setupSettingsHub(page);
    await gotoSettings(page, '/settings/privacy');

    const subNav = page.getByRole('navigation', { name: SUB_NAV_LABEL });
    await subNav.getByRole('button', { name: /AI & data consent/i }).click();

    await expect(page).toHaveURL(/\/settings\/ai-consent$/);
    await expect(page.getByText(/your privacy matters/i)).toBeVisible({ timeout: 5000 });
  });
});
