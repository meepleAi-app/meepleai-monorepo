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
 * Auth: the `(authenticated)` layout does NOT gate server-side; `PLAYWRIGHT_AUTH_BYPASS=true`
 * (set in `playwright.config.ts` webServer env) allows Playwright to access the
 * route. No seedAuthSession needed for the shell render assertion.
 *
 * data-slot selectors match committed Task 3 orchestrator shells:
 *   - default:   `[data-slot="player-detail-view"]`
 *   - not-found: `[data-slot="player-detail-not-found"]`
 *
 * Dispatched manually post-merge via:
 *   `gh workflow run smoke.yml --ref main-dev -f SMOKE_PLAYER_ID=<slug>`
 */
import { test, expect } from '@playwright/test';

/**
 * Deterministic slug that NEVER maps to a real player in any environment.
 * Drives the frontend not-found shell without needing a seeded player.
 * Uses a recognizable pattern to avoid accidental real-API calls triggering
 * unexpected FSM states.
 */
const NEVER_EXISTS_ID = 'never-exists-player-id-smoke-test' as const;

test.describe('SMOKE — /players/[id] real backend', () => {
  test('frontend /players/{id} renders not-found shell for deterministic slug', async ({
    page,
  }) => {
    // NEVER_EXISTS_ID ensures the backend returns no statistics → frontend renders
    // not-found shell. Validates the FSM not-found path against real backend.
    await page.goto(`/players/${NEVER_EXISTS_ID}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector('[data-slot="player-detail-not-found"]', { timeout: 30_000 });
    await expect(page.locator('[data-slot="player-detail-not-found-cta"]')).toBeVisible();
  });

  test('frontend /players/{id} renders hero or not-found shell for seeded slug', async ({
    page,
  }) => {
    // If SMOKE_PLAYER_ID is set, navigate to a real seeded player slug and
    // assert that EITHER the default render shell (player found — statistics present)
    // OR the not-found shell (no statistics for this slug) renders.
    // Falls back to NEVER_EXISTS_ID when env is unset (CI without seeded data).
    const targetId = process.env.SMOKE_PLAYER_ID ?? NEVER_EXISTS_ID;

    await page.goto(`/players/${targetId}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="player-detail-view"], [data-slot="player-detail-not-found"]',
      { timeout: 30_000 }
    );
  });
});
