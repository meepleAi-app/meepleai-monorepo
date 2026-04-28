/**
 * Visual contract — /shared-games/[id] route migrata vs mockup baseline.
 *
 * Issue #603 (Wave A.4 Shared Game Detail migration) · umbrella #579
 * V2 Migration Phase 1.
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(public)/shared-games/[id]/page-client.tsx`)
 *     con hero + tabs (5 tabpanels) + sticky CTA + community contributors.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run 266963272 --ref feature/issue-603-shared-game-detail-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il workflow setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` prima del
 *   build, così il fixture statico (`VISUAL_TEST_FIXTURE_ID`) viene servito
 *   da `loadInitialData()` senza richiedere il backend API.
 *
 * Strategia ID (visual-test fixture, NOT a backend fetch):
 *   - Importa `VISUAL_TEST_FIXTURE_ID` dal modulo
 *     `@/lib/shared-games/visual-test-fixture`.
 *   - Naviga a `/shared-games/<fixture>` con tab default "overview".
 *   - In production deploy il fixture è dead code (constant-fold) — NON
 *     espone alcun shape pubblico.
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` sono mascherate per evitare flake.
 */
import { test, expect, type Page } from '@playwright/test';

import { VISUAL_TEST_FIXTURE_ID } from '../../src/lib/shared-games/visual-test-fixture';

const SLUG = 'sp3-shared-game-detail';

/**
 * Seed cookie-consent localStorage entry BEFORE the page loads so the
 * GDPR banner (`CookieConsentBanner`) never mounts. Mirror of the
 * helper in `e2e/v2-states/shared-game-detail.spec.ts` — the banner is
 * a known visual-flake source on mobile fullPage screenshots.
 */
async function seedCookieConsent(page: Page): Promise<void> {
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
      // localStorage unavailable — banner may render, accept the risk.
    }
  });
}

async function waitForDetailReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-testid="shared-game-detail-page"]', { timeout: 30_000 });

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

test.describe('V2 Visual Migrated — /shared-games/[id] matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Shared game detail default tab matches sp3-shared-game-detail mockup', async ({ page }) => {
    await seedCookieConsent(page);
    await page.goto(`/shared-games/${VISUAL_TEST_FIXTURE_ID}`, { waitUntil: 'networkidle' });
    await waitForDetailReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
