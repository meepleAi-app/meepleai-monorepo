/**
 * Visual contract — /agents/[id] desktop route migrata vs mockup baseline.
 *
 * Issue #581 (Wave C.2 agent detail migration) · umbrella #578
 * V2 Migration Phase 1 (Wave C.2 brownfield big-bang).
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(authenticated)/agents/[id]/_components/
 *     AgentDetailViewV2.tsx`) con hero + AgentTabs (5 tab) + tabpanel.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run visual-regression-migrated.yml \
 *     --ref feature/issue-581-wave-c2-agent-detail-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il workflow setta `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` prima del
 *   build, così il fixture deterministico (Catan-shaped AgentDetail)
 *   viene short-circuitato dal `AgentDetailViewV2` orchestrator senza
 *   richiedere il backend API.
 *
 * Strategia ID (visual-test fixture, NOT a backend fetch):
 *   - `AgentDetailViewV2` legge `IS_VISUAL_TEST_BUILD` da
 *     `@/lib/agents/agent-detail-visual-test-fixture` e sostituisce
 *     `agentQuery.data` con entries deterministiche (Catan-shaped AgentDto).
 *   - In production deploy il fixture è dead code (constant-fold) — NON
 *     espone alcun shape pubblico.
 *   - `?fixture=standalone` carica il fixture senza gameId per la Cell 10
 *     visual baseline (Knowledge tab → standalone empty state).
 *
 * Auth bypass:
 *   La route `/agents/[id]` è sotto `(authenticated)/` ma il layout non
 *   gate-keepa server-side; `PLAYWRIGHT_AUTH_BYPASS=true` settato in
 *   `playwright.config.ts:434` (webServer env) garantisce navigazione senza
 *   session cookie. `seedAuthSession` + `seedCookieConsent` + `mockAuthEndpoints`
 *   soddisfano il `proxy.ts` middleware gate + i flussi React-side `AuthProvider`
 *   / `useSessionCheck` (Wave B.1/B.2/B.3 lesson learned, triple-helper pattern).
 *
 * Cherry-picked from Wave C.1 sp4-game-detail.spec.ts — updated data-slot selectors to
 * match Task 2 components (agent-detail-view, agent-detail-hero, agent-detail-tabs).
 * networkidle anti-pattern replaced with domcontentloaded + explicit waitForSelector
 * (Wave B.1 lesson).
 *
 * Wave C.2 unique: standalone fixture baseline via `?fixture=standalone` URL param
 * for Cell 10 (agent without gameId — Knowledge tab shows standalone empty state).
 */
import { test, expect, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from '../_helpers/seedAuthSession';
import { seedCookieConsent } from '../_helpers/seedCookieConsent';

const SLUG = 'sp4-agent-detail';

/**
 * Deterministic fixture agent ID — default (active with gameId).
 * Matches `VISUAL_TEST_FIXTURE_AGENT_DETAIL_ID` from
 * `lib/agents/agent-detail-visual-test-fixture.ts`.
 */
const FIXTURE_AGENT_ID = '00000000-0000-4000-8000-000000000581';

async function waitForAgentDetailReady(page: Page): Promise<void> {
  // Default state: wait for agent-detail-view root + hero + tablist
  await page.waitForSelector('[data-slot="agent-detail-view"]', { timeout: 30_000 });
  await page.waitForSelector('[data-slot="agent-detail-hero"]', { timeout: 10_000 });
  await page.waitForSelector('[data-slot="agent-detail-tabs"]', { timeout: 10_000 });

  // Fonts + RAF double-flush for stable screenshot
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

test.describe('V2 Visual Migrated — /agents/[id] matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Agent detail desktop 1280x720 — active variant matches sp4-agent-detail mockup', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 1280, height: 720 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto(`/agents/${FIXTURE_AGENT_ID}`, { waitUntil: 'domcontentloaded' });
    await waitForAgentDetailReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}-desktop.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('Agent detail mobile 375x812 — active variant matches sp4-agent-detail mockup', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await seedAuthSession(page);
    await seedCookieConsent(page);
    await mockAuthEndpoints(page);
    await page.goto(`/agents/${FIXTURE_AGENT_ID}`, { waitUntil: 'domcontentloaded' });
    await waitForAgentDetailReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}-mobile.png`, {
      fullPage: true,
      animations: 'disabled',
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
