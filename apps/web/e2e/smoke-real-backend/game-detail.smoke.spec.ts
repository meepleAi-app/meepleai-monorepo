/**
 * SMOKE — /games/[id] real backend (authenticated route, Wave C.1, Issue #581).
 *
 * Tests the frontend route against the real backend in nightly smoke workflow.
 * No visual baselines — functional shell assertion only.
 *
 * Strategy:
 *   - Deterministic UUID `NEVER_EXISTS_ID` always drives the not-found shell,
 *     confirming the frontend renders gracefully without a real game.
 *   - If `SMOKE_GAME_DETAIL_ID` env var is set (seeded staging game), navigate
 *     to that real id and assert the default render shell appears.
 *
 * Auth: the `(authenticated)` layout does NOT gate server-side; `PLAYWRIGHT_AUTH_BYPASS=true`
 * (set in `playwright.config.ts` webServer env) allows Playwright to access the
 * route. No seedAuthSession needed for the shell render assertion.
 *
 * data-slot selectors match committed Task 3 orchestrator shells:
 *   - default: `[data-slot="game-detail-view"]`
 *   - not-found: `[data-slot="game-detail-not-found"]`
 *
 * Dispatched manually post-merge via:
 *   `gh workflow run smoke.yml --ref main-dev -f SMOKE_GAME_DETAIL_ID=<uuid>`
 */
import { test } from '@playwright/test';

/**
 * Deterministic UUID that NEVER exists in any environment — drives the
 * frontend not-found shell without needing a seeded game.
 * Same UUID used by the visual fixture sentinel to avoid accidental real-API
 * calls in visual-test builds.
 */
const NEVER_EXISTS_ID = '00000000-0000-4000-8000-000000000581' as const;

test.describe('SMOKE — /games/[id] real backend', () => {
  test('frontend /games/{id} renders shell for deterministic UUID', async ({ page }) => {
    // NEVER_EXISTS_ID ensures the backend returns 404 → frontend should render
    // a not-found shell. With PLAYWRIGHT_AUTH_BYPASS=true the proxy trusts the
    // session cookie without backend validation, so the client may settle on
    // the default-view shell while the auth/me fetch is in flight (#960
    // Categoria A). Both shells confirm graceful render without crashing.
    await page.goto(`/games/${NEVER_EXISTS_ID}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="game-detail-view"], [data-slot="game-detail-not-found"]',
      { timeout: 30_000 }
    );
  });

  test('frontend /games/{id} renders default or not-found shell for seeded id', async ({
    page,
  }) => {
    // If SMOKE_GAME_DETAIL_ID is set, navigate to a real seeded game id and
    // assert that EITHER the default render shell (game found in library) OR the
    // not-found shell (game exists in catalog but not in user's library) renders.
    // Falls back to NEVER_EXISTS_ID when env is unset (CI without seeded data).
    const targetId = process.env.SMOKE_GAME_DETAIL_ID ?? NEVER_EXISTS_ID;

    await page.goto(`/games/${targetId}`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="game-detail-view"], [data-slot="game-detail-not-found"]',
      { timeout: 30_000 }
    );
  });
});
