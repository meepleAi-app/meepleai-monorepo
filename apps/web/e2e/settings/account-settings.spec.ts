/**
 * SET-01: Account Settings
 * Issue #3082 - P3 Low
 *
 * Account data lives in the `profile` section of the settings hub: display name
 * and email, read and written by `ProfileSection` through
 * `GET`/`PUT /api/v1/users/profile`.
 *
 * ─── Address (#3938) ────────────────────────────────────────────────────────────
 * This file used to navigate to `/settings/account`. `account` has never been a
 * section id — `SETTINGS_SECTIONS` is profile · security · ai-consent ·
 * notifications · preferences · api-keys · services — and the URL only ever
 * "worked" because nine `next.config.js` redirects mapped every `/settings/*`
 * onto `/profile?tab=settings`. Next resolves config redirects BEFORE filesystem
 * routing, so those 308s shadowed `settings/page.tsx` and
 * `settings/[section]/page.tsx`; they are gone, and `/settings/*` now resolves
 * for real. The tests below address `/settings/profile` and assert the URL
 * STAYS there — the same check `gotoChecked` makes in `e2e/accessibility.spec.ts`
 * (#3917), and a stronger one than the redirect it replaces.
 *
 * ─── DELETED COVERAGE ───────────────────────────────────────────────────────────
 * "should show delete account option" — DELETED, not repointed. There is no
 * delete-account UI anywhere in the app: `ProfileSection` renders a display
 * name, an email and a save button, no other section exposes account deletion,
 * and `grep -rniE "delete.?account" apps/web/src` returns only stories and unit
 * tests. The assertion was ALREADY failing before the redirects were removed —
 * only the page it fails on changed — so "making it pass" would have meant
 * asserting something else entirely. `profile-settings.spec.ts` records the same
 * deletion for the same reason ("the danger-zone section was removed from scope
 * entirely"). The day a danger zone ships, re-add it in its own spec; do not
 * resurrect it against a section that cannot satisfy it.
 *
 * ─── Mock addressing ────────────────────────────────────────────────────────────
 * Routes are host-agnostic regexes rather than `${API_BASE}/...` globs. In the
 * browser `getApiBase()` returns '' and `httpClient` issues RELATIVE urls, which
 * reach the Next server on :3000 — a handler anchored to `http://localhost:8080`
 * never matches them, so it mocks nothing while looking like it does. Same
 * rationale, and same regex shape, as `mockAuthEndpoints` in
 * `e2e/_helpers/seedAuthSession.ts`. The regexes still match the absolute :8080
 * form, so nothing is lost.
 */

import { seedAuthSession } from '../_helpers/seedAuthSession';
import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

// ── Mock helpers ───────────────────────────────────────────────────────────────

interface AccountUser {
  id: string;
  email: string;
  displayName: string;
}

const TEST_USER: AccountUser = {
  // `AuthUserSchema.id` and `UserProfileSchema.id` are `z.string().uuid()`: a
  // non-uuid id fails validation in httpClient and the payload is discarded.
  id: '00000000-0000-4000-8000-0000000a0001',
  email: 'test@example.com',
  displayName: 'Test User',
};

/** Full `UserProfileSchema` payload — a partial one is rejected before it reaches the UI. */
function profileDto(user: AccountUser) {
  return {
    id: user.id,
    email: user.email,
    displayName: user.displayName,
    role: 'User',
    createdAt: new Date('2026-01-01T00:00:00Z').toISOString(),
    isTwoFactorEnabled: false,
    twoFactorEnabledAt: null,
    language: 'it',
    theme: 'system',
    emailNotifications: true,
    dataRetentionDays: 365,
    avatarUrl: null,
  };
}

