/**
 * SMOKE — /sessions/[id]/live SSE stream surface (Issue #2561 SP2 T11).
 *
 * Two concerns:
 *
 *   A) The CANONICAL stream route `/api/v1/live-sessions/{id}/stream` (T4) is the
 *      target for new consumers and must be reachable from within the page context.
 *
 *   B) The LEGACY route `/api/v1/game-sessions/{id}/stream/v2` is deprecated
 *      (expand-and-contract, T11 Part B) but still functional. The SessionLiveView
 *      surface (post T10 wiring) must NOT be calling it for SSE — it must exclusively
 *      use the canonical `/live-sessions/{id}/stream` route.
 *
 * ## Anti-pattern avoided
 * The previous false-green pattern (`page.route('**/ game - sessions; /**', abort)`) was
 * a blind abort that would pass even if the page never hit that route at all. This
 * spec uses EXPLICIT route intercept with request tracking to assert:
 *   - native stream: intercepted and counted (≥ 1 call).
 *   - legacy stream/v2: intercepted and counted → must be ZERO calls.
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

  // ── A2) Explicit: SessionLiveView MUST NOT call the deprecated /stream/v2 ─

  test('A2: SessionLiveView does NOT call /game-sessions/{id}/stream/v2 (deprecation enforcement)', async ({
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

  // ── B) Deprecation headers on the legacy endpoint (documented expectation) ─

  test('B1 [doc-test]: /game-sessions/{id}/stream/v2 deprecation-header SHAPE (real gate is the BE integration test)', async ({
    page,
  }) => {
    // DOC-TEST (#2565): tautological by design — it asserts the shape of the FE-provided mock
    // response, NOT real BE behavior. The authoritative gate for the deprecation headers is the BE
    // integration test StreamV2DeprecationHeadersEndpointTests.cs. Kept as living FE documentation
    // of the expected response shape during legacy-consumer migration.
    test.info().annotations.push({
      type: 'doc-test',
      description:
        'Asserts the FE mock shape, not BE behavior. Authoritative gate: StreamV2DeprecationHeadersEndpointTests.cs (BE).',
    });

    // This test documents the EXPECTED deprecation headers on the /stream/v2 endpoint
    // (Part B of T11). It intercepts the route and inspects what the BROWSER sees
    // in the response headers via network request listener.
    //
    // NOTE: In E2E tests with a mocked backend (PLAYWRIGHT_AUTH_BYPASS), the actual
    // /stream/v2 endpoint is not reached — the FE mocks intercept it. The real header
    // assertions are covered by the BE integration test
    // (StreamV2DeprecationHeadersEndpointTests.cs, CI-needed).
    //
    // This test instead verifies the SHAPE of a correctly-deprecated response via a
    // page.evaluate() EventSource call to the mocked route — confirming that when the
    // FE DOES call the legacy route (e.g., during legacy consumer migration), the response
    // it would receive in production carries the right headers.
    //
    // For the E2E harness: we set up a mock fulfillment that mirrors what the real BE
    // will serve (Deprecation: true, Sunset, Link), then assert the request was made and
    // the mock response structure is correct.

    let legacyResponseHeaders: Record<string, string> = {};

    // Mock the legacy route with the headers the real BE serves (T11 Part B).
    await page.route(LEGACY_STREAM_V2_PATTERN, async route => {
      const headers: Record<string, string> = {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive',
        // Mirrors the actual headers added in SessionQueryEndpoints.cs (T11 Part B).
        Deprecation: 'true',
        Sunset: 'Mon, 29 Sep 2026 00:00:00 GMT',
        Link: `</api/v1/live-sessions/${FIXTURE_SESSION_ID}/stream>; rel="successor-version"`,
      };
      legacyResponseHeaders = headers;
      await route.fulfill({
        status: 200,
        headers,
        body: ': legacy-deprecated\n\n',
      });
    });

    // Navigate to the page first so we have a page context for page.evaluate.
    await seedAuth(page);
    await page.goto(`/sessions/${FIXTURE_SESSION_ID}/live?fixture=host`, {
      waitUntil: 'domcontentloaded',
    });
    await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
      timeout: 30_000,
    });

    // Programmatically hit the legacy route via fetch() inside the browser
    // (EventSource headers are not readable from JS, but fetch() response headers are).
    const deprecationHeaders = await page.evaluate<{
      deprecation: string | null;
      sunset: string | null;
      link: string | null;
      status: number;
    }>(async sessionId => {
      try {
        // fetch with streaming mode (we only care about headers, not the body).
        const res = await fetch(`/api/v1/game-sessions/${sessionId}/stream/v2`, {
          credentials: 'include',
          headers: { Accept: 'text/event-stream' },
        });
        return {
          deprecation: res.headers.get('Deprecation'),
          sunset: res.headers.get('Sunset'),
          link: res.headers.get('Link'),
          status: res.status,
        };
      } catch {
        return { deprecation: null, sunset: null, link: null, status: 0 };
      }
    }, FIXTURE_SESSION_ID);

    // The mock route fulfills with a 200 and the deprecation headers.
    expect(deprecationHeaders.status).toBe(200);

    // Assert the mock-fulfilled response carries the correct deprecation headers.
    // These values must match what the real BE sets (SessionQueryEndpoints.cs T11 Part B).
    expect(deprecationHeaders.deprecation).toBe('true');
    expect(deprecationHeaders.sunset).toBe('Mon, 29 Sep 2026 00:00:00 GMT');
    expect(deprecationHeaders.link).toContain(`/api/v1/live-sessions/${FIXTURE_SESSION_ID}/stream`);
    expect(deprecationHeaders.link).toContain('successor-version');

    // Confirm the mock was exercised (legacyResponseHeaders populated).
    expect(legacyResponseHeaders).toMatchObject({
      Deprecation: 'true',
      Sunset: 'Mon, 29 Sep 2026 00:00:00 GMT',
    });
  });
});
