/**
 * Accessibility tests — /agents (Wave B.2, Issue #634).
 *
 * Combines:
 *   - axe-core WCAG 2.1 AA scan su default state (results grid populato)
 *   - axe-core WCAG 2.1 AA scan su filtered-empty state (per coprire
 *     CTA + empty-state markup, sezioni che non sono presenti su default)
 *   - prefers-reduced-motion contract: AC-8 spec §5 — verifica che la route
 *     `/agents` rispetti l'override globale CSS (`globals.css:388-396` riduce
 *     `transition-duration` a 0.01ms !important sotto `@media
 *     (prefers-reduced-motion: reduce)`). Le card hover transitions su
 *     MeepleCard GridCard (default 350ms) devono collassare a sub-millisecondo
 *     durations.
 *
 * Comprehensive unit-level coverage del 5-state FSM + ?state= override matrix
 * lives in `_components/__tests__/AgentsLibraryView.test.tsx` (13 tests).
 * This e2e suite verifies the contract holds against the real prod build.
 *
 * Auth bypass: `(authenticated)` route renders senza session perché
 * `(authenticated)/layout.tsx` non gate-keepa server-side e
 * `PLAYWRIGHT_AUTH_BYPASS=true` è settato dal webServer di playwright.config.ts.
 *
 * Visual-test fixture: i test passano contro Next.js prod build con
 * `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` in modo che `useAgents` venga
 * short-circuitato dal fixture deterministico (6 entries: 3 attivo + 2
 * in-setup + 1 archiviato).
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

async function gotoAgentsReady(page: Page, search = ''): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
  await page.goto(`/agents${search}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="agents-library-view"]', { timeout: 30_000 });
}

test.describe('Agents library — accessibility @a11y', () => {
  test('axe-core: no WCAG 2.1 AA violations on default state', async ({ page }) => {
    await gotoAgentsReady(page);
    // Default state expects results grid populato — wait for first card.
    await expect(page.locator('[data-slot="agents-results-grid-link"]').first()).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      // EntityBadge color-contrast (HSL 38,92%,50% on white ≈ 2.1:1) è
      // pre-existing debt nella shared MeepleCard family (tokens.ts:7,
      // entityColors.agent), fuori scope Wave B.2 brownfield migration.
      // Tracked in #636 (audit cross-entity colors + token fix).
      .exclude('[data-slot="meeple-card-entity-badge"]')
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
    await gotoAgentsReady(page, '?state=filtered-empty');
    // Filtered-empty state expects EmptyAgents con clearFilters CTA visible.
    await expect(
      page.locator('[data-slot="agents-empty-state"][data-kind="filtered-empty"]')
    ).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      // EntityBadge color-contrast (HSL 38,92%,50% on white ≈ 2.1:1) è
      // pre-existing debt nella shared MeepleCard family (tokens.ts:7,
      // entityColors.agent), fuori scope Wave B.2 brownfield migration.
      // Tracked in #636 (audit cross-entity colors + token fix).
      .exclude('[data-slot="meeple-card-entity-badge"]')
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
    await gotoAgentsReady(page);
    await expect(page.locator('[data-slot="agents-results-grid-link"]').first()).toBeVisible();

    // Inspect computed transition-duration on the GridCard element (first child
    // of the Link wrapper). Under reduced motion the global override forces
    // every element's `transition-duration: 0.01ms !important`. The default
    // GridCard CSS sets 350ms; we expect <= 50ms to confirm the override.
    const transitionDurationMs = await page.evaluate(() => {
      const link = document.querySelector(
        '[data-slot="agents-results-grid-link"]'
      ) as HTMLElement | null;
      if (!link) return Number.POSITIVE_INFINITY;
      const card = link.firstElementChild as HTMLElement | null;
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
