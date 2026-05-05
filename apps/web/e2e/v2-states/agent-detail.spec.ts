/**
 * V2 State Coverage — /agents/[id] desktop route (Issue #581, Wave C.2).
 *
 * Captures 4 surface states of the 5-state FSM + Cell 10 standalone:
 *   `default | loading | not-found | standalone`
 *
 * `error` è escluso dalla coverage visual perché surface dipendente da
 * `agentQuery.isError` (TanStack Query), che richiede un mock di network
 * failure non riproducibile in modo deterministico via URL override. Coperto
 * invece dal unit test orchestratore
 * (`_components/__tests__/AgentDetailViewV2.test.tsx`) che esercita l'override
 * `?state=error` direttamente.
 *
 * `empty` per Wave C.2 non è un alias di `not-found` — non è in `VALID_OVERRIDES`
 * del orchestrator. L'orchestratore supporta solo `['loading', 'error', 'not-found']`
 * come `?state=` override. Il standalone state (Cell 10) è accessibile via
 * `?fixture=standalone` quando `IS_VISUAL_TEST_BUILD === true`.
 *
 * FSM states covered:
 *   - `default`:    [data-slot="agent-detail-view"]    — active agent, IS_VISUAL_TEST_BUILD fixture
 *   - `loading`:    [data-slot="agent-detail-loading"] — ?state=loading override
 *   - `not-found`:  [data-slot="agent-detail-not-found"] — ?state=not-found override
 *   - `standalone`: [data-slot="agent-detail-view"]    — ?fixture=standalone, gameId=null (Cell 10)
 *
 * Uses test-only `?state=...` and `?fixture=...` query params, gated da
 * `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD` in
 * `_components/AgentDetailViewV2.tsx`.
 *
 * Snapshots written to `apps/web/e2e/v2-states/agent-detail.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-581-wave-c2-agent-detail-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *
 * Cherry-picked from Wave C.1 game-detail.spec.ts — data-slot selectors updated to
 * match Task 2/3 committed components (separate shells per FSM state).
 * networkidle anti-pattern replaced with domcontentloaded + explicit waitForSelector
 * (Wave B.1 lesson).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

/**
 * Primary fixture agent ID — active agent WITH gameId set.
 * Matches `VISUAL_TEST_FIXTURE_AGENT_DETAIL_ID` from
 * `lib/agents/agent-detail-visual-test-fixture.ts`.
 */
const FIXTURE_AGENT_ID = '00000000-0000-4000-8000-000000000581';

const VIEWPORTS = [
  { name: 'desktop', width: 1280, height: 720 },
  { name: 'mobile', width: 375, height: 812 },
] as const;

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

async function waitForFontsAndRaf(page: Page): Promise<void> {
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

test.describe('Agent detail — state coverage', () => {
  test.describe.configure({ retries: 0 });

  for (const viewport of VIEWPORTS) {
    test.describe(`${viewport.name} ${viewport.width}x${viewport.height}`, () => {
      test('default state', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/agents/${FIXTURE_AGENT_ID}`, { waitUntil: 'domcontentloaded' });
        // Default state: root shell + hero + tablist all rendered via IS_VISUAL_TEST_BUILD fixture
        await page.waitForSelector('[data-slot="agent-detail-view"]', { timeout: 30_000 });
        await page.waitForSelector('[data-slot="agent-detail-hero"]', { timeout: 10_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`agent-detail-${viewport.name}-default.png`, {
          fullPage: true,
          animations: 'disabled',
          mask: [page.locator('[data-dynamic]')],
        });
      });

      test('loading state', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/agents/${FIXTURE_AGENT_ID}?state=loading`, {
          waitUntil: 'domcontentloaded',
        });
        // Loading state: separate shell with animate-pulse skeletons
        await page.waitForSelector('[data-slot="agent-detail-loading"]', { timeout: 30_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`agent-detail-${viewport.name}-loading.png`, {
          fullPage: true,
          animations: 'disabled',
          // Skeleton pulse animations — mask to avoid flake (Wave B.1/B.2 pattern).
          mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
        });
      });

      test('not-found state', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        await page.goto(`/agents/${FIXTURE_AGENT_ID}?state=not-found`, {
          waitUntil: 'domcontentloaded',
        });
        // Not-found state: CTA shell with back-to-agents link
        await page.waitForSelector('[data-slot="agent-detail-not-found"]', { timeout: 30_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`agent-detail-${viewport.name}-not-found.png`, {
          fullPage: true,
          animations: 'disabled',
          mask: [page.locator('[data-dynamic]')],
        });
      });

      test('standalone state (Cell 10 — agent without gameId)', async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await seedAuth(page);
        // Cell 10: standalone agent fixture via ?fixture=standalone
        // IS_VISUAL_TEST_BUILD fixture loads FIXTURE_STANDALONE (gameId=null) →
        // Knowledge tab renders the standalone empty state instead of KB doc list.
        // Note: ?state= override is NOT used here — standalone is accessed via ?fixture=.
        await page.goto(`/agents/${FIXTURE_AGENT_ID}?fixture=standalone`, {
          waitUntil: 'domcontentloaded',
        });
        // Standalone renders the default shell (agent-detail-view) — NOT a distinct shell.
        // The visual difference is in the Knowledge tab panel content.
        await page.waitForSelector('[data-slot="agent-detail-view"]', { timeout: 30_000 });
        await page.waitForSelector('[data-slot="agent-detail-hero"]', { timeout: 10_000 });
        await waitForFontsAndRaf(page);

        await expect(page).toHaveScreenshot(`agent-detail-${viewport.name}-standalone.png`, {
          fullPage: true,
          animations: 'disabled',
          mask: [page.locator('[data-dynamic]')],
        });
      });
    });
  }
});
