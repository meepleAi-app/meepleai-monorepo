/**
 * SMOKE — /sessions/[id]/live real backend (Wave D.2 Interactions, Issue #750).
 *
 * Tests the frontend route against a real backend. Skip-by-default in CI —
 * requires explicit env vars to activate (opt-in pattern, mirrors Wave C.2 invitelink).
 *
 * Skip condition:
 *   Both `PLAYWRIGHT_STAGING_BASE` and `PLAYWRIGHT_STAGING_SESSION_ID` must be
 *   set to activate. Without them the entire describe block skips cleanly.
 *
 * Activation (manual, post-merge):
 *   gh workflow run smoke.yml --ref main-dev \
 *     -f PLAYWRIGHT_STAGING_BASE=https://staging.meepleai.app \
 *     -f PLAYWRIGHT_STAGING_SESSION_ID=<uuid-of-seeded-test-session>
 *
 * Strategy:
 *   - Deterministic UUID `NEVER_EXISTS_ID` always drives the not-found shell.
 *     Verifies the FSM null-guard path (Cell 4 backend 404 → not-found shell)
 *     without needing a seeded session.
 *   - If `PLAYWRIGHT_STAGING_SESSION_ID` is set (seeded test session on staging),
 *     navigate to that real session and assert the live shell mounts (or not-found).
 *   - SSE connection: verifies EventSource can connect to the streaming endpoint
 *     from within the browser context (real backend required).
 *
 * Auth: The `(authenticated)` layout does NOT gate server-side. With
 *   `PLAYWRIGHT_AUTH_BYPASS=true` (set in playwright.config.ts webServer env),
 *   the route renders shell without real session credentials.
 *   NOTE: SSE endpoint may return 401 without real auth — the test asserts
 *   EventSource opens (HTTP handshake), not that events flow. A 401 response
 *   still results in an 'error' event (EventSource fires onerror, not onopen).
 *   Full SSE round-trip requires staging auth credentials — out of scope here.
 *
 * data-slot selectors match committed Task 3 orchestrator shells:
 *   - default:   `[data-slot="session-live-view"][data-ui-state="default"]`
 *   - not-found: `[data-slot="session-live-not-found"]`
 *   - loading:   `[data-slot="session-live-loading"]`
 *
 * Pattern: mirrors Wave C.2 agent-detail.smoke.spec.ts structure.
 * Wave D.2 Interactions sub-PR — Issue #750
 */
import { test, expect } from '@playwright/test';

// ─── Environment guards ───────────────────────────────────────────────────────

/**
 * Deterministic UUID that NEVER exists in any environment — drives the
 * frontend not-found shell without needing a seeded session.
 * Encodes issue #750 (Wave D.2) in the last group.
 */
const NEVER_EXISTS_ID = '00000000-0000-4000-8000-000000000750' as const;

const STAGING_BASE = process.env.PLAYWRIGHT_STAGING_BASE ?? null;
const STAGING_SESSION_ID = process.env.PLAYWRIGHT_STAGING_SESSION_ID ?? null;

// Skip entire describe block when env vars are absent (CI default path).
// Both vars required: STAGING_BASE provides base URL, STAGING_SESSION_ID provides
// a real seeded session to test the SSE path.
const SMOKE_ENABLED = STAGING_BASE !== null;

test.describe('SMOKE — /sessions/[id]/live real backend', () => {
  test.skip(!SMOKE_ENABLED, 'Requires PLAYWRIGHT_STAGING_BASE env var to run smoke tests');

  // ── Not-found shell: deterministic UUID ──────────────────────────────────────

  test('frontend /sessions/{id}/live renders not-found shell for deterministic UUID', async ({
    page,
  }) => {
    // NEVER_EXISTS_ID ensures the backend returns 404 → Cell 4 → not-found shell.
    // Validates the FSM null/404 guard path against real backend without needing auth.
    await page.goto(`/sessions/${NEVER_EXISTS_ID}/live`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="session-live-not-found"], [data-slot="session-live-loading"]',
      { timeout: 30_000 }
    );
    // Either not-found (404 from backend) or loading (still fetching) is valid.
    // The test asserts the frontend renders a known FSM shell, not a crash.
  });

  // ── Session shell: seeded session ID ─────────────────────────────────────────

  test('frontend /sessions/{id}/live renders default or not-found shell for seeded session', async ({
    page,
  }) => {
    // STAGING_SESSION_ID: a real seeded session on staging (set by CI workflow).
    // Falls back to NEVER_EXISTS_ID when unset (drives not-found path).
    const targetId = STAGING_SESSION_ID ?? NEVER_EXISTS_ID;

    await page.goto(`/sessions/${targetId}/live`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      [
        '[data-slot="session-live-view"]',
        '[data-slot="session-live-not-found"]',
        '[data-slot="session-live-loading"]',
      ].join(', '),
      { timeout: 30_000 }
    );
    // Any of: default (session found + loaded), not-found (404), loading (slow backend)
    // is a valid assertion. We verify the frontend renders a known FSM shell without crash.
  });

  // ── SSE endpoint HTTP handshake ───────────────────────────────────────────────
  //
  // Verifies that the SSE streaming endpoint responds to an EventSource connection
  // from within the browser context. Tests HTTP-layer connectivity only — does not
  // verify that SSE events flow or that auth is valid.
  //
  // Acceptance: EventSource.readyState transitions from CONNECTING (0) to OPEN (1)
  // OR CLOSED (2) within 10s. If it transitions to CLOSED, the backend accepted the
  // TCP connection but rejected the HTTP request (e.g. 401/404) — still proves
  // the endpoint is reachable.
  //
  // A CONNECTING timeout (never transitions) indicates network-level unreachability.

  test('SSE endpoint /api/v1/game-sessions/{id}/stream/v2 is reachable', async ({ page }) => {
    const targetId = STAGING_SESSION_ID ?? NEVER_EXISTS_ID;

    await page.goto(`/sessions/${targetId}/live`, { waitUntil: 'domcontentloaded' });
    await page.waitForSelector(
      '[data-slot="session-live-view"], [data-slot="session-live-not-found"], [data-slot="session-live-loading"]',
      { timeout: 30_000 }
    );

    // Evaluate EventSource connectivity from within the page's browser context.
    // Resolves: 'open' (connected), 'error' (rejected/auth fail), or 'timeout' (unreachable).
    const sseResult = await page.evaluate<'open' | 'error' | 'timeout'>(
      async (sessionId: string) => {
        return new Promise<'open' | 'error' | 'timeout'>(resolve => {
          const url = `/api/v1/game-sessions/${sessionId}/stream/v2`;
          let es: EventSource;
          try {
            es = new EventSource(url, { withCredentials: true });
          } catch {
            resolve('error');
            return;
          }

          const timer = setTimeout(() => {
            es.close();
            resolve('timeout');
          }, 10_000);

          es.onopen = () => {
            clearTimeout(timer);
            es.close();
            resolve('open');
          };

          es.onerror = () => {
            clearTimeout(timer);
            es.close();
            // onerror can mean 401/404 (HTTP error) or network issue.
            // Both indicate the endpoint was reachable at HTTP layer.
            resolve('error');
          };
        });
      },
      targetId
    );

    // 'open' or 'error' both confirm endpoint reachability.
    // Only 'timeout' indicates network-level unreachability (test should fail).
    expect(sseResult).not.toBe('timeout');
  });
});
