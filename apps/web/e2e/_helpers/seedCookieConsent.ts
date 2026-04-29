/**
 * Cookie-consent seeding helper for Playwright specs.
 *
 * **Why this helper exists** (Wave A.4 lesson learned, Issue #603 / PR #605):
 *   `CookieConsentBanner` (lib/cookie-consent.ts) renders at the bottom of every public
 *   page until the user dismisses it. On mobile fullPage screenshots the banner shifts
 *   between Playwright's two consecutive stability captures (separate from the threshold
 *   compare phase) → diff regions blow past the threshold and the visual gate fails.
 *
 * **Contract** (must match `lib/cookie-consent.ts` exactly):
 *   storage key = `'meepleai-cookie-consent'`
 *   schema      = `{ version, essential, analytics, functional, timestamp }`
 *   version     = `'1.0'` (current)
 *
 * **Defaults**: opt-in cookies (analytics, functional) DENIED — mirrors realistic
 * production user state where consent is essential-only. Timestamp is fixed to
 * `2026-01-01T00:00:00.000Z` so the seed is fully deterministic across CI runs
 * (avoids `Date.now()` non-determinism that would invalidate visual baselines).
 *
 * **Usage**:
 *   ```ts
 *   import { seedCookieConsent } from '../_helpers/seedCookieConsent';
 *   test.beforeEach(async ({ page }) => { await seedCookieConsent(page); });
 *   ```
 *
 * Apply via `page.addInitScript()` BEFORE `page.goto()` so `getStoredConsent()`
 * short-circuits the banner mount on first paint.
 *
 * Replaces inline variants previously embedded in:
 *   - e2e/v2-states/invites-token.spec.ts
 *   - e2e/v2-states/shared-game-detail.spec.ts
 *   - e2e/visual-migrated/sp3-accept-invite.spec.ts
 *   - e2e/visual-migrated/sp3-shared-game-detail.spec.ts
 */

import type { Page } from '@playwright/test';

export async function seedCookieConsent(page: Page): Promise<void> {
  await page.addInitScript(() => {
    try {
      window.localStorage.setItem(
        'meepleai-cookie-consent',
        JSON.stringify({
          version: '1.0',
          essential: true,
          analytics: false,
          functional: false,
          timestamp: '2026-01-01T00:00:00.000Z',
        })
      );
    } catch {
      // localStorage unavailable in test context — banner may render, accept the risk.
    }
  });
}
