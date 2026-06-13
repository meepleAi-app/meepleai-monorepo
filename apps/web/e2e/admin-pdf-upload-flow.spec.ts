/**
 * E2E: Admin PDF Upload Flow — Issue #2246 (epic #2242 Sub #4 P1)
 *
 * Scenarios covered (skeleton only — fixtures wired in follow-up):
 *   1. Admin uploads PDF → admin lists refresh without reload
 *   2. STAGE_ORDER (backend-aligned) renders in the indexing card
 *   3. kb-cards refetchInterval flips Completed without page reload
 *
 * All scenarios use `test.fixme` pending the admin auth fixture and a real
 * PDF processing pipeline available in CI. Pattern mirrors
 * `apps/web/e2e/pdf-indexing-flow.spec.ts` / `epic-4-pdf-status.spec.ts`.
 */
import { test, expect } from '@playwright/test';

test.describe('Admin PDF Upload Flow — Issue #2246', () => {
  test.fixme('Block A: 6 admin caches invalidate on upload success', async ({ page }) => {
    // 1. Login as admin
    // 2. Navigate to /admin/knowledge-base/upload
    // 3. Select a game from search
    // 4. Drop a small PDF file
    // 5. Wait for "completed" state
    // 6. Assert: /admin/knowledge-base/documents reflects the new doc
    //    WITHOUT a page reload (cache invalidation)
    // 7. Assert: /admin/shared-games/{id}#documents reflects the new doc
    await page.goto('/admin/knowledge-base/upload');
    await expect(page).toHaveURL(/upload/);
  });

  test.fixme('Block C: STAGE_ORDER renders in indexing card with backend PdfProcessingState values', async ({
    page,
  }) => {
    // 1. Login as admin
    // 2. Navigate to a shared-game detail page with an in-progress PDF
    // 3. Assert: indexing card shows stages in canonical order:
    //    Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready
    // 4. Assert: at any given time exactly one stage row carries the
    //    "in-progress" affordance
    await page.goto('/admin/shared-games');
  });

  test.fixme('Block D: kb-cards refetchInterval flips card to Completed without reload', async ({
    page,
  }) => {
    // 1. Login as admin
    // 2. Navigate to /admin/shared-games/{id}#agent
    // 3. Upload a PDF (or wait for in-progress indexing)
    // 4. Wait <= 10s
    // 5. Assert: kb-card row transitions from "Pending"/"Processing" badge
    //    to "Completed" badge WITHOUT a page reload (useQuery refetch)
    await page.goto('/admin/shared-games');
  });
});