async function setupAccountSettingsMocks(page: Page): Promise<{ getUser: () => AccountUser }> {
  // Cookies FIRST: `/settings` is in PROTECTED_ROUTES, so without a session
  // cookie proxy.ts answers every navigation below with a redirect to /login and
  // the assertions would measure the login page instead (#633).
  await seedAuthSession(page);

  const user: AccountUser = { ...TEST_USER };

  // Catch-all FIRST — page routes are matched in reverse registration order, so
  // registering it here means it is consulted LAST, after the specific handlers
  // below. Its job is to stop an unmocked v1 call from stalling `networkidle`.
  await page.route(/\/api\/v1\//, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: route.request().method() === 'GET' ? '[]' : JSON.stringify({ ok: true }),
    });
  });

  // Authenticated identity — AuthProvider mounts and calls this.
  await page.route(/\/api\/v1\/auth\/me(\?.*)?$/, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { ...user, role: 'User', onboardingCompleted: true, emailVerified: true },
      }),
    });
  });

  // Session polling (`useSessionCheck`) — a far-future expiry so nothing auto-logs-out.
  await page.route(/\/api\/v1\/auth\/session\/status(\?.*)?$/, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        expiresAt: '2099-12-31T23:59:59Z',
        lastSeenAt: new Date().toISOString(),
        remainingMinutes: 60,
      }),
    });
  });

  // 2FA status — `SettingsTab` queries it to badge the Security row in the sub-nav.
  await page.route(/\/api\/v1\/users\/me\/2fa\/status(\?.*)?$/, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ isEnabled: false, enabledAt: null, unusedBackupCodesCount: 0 }),
    });
  });

  // The account form itself. Note the path: `authClient.getProfile`/`updateProfile`
  // hit `/api/v1/users/profile` with GET/PUT — NOT `/api/v1/users/me` with
  // PATCH/DELETE, which is what this file used to mock and no caller ever sent.
  await page.route(/\/api\/v1\/users\/profile(\?.*)?$/, async route => {
    if (route.request().method() === 'PUT') {
      const body = (await route.request().postDataJSON()) as Partial<AccountUser> | null;
      if (typeof body?.displayName === 'string') user.displayName = body.displayName;
      if (typeof body?.email === 'string') user.email = body.email;
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ok: true, message: 'Profile updated' }),
      });
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(profileDto(user)),
    });
  });

  return { getUser: () => ({ ...user }) };
}

// ── Navigation helper ──────────────────────────────────────────────────────────

/**
 * Navigate and prove the hub answered at the address we asked for.
 *
 * Status first: a 404 keeps the pathname, so a pathname-only check would go
 * green the day the route disappears again. Pathname second: `/settings/*` must
 * NOT redirect, and a returning 308 onto `/profile?tab=settings` would be
 * invisible to an assertion that only looks at the rendered DOM.
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

/**
 * The sub-nav row of the active section. `SettingsSubNav` marks it with
 * `aria-current="page"` on a `SettingsRow` button — NOT with `role="tab"`, which
 * is what `/profile` used before #3938 reduced it to three tabs.
 */
function activeSectionRow(page: Page) {
  return page
    .getByRole('navigation', { name: 'Settings sections' })
    .locator('[aria-current="page"]');
}

// ── Tests ──────────────────────────────────────────────────────────────────────

test.describe('SET-01: Account Settings', () => {
  test('should display current account info', async ({ page }) => {
    await setupAccountSettingsMocks(page);
    await gotoSettings(page, '/settings/profile');

    // Landing is not enough: an unknown section degrades onto DEFAULT_SECTION
    // (see the `/settings/account` test below), so pin which section is active.
    await expect(activeSectionRow(page)).toContainText('Profile');

    // The account data is in form fields, not free text — assert the values,
    // which is also what proves the profile GET was consumed rather than errored.
    await expect(page.getByLabel(/display name/i)).toHaveValue('Test User');
    await expect(page.getByLabel(/^email$/i)).toHaveValue('test@example.com');
  });

  test('should update display name', async ({ page }) => {
    const mocks = await setupAccountSettingsMocks(page);
    await gotoSettings(page, '/settings/profile');

    const nameInput = page.getByLabel(/display name/i);
    // Wait for the hydration effect before typing: filling an empty field and
    // saving would submit an empty email along with the new name.
    await expect(nameInput).toHaveValue('Test User');
    await nameInput.clear();
    await nameInput.fill('New Name');

    await page.getByTestId('save-profile-button').click();

    // Scoped to the profile card: `ProfileSection` reuses `role="alert"` for its
    // load-error state, and the shell can raise a critical StatusBanner with the
    // same role, so an unscoped query would be ambiguous rather than wrong.
    const profileCard = page.locator('section:has([data-testid="save-profile-button"])');
    await expect(profileCard.getByRole('alert')).toContainText(/profile updated/i, {
      timeout: 5000,
    });

    // The banner alone would also show for a UI that faked success. Assert the
    // PUT actually reached the server double, and that it carried the untouched
    // email too — `updateProfile` sends both fields, so a broken hydration would
    // silently blank the address here.
    expect(mocks.getUser()).toMatchObject({
      displayName: 'New Name',
      email: 'test@example.com',
    });
  });

  test('legacy /settings/account resolves onto the default section', async ({ page }) => {
    // `account` is not a section id. `settings/[section]/page.tsx` deliberately
    // degrades an unknown section onto DEFAULT_SECTION instead of 404-ing,
    // because the backend emits section links that have no section —
    // `NotificationRoutes.SettingsSubscription` is `/settings/subscription`.
    // This test pins that decision: the request must resolve (no 404), keep its
    // URL (no redirect), and render the profile section.
    //
    // It is also the reason a typo'd section can never fail loudly, which is why
    // the other tests in this file address `/settings/profile` explicitly.
    await setupAccountSettingsMocks(page);
    await gotoSettings(page, '/settings/account');

    await expect(activeSectionRow(page)).toContainText('Profile');
    await expect(page.getByTestId('save-profile-button')).toBeVisible();
  });
});
