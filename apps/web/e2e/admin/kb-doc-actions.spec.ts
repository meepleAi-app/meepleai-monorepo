/**
 * Admin KB Doc Actions smoke — F3-FU-4 T10
 *
 * Verifies that:
 * 1. Landing on /admin/knowledge-base renders the explorer (tree + empty-state
 *    detail panel) with no doc selected.
 * 2. Selecting a document from the tree (via ?doc= deep-link) mounts the
 *    KbDocDetailPanel and the action-bar — Re-index, Download, Delete,
 *    Export chunks, and the Used-by link are all visible.
 * 3. Clicking Used-by updates the URL to contain `tab=used-by`.
 *
 * Destructive actions (Re-index, Delete) are NOT clicked — visibility only.
 * Deep behaviour is covered by unit tests; this test smokes the navigation
 * and rendering layer.
 *
 * Pattern mirrors apps/web/e2e/admin-kb-explorer.spec.ts (F3.1 T6).
 */

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

// ─── Route setup helpers ─────────────────────────────────────────────────────

/**
 * Register all KB-related route mocks BEFORE navigation.
 * Order: specific routes first, then the AdminHelper catch-all (registered by
 * setupAdminAuth) handles anything else. Playwright evaluates handlers in
 * registration order — first match wins for the context-level handler.
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

  // 2. Game documents for the tree (KbTreeGameDocs, fetched when game node expands)
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

  // 4. Chunks list (KbDocDetailPanel → useKbChunksList) — return empty pages
  await page.route(`${apiBase}/api/v1/kb-docs/${DOC_ID}/chunks*`, async route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: [], nextCursor: null }),
    })
  );

  // 5. Consuming-agents list (only fetched when delete dialog opens — won't fire)
  await page.route(`${apiBase}/api/v1/admin/kb-docs/${DOC_ID}/consuming-agents*`, async route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    })
  );

  // 6. Export-chunks endpoint — won't fire (we don't click Export in this smoke)
  await page.route(`${apiBase}/api/v1/admin/kb-docs/${DOC_ID}/chunks/export`, async route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    })
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

test.describe('Admin KB Doc Actions smoke (F3-FU-4)', () => {
  test.beforeEach(async ({ adminPage: page }) => {
    await setupKbMocks(page);
  });

  // ── Scenario 1: Explorer renders tree + empty-state detail panel ─────────

  test('landing /admin/knowledge-base shows KbTree + empty detail panel', async ({
    adminPage: page,
  }) => {
    await page.goto('/admin/knowledge-base');

    // Tree is visible
    const tree = page.getByRole('tree', { name: /Knowledge Base alberatura/i });
    await expect(tree).toBeVisible({ timeout: 15_000 });

    // No doc selected → empty-state placeholder
    await expect(page.getByTestId('kb-doc-detail-empty')).toBeVisible({ timeout: 10_000 });
  });

  // ── Scenario 2: Select a doc → detail panel + action-bar visible ─────────

  test('selecting a doc via ?doc= mounts detail panel with action-bar controls', async ({
    adminPage: page,
  }) => {
    // Deep-link directly to the document (simulates selecting the doc in the tree)
    await page.goto(`/admin/knowledge-base?doc=${DOC_ID}`);

    // Detail panel should not be the empty-state
    await expect(page.getByTestId('kb-doc-detail-empty')).not.toBeVisible({ timeout: 10_000 });

    // ── Action-bar button visibility checks ──────────────────────────────────
    // Re-index button
    const reindexBtn = page.getByRole('button', { name: /Re-index/i });
    await expect(reindexBtn).toBeVisible({ timeout: 15_000 });

    // Download link (rendered as <a download>)
    const downloadLink = page.getByRole('link', { name: /Download/i });
    await expect(downloadLink).toBeVisible();

    // Delete button
    const deleteBtn = page.getByRole('button', { name: /Elimina/i });
    await expect(deleteBtn).toBeVisible();

    // Export chunks button
    const exportBtn = page.getByRole('button', { name: /Export chunks/i });
    await expect(exportBtn).toBeVisible();

    // Used-by / Agent link
    const usedByLink = page.getByRole('link', { name: /Agent/i });
    await expect(usedByLink).toBeVisible();
  });

  // ── Scenario 3 (optional): Used-by link updates the URL tab param ─────────

  test('clicking Used-by link navigates to ?tab=used-by', async ({ adminPage: page }) => {
    await page.goto(`/admin/knowledge-base?doc=${DOC_ID}`);

    // Wait for the action-bar to appear before clicking
    const usedByLink = page.getByRole('link', { name: /Agent/i });
    await expect(usedByLink).toBeVisible({ timeout: 15_000 });

    await usedByLink.click();

    // URL should now contain both doc and tab=used-by
    await expect(page).toHaveURL(/tab=used-by/, { timeout: 10_000 });
    await expect(page).toHaveURL(new RegExp(`doc=${DOC_ID}`));
  });
});
