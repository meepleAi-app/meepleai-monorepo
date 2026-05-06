/**
 * Visual contract — /sessions route migrata vs mockup baseline.
 *
 * Issue #735 (Wave D.1 Sessions migration)
 * V2 Migration Phase 1 (Wave D.1).
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(authenticated)/sessions/_components/
 *     SessionsLibraryView.tsx`) con hero + filters + results list.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run visual-regression-migrated.yml --ref feature/issue-735-wave-d1-sessions-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il workflow setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` prima del
 *   build, così il fixture deterministico (6 entries: Brass: Birmingham, Wingspan×2,
 *   Azul, Ark Nova, 7 Wonders) viene short-circuitato dal
 *   `SessionsLibraryView` orchestrator senza richiedere il backend API.
 *
 * Strategia ID (visual-test fixture, NOT a backend fetch):
 *   - Il `SessionsLibraryView` orchestrator legge `IS_VISUAL_TEST_BUILD` da
 *     `@/lib/sessions/sessions-visual-test-fixture` e sostituisce
 *     `sessionsQuery.data` con entries deterministiche.
 *   - In production deploy il fixture è dead code (constant-fold) — NON
 *     espone alcun shape pubblico.
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` sono mascherate per evitare flake.
 *
 * Auth bypass:
 *   La route `/sessions` è sotto `(authenticated)/` ma il layout non gate-keepa
 *   server-side; `PLAYWRIGHT_AUTH_BYPASS=true` settato in
 *   `playwright.config.ts:434` (webServer env) garantisce navigazione senza
 *   session cookie. `seedAuthSession` + `mockAuthEndpoints` soddisfano il
 *   `proxy.ts` middleware gate + i flussi React-side `AuthProvider` /
 *   `useSessionCheck` (Wave B.1 lesson learned).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp4-sessions-index';

async function waitForSessionsReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="sessions-library-view"]', { timeout: 30_000 });
  // Wait for hero and filters to confirm the view is fully mounted.
  await page.waitForSelector('[data-slot="sessions-hero"]', { timeout: 10_000 });
  await page.waitForSelector('[data-slot="sessions-filters"]', { timeout: 10_000 });

  await page.evaluate(async () => {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await (document as Document & { fonts: { ready: Promise<void> } }).fonts.ready;
    }
  });

  await page.evaluate(
    () =>
      new Promise<void>(resolve => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      })
  );
}

test.describe('V2 Visual Migrated — /sessions matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Sessions index desktop 1280x720 — default state matches sp4-sessions-index mockup', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/sessions', { waitUntil: 'domcontentloaded' });
    await waitForSessionsReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}-desktop.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('Sessions index mobile 375x812 — default state matches sp4-sessions-index mockup', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto('/sessions', { waitUntil: 'domcontentloaded' });
    await waitForSessionsReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}-mobile.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
