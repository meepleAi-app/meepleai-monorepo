/**
 * Accessibility tests — /shared-games/[id] (Wave A.4, Issue #603).
 *
 * Combines:
 *   - axe-core WCAG 2.1 AA scan on the rendered detail page
 *   - WAI-ARIA tablist keyboard navigation smoke (Issue #588 absorbed):
 *     ArrowRight/Left wrap, Home/End focus, aria-selected sync.
 *
 * Comprehensive unit-level keyboard nav coverage lives in
 * `src/components/ui/v2/shared-game-detail/tabs.test.tsx` (10 tests).
 * This e2e suite verifies the contract holds against the real prod build.
 */
import AxeBuilder from '@axe-core/playwright';
import { test, expect, type APIRequestContext } from '@playwright/test';

const WCAG_TAGS = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

async function fetchFirstSharedGameId(request: APIRequestContext): Promise<string> {
  const res = await request.get('/api/v1/shared-games?pageSize=1');
  expect(res.ok()).toBeTruthy();
  const body = (await res.json()) as { items: ReadonlyArray<{ id: string }> };
  expect(body.items.length).toBeGreaterThan(0);
  return body.items[0].id;
}

test.describe('Shared game detail — accessibility @a11y', () => {
  test('axe-core: no WCAG 2.1 AA violations on default tab', async ({ page, request }) => {
    const id = await fetchFirstSharedGameId(request);
    await page.goto(`/shared-games/${id}`, { waitUntil: 'networkidle' });
    await page.waitForSelector('[data-testid="shared-game-detail-page"]', { timeout: 30_000 });

    const results = await new AxeBuilder({ page })
      .withTags(WCAG_TAGS)
      .exclude('#webpack-dev-server-client-overlay')
      .analyze();

    if (results.violations.length > 0) {
      // Print a concise summary in the test output before failing.
      const summary = results.violations
        .map(v => `[${v.impact}] ${v.id}: ${v.help} (${v.nodes.length} nodes)`)
        .join('\n');
      console.log('axe violations:\n' + summary);
    }
    expect(results.violations).toEqual([]);
  });

  test('tablist keyboard navigation: ArrowRight wraps last → first', async ({ page, request }) => {
    const id = await fetchFirstSharedGameId(request);
    await page.goto(`/shared-games/${id}`, { waitUntil: 'networkidle' });
    await page.waitForSelector('[data-testid="shared-game-detail-page"]', { timeout: 30_000 });

    const tabs = page.locator('[role="tab"]');
    await expect(tabs).toHaveCount(5);

    // Focus the last tab (community), press ArrowRight → expect overview focused (wrap).
    await tabs.nth(4).focus();
    await page.keyboard.press('ArrowRight');
    await expect(tabs.nth(0)).toBeFocused();
    await expect(tabs.nth(0)).toHaveAttribute('aria-selected', 'true');
  });

  test('tablist keyboard navigation: ArrowLeft wraps first → last', async ({ page, request }) => {
    const id = await fetchFirstSharedGameId(request);
    await page.goto(`/shared-games/${id}`, { waitUntil: 'networkidle' });
    await page.waitForSelector('[data-testid="shared-game-detail-page"]', { timeout: 30_000 });

    const tabs = page.locator('[role="tab"]');
    await tabs.nth(0).focus();
    await page.keyboard.press('ArrowLeft');
    await expect(tabs.nth(4)).toBeFocused();
    await expect(tabs.nth(4)).toHaveAttribute('aria-selected', 'true');
  });

  test('tablist keyboard navigation: Home/End jump to first/last', async ({ page, request }) => {
    const id = await fetchFirstSharedGameId(request);
    await page.goto(`/shared-games/${id}`, { waitUntil: 'networkidle' });
    await page.waitForSelector('[data-testid="shared-game-detail-page"]', { timeout: 30_000 });

    const tabs = page.locator('[role="tab"]');

    // Press End from the first tab → focus last.
    await tabs.nth(0).focus();
    await page.keyboard.press('End');
    await expect(tabs.nth(4)).toBeFocused();
    await expect(tabs.nth(4)).toHaveAttribute('aria-selected', 'true');

    // Press Home from the last tab → focus first.
    await page.keyboard.press('Home');
    await expect(tabs.nth(0)).toBeFocused();
    await expect(tabs.nth(0)).toHaveAttribute('aria-selected', 'true');
  });
});
