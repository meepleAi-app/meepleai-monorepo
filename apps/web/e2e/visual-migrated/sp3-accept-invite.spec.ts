/**
 * Visual contract — /invites/[token] route migrata vs mockup baseline.
 *
 * Issue #611 (Wave A.5b /invites/[token] frontend) · umbrella #579
 * V2 Migration Phase 1.
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): le baseline PNG non esistono ancora.
 *   - **Green**: route v2 attiva (`(public)/invites/[token]/page-client.tsx`)
 *     con auth-card-shell + hero + InviteHostCard + SessionMetaGrid + accept/
 *     decline CTA bar.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run 266963272 --ref feature/issue-611-invites-token-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il workflow setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` prima del
 *   build, così il fixture statico (`VISUAL_TEST_FIXTURE_TOKEN`) viene servito
 *   da `loadInitialData()` senza richiedere il backend API (`getInvitation`
 *   in CI Next.js prod-only build non può raggiungere `:8080`).
 *
 * Strategia token (visual-test fixture, NOT a backend fetch):
 *   - Importa `VISUAL_TEST_FIXTURE_TOKEN` dal modulo
 *     `@/lib/invites/visual-test-fixture`.
 *   - Naviga a `/invites/<fixture>` rendering il pending state default.
 *   - In production deploy il fixture è dead code (constant-fold) — NON
 *     espone alcun shape pubblico.
 *
 * Cookie banner suppression (Wave A.4 pattern):
 *   `seedCookieConsent` pre-popola localStorage prima del navigate per
 *   evitare il mount di `CookieConsentBanner` (visual-flake source nei
 *   fullPage screenshot mobile).
 */
import { test, expect, type Page } from '@playwright/test';

import { VISUAL_TEST_FIXTURE_TOKEN } from '../../src/lib/invites/visual-test-fixture';

const SLUG = 'sp3-accept-invite';

/**
 * Seed cookie-consent localStorage entry BEFORE the page loads so the
 * GDPR banner (`CookieConsentBanner`) never mounts. Mirror of the
 * helper in `e2e/v2-states/invites-token.spec.ts`. Banner mount/animation
 * is a known flake source on mobile fullPage screenshots: it can occur
 * between Playwright's two consecutive stability captures, blowing the
 * diff threshold even with `animations: 'disabled'`.
 *
 * Contract mirrors `apps/web/src/lib/cookie-consent.ts`:
 *   key     = 'meepleai-cookie-consent'
 *   version = '1.0'
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

async function waitForInviteReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-slot="invites-token-page"]', { timeout: 30_000 });
  await page.waitForSelector('[data-slot="invites-token-card"]', { timeout: 30_000 });

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

test.describe('V2 Visual Migrated — /invites/[token] matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Accept-invite default state matches sp3-accept-invite mockup', async ({ page }) => {
    await seedCookieConsent(page);
    await page.goto(`/invites/${VISUAL_TEST_FIXTURE_TOKEN}`, { waitUntil: 'networkidle' });
    await waitForInviteReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
