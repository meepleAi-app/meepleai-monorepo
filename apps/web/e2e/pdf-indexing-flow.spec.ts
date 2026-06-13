import { test, expect } from '@playwright/test';

/**
 * Epic #2242 — PDF indexing flow repair.
 * Sub #6 (#2248) Block D — end-to-end Playwright skeleton.
 *
 * Walks the full journey the bug broke (#2243 root cause): admin uploads a PDF,
 * the BG pipeline reaches Ready, the catalog DTOs surface HasKnowledgeBase=true,
 * and the user-facing badge "agente pronto" appears on the library and the
 * game-detail page.
 *
 * Tests are skipped by default — they rely on the backend running with the
 * embedding-service mock + a staged admin/user fixture pair which is not yet
 * wired into the e2e harness. Once `test-fixtures/auth/admin.json` and
 * `test-fixtures/auth/user.json` storage states ship from the auth workstream,
 * remove the `test.fixme` annotations and run via `pnpm test:e2e -- pdf-indexing-flow`.
 *
 * Until then this file acts as the executable BDD specification for the epic:
 * the assertions below describe the contract every block of #2242 must honour
 * end-to-end. Reviewers can use it as a checklist when validating Sub #4 / #5
 * frontend changes.
 */

test.describe('Epic #2242 — PDF indexing flow end-to-end', () => {
  test.fixme('admin uploads PDF, sees Ready, then user sees agente pronto on library + game-detail', async ({
    browser,
  }) => {
    // ────────────────────────────────────────────────────────────────────
    // ADMIN JOURNEY
    // ────────────────────────────────────────────────────────────────────
    const adminContext = await browser.newContext({
      storageState: 'test-fixtures/auth/admin.json',
    });
    const adminPage = await adminContext.newPage();

    await adminPage.goto('/admin/knowledge-base/upload');

    // Pick the test SharedGame seeded by the fixture harness.
    const gameTitle = 'Wingspan Test Fixture';
    await adminPage.getByTestId('game-search').fill(gameTitle);
    await adminPage.getByRole('option', { name: gameTitle }).click();

    // Upload a small valid PDF.
    await adminPage
      .getByTestId('upload-zone')
      .locator('input[type="file"]')
      .setInputFiles('test-fixtures/pdfs/wingspan-rules-mock.pdf');

    // Block A invariant: upload accepts and the per-doc status badge climbs
    // through the 7-state pipeline (Pending → Uploading → … → Ready).
    // The new admin queue page (Sub #4 cache invalidation) must reflect the
    // change without a manual refresh.
    await expect(
      adminPage.getByTestId('upload-status'),
      'upload zone must report completion within the BG pipeline window'
    ).toContainText(/completed/i, { timeout: 60_000 });

    // Block C invariant: navigating to the game-detail Documents tab shows
    // the new doc in Ready state (the polling stage labels must match the
    // backend enum — Sub #4 Block C in #2246 fixes the previous mismatch).
    await adminPage.goto('/admin/shared-games/wingspan');
    await expect(adminPage.getByTestId('pdf-row-wingspan-rules-mock')).toHaveAttribute(
      'data-state',
      'Ready',
      { timeout: 30_000 }
    );

    // Sub #4 Block A invariant: the KB documents list refreshes without a
    // manual navigation (cache invalidation post-upload).
    await adminPage.goto('/admin/shared-games/wingspan/knowledge-base');
    await expect(adminPage.getByTestId('kb-documents-list')).not.toBeEmpty();

    await adminContext.close();

    // ────────────────────────────────────────────────────────────────────
    // USER JOURNEY
    // ────────────────────────────────────────────────────────────────────
    const userContext = await browser.newContext({
      storageState: 'test-fixtures/auth/user.json',
    });
    const userPage = await userContext.newPage();

    // Sub #1 Block A+C invariant: GET /api/v1/games returns HasKnowledgeBase
    // and GET /api/v1/shared-games returns KbsCount>0, so the library grid
    // can render the "📄 KB" chip via MeepleLibraryGameCard.
    await userPage.goto('/library');
    await expect(userPage.getByTestId('library-card-wingspan').getByTestId('kb-chip')).toBeVisible({
      timeout: 10_000,
    });

    // Sub #5 (#2247) Block A invariant: /games/[id] Documents tab is wired
    // to safeDetail.kbs instead of the previous hardcoded `docs=[]`.
    await userPage.goto('/games/wingspan');
    await expect(
      userPage.getByTestId('game-detail-kb-doc-list'),
      'game-detail Documents tab must render the published KB list (not the empty hardcoded array)'
    ).not.toBeEmpty();

    // Sub #5 Block A invariant: the chat CTA is enabled because
    // safeDetail.hasKnowledgeBase=true.
    await expect(userPage.getByTestId('chat-cta')).toBeEnabled();

    await userContext.close();
  });

  test.fixme('discover catalog shows AI Ready badge once HasKnowledgeBase=true', async ({
    page,
  }) => {
    // Sub #5 Block B invariant — discover badge wired to SharedGame.hasKnowledgeBase.
    await page.goto('/games?tab=discover');
    await expect(
      page.getByTestId('discover-card-wingspan').getByTestId('ai-ready-badge')
    ).toBeVisible({ timeout: 10_000 });
  });

  test.fixme('GamesFilterPanel quick-link AI Ready navigates to /games?hasKb=true', async ({
    page,
  }) => {
    // Sub #5 Block D invariant — fixes the URL pointing at /library?hasKb=true.
    await page.goto('/games');
    await page.getByRole('link', { name: /ai ready/i }).click();
    await expect(page).toHaveURL(/\/games\?.*hasKb=true/);
  });
});
