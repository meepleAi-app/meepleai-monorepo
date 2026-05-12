/**
 * Accessibility tests — /gamebook/upload Tier L wizard (SP6 Phase C.1.D
 * Foundation sub-PR, Issue #789).
 *
 * Combines (Phase 0.5 contract §14 + §15 + AC11 + AC12):
 *   - axe-core WCAG 2.1 AA scans across 6 representative FSM cells:
 *       step1-default, step1-no-results, step2-ready, step2-denied,
 *       step3-progress, step3-cancel-modal
 *     (Other variants — step1-bgg-loading, step1-searching, step2-low-light,
 *     step2-failed, step2-capturing, step3-partial, step3-complete,
 *     step3-offline, wizard-cancelled — share the same shells / placeholders
 *     as the scanned cells; redundant scans omitted to keep CI runtime sane.)
 *   - Keyboard nav contract (StepIndicator): Tab traversal preserves
 *     `aria-current="step"` on the active step (no JS reset under focus
 *     change).
 *   - Keyboard nav contract (GameSearchBar tabs): tablist with `role="tab"`
 *     + `aria-selected` cycles via ArrowLeft/ArrowRight / Home / End via
 *     the shared `useTablistKeyboardNav` hook (Wave A.6 PR #623). Activation
 *     is automatic on focus per APG.
 *   - prefers-reduced-motion contract: orchestrator + components emit
 *     `motion-reduce:*` Tailwind utilities so animations are suspended
 *     under reduced-motion media query (verified via computed style
 *     `animation-name === 'none'` on a reduced-motion-aware element).
 *
 * Auth bypass: triple helper required for `(authenticated)` routes — Wave B.1
 * lesson learned (Issue #633). No `networkidle` — `domcontentloaded` +
 * explicit `waitForSelector`.
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

// See sessions-index.spec.ts for rationale.
test.use({ colorScheme: 'dark' });

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function gotoUpload(page: Page, fixture: string): Promise<void> {
  await seedAuth(page);
  await page.goto(`/gamebook/upload?fixture=${fixture}`, { waitUntil: 'domcontentloaded' });
  await page.waitForSelector(`[data-slot="gamebook-upload-view"][data-ui-state="${fixture}"]`, {
    timeout: 30_000,
  });
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

async function scanAxe(page: Page, fixture: string, label: string): Promise<void> {
  await gotoUpload(page, fixture);
  const results = await new AxeBuilder({ page })
    .withTags(WCAG_TAGS)
    .exclude('#webpack-dev-server-client-overlay')
    .analyze();
  logViolations(label, results.violations);
  expect(results.violations).toEqual([]);
}

test.describe('Gamebook upload — accessibility @a11y', () => {
  test.describe.configure({ retries: 0 });

  // ── axe-core scans across 6 representative FSM cells ─────────────────
  test('axe-core: no WCAG 2.1 AA violations on step1-default', async ({ page }) => {
    await scanAxe(page, 'step1-default', 'step1-default');
  });

  test('axe-core: no WCAG 2.1 AA violations on step1-no-results', async ({ page }) => {
    await scanAxe(page, 'step1-no-results', 'step1-no-results');
  });

  test('axe-core: no WCAG 2.1 AA violations on step2-ready (placeholder)', async ({ page }) => {
    await scanAxe(page, 'step2-ready', 'step2-ready');
  });

  test('axe-core: no WCAG 2.1 AA violations on step2-denied (placeholder)', async ({ page }) => {
    await scanAxe(page, 'step2-denied', 'step2-denied');
  });

  test('axe-core: no WCAG 2.1 AA violations on step3-progress (placeholder)', async ({ page }) => {
    await scanAxe(page, 'step3-progress', 'step3-progress');
  });

  test('axe-core: no WCAG 2.1 AA violations on step3-cancel-modal (placeholder)', async ({
    page,
  }) => {
    // Cancel modal placeholder: role="dialog" + aria-modal="true" + aria-label
    // pre-resolved by orchestrator via i18n; axe scans the whole page including
    // the overlay shell.
    await scanAxe(page, 'step3-cancel-modal', 'step3-cancel-modal');
  });

  // ── Keyboard nav: StepIndicator preserves aria-current="step" ────────
  test('keyboard nav StepIndicator: aria-current="step" remains on active step under tab traversal', async ({
    page,
  }) => {
    await gotoUpload(page, 'step1-default');

    // Step 1 active by default — wizard-step-indicator-circle for stepNumber=1
    // hosts data-state="active" + aria-current="step" on its parent div.
    const activeStep = page
      .locator('[data-slot="wizard-step-indicator"] [data-step-number="1"][data-state="active"]')
      .first();
    await expect(activeStep).toHaveAttribute('aria-current', 'step');

    // Tab a few times — the StepIndicator is sticky `<nav role="navigation">`
    // not a tablist, so Tab does not change `aria-current`. Verify the
    // attribute persists (defensive: catch regressions where focus handlers
    // mutate state).
    for (let i = 0; i < 3; i++) {
      await page.keyboard.press('Tab');
    }
    await expect(activeStep).toHaveAttribute('aria-current', 'step');
  });

  // ── Keyboard nav: GameSearchBar tabs cycle via ArrowLeft/Right ───────
  test('keyboard nav GameSearchBar tabs: ArrowRight cycles to BGG, ArrowLeft cycles back', async ({
    page,
  }) => {
    await gotoUpload(page, 'step1-default');
    await page.waitForSelector('[data-slot="game-search-bar"]', { timeout: 10_000 });

    const catalogTab = page.locator('[data-slot="game-search-tab"][data-tab-key="catalog"]');
    const bggTab = page.locator('[data-slot="game-search-tab"][data-tab-key="bgg"]');

    // Default `catalog` is selected (aria-selected="true", tabIndex=0).
    await expect(catalogTab).toHaveAttribute('role', 'tab');
    await expect(catalogTab).toHaveAttribute('aria-selected', 'true');
    await expect(bggTab).toHaveAttribute('aria-selected', 'false');

    // Focus catalog tab; ArrowRight moves focus to bgg (and per APG hook,
    // activation is automatic — `activeTab` flips, URL replaces).
    await catalogTab.focus();
    await expect(catalogTab).toBeFocused();
    await page.keyboard.press('ArrowRight');
    await expect(bggTab).toBeFocused();

    // ArrowLeft from bgg returns focus to catalog (wrap is left→right via
    // hook config; verify left-from-second resolves to first regardless).
    await page.keyboard.press('ArrowLeft');
    await expect(catalogTab).toBeFocused();
  });

  // ── prefers-reduced-motion contract ──────────────────────────────────
  test('reduced-motion: motion-reduce:transition-none suspends transitions on tab tabs', async ({
    page,
  }) => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await gotoUpload(page, 'step1-default');
    await page.waitForSelector('[data-slot="game-search-bar"]', { timeout: 10_000 });

    // GameSearchBar tabs apply `transition-colors motion-reduce:transition-none`.
    // Under reduced-motion media query the transition shorthand collapses to
    // `none` — verify via computed style.
    const transitionProperty = await page
      .locator('[data-slot="game-search-tab"][data-tab-key="catalog"]')
      .evaluate(el => getComputedStyle(el).transitionProperty);

    expect(transitionProperty).toBe('none');
  });
});
