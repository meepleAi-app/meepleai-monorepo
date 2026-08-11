/**
 * SMOKE — /sessions/[id]/live SSE stream surface (Issue #2561 SP2 T11).
 *
 * Two concerns:
 *
 *   A) The CANONICAL stream route `/api/v1/live-sessions/{id}/stream` (T4) is the
 *      target for new consumers and must be reachable from within the page context.
 *
 *   B) The LEGACY route `/api/v1/game-sessions/{id}/stream/v2` was deprecated
 *      (expand-and-contract, T11 Part B) and has been **removed early 2026-07-01**
 *      (Slice B, #2588) ahead of the published Sunset 2026-09-29, by owner decision
 *      (zero remaining consumers verified). The A2 guard below is a permanent
 *      regression guard — now that the endpoint is gone it only strengthens the
 *      assertion that no consumer re-introduces a call to the removed route.
 *
 * ## Anti-pattern avoided
 * The previous false-green pattern (`page.route('**/ game - sessions; /**', abort)`) was
 * a blind abort that would pass even if the page never hit that route at all. This
 * spec uses EXPLICIT route intercept with request tracking to assert:
 *   - native stream: intercepted and counted (≥ 1 call).
 *   - legacy stream/v2 (removed): intercepted and counted → must be ZERO calls.
 *
 * ## E2E vs real backend
 * These tests run against the Next.js dev server with mocked backend routes
 * (PLAYWRIGHT_AUTH_BYPASS=true). The SSE endpoints are intercepted at the
 * Playwright network layer — no real backend or real SSE stream is needed.
 * For real-backend connectivity tests see:
 *   apps/web/e2e/smoke-real-backend/session-live.smoke.spec.ts
 *
 * ## Auth pattern
 * Triple-helper pattern (seedAuthSession + seedCookieConsent + mockAuthEndpoints)
 * required for `(authenticated)` routes — identical to session-live-mobile.spec.ts
 * and session-live-chat-agent-g3.spec.ts.
 *
 * ## Fixture session
 * `?fixture=host` activates STATE_OVERRIDE_ENABLED visual-test mode (NODE_ENV !== 'production'),
 * which renders the session with deterministic data and avoids backend calls.
 * This is the same fixture used by session-live-chat-agent-g3.spec.ts.
 *
 * Issue #2561 SP2 T11.
 */

import { expect, test, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

// SessionLiveView applies data-theme="dark" — next-themes also applies .dark class.
test.use({ colorScheme: 'dark' });

/** Sentinel session ID matching VISUAL_TEST_FIXTURE_SESSION.id. */
const FIXTURE_SESSION_ID = '00000000-0000-4000-8000-000000000d20' as const;

/** Canonical native SSE route (T4, Issue #2561 SP2). */
const NATIVE_STREAM_PATTERN = /\/api\/v1\/live-sessions\/[^/]+\/stream(\?.*)?$/;

/** Legacy SSE route, deprecated T11 expand-and-contract. */
const LEGACY_STREAM_V2_PATTERN = /\/api\/v1\/game-sessions\/[^/]+\/stream\/v2(\?.*)?$/;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

/**
 * Navigate to the fixture-loaded session-live page and wait for the shell to render.
 * Mirrors gotoSessionLive in session-live-chat-agent-g3.spec.ts.
 */
async function gotoFixtureLivePage(page: Page, extraSearch = ''): Promise<void> {
  await seedAuth(page);
  await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=host${extraSearch}`, {
    waitUntil: 'domcontentloaded',
  });
  await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
    timeout: 30_000,
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('#2561 SP2 T11 — Session live SSE stream surface smoke', () => {
  // ── A) Canonical native stream reachability ────────────────────────────────

  test('A1: canonical /live-sessions/{id}/stream is intercepted (mock route handler, not blind abort)', async ({
    page,
  }) => {
    // KNOWN GAP (#2565): in fixture mode nativeStreamCallCount cannot reach >= 1 (no real backend
    // session), so this test's real enforcement is only the co-located A2 contract below. The
    // native-intercept assertion is deferred to the smoke-real-backend/ harness — tagged as a
    // machine-readable annotation so the gap is visible in the Playwright report.
    test.info().annotations.push({
      type: 'known-gap',
      description:
        'nativeStreamCallCount >= 1 not assertable in fixture mode; deferred to smoke-real-backend/ (#2565).',
    });

    let nativeStreamCallCount = 0;

    // Explicit route handler for the canonical stream endpoint.
    // Fulfills with a minimal SSE `200 text/event-stream` response so the
    // page's EventSource-like consumer gets a valid HTTP layer response.
    // This is NOT a blind abort — it is a tracked intercept that records calls.
    await page.route(NATIVE_STREAM_PATTERN, async route => {
      nativeStreamCallCount++;
      await route.fulfill({
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          Connection: 'keep-alive',
        },
        body: ': connected\n\n',
      });
    });

    // Intercept the legacy route to detect any stray calls (should be zero).
    let legacyStreamV2CallCount = 0;
    await page.route(LEGACY_STREAM_V2_PATTERN, async route => {
      legacyStreamV2CallCount++;
      // Fulfill (not abort) so we don't mask call detection via network errors.
      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: ': legacy-connected\n\n',
      });
    });

    await gotoFixtureLivePage(page);

    // Allow a brief settling window for any deferred SSE connections.
    await page.waitForTimeout(500);

    // NOTE (fixture mode — intentional relaxation):
    // `?fixture=host` activates STATE_OVERRIDE_ENABLED which drives the session shell
    // from deterministic in-memory state without making any backend calls.
    // The SessionLiveView SSE connection is gated on a non-placeholder session ID
    // that resolves to a live backend session; in fixture mode that gate is never
    // satisfied, so nativeStreamCallCount is reliably 0.
    //
    // We cannot assert `>= 1` here without a real backend + real session.
    // The primary enforcement gate for this test is A2 (legacy calls = 0), which
    // is always checkable regardless of fixture mode.
    // Strengthen to `>= 1` once a real-backend E2E harness (smoke-real-backend/)
    // is wired for session-live (see apps/web/e2e/smoke-real-backend/).
    //
    // Do NOT replace this comment with `expect(nativeStreamCallCount).toBeGreaterThanOrEqual(0)` —
    // that assertion is tautological (any integer satisfies it) and provides no safety net.

    // A2 (co-located): legacy /stream/v2 MUST NOT be called by the live surface.
    expect(legacyStreamV2CallCount).toBe(0);
  });

  // ── A2) Explicit: SessionLiveView MUST NOT call the removed /stream/v2 ────

  test('A2: SessionLiveView does NOT call /game-sessions/{id}/stream/v2 (removed, regression guard)', async ({
    page,
  }) => {
    // Track any calls to the legacy deprecated route.
    const legacyCalls: string[] = [];
    await page.route(LEGACY_STREAM_V2_PATTERN, async route => {
      legacyCalls.push(route.request().url());
      // Fulfill to avoid masking the call through a network error.
      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream' },
        body: ': legacy\n\n',
      });
    });

    // Also intercept native stream to prevent it from hanging the page navigation.
    await page.route(NATIVE_STREAM_PATTERN, async route => {
      await route.fulfill({
        status: 200,
        headers: { 'Content-Type': 'text/event-stream', 'Cache-Control': 'no-cache' },
        body: ': native-connected\n\n',
      });
    });

    await gotoFixtureLivePage(page);

    // Settle: allow time for any deferred network requests to fire.
    await page.waitForTimeout(1_000);

    // Primary contract: zero calls to the deprecated legacy route.
    expect(legacyCalls).toHaveLength(0);
  });
});
