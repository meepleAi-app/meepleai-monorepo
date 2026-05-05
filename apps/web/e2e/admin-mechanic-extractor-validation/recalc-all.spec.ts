/**
 * E2E — Mechanic Extractor AI Validation: Recalc-all async pipeline
 * (ADR-051 Sprint 2 / Task 29).
 *
 * Verifies the operator end-to-end loop on
 * `/admin/knowledge-base/mechanic-extractor/dashboard`:
 *
 *  1. Click `Recalculate all` → page fires `POST /metrics/recalculate-all`,
 *     receives a 202 Accepted with `{ jobId }`, and mounts the
 *     `RecalcProgressDrawer` inline.
 *  2. The drawer polls `GET /metrics/recalc-jobs/{jobId}` every 2s. The mock
 *     here drives a stateful counter so successive polls return:
 *       call 1 → Pending  (processed=0,   total=100)
 *       call 2 → Running  (processed=50,  total=100)  // 50% progress bar
 *       call 3+ → Completed (processed=100, total=100)
 *     Each transition is asserted via the `processed / total` text in the
 *     drawer body.
 *  3. On `Completed` the polling stops (`refetchInterval` returns `false`)
 *     and the drawer fires the latched terminal toast
 *     `Recalculated 100 analyses` exactly once.
 *
 * Backend wire format: `RecalcJobStatusDto` (Sprint 2 Task 21) — the mock
 * payload mirrors all required fields from the zod schema (id, status,
 * triggeredByUserId, counters, timestamps, etaSeconds). The spec runs against
 * fully-mocked routes — no live worker, no DB — so it's hermetic and stable
 * in CI. The 2s poll interval (`useRecalcJobStatus.POLL_INTERVAL_MS`) means
 * the test naturally waits ~6s for all three transitions.
 *
 * Feature flag: requires `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true`
 * at server start. Forwarded by `playwright.config.ts` webServer env.
 */

import { test, expect, type Page, type Route } from '@playwright/test';

import { setupMockAuth } from '../fixtures/auth';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const DASHBOARD_PATH = '/admin/knowledge-base/mechanic-extractor/dashboard';

// Stable UUIDs — `EnqueueRecalcAllResponseSchema.jobId` and
// `RecalcJobStatusDtoSchema.id` / `triggeredByUserId` all require uuid().
const JOB_ID = '99999999-aaaa-bbbb-cccc-111122223333';
const TRIGGERED_BY = '00000000-0000-0000-0000-000000000001';

// The job is sized to 100 analyses so the running-state progress (50/100)
// renders cleanly as 50% on the progress bar.
const TOTAL = 100;

/**
 * Builds a `RecalcJobStatusDto`-shaped payload mirroring the zod schema.
 * Counters and timestamps are filled per status to match the contract a
 * real `MechanicRecalcBackgroundService` would emit:
 *  - Pending   → no startedAt yet, no etaSeconds
 *  - Running   → startedAt + heartbeatAt set, etaSeconds populated
 *  - Completed → startedAt + completedAt + heartbeatAt all set
 */
function buildStatusPayload(
  status: 'Pending' | 'Running' | 'Completed',
  processed: number
): Record<string, unknown> {
  const createdAt = '2026-04-25T10:00:00Z';
  const startedAt = status === 'Pending' ? null : '2026-04-25T10:00:01Z';
  const completedAt = status === 'Completed' ? '2026-04-25T10:00:10Z' : null;
  const heartbeatAt = status === 'Pending' ? null : '2026-04-25T10:00:05Z';
  const etaSeconds = status === 'Running' ? 5 : null;

  return {
    id: JOB_ID,
    status,
    triggeredByUserId: TRIGGERED_BY,
    total: TOTAL,
    processed,
    failed: 0,
    skipped: 0,
    consecutiveFailures: 0,
    lastError: null,
    cancellationRequested: false,
    createdAt,
    startedAt,
    completedAt,
    heartbeatAt,
    etaSeconds,
  };
}

/**
 * Stubs the dashboard page's data endpoints + the recalc enqueue/status
 * pair. The status endpoint uses a stateful counter so successive polls
 * return Pending → Running → Completed (the third call onward is sticky on
 * Completed; the hook stops polling at terminal anyway).
 *
 * Returns `getStatusCallCount()` so the test can assert that polling
 * stopped after the terminal status was observed.
 */
