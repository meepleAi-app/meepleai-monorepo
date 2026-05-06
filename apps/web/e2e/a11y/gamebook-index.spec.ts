/**
 * Accessibility tests — /gamebook index (SP6 Phase B Tier M, Issue #788).
 *
 * Combines:
 *   - axe-core WCAG 2.1 AA scan: default state (full layout)
 *   - axe-core WCAG 2.1 AA scan: empty state (EmptyGamebooks CTA)
 *   - axe-core WCAG 2.1 AA scan: quota-soft state (soft banner + upgrade CTA)
 *   - axe-core WCAG 2.1 AA scan: quota-hard state (hard banner + disabled CTA)
 *   - axe-core WCAG 2.1 AA scan: loading state (aria-live="polite" skeleton)
 *   - axe-core WCAG 2.1 AA scan: error state (role="alert" + retry CTA)
 *   - prefers-reduced-motion contract: skeleton ShimmerBlock motion-safe class
 *     does not animate under reduced-motion.
 *   - Keyboard nav contract: Tab cycles through focusable elements, reaches
 *     a GamebookCard with `data-status="ready"` (visible focus ring on
 *     interactive cards).
 *
 * NOTE — Hidden cells excluded from a11y scan:
 *   None — all 6 cells are independently axe-scanned via fixture URL hatch.
 *
 * Auth bypass: triple helper required for `(authenticated)` routes — Wave B.1
 * lesson learned (Issue #633). No `networkidle` — `domcontentloaded` +
 * explicit `waitForSelector`.
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function gotoGamebook(page: Page, fixture: string): Promise<void> {
  await seedAuth(page);
  await page.goto(`/gamebook?fixture=${fixture}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector('[data-slot="gamebook-index-view"]', { timeout: 30_000 });
}

function logViolations(
  label: string,
  violations: ReadonlyArray<{
    impact?: string | null;
    id: string;
    help: string;
    nodes: ReadonlyArray<unknown>;
  }>
): void {
  if (violations.length > 0) {
    const summary = violations
      .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
      .join('\n');
    console.log(`axe violations (${label}):\n${summary}`);
  }
}

test.describe('Gamebook index — accessibility @a11y', () => {
  test.describe.configure({ retries: 0 });

  // ─────────────────────────────────────────────────────────────────────────
  // axe-core scans across 6 fixture variants (one per FSM cell)
  // ─────────────────────────────────────────────────────────────────────────

  test('axe-core: no WCAG 2.1 AA violations on default state', async ({ page }) => {
    await gotoGamebook(page, 'default');
    await expect(
      page.locator('[data-slot="gamebook-index-view"][data-ui-state="default"]')
    ).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('default', results.violations);
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations on empty state', async ({ page }) => {
    await gotoGamebook(page, 'empty');
    await expect(page.locator('[data-slot="empty-gamebooks"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('empty', results.violations);
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations on quota-soft state', async ({ page }) => {
    await gotoGamebook(page, 'quota-soft');
    await expect(page.locator('[data-slot="quota-widget"][data-variant="soft"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('quota-soft', results.violations);
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations on quota-hard state', async ({ page }) => {
    await gotoGamebook(page, 'quota-hard');
    await expect(page.locator('[data-slot="quota-widget"][data-variant="hard"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('quota-hard', results.violations);
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations on loading state', async ({ page }) => {
    await gotoGamebook(page, 'loading');
    await expect(
      page.locator('[data-slot="gamebook-index-view"][data-ui-state="loading"]')
    ).toBeVisible();
    await expect(page.locator('[data-slot="gamebook-card-skeleton"]').first()).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('loading', results.violations);
    expect(results.violations).toEqual([]);
  });

  test('axe-core: no WCAG 2.1 AA violations on error state', async ({ page }) => {
    await gotoGamebook(page, 'error');
    await expect(page.locator('[data-slot="gamebook-index-error"]')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    logViolations('error', results.violations);
    expect(results.violations).toEqual([]);
  });

  // ─────────────────────────────────────────────────────────────────────────
  // prefers-reduced-motion contract
  // ─────────────────────────────────────────────────────────────────────────

  test('reduced-motion: skeleton shimmer animation suspended', async ({ page }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await gotoGamebook(page, 'loading');
    await expect(page.locator('[data-slot="gamebook-card-skeleton"]').first()).toBeVisible();

    // Skeleton uses `motion-safe:animate-pulse` Tailwind utility — under
    // reduced-motion media query the animation is NOT applied, leaving
    // animationName === 'none'. The `aria-hidden="true"` shimmer block
    // children inside the card emit the same class.
    const animationName = await page
      .locator('[data-slot="gamebook-card-skeleton"]')
      .first()
      .locator('div')
      .first()
      .evaluate(el => getComputedStyle(el).animationName);

    expect(animationName).toBe('none');
  });

  // ─────────────────────────────────────────────────────────────────────────
  // Keyboard navigation contract
  // ─────────────────────────────────────────────────────────────────────────

  test('keyboard nav: Tab reaches an actionable gamebook card with status="ready"', async ({
    page,
  }) => {
    await gotoGamebook(page, 'default');
    await expect(
      page.locator('[data-slot="gamebook-index-view"][data-ui-state="default"]')
    ).toBeVisible();

    // Cycle Tab presses until focus lands inside a `data-slot="gamebook-card"`
    // with `data-status="ready"` (the only interactive variant). Cap iterations
    // to defend against runaway tab loops.
    const MAX_TABS = 30;
    let landedOnReadyCard = false;

    for (let i = 0; i < MAX_TABS; i++) {
      await page.keyboard.press('Tab');
      const status = await page.evaluate(() => {
        const el = document.activeElement;
        if (!el) return null;
        const card = el.closest('[data-slot="gamebook-card"]');
        return card ? card.getAttribute('data-status') : null;
      });
      if (status === 'ready') {
        landedOnReadyCard = true;
        break;
      }
    }

    expect(landedOnReadyCard).toBe(true);
  });
});
