/**
 * SMOKE — #2787 G6a Catan flavor tab in the session-live shell.
 *
 * ## What this covers here vs elsewhere
 * The Catan flavor tab is gated on `liveSessionDto` (the real `LiveSessionDto`
 * from `useLiveSession`), because `CatanLiveFlavor` needs the rich player fields
 * (color / totalScore / currentRank / isActive) + `gameSlug` + `scoringConfig`
 * that the minimal `LiveSessionFixture` (which drives `?fixture=host`) does NOT
 * carry. So:
 *
 *   - The POSITIVE case (flavor tab present + renders CatanLiveFlavor for a
 *     `gameSlug=catan` session) is fully covered by UNIT tests that mock
 *     `useLiveSession` with a real Catan `LiveSessionDto`:
 *       · SessionLiveView.test.tsx  → "Catan flavor tab (#2787)"
 *       · FlavorRenderer.test.tsx / CatanLiveFlavor.test.tsx (+ jest-axe AA)
 *     It cannot be reproduced under `?fixture=host` (no real backend session),
 *     the same known-gap the SSE smoke documents (see session-live.smoke.spec.ts).
 *
 *   - This spec adds a browser-level REGRESSION GUARD: the default fixture
 *     session (Wingspan, no Catan flavor) must NOT spuriously show a Catan tab.
 *
 * ## Auth pattern
 * Triple-helper (seedAuthSession + seedCookieConsent + mockAuthEndpoints) —
 * identical to session-live.smoke.spec.ts.
 */

import { expect, test, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

// SessionLiveView applies data-theme="dark".
test.use({ colorScheme: 'dark' });

/** Sentinel session ID matching VISUAL_TEST_FIXTURE_SESSION.id (Wingspan fixture). */
const FIXTURE_SESSION_ID = '00000000-0000-4000-8000-000000000d20' as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function gotoFixtureLivePage(page: Page, extraSearch = ''): Promise<void> {
  await seedAuth(page);
  await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=host${extraSearch}`, {
    waitUntil: 'domcontentloaded',
  });
  await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
    timeout: 30_000,
  });
}

test.describe('#2787 G6a — Catan flavor tab', () => {
  test('regression guard: default (Wingspan) fixture session shows NO Catan flavor tab', async ({
    page,
  }) => {
    await gotoFixtureLivePage(page);
    // The desktop RightColumnTabs must render (score tab always present)…
    await expect(page.getByRole('tab', { name: 'Score' }).first()).toBeVisible();
    // …but the game-conditional Catan flavor tab must be absent for a non-catan session.
    await expect(page.getByRole('tab', { name: 'Catan' })).toHaveCount(0);
  });

  test.fixme('positive: catan session shows the flavor tab and renders CatanLiveFlavor on click', async ({
    page,
  }) => {
    // KNOWN GAP (#2787): the flavor tab is driven by the real `LiveSessionDto`
    // (gameSlug=catan + rich players), which the `?fixture=host` harness does
    // not provide (it uses the minimal LiveSessionFixture). Reproducing this in
    // the browser needs a real backend session OR a rich-DTO fixture harness
    // (see e2e/smoke-real-backend/). The behaviour is fully unit-covered.
    test.info().annotations.push({
      type: 'known-gap',
      description:
        'flavor tab requires a real LiveSessionDto (gameSlug=catan); not renderable under ?fixture=host. Unit-covered in SessionLiveView/FlavorRenderer/CatanLiveFlavor tests; browser path deferred to smoke-real-backend/.',
    });
    await gotoFixtureLivePage(page);
    await page.getByRole('tab', { name: 'Catan' }).click();
    await expect(page.locator('[data-slot="catan-flavor-live"]')).toBeVisible();
  });
});