async function mockRecalcRoutes(page: Page) {
  let statusCallCount = 0;
  let postCount = 0;

  // Dashboard rows + thresholds — needed for the page to render past the
  // loading skeleton. We stub both with hermetic minimal payloads; this
  // test doesn't care about their content.
  await page
    .context()
    .route(`${API_BASE}/api/v1/admin/mechanic-extractor/dashboard`, async (route: Route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
        return;
      }
      await route.continue();
    });

  await page
    .context()
    .route(`${API_BASE}/api/v1/admin/mechanic-extractor/thresholds`, async (route: Route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            minCoveragePct: 70,
            maxPageTolerance: 1,
            minBggMatchPct: 60,
            minOverallScore: 75,
          }),
        });
        return;
      }
      await route.continue();
    });

  // POST /metrics/recalculate-all → 202 Accepted with the canonical body.
  await page
    .context()
    .route(
      `${API_BASE}/api/v1/admin/mechanic-extractor/metrics/recalculate-all`,
      async (route: Route) => {
        if (route.request().method() === 'POST') {
          postCount += 1;
          await route.fulfill({
            status: 202,
            contentType: 'application/json',
            headers: {
              Location: `/api/v1/admin/mechanic-extractor/metrics/recalc-jobs/${JOB_ID}`,
            },
            body: JSON.stringify({ jobId: JOB_ID }),
          });
          return;
        }
        await route.continue();
      }
    );

  // GET /metrics/recalc-jobs/{jobId} → progressively Pending → Running → Completed.
  await page
    .context()
    .route(
      `${API_BASE}/api/v1/admin/mechanic-extractor/metrics/recalc-jobs/${JOB_ID}`,
      async (route: Route) => {
        if (route.request().method() === 'GET') {
          statusCallCount += 1;
          let payload: Record<string, unknown>;
          if (statusCallCount === 1) {
            payload = buildStatusPayload('Pending', 0);
          } else if (statusCallCount === 2) {
            payload = buildStatusPayload('Running', 50);
          } else {
            payload = buildStatusPayload('Completed', TOTAL);
          }
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(payload),
          });
          return;
        }
        await route.continue();
      }
    );

  return {
    getStatusCallCount: () => statusCallCount,
    getPostCount: () => postCount,
  };
}

test.describe('Admin · Mechanic Extractor · Mass recalculation', () => {
  test('admin enqueues recalc, drawer renders Pending → Running → Completed and surfaces the success toast', async ({
    page,
  }) => {
    await setupMockAuth(page, 'Admin', 'admin@meepleai.dev');
    const mocks = await mockRecalcRoutes(page);

    await page.goto(DASHBOARD_PATH);
    await page.waitForLoadState('networkidle');

    // Page header trigger — fires the enqueue and forwards the jobId to the
    // dashboard's local `activeJobId` state.
    const recalcButton = page.getByTestId('recalc-all-button');
    await expect(recalcButton).toBeVisible();
    await recalcButton.click();

    // Drawer mounts as soon as `setActiveJobId` runs. The first poll lands
    // ~immediately (TanStack Query's initial fetch), so the Pending state
    // is the first thing the operator sees.
    const drawer = page.getByTestId('recalc-progress-drawer');
    await expect(drawer).toBeVisible();

    // Pending — processed=0, total=100. Status label uses the raw enum text
    // (uppercase via CSS, but the DOM still carries the original casing).
    await expect(drawer.getByText('0 / 100')).toBeVisible();
    await expect(drawer.getByText('Pending')).toBeVisible();

    // Running — drawer re-renders after the 2s poll lands the next snapshot.
    // Test timeout (60s local / 90s CI) easily covers the wait.
    await expect(drawer.getByText('50 / 100')).toBeVisible();
    await expect(drawer.getByText('Running')).toBeVisible();

    // Completed — third poll. The drawer also reveals the close button
    // (only rendered when `isTerminal(data.status)` is true), so we assert
    // it as a defense-in-depth signal that we hit the terminal path.
    await expect(drawer.getByText('100 / 100')).toBeVisible();
    await expect(drawer.getByText('Completed')).toBeVisible();
    await expect(page.getByTestId('recalc-progress-drawer-close')).toBeVisible();

    // Sonner success toast — fired exactly once on the Pending/Running →
    // Completed transition (latched via `lastToastedTerminalRef`). The
    // string here matches `RecalcProgressDrawer` line 92 verbatim.
    await expect(page.getByText('Recalculated 100 analyses')).toBeVisible();

    // Polling halted on terminal status. We tolerate ≥3 calls (one per
    // status transition) with a small upper bound — extra calls would
    // indicate `refetchInterval` failed to return `false` on terminal,
    // which would be a regression in `useRecalcJobStatus`.
    expect(mocks.getPostCount()).toBe(1);
    expect(mocks.getStatusCallCount()).toBeGreaterThanOrEqual(3);
    expect(mocks.getStatusCallCount()).toBeLessThanOrEqual(5);
  });
});
