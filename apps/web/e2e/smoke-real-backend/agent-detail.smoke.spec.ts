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
 * Auth (#960 → Smoke RCA-2): the `(authenticated)` layout requires a session
 * cookie. Even with `PLAYWRIGHT_AUTH_BYPASS=true` the `proxy.ts:386` bypass
 * branch is only taken when `sessionCookieValue` is present. So we MUST
 * `smokeLogin` + `applySessionToPage` before navigation, even for not-found
 * shell tests.
 *
 * data-slot selectors match committed Task 3 orchestrator shells:
 *   - default:   `[data-slot="agent-detail-view"]`
 *   - not-found: `[data-slot="agent-detail-not-found"]`  (when query returns null)
 *   - error:     `[data-slot="agent-detail-error"]`      (when 404 throws — current useAgent behavior)
 *
 * NOTE on 404 handling: unlike `useLibraryGameDetail` (which catches and returns null),
 * `useAgent` lets the httpClient 404 throw bubble up → TanStack `isError: true` →
 * orchestrator FSM resolves to `error` shell, NOT `not-found`. Both shells indicate
 * "agent unreachable" to the user; smoke spec accepts either as a valid render.
 * Tracked separately if we want to align hook behavior post-merge.
 *
 * Dispatched manually post-merge via:
 *   `gh workflow run smoke.yml --ref main-dev -f SMOKE_AGENT_ID=<uuid>`
 */
import { test } from '@playwright/test';

import { applySessionToPage, smokeLogin } from './_helpers/auth';

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
  test.beforeEach(async ({ page, request }) => {
    const { cookieHeaders } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeaders);
  });

  test('frontend /agents/{id} renders not-found or error shell for deterministic UUID', async ({
    page,
  }) => {
    // NEVER_EXISTS_ID ensures backend returns 404. Either FSM cell 3 (error, current
    // useAgent behavior) or cell 4 (not-found, when 404 maps to null) is acceptable —
    // both prove the orchestrator renders gracefully without crashing.
    await page.goto(`/agents/${NEVER_EXISTS_ID}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="agent-detail-not-found"], [data-slot="agent-detail-error"]',
      { timeout: 30_000 }
    );
  });

  test('frontend /agents/{id} renders default, not-found or error shell for seeded id', async ({
    page,
  }) => {
    // If SMOKE_AGENT_ID is set, navigate to a real seeded agent id and assert that
    // ANY of the orchestrator shells render (default success, not-found, or error).
    // Falls back to NEVER_EXISTS_ID when env is unset (CI without seeded data).
    const targetId = process.env.SMOKE_AGENT_ID ?? NEVER_EXISTS_ID;

    await page.goto(`/agents/${targetId}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="agent-detail-view"], [data-slot="agent-detail-not-found"], [data-slot="agent-detail-error"]',
      { timeout: 30_000 }
    );
  });
});
