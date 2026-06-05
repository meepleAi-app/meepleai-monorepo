/**
 * Admin KB — Per-doc embeddings viewer (Issue #1674).
 *
 * Mocks all endpoints involved in the drawer flow:
 *   - /api/v1/auth/me (admin session)
 *   - /api/v1/admin/kb/nav-counts (tree no-op)
 *   - /api/v1/admin/kb/docs/{DOC_ID} (doc detail panel mount)
 *   - /api/v1/admin/kb/docs/{DOC_ID}/embeddings/meta (drawer meta strip)
 *   - /api/v1/admin/kb/docs/{DOC_ID}/chunks/search (search panel)
 *   - /api/v1/admin/kb/docs/{DOC_ID}/chunks/export (export footer)
 *
 * Verifies 4 acceptance criteria from spec §10:
 *   EM-01 happy path: open drawer → meta + search results
 *   EM-02 not-indexed: drawer shows empty state on 404
 *   EM-03 export: anchor URL points to chunks/export endpoint
 *   EM-04 a11y: axe-core AA pass on drawer
 */

import AxeBuilder from '@axe-core/playwright';
import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const SKIP_E2E = process.env.KB_EMBEDDINGS_E2E_SKIP === 'true';

const DOC_ID = '11111111-1111-1111-1111-111111111111';
const GAME_ID = '33333333-3333-3333-3333-333333333333';
const ADMIN_ID = '99999999-9999-9999-9999-999999999999';

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

async function mockKbDocShell(page: Page, processingStatus: 'ready' | 'failed' = 'ready') {
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
        status: processingStatus === 'ready' ? 'ready' : 'failed',
        doc: {
          id: DOC_ID,
          title: 'Wingspan-Oceania.pdf',
          gameId: GAME_ID,
          gameName: 'Wingspan',
          docType: 'rulebook',
          uploadedAt: '2026-06-02T10:00:00Z',
          lastIngestedAt: '2026-06-02T10:05:00Z',
          chunkCount: 412,
          pageCount: 42,
          language: 'en',
          fileSize: 1_234_567,
          processingStatus,
          indexerVersion: 'v1',
        },
      }),
    })
  );

  // Also mock the kb-docs/{id} endpoint that useKbDocDetail consumes directly.
  await page.context().route(`${API_BASE}/api/v1/kb-docs/${DOC_ID}`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: DOC_ID,
        title: 'Wingspan-Oceania.pdf',
        gameId: GAME_ID,
        processingStatus,
        fileName: 'Wingspan-Oceania.pdf',
      }),
    })
  );
}

async function mockEmbeddingsMeta(page: Page, status: 200 | 404 = 200) {
  await page
    .context()
    .route(`${API_BASE}/api/v1/admin/kb/docs/${DOC_ID}/embeddings/meta`, route => {
      if (status === 404) {
        return route.fulfill({
          status: 404,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            type: 'NotFound',
            title: 'Not Found',
            status: 404,
            detail: "Embeddings with identifier 'document-id' was not found",
          }),
        });
      }
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          docId: DOC_ID,
          model: 'bge-base-en-v1.5',
          dimensions: 768,
          totalChunks: 412,
          indexedAt: '2026-05-28T14:22:14Z',
          language: 'en',
        }),
      });
    });
}

async function mockSearchChunks(page: Page) {
  await page.context().route(`${API_BASE}/api/v1/admin/kb/docs/${DOC_ID}/chunks/search`, route => {
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        results: [
          { chunkIndex: 218, pageNumber: 22, snippet: 'predator activation order', score: 0.912 },
          { chunkIndex: 142, pageNumber: 14, snippet: 'power activation rules', score: 0.847 },
        ],
        errorMessage: null,
      }),
    });
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('Admin KB · Per-doc embeddings viewer (#1674)', () => {
  test.skip(SKIP_E2E, 'KB_EMBEDDINGS_E2E_SKIP=true — e2e disabled');

  test('EM-01 happy path: open drawer → meta strip + semantic search', async ({ page }) => {
    await mockAdminAuth(page);
    await mockKbDocShell(page, 'ready');
    await mockEmbeddingsMeta(page, 200);
    await mockSearchChunks(page);

    await page.goto(`/admin/knowledge-base?doc=${DOC_ID}`);

    // Trigger drawer open
    await page.getByRole('button', { name: /View embeddings/i }).click();

    const drawer = page.getByRole('dialog');
    await expect(drawer).toBeVisible();
    await expect(drawer.getByText(/Embeddings · Wingspan-Oceania\.pdf/)).toBeVisible();

    // Meta strip (4 KPI)
    await expect(drawer.getByText('bge-base-en-v1.5')).toBeVisible();
    await expect(drawer.getByText('768')).toBeVisible();
    await expect(drawer.getByText('412')).toBeVisible();

    // Semantic search
    await drawer.getByLabel('Query semantica').fill('predator');
    await drawer.getByRole('button', { name: 'Cerca' }).click();
    await expect(drawer.getByText(/2 risultati trovati/i)).toBeVisible({ timeout: 5000 });
    await expect(drawer.getByText('predator activation order')).toBeVisible();
  });

  test('EM-02 not-indexed doc: shows EmptyState', async ({ page }) => {
    await mockAdminAuth(page);
    await mockKbDocShell(page, 'ready');
    await mockEmbeddingsMeta(page, 404);

    await page.goto(`/admin/knowledge-base?doc=${DOC_ID}`);
    await page.getByRole('button', { name: /View embeddings/i }).click();

    const drawer = page.getByRole('dialog');
    await expect(drawer).toBeVisible();
    await expect(drawer.getByText(/Documento non indicizzato/i)).toBeVisible();
  });

  test('EM-03 export anchor points to /chunks/export endpoint', async ({ page }) => {
    await mockAdminAuth(page);
    await mockKbDocShell(page, 'ready');
    await mockEmbeddingsMeta(page, 200);

    await page.goto(`/admin/knowledge-base?doc=${DOC_ID}`);
    await page.getByRole('button', { name: /View embeddings/i }).click();

    const drawer = page.getByRole('dialog');
    const exportLink = drawer.getByRole('link', { name: /Export chunks JSON/i });
    await expect(exportLink).toBeVisible();
    await expect(exportLink).toHaveAttribute(
      'href',
      `/api/v1/admin/kb/docs/${DOC_ID}/chunks/export`
    );
  });

  test('EM-04 a11y axe: no AA violations on open drawer', async ({ page }) => {
    await mockAdminAuth(page);
    await mockKbDocShell(page, 'ready');
    await mockEmbeddingsMeta(page, 200);

    await page.goto(`/admin/knowledge-base?doc=${DOC_ID}`);
    await page.getByRole('button', { name: /View embeddings/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible();

    const results = await new AxeBuilder({ page })
      .include('[role="dialog"]')
      .withTags(['wcag2a', 'wcag2aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});
