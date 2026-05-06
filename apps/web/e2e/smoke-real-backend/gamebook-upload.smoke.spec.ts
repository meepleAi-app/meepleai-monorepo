/**
 * SMOKE — /gamebook/upload real backend (Issue #789, SP6 Phase C.2.D).
 *
 * Tests the frontend route against a real backend in the nightly smoke
 * workflow. Skip-by-default in CI — requires explicit env vars to activate
 * (opt-in pattern, mirrors Wave D.2 `session-live.smoke.spec.ts`).
 *
 * Skip condition:
 *   `PLAYWRIGHT_STAGING_BASE` must be set to activate the smoke checks.
 *   Without it the entire describe block skips cleanly so default CI runs
 *   (which dispatch the workflow without env vars) stay green.
 *
 * Activation (manual, post-merge):
 *   gh workflow run e2e-smoke-real-backend.yml --ref main-dev \
 *     -f PLAYWRIGHT_STAGING_BASE=https://staging.meepleai.app
 *
 * Strategy:
 *   - Step 1 of the wizard is publicly addressable via `?step=1` and does
 *     not require a real game id — the orchestrator's `useGames` query
 *     returns the staging catalog.
 *   - The smoke test asserts the wizard shell mounts (FSM cell visible)
 *     against the real backend without crashing. Camera + upload flows are
 *     out of scope for smoke (require a real device + auth credentials).
 *
 * Auth: The `(authenticated)` layout does NOT gate server-side. With
 * `PLAYWRIGHT_AUTH_BYPASS=true` (set in `playwright.config.ts` webServer env),
 * the route renders the shell without real session credentials. Real
 * `useGames` will 401 in this configuration; the orchestrator falls back to
 * the `step1-default` empty-catalog cell and the FSM shell remains visible.
 *
 * data-slot selectors match the Phase C.2.C orchestrator:
 *   - root: `[data-slot="gamebook-upload-view"]`
 *   - any FSM cell: `[data-slot="gamebook-upload-view"][data-ui-state="…"]`
 *
 * Pattern: mirrors Wave D.2 `session-live.smoke.spec.ts` env-gated structure.
 */
import { expect, test } from '@playwright/test';

const STAGING_BASE = process.env.PLAYWRIGHT_STAGING_BASE ?? null;
const SMOKE_ENABLED = STAGING_BASE !== null;

test.describe('SMOKE — /gamebook/upload real backend', () => {
  test.skip(!SMOKE_ENABLED, 'Requires PLAYWRIGHT_STAGING_BASE env var to run smoke tests');

  // ── Wizard shell mounts against real backend ─────────────────────────────
  test('frontend /gamebook/upload renders Step 1 wizard shell against real backend', async ({
    page,
  }) => {
    await page.goto('/gamebook/upload?step=1', { waitUntil: 'domcontentloaded' });
    // Either step1-default (catalog OK), step1-searching (preloaded query)
    // or any step1-* cell is acceptable — the smoke assertion is "wizard
    // mounts without crashing on real backend".
    await page.waitForSelector('[data-slot="gamebook-upload-view"]', { timeout: 30_000 });

    // Confirm the FSM emitted a known step1 cell (no error boundary, no
    // server-side redirect to /login, no exception in the orchestrator).
    const view = page.locator('[data-slot="gamebook-upload-view"]');
    await expect(view).toBeVisible();
    await expect(view).toHaveAttribute('data-ui-state', /^step1-/);
  });
});
