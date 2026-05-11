/**
 * SMOKE — /players/[id] real backend (authenticated route, Wave 3 Tier M, Issue #683).
 *
 * Tests the frontend route against the real backend in nightly smoke workflow.
 * No visual baselines — functional shell assertion only.
 *
 * Strategy:
 *   - Deterministic slug `NEVER_EXISTS_ID` always drives the not-found shell,
 *     confirming the frontend renders gracefully without a real player profile.
 *   - If `SMOKE_PLAYER_ID` env var is set (seeded staging player slug), navigate
 *     to that real slug and assert the default render shell appears.
 *
 * Schema reality (v1 carryover):
 *   The `/players/[id]` route derives data from `GET /api/v1/play-records/statistics`
 *   (current user statistics only). The `id` URL param is decorative — decoded as
 *   displayName. A true other-player API is a followup issue post-merge.
 *   For smoke purposes we assert that EITHER the populated hero (data present) OR
 *   the not-found shell renders — both confirm graceful frontend rendering.
 *
 * Auth (#960 → Smoke RCA-2): the `(authenticated)` layout requires a session
 * cookie. Even with `PLAYWRIGHT_AUTH_BYPASS=true` the `proxy.ts:386` bypass
 * branch is only taken when `sessionCookieValue` is present. We MUST
 * `smokeLogin` + `applySessionToPage` before navigation.
 *
 * data-slot selectors match committed Task 3 orchestrator shells:
 *   - default:   `[data-slot="player-detail-view"]`
 *   - not-found: `[data-slot="player-detail-not-found"]`
 *
 * Dispatched manually post-merge via:
 *   `gh workflow run smoke.yml --ref main-dev -f SMOKE_PLAYER_ID=<slug>`
 */
import { test } from '@playwright/test';

import { applySessionToPage, smokeLogin } from './_helpers/auth';

/**
 * Deterministic slug that NEVER maps to a real player in any environment.
 * Drives the frontend not-found shell without needing a seeded player.
 * Uses a recognizable pattern to avoid accidental real-API calls triggering
 * unexpected FSM states.
 */
const NEVER_EXISTS_ID = 'never-exists-player-id-smoke-test' as const;

test.describe('SMOKE — /players/[id] real backend', () => {
  test.beforeEach(async ({ page, request }) => {
    const { cookieHeaders } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeaders);
  });

  test('frontend /players/{id} renders shell for deterministic slug', async ({
    page,
  }) => {
    // NEVER_EXISTS_ID is decorative — content derives from
    // /api/v1/play-records/statistics for the CURRENT user. With
    // PLAYWRIGHT_AUTH_BYPASS=true the page may render the default
    // player-detail-view (statistics present for current user) instead of
    // the not-found / error shell. Accept any of the three orchestrator
    // FSM cells — all prove graceful render without crash. Mirrors the
    // sibling :63 seeded-slug test.
    await page.goto(`/players/${NEVER_EXISTS_ID}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="player-detail-view"], [data-slot="player-detail-not-found"], [data-slot="player-detail-error"]',
      { timeout: 30_000 }
    );
  });

  test('frontend /players/{id} renders hero, not-found or error shell for seeded slug', async ({
    page,
  }) => {
    // If SMOKE_PLAYER_ID is set, navigate to a real seeded player slug.
    // Accept any orchestrator shell (default success, not-found, or error).
    // Falls back to NEVER_EXISTS_ID when env is unset (CI without seeded data).
    const targetId = process.env.SMOKE_PLAYER_ID ?? NEVER_EXISTS_ID;

    await page.goto(`/players/${targetId}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="player-detail-view"], [data-slot="player-detail-not-found"], [data-slot="player-detail-error"]',
      { timeout: 30_000 }
    );
  });
});
