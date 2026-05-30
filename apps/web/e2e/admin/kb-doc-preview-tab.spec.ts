/**
 * Admin KB Doc Preview tab smoke — F3-FU-5 T6 (#1654)
 *
 * Verifies that:
 * 1. Landing on /admin/knowledge-base?doc={id}&tab=preview mounts the
 *    `KbDocPreviewPanel` (which renders `PdfInlineViewer` with the admin
 *    toolbar) and the toolbar shows Prev/Next + the page counter
 *    "Pagina N / M".
 * 2. Tab routing: clicking Preview from the overview view updates the URL
 *    to include `tab=preview`; clicking Overview removes the param.
 *
 * Helper pattern: follows F3-FU-4 (`kb-doc-actions.spec.ts`, commit
 * `23b17f6a5`) — `AdminHelper.setupAdminAuth(true)` + per-test route mocks.
 * `seedKbDocFixture` (referenced in the plan template) does not exist; we
 * mock the doc + chunks + PDF blob endpoints directly. Same approach as
 * F3-FU-4's smoke for parity (visibility-only, no destructive actions).
 *
 * PDF blob source: `apps/web/e2e/test-data/pandemic_rulebook.pdf` (real
 * 1-page PDF, 808 bytes). The assertion uses a loose regex that matches
 * "Pagina N / M" for any M ≥ 1.
 *
 * Spec: docs/superpowers/specs/2026-05-30-sp5-admin-kb-f3-fu5-preview-tab-design.md
 * Plan: docs/superpowers/plans/2026-05-30-sp5-admin-kb-f3-fu5-preview-tab.md (Task 6)
 */

import * as fs from 'fs';
import * as path from 'path';

import { expect, Page, test as base } from '@playwright/test';

import { AdminHelper } from '../pages';

const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ─── Fixtures ───────────────────────────────────────────────────────────────

const test = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const adminHelper = new AdminHelper(page);
    // Sets up /auth/me mock + catch-all /api/v1/admin/** stub.
    // skip navigation = true (each test navigates explicitly).
    await adminHelper.setupAdminAuth(true);
    await use(page);
  },
});

// ─── Mock data ───────────────────────────────────────────────────────────────

const DOC_ID = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee';
const GAME_ID = 'gggggggg-hhhh-iiii-jjjj-kkkkkkkkkkkk';
const FILE_NAME = 'catan-rulebook.pdf';

const MOCK_GAMES = [
  {
    gameId: GAME_ID,
    gameName: 'Catan',
    totalChunks: 142,
    docCount: 1,
  },
];

const MOCK_GAME_DOCS = [
  {
    id: DOC_ID,
    title: FILE_NAME,
    status: 'indexed' as const,
    pageCount: 24,
    gameId: GAME_ID,
  },
];

const MOCK_DOC_DETAIL = {
  id: DOC_ID,
  title: FILE_NAME,
  gameId: GAME_ID,
  gameName: 'Catan',
  docType: 'rulebook',
  uploadedAt: '2026-01-15T10:00:00Z',
  processingStatus: 'ready' as const,
  chunkCount: 142,
  pageCount: 24,
  language: 'it',
};

// Real PDF fixture (1 page, 808 bytes). The viewer asserts `Pagina N / M`
// with M ≥ 1 — the exact page count is not load-bearing for the smoke.
const PDF_FIXTURE_PATH = path.resolve(__dirname, '../test-data/pandemic_rulebook.pdf');

// ─── Route setup helpers ─────────────────────────────────────────────────────

/**
 * Register KB explorer + PDF download route mocks BEFORE navigation.
 * Order: specific routes first; the AdminHelper catch-all (registered by
 * setupAdminAuth) covers anything else.
 */
async function setupKbMocks(page: Page) {
  // 1. KB game-statuses list (KbExplorer queries this on mount)
  await page.route(`${apiBase}/api/v1/admin/kb/games/`, async route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: MOCK_GAMES }),
    })
  );

  // 2. Game documents for the tree
  await page.route(`${apiBase}/api/v1/knowledge-base/${GAME_ID}/documents`, async route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_GAME_DOCS),
    })
  );

  // 3. Doc detail hero (KbDocDetailPanel → useKbDocDetail)
  await page.route(`${apiBase}/api/v1/kb-docs/${DOC_ID}`, async route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(MOCK_DOC_DETAIL),
    })
  );

  // 4. Chunks list (KbDocDetailPanel → useKbChunksList) — empty pages
  await page.route(`${apiBase}/api/v1/kb-docs/${DOC_ID}/chunks*`, async route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [], nextCursor: null }),
    })
  );

  // 5. PDF blob download — returns the real test fixture PDF.
  //    PdfInlineViewer calls fetch(downloadUrl, { credentials: 'include' })
  //    and feeds the Blob to react-pdf's `<Document file={blob}>`.
  const pdfBuffer = fs.readFileSync(PDF_FIXTURE_PATH);
  await page.route(`${apiBase}/api/v1/pdfs/${DOC_ID}/download`, async route =>
    route.fulfill({
      status: 200,
      contentType: 'application/pdf',
      body: pdfBuffer,
    })
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

test.describe('KB doc Preview tab (#1654)', () => {
  test.beforeEach(async ({ adminPage: page }) => {
    await setupKbMocks(page);
  });

  // ── Scenario 1: ?tab=preview mounts viewer with admin toolbar ────────────

  test('admin opens ?tab=preview and sees the PDF viewer toolbar', async ({ adminPage: page }) => {
    await page.goto(`/admin/knowledge-base?doc=${DOC_ID}&tab=preview`);

    // Toolbar Prev / Next buttons render even before the PDF blob loads —
    // they are part of the static toolbar in PdfInlineViewer.
    const prevBtn = page.getByRole('button', { name: /prev/i });
    const nextBtn = page.getByRole('button', { name: /next/i });
    await expect(prevBtn).toBeVisible({ timeout: 15_000 });
    await expect(nextBtn).toBeVisible({ timeout: 15_000 });

    // Page counter shows "Pagina N / M" once react-pdf calls onLoadSuccess.
    // We assert a loose regex (M may vary if the fixture changes).
    await expect(page.getByText(/Pagina\s+\d+\s*\/\s*\d+/i)).toBeVisible({ timeout: 15_000 });
  });

  // ── Scenario 2: Tab routing — Preview ↔ Overview ─────────────────────────

  test('tab routing: Preview ↔ Overview updates URL param', async ({ adminPage: page }) => {
    // Start on the overview view (default tab).
    await page.goto(`/admin/knowledge-base?doc=${DOC_ID}`);

    // Wait for the tab nav to mount.
    const tabNav = page.getByRole('navigation', { name: /Sezione documento/i });
    await expect(tabNav).toBeVisible({ timeout: 15_000 });

    // Click Preview tab → URL gets `tab=preview` appended.
    await tabNav.getByRole('link', { name: /preview/i }).click();
    await expect(page).toHaveURL(new RegExp(`doc=${DOC_ID}&tab=preview`), { timeout: 10_000 });

    // Click Overview tab → URL drops the `tab` param (overview is the default).
    await tabNav.getByRole('link', { name: /overview/i }).click();
    await expect(page).toHaveURL(new RegExp(`doc=${DOC_ID}(?!.*tab=)`), { timeout: 10_000 });
  });
});
