/**
 * Accessibility tests — /library desktop (Wave B.3, Issue #574).
 *
 * Combines:
 *   - axe-core WCAG 2.1 AA scan su default state (LibraryHybridGrid populato)
 *   - axe-core WCAG 2.1 AA scan su filtered-empty state (per coprire
 *     CTA + empty-state markup, sezioni che non sono presenti su default)
 *   - prefers-reduced-motion contract: AC §reduced-motion — verifica che la
 *     route `/library` rispetti l'override globale CSS (`globals.css:388-396`
 *     riduce `transition-duration` a 0.01ms !important sotto `@media
 *     (prefers-reduced-motion: reduce)`). Le card hover transitions su
 *     MeepleCard GridCard (default 350ms) devono collassare a sub-millisecondo.
 *
 * Comprehensive unit-level coverage del 5-state FSM + ?state= override matrix
 * lives in `_components/__tests__/LibraryHubV2.test.tsx` (18 tests).
 * This e2e suite verifies the contract holds against the real prod build.
 *
 * Auth bypass: `(authenticated)` route renders senza session perché
 * `(authenticated)/layout.tsx` non gate-keepa server-side e
 * `PLAYWRIGHT_AUTH_BYPASS=true` è settato dal webServer di playwright.config.ts.
 *
 * Visual-test fixture: i test passano contro Next.js prod build con
 * `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` in modo che `useLibrary` venga
 * short-circuitato dal fixture deterministico (12 entries: hero stats coverage
 * + 3-tab grid baseline).
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

async function gotoLibraryReady(page: Page, search = ''): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
  await page.goto(`/library${search}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
}

test.describe('Library desktop — accessibility @a11y', () => {
  test('axe-core: no WCAG 2.1 AA violations on default state', async ({ page }) => {
    await gotoLibraryReady(page);
    // Default state expects LibraryHybridGrid populato — wait for first card.
    await expect(page.locator('[data-slot="library-grid-card"]').first()).toBeVisible();

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

  test('axe-core: no WCAG 2.1 AA violations on filtered-empty state', async ({ page }) => {
    await gotoLibraryReady(page, '?state=filtered-empty');
    // Filtered-empty state expects EmptyLibrary con clearFilters CTA visible.
    await expect(
      page.locator('[data-slot="library-empty-state"][data-kind="filtered-empty"]')
    ).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (results.violations.length > 0) {
      const summary = results.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations (filtered-empty):\n' + summary);
    }
    expect(results.violations).toEqual([]);
  });

  test('prefers-reduced-motion: card hover transitions collapse to sub-ms', async ({ page }) => {
    // Emulate reduced-motion BEFORE goto so the global CSS rule
    // (globals.css:388-396) applies on first paint. Without this the
    // GridCard `transition-all duration-[350ms]` runs full hover animation.
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await gotoLibraryReady(page);
    await expect(page.locator('[data-slot="library-grid-card"]').first()).toBeVisible();

    // Inspect computed transition-duration on the GridCard element. Under
    // reduced motion the global override forces every element's
    // `transition-duration: 0.01ms !important`. The default GridCard CSS sets
    // 350ms; we expect <= 50ms to confirm the override.
    const transitionDurationMs = await page.evaluate(() => {
      const card = document.querySelector('[data-slot="library-grid-card"]') as HTMLElement | null;
      if (!card) return Number.POSITIVE_INFINITY;
      // GridCard internal element con transition lives below the data-slot
      // wrapper — inspect the deepest interactive child OR the wrapper itself.
      const inspectTarget = (card.querySelector('[class*="transition"]') as HTMLElement) ?? card;
      const cs = window.getComputedStyle(inspectTarget);
      // transition-duration may be a comma-separated list (e.g. "0.01s, 0.01s").
      // Take the max value.
      const durations = cs.transitionDuration.split(',').map(s => {
        const trimmed = s.trim();
        if (trimmed.endsWith('ms')) return parseFloat(trimmed);
        if (trimmed.endsWith('s')) return parseFloat(trimmed) * 1000;
        return parseFloat(trimmed) * 1000; // fallback: assume seconds
      });
      return Math.max(...durations);
    });

    expect(transitionDurationMs).toBeLessThanOrEqual(50);
  });
});
