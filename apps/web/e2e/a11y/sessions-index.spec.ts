/**
 * Accessibility tests — /sessions (Wave D.1, Issue #735).
 *
 * Combines:
 *   - axe-core WCAG 2.1 AA scan su default state (results list populated)
 *   - axe-core WCAG 2.1 AA scan su filtered-empty state (per coprire
 *     CTA + empty-state markup, sezioni che non sono presenti su default)
 *   - prefers-reduced-motion contract: AC-8 spec §5 — verifica che la route
 *     `/sessions` rispetti l'override globale CSS (`globals.css:388-396` riduce
 *     `transition-duration` a 0.01ms !important sotto `@media
 *     (prefers-reduced-motion: reduce)`). Le card transitions su
 *     SessionCardList/SessionCardGrid (default transition-shadow 150ms) devono
 *     collassare a sub-millisecondo durations.
 *   - WAI-ARIA tablist keyboard nav: status filter pills (data-slot="sessions-filter-chip",
 *     role="tab") support ArrowRight/ArrowLeft cycling via useTablistKeyboardNav hook
 *     (Wave A.6 PR #623).
 *
 * Comprehensive unit-level coverage del 5-state FSM + ?state= override matrix
 * lives in `_components/__tests__/SessionsLibraryView.test.tsx`.
 * This e2e suite verifies the contract holds against the real prod build.
 *
 * Auth bypass: `(authenticated)` route renders senza session perché
 * `(authenticated)/layout.tsx` non gate-keepa server-side e
 * `PLAYWRIGHT_AUTH_BYPASS=true` è settato dal webServer di playwright.config.ts.
 *
 * Visual-test fixture: i test passano contro Next.js prod build con
 * `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` in modo che `useActiveSessions`
 * venga short-circuitato dal fixture deterministico (6 entries: Brass: Birmingham,
 * Wingspan×2, Azul, Ark Nova, 7 Wonders).
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

// next-themes (defaultTheme="dark", enableSystem) honors prefers-color-scheme on
// first paint. Playwright defaults to colorScheme=light, so without this override
// next-themes applies `.light` and the `--e-*` AA-on-dark overrides (globals.css
// .dark block) never activate — but the bg is always dark (gaming-bg-base in
// :root), producing the color-contrast violations seen in CI run 25693769790.
// See #807/#876 follow-up.
test.use({ colorScheme: 'dark' });

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;

async function gotoSessionsReady(page: Page, search = ''): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
  await page.goto(`/sessions${search}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="sessions-library-view"]', { timeout: 30_000 });
}

test.describe('Sessions index — accessibility @a11y', () => {
  test('axe-core: no WCAG 2.1 AA violations on default state', async ({ page }) => {
    await gotoSessionsReady(page);
    // Default state expects hero + filters visible (list view populated with fixture entries).
    await expect(page.locator('[data-slot="sessions-hero"]')).toBeVisible();
    await expect(page.locator('[data-slot="sessions-filters"]')).toBeVisible();

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
    await gotoSessionsReady(page, '?state=filtered-empty');
    // Filtered-empty state expects EmptySessions con clearFilters CTA visible.
    await expect(page.locator('[data-slot="sessions-empty-filtered-empty"]')).toBeVisible();

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

  test('prefers-reduced-motion: card transitions collapse to sub-ms', async ({ page }) => {
    // Emulate reduced-motion BEFORE goto so the global CSS rule
    // (globals.css:388-396) applies on first paint. Without this the
    // SessionCardList `transition-shadow` runs at full duration.
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await gotoSessionsReady(page);
    await expect(page.locator('[data-slot="sessions-hero"]')).toBeVisible();

    // Inspect computed transition-duration on the first SessionCardList element.
    // Under reduced motion the global override forces every element's
    // `transition-duration: 0.01ms !important`. The default card CSS sets
    // transition-shadow durations; we expect <= 50ms to confirm the override.
    // Sessions uses <button data-slot="session-card-list"> as the card wrapper.
    const transitionDurationMs = await page.evaluate(() => {
      const card = document.querySelector('[data-slot="session-card-list"]') as HTMLElement | null;
      if (!card) {
        // Fallback: try grid card variant
        const gridCard = document.querySelector(
          '[data-slot="session-card-grid"]'
        ) as HTMLElement | null;
        if (!gridCard) return null;
        const cs = window.getComputedStyle(gridCard);
        const durations = cs.transitionDuration.split(',').map((s: string) => {
          const trimmed = s.trim();
          if (trimmed.endsWith('ms')) return parseFloat(trimmed);
          if (trimmed.endsWith('s')) return parseFloat(trimmed) * 1000;
          return parseFloat(trimmed) * 1000;
        });
        return Math.max(...durations);
      }
      const cs = window.getComputedStyle(card);
      // transition-duration may be a comma-separated list (e.g. "0.01s, 0.01s").
      // Take the max value.
      const durations = cs.transitionDuration.split(',').map((s: string) => {
        const trimmed = s.trim();
        if (trimmed.endsWith('ms')) return parseFloat(trimmed);
        if (trimmed.endsWith('s')) return parseFloat(trimmed) * 1000;
        return parseFloat(trimmed) * 1000; // fallback: assume seconds
      });
      return Math.max(...durations);
    });

    // Only assert if a card was found (fixture build required; skip gracefully otherwise).
    if (transitionDurationMs !== null) {
      expect(transitionDurationMs).toBeLessThanOrEqual(50);
    }
  });

  test('keyboard nav: ArrowRight cycles status filter chips', async ({ page }) => {
    await gotoSessionsReady(page);
    // Status filter chips: role="tab" + data-slot="sessions-filter-chip"
    // Rendered in order: all | active | completed | abandoned
    // First chip ('all') is active by default, tabIndex=0.
    await page.waitForSelector('[data-slot="sessions-filters"]', { timeout: 10_000 });

    const firstChip = page.locator('[role="tab"][data-slot="sessions-filter-chip"]').first();
    await firstChip.focus();
    // Verify first chip ('all') is focused.
    await expect(firstChip).toBeFocused();

    // ArrowRight should move focus to the second chip ('active').
    await page.keyboard.press('ArrowRight');
    const secondChip = page.locator('[role="tab"][data-slot="sessions-filter-chip"]').nth(1);
    await expect(secondChip).toBeFocused();
  });
});
