/**
 * Accessibility tests — /players (Wave 4 D1, Issue #682).
 *
 * Combines:
 *   - axe-core WCAG 2.1 AA scan su default state (results grid popolato)
 *   - axe-core WCAG 2.1 AA scan su filtered-empty state (per coprire
 *     CTA + empty-state markup, sezioni che non sono presenti su default)
 *   - prefers-reduced-motion contract: AC-8 spec §5 — verifica che la route
 *     `/players` rispetti l'override globale CSS (`globals.css:388-396` riduce
 *     `transition-duration` a 0.01ms !important sotto `@media
 *     (prefers-reduced-motion: reduce)`). Le card hover transitions su
 *     MeepleCard GridCard (default 350ms) devono collassare a sub-millisecondo
 *     durations.
 *
 * Players usa `<button data-slot="players-results-grid-item">` come wrapper
 * delle MeepleCard (non un `<a>` link). La reduced-motion evaluation punta
 * al `firstElementChild` del button (la MeepleCard root) — mirror del pattern
 * agents-index.spec.ts (Link → firstElementChild).
 *
 * Comprehensive unit-level coverage del 5-state FSM + ?state= override matrix
 * lives in `_components/__tests__/PlayersLibraryView.test.tsx`.
 * This e2e suite verifies the contract holds against the real prod build.
 *
 * Auth bypass: `(authenticated)` route renders senza session perché
 * `(authenticated)/layout.tsx` non gate-keepa server-side e
 * `PLAYWRIGHT_AUTH_BYPASS=true` è settato dal webServer di playwright.config.ts.
 *
 * Visual-test fixture: i test passano contro Next.js prod build con
 * `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` in modo che `usePlayerStatistics`
 * venga short-circuitato dal fixture deterministico (5 entries: Wingspan, Azul,
 * Catan, Terraforming Mars, Splendor).
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;

async function gotoPlayersReady(page: Page, search = ''): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
  await page.goto(`/players${search}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="players-library-view"]', { timeout: 30_000 });
}

test.describe('Players index — accessibility @a11y', () => {
  test('axe-core: no WCAG 2.1 AA violations on default state', async ({ page }) => {
    await gotoPlayersReady(page);
    // Default state expects results grid popolato — wait for first grid item.
    await expect(page.locator('[data-slot="players-results-grid-item"]').first()).toBeVisible();

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
    await gotoPlayersReady(page, '?state=filtered-empty');
    // Filtered-empty state expects EmptyPlayers con clearFilters CTA visible.
    await expect(
      page.locator('[data-slot="players-empty"][data-kind="filtered-empty"]')
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
    await gotoPlayersReady(page);
    await expect(page.locator('[data-slot="players-results-grid-item"]').first()).toBeVisible();

    // Inspect computed transition-duration on the MeepleCard element (first child
    // of the button wrapper). Under reduced motion the global override forces
    // every element's `transition-duration: 0.01ms !important`. The default
    // GridCard CSS sets 350ms; we expect <= 50ms to confirm the override.
    // Players uses <button data-slot="players-results-grid-item"> as the wrapper
    // (not a Link), so firstElementChild is the MeepleCard root div.
    const transitionDurationMs = await page.evaluate(() => {
      const button = document.querySelector(
        '[data-slot="players-results-grid-item"]'
      ) as HTMLElement | null;
      if (!button) return Number.POSITIVE_INFINITY;
      const card = button.firstElementChild as HTMLElement | null;
      if (!card) return Number.POSITIVE_INFINITY;
      const cs = window.getComputedStyle(card);
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
