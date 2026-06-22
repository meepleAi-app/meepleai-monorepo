/**
 * Admin KB Quality — happy-path eval trigger (#1675 Task 31).
 *
 * Mocks the kbQuality + supporting endpoints so the test runs without a
 * live backend, LLM, or seeded data. Verifies that:
 * 1. The Quality tab renders the empty history + trigger button.
 * 2. Clicking the trigger fires POST → list invalidates → row appears.
 * 3. Selecting the row loads the detail panel with all metric tiles.
 * 4. The QualityBandChip renders (Green for the mock metrics).
 *
 * Set KB_QUALITY_E2E_SKIP=true to skip in CI environments where the page
 * shell or auth flow isn't fully reproducible.
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const SKIP_E2E = process.env.KB_QUALITY_E2E_SKIP === 'true';

const DOC_ID = '11111111-1111-1111-1111-111111111111';
const EVAL_ID = '22222222-2222-2222-2222-222222222222';
const ADMIN_ID = '99999999-9999-9999-9999-999999999999';
const GAME_ID = '33333333-3333-3333-3333-333333333333';

async function mockAdminAuth(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: { id: ADMIN_ID, email: 'admin@meepleai.dev', displayName: 'Admin', role: 'admin' },
        expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
      }),
    })
  );
}

async function mockKbDocShell(page: Page) {
  // KB tree + doc detail enough for KbDocDetailPanel to render in 'ready' state.
  // The actual tree query path is /api/v1/admin/kb/nav-counts — we no-op it so
  // the doc detail panel still mounts via the ?doc= URL parameter.
  await page
    .context()
    .route(/\/api\/v1\/admin\/kb\/.*nav-counts.*/, route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: '{}' })
    );

  await page.context().route(`${API_BASE}/api/v1/admin/kb/docs/${DOC_ID}`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        status: 'ready',
        doc: {
          id: DOC_ID,
          title: 'Test Manual.pdf',
          gameId: GAME_ID,
          gameName: 'Test Game',
          docType: 'rulebook',
          uploadedAt: '2026-06-02T10:00:00Z',
          lastIngestedAt: '2026-06-02T10:05:00Z',
          chunkCount: 42,
          pageCount: 24,
          language: 'en',
          fileSize: 1_234_567,
          processingStatus: 'ready',
          indexerVersion: 'v1',
        },
      }),
    })
  );
}

async function mockKbQualityLifecycle(page: Page) {
  let runCreated = false;

  // GET list — empty until the POST has been hit, then 1-row paged result.
  await page
    .context()
    .route(
      new RegExp(
        `${API_BASE.replace(/[.*+?^${}()|[\\]\\\\]/g, '\\$&')}/api/v1/admin/kb/docs/${DOC_ID}/evaluations\\?.*`
      ),
      route => {
        if (!runCreated) {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20 }),
          });
        }
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            items: [
              {
                evaluationId: EVAL_ID,
                startedAt: '2026-06-02T10:10:00Z',
                completedAt: '2026-06-02T10:10:45Z',
                status: 'Completed',
                goldsetVersion: 'auto-v1',
                precisionAt5: 0.8,
                mrr: 0.6,
                latencyP95Ms: 420,
                costUsd: 0.012,
                qualityBand: 'Green',
              },
            ],
            totalCount: 1,
            page: 1,
            pageSize: 20,
          }),
        });
      }
    );

  // POST — flips the gate so subsequent GETs return the persisted row.
  await page.context().route(`${API_BASE}/api/v1/admin/kb/docs/${DOC_ID}/evaluations`, route => {
    if (route.request().method() !== 'POST') {
      return route.fallback();
    }
    runCreated = true;
    return route.fulfill({
      status: 202,
      contentType: 'application/json',
      headers: { Location: `/api/v1/admin/kb/docs/${DOC_ID}/evaluations/${EVAL_ID}` },
      body: JSON.stringify({
        evaluationId: EVAL_ID,
        locationCreatedAt: '2026-06-02T10:10:00Z',
        rateLimitRemaining: 0,
        rateLimitReset: '2026-06-02T10:20:00Z',
        costCapRemaining: 49.5,
        costCapEstimate: 0.012,
      }),
    });
  });

  // GET detail — terminal Completed run with the full metrics shape so the
  // detail panel renders all Stat tiles + the Green QualityBandChip.
  await page
    .context()
    .route(`${API_BASE}/api/v1/admin/kb/docs/${DOC_ID}/evaluations/${EVAL_ID}`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          evaluationId: EVAL_ID,
          pdfDocumentId: DOC_ID,
          startedAt: '2026-06-02T10:10:00Z',
          completedAt: '2026-06-02T10:10:45Z',
          status: 'Completed',
          goldsetVersion: 'auto-v1',
          goldsetGenerationSeed: 12345,
          metrics: {
            precision: { at1: 0.92, at3: 0.85, at5: 0.8 },
            ranking: { mrr: 0.6 },
            latency: { p50Ms: 180, p95Ms: 420 },
            queryCount: 15,
            costUsd: 0.012,
            qualityBand: 'Green',
          },
          costUsd: 0.012,
          triggeredByAdminId: ADMIN_ID,
          errorMessage: null,
        }),
      })
    );
}

test.describe('Admin KB Quality — happy-path eval trigger', () => {
  test.skip(SKIP_E2E, 'KB_QUALITY_E2E_SKIP=true — admin shell not reproducible in this env');

  test.beforeEach(async ({ page }) => {
    await mockAdminAuth(page);
    await mockKbDocShell(page);
    await mockKbQualityLifecycle(page);
  });

  test('admin triggers eval and sees Completed run with metrics', async ({ page }) => {
    await page.goto(`/admin/knowledge-base?doc=${DOC_ID}&tab=quality`);

    // QualityTabPanel mounts with the empty list state.
    await expect(page.getByTestId('kb-quality-tab-panel')).toBeVisible();
    await expect(page.getByTestId('eval-list-empty')).toBeVisible();

    // Trigger an eval — POST flips the mock gate so the next list GET returns 1 row.
    await page.getByTestId('eval-trigger-button').click();

    // History list re-renders with the persisted run.
    const row = page.getByTestId(`eval-list-row-${EVAL_ID}`);
    await expect(row).toBeVisible({ timeout: 30_000 });

    // Click row → detail panel populates from the GET detail mock.
    await row.click();
    await expect(page.getByTestId('eval-detail-panel')).toBeVisible();

    // Metric tiles + quality band chip render the expected shape.
    await expect(page.getByText(/Precision@5/i)).toBeVisible();
    await expect(page.getByText(/MRR/i)).toBeVisible();
    await expect(page.getByTestId('quality-band-chip-green')).toBeVisible();
  });
});
