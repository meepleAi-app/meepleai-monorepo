/**
 * Accessibility tests — /game-nights/new wizard (Issue #950 W4).
 *
 * Runs axe-core WCAG 2.1 AA scans on a representative subset of the
 * wizard's 10 FSM fixture states. We don't scan every state because most
 * share the same component tree (only the reducer payload differs); axe
 * findings on the shared shell would be duplicated. Scope chosen to
 * cover the distinct component compositions:
 *
 *   - `step1-warning`: alert + conflict list (Step 1 + warning surface)
 *   - `step2-location`: 4-option radiogroup (Step 2)
 *   - `step3-filled`: chip list + regulars suggestions (Step 3 most complex)
 *   - `step4-decide-group`: toggle + dimmed library (Step 4 disabled state)
 *
 * The `?fixture=` hatch is read by the orchestrator only when
 * `IS_VISUAL_TEST_BUILD === true`. The Playwright webServer sets
 * `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` for the prod build (Wave
 * B.1/B.2/C.2 pattern).
 *
 * Per spec §11 AC-E.3: axe-core WCAG 2.1 AA per state + reduced-motion
 * contract verified at the wizard root.
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

const A11Y_FIXTURES = [
  'step1-warning',
  'step2-location',
  'step3-filled',
  'step4-decide-group',
] as const;

async function gotoWizard(page: Page, fixture: string): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
  await page.goto(`/game-nights/new?fixture=${fixture}`, {
    waitUntil: 'domcontentloaded',
  });
  await page.waitForSelector('[data-slot="game-night-create-wizard"]', { timeout: 30_000 });

  // Fonts + RAF flush so axe's color-contrast pass has stable values.
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

test.describe('A11y — /game-nights/new wizard', () => {
  test.describe.configure({ retries: 0 });

  for (const fixture of A11Y_FIXTURES) {
    test(`axe-core WCAG 2.1 AA — fixture=${fixture}`, async ({ page }) => {
      await page.setViewportSize({ width: 1280, height: 720 });
      await gotoWizard(page, fixture);

      const results = await new AxeBuilder({ page }).withTags(WCAG_TAGS).analyze();

      // Fail on any violation. Wave A11y tracker (#1094) covers cross-route
      // restoration of the gate — this is a new route added clean.
      expect(results.violations).toEqual([]);
    });
  }

  test('respects prefers-reduced-motion at the wizard root', async ({ page }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await gotoWizard(page, 'step1-warning');

    // Sample any motion-safe utility on the wizard root; the global override
    // in globals.css collapses transition-duration to 0.01ms under
    // prefers-reduced-motion.
    const transitionDuration = await page.evaluate(() => {
      const root = document.querySelector('[data-slot="game-night-create-wizard"]');
      if (!root) return null;
      const style = window.getComputedStyle(root);
      return style.transitionDuration;
    });

    // Either "0s" (no animation) or near-zero ms — the global override
    // reduces to 0.01ms, which most browsers report as `0s`.
    expect(transitionDuration ?? '').toMatch(/^0s|0\.01ms|0\.0\d+s$/);
  });
});
