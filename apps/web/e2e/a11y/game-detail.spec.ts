/**
 * Accessibility tests — /games/[id] desktop (Wave C.1, Issue #581).
 *
 * Combines:
 *   - axe-core WCAG 2.1 AA scan su default state (game detail populato)
 *   - axe-core WCAG 2.1 AA scan su not-found state (per coprire CTA + empty
 *     surface markup)
 *   - prefers-reduced-motion contract: AC §reduced-motion — verifica che la
 *     route `/games/[id]` rispetti l'override globale CSS (`globals.css:388-396`
 *     riduce `transition-duration` a 0.01ms !important sotto `@media
 *     (prefers-reduced-motion: reduce)`). Le tabs animated underline transitions
 *     (default 300ms) devono collassare a sub-millisecondo.
 *
 * Comprehensive unit-level coverage del 5-state FSM + ?state= override matrix
 * lives in `_components/__tests__/GameDetailViewV2.test.tsx`. This e2e suite
 * verifies the contract holds against the real prod build.
 *
 * Auth bypass: `(authenticated)` route renders senza session perché
 * `(authenticated)/layout.tsx` non gate-keepa server-side e
 * `PLAYWRIGHT_AUTH_BYPASS=true` è settato dal webServer di playwright.config.ts.
 *
 * Visual-test fixture: i test passano contro Next.js prod build con
 * `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` in modo che `useLibraryGameDetail`
 * venga short-circuitato dal fixture deterministico (Wingspan-shaped data).
 *
 * EntityBadge / StatusBadge color-contrast: WCAG `color-contrast` rule è
 * exclusa via `data-slot` selectors per il MeepleCard family debt tracked in
 * Issue #636 (cross-Wave concern). I componenti game-detail v2 non utilizzano
 * direttamente quelle slot ma il pattern di esclusione resta consistente.
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];
const GAME_ID = '00000000-0000-4000-8000-000000000581';

async function gotoGameDetailReady(page: Page, search = ''): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
  await page.goto(`/games/${GAME_ID}${search}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="game-detail-view"]', { timeout: 30_000 });
}

test.describe('Game detail — accessibility @a11y', () => {
  test('axe-core: no WCAG 2.1 AA violations on default state', async ({ page }) => {
    await gotoGameDetailReady(page);
    // Default state expects hero + tabs visible.
    await expect(page.locator('[data-slot="game-detail-hero"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (results.violations.length > 0) {
      const summary = results.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations (default):\n' + summary);
    }
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations on not-found state', async ({ page }) => {
    await gotoGameDetailReady(page, '?state=not-found');
    await expect(
      page.locator('[data-slot="game-detail-view"][data-state="not-found"]')
    ).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (results.violations.length > 0) {
      const summary = results.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations (not-found):\n' + summary);
    }
    expect(results.violations).toEqual([]);
  });

  test.describe('prefers-reduced-motion contract', () => {
    test.use({ colorScheme: 'no-preference' });

    test('tabs underline animation collapses under reduced-motion', async ({ page }) => {
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await gotoGameDetailReady(page);
      const indicator = page.locator('[data-slot="game-detail-tab-indicator"]');
      await expect(indicator).toBeVisible();

      // Verify the rule has been applied: under reduced-motion the transition
      // duration is collapsed by globals.css to ~0.01ms.
      const duration = await indicator.evaluate(el => {
        return window.getComputedStyle(el).transitionDuration;
      });
      // Expect a value that is either '0s' (motion-reduce: tailwind utility) or
      // very small (globals.css override). Accept any value below 100ms.
      const ms = duration.endsWith('ms')
        ? parseFloat(duration)
        : duration.endsWith('s')
          ? parseFloat(duration) * 1000
          : NaN;
      expect(Number.isFinite(ms)).toBe(true);
      expect(ms).toBeLessThan(100);
    });
  });
});
