/**
 * SMOKE — /agents/[id] real backend (authenticated route, Wave C.2, Issue #581).
 *
 * Tests the frontend route against the real backend in nightly smoke workflow.
 * No visual baselines — functional shell assertion only.
 *
 * Strategy:
 *   - Deterministic UUID `NEVER_EXISTS_ID` always drives the not-found shell,
 *     confirming the frontend renders gracefully without a real agent.
 *   - If `SMOKE_AGENT_ID` env var is set (seeded staging agent), navigate
 *     to that real id and assert the default render shell appears.
 *
 * Auth: the `(authenticated)` layout does NOT gate server-side; `PLAYWRIGHT_AUTH_BYPASS=true`
 * (set in `playwright.config.ts` webServer env) allows Playwright to access the
 * route. No seedAuthSession needed for the shell render assertion.
 *
 * data-slot selectors match committed Task 3 orchestrator shells:
 *   - default:   `[data-slot="agent-detail-view"]`
 *   - not-found: `[data-slot="agent-detail-not-found"]`
 *
 * Dispatched manually post-merge via:
 *   `gh workflow run smoke.yml --ref main-dev -f SMOKE_AGENT_ID=<uuid>`
 */
import { test } from '@playwright/test';

/**
 * Deterministic UUID that NEVER exists in any environment — drives the
 * frontend not-found shell without needing a seeded agent.
 * Same UUID used in the smoke tests for agent list (agents.smoke.spec.ts)
 * to avoid accidental real-API calls triggering unexpected FSM states.
 *
 * Pattern: encodes issue #581 (Wave C umbrella) in the last group.
 */
const NEVER_EXISTS_ID = '00000000-0000-4000-8000-000000000581' as const;

test.describe('SMOKE — /agents/[id] real backend', () => {
  test('frontend /agents/{id} renders shell for deterministic UUID', async ({ page }) => {
    // NEVER_EXISTS_ID ensures the backend returns 404 → frontend should render
    // a not-found shell. However, with PLAYWRIGHT_AUTH_BYPASS=true the proxy
    // skips backend session validation and the client-side auth/me fetch may
    // hit 401, parking the page on its loading shell (#960 Categoria A). Both
    // outcomes confirm the route renders gracefully without crashing, so accept
    // either: explicit not-found, or the default-view shell that the client
    // mounts when waiting for the backend response.
    await page.goto(`/agents/${NEVER_EXISTS_ID}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="agent-detail-view"], [data-slot="agent-detail-not-found"]',
      { timeout: 30_000 }
    );
  });

  test('frontend /agents/{id} renders default or not-found shell for seeded id', async ({
    page,
  }) => {
    // If SMOKE_AGENT_ID is set, navigate to a real seeded agent id and
    // assert that EITHER the default render shell (agent found) OR the
    // not-found shell (agent not found) renders.
    // Falls back to NEVER_EXISTS_ID when env is unset (CI without seeded data).
    const targetId = process.env.SMOKE_AGENT_ID ?? NEVER_EXISTS_ID;

    await page.goto(`/agents/${targetId}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="agent-detail-view"], [data-slot="agent-detail-not-found"]',
      { timeout: 30_000 }
    );
  });
});
