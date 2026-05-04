/**
 * Accessibility tests — /games/[id] desktop (Wave C.1, Issue #581).
 *
 * Combines:
 *   - axe-core WCAG 2.1 AA scan su default state (game detail populato via
 *     visual-test fixture — hero + tabs + info panel)
 *   - axe-core WCAG 2.1 AA scan su not-found state (per coprire CTA + empty
 *     surface markup, sezioni non presenti su default)
 *   - prefers-reduced-motion contract: verifica che la route `/games/[id]`
 *     rispetti l'override globale CSS (`globals.css` riduce `transition-duration`
 *     a 0.01ms !important sotto `@media (prefers-reduced-motion: reduce)`).
 *     La tabs animated underline (GameDetailTabsAnimated `motion-safe:transition-all
 *     motion-safe:duration-300`) deve collassare a sub-millisecondo.
 *
 * Comprehensive unit-level coverage del 5-state FSM + ?state= override matrix
 * lives in `_components/__tests__/GameDetailViewV2.test.tsx` (18 tests).
 * This e2e suite verifies the contract holds against the real prod build.
 *
 * Auth bypass: `(authenticated)` route renders senza session perché
 * `(authenticated)/layout.tsx` non gate-keepa server-side e
 * `PLAYWRIGHT_AUTH_BYPASS=true` è settato dal webServer di playwright.config.ts.
 *
 * Visual-test fixture: i test passano contro Next.js prod build con
 * `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` in modo che `useLibraryGameDetail`
 * venga short-circuitato dal fixture deterministico (Wingspan-shaped game data).
 *
 * EntityBadge / StatusBadge color-contrast: se axe trova violazioni dai
 * MeepleCard shared components, aggiungere `.exclude('[data-slot="..."]')` chain
 * con rationale comment e estendere Issue #636 (Wave B.1/B.2 audit-driven pattern).
 * I componenti game-detail v2 sono pure components NON basati su MeepleCard —
 * le esclusioni non sono preemptively aggiunte qui per non mascherare nuove violazioni.
 *
 * Cherry-picked from PR #697 commit 30d48a26e — data-slot selectors updated to
 * match Task 2/3 committed components. Reduced-motion test simplified to inspect
 * the game-detail-tab-indicator element directly (committed data-slot in
 * GameDetailTabsAnimated.tsx line 182).
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
  // Wait for the default shell root — fixture short-circuits the API call
  await page.waitForSelector('[data-slot="game-detail-view"]', { timeout: 30_000 });
}

test.describe('Game detail — accessibility @a11y', () => {
  test('axe-core: no WCAG 2.1 AA violations on default state', async ({ page }) => {
    await gotoGameDetailReady(page);
    // Default state: hero + tablist visible from visual-test fixture
    await expect(page.locator('[data-slot="game-detail-hero"]')).toBeVisible();
    await expect(page.locator('[data-slot="game-detail-tabs"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      // Add .exclude() here ONLY if shared-component debt surfaces during CI
      // (Wave B.1/B.2 audit-driven pattern — do NOT preemptively exclude).
      // If axe finds MeepleCard-family violations, file/extend Issue #636 first,
      // then add data-slot on the component and exclude here.
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
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto(`/games/${GAME_ID}?state=not-found`, { waitUntil: 'domcontentloaded' });
    // Not-found shell: CTA + back-to-games link
    await page.waitForSelector('[data-slot="game-detail-not-found"]', { timeout: 30_000 });
    await expect(page.locator('[data-slot="game-detail-not-found-cta"]')).toBeVisible();

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
      // Emulate BEFORE goto so global CSS rule applies on first paint.
      // globals.css reduces all transition-duration to 0.01ms !important under
      // `@media (prefers-reduced-motion: reduce)`. GameDetailTabsAnimated uses
      // `motion-safe:transition-all motion-safe:duration-300` on the indicator
      // span — this collapses to instant under reduced-motion.
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await gotoGameDetailReady(page);
      await expect(page.locator('[data-slot="game-detail-tabs"]')).toBeVisible();

      // GameDetailTabsAnimated indicator (data-slot="game-detail-tab-indicator",
      // line 182 of GameDetailTabsAnimated.tsx). Under reduced-motion the
      // `motion-safe:transition-all motion-safe:duration-300` class is inert, and
      // the global CSS override forces `transition-duration: 0.01ms !important`.
      const indicator = page.locator('[data-slot="game-detail-tab-indicator"]');
      await expect(indicator).toBeAttached({ timeout: 10_000 });

      const durationMs = await indicator.evaluate(el => {
        const cs = window.getComputedStyle(el);
        // transition-duration may be a comma-separated list (e.g. "0.01s, 0.01s").
        // Take the max value across all properties.
        const durations = cs.transitionDuration.split(',').map(s => {
          const trimmed = s.trim();
          if (trimmed.endsWith('ms')) return parseFloat(trimmed);
          if (trimmed.endsWith('s')) return parseFloat(trimmed) * 1000;
          return parseFloat(trimmed) * 1000; // fallback: assume seconds
        });
        return Math.max(...durations);
      });

      // Expect sub-50ms: either 0s (motion-reduce Tailwind utility) or
      // 0.01ms (globals.css !important override). 50ms gives generous CI margin.
      expect(durationMs).toBeLessThan(50);
    });
  });
});
