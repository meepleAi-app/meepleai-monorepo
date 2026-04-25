/**
 * E2E — Mechanic Extractor AI Validation: BGG bulk-import dialog
 * (ADR-051 Sprint 2 / Task 28).
 *
 * Verifies the operator end-to-end loop on
 * `/admin/knowledge-base/mechanic-extractor/golden/[gameId]`:
 *
 *  1. Click `Import BGG tags` → dialog opens with empty textarea, Insert
 *     button disabled.
 *  2. Paste a valid TSV block (3 unique rows) → preview re-renders with
 *     "3 row(s) ready to import"; Insert button activates with
 *     `Insert 3 tag(s)` label.
 *  3. Click Insert → fires `POST /golden/{sharedGameId}/bgg-tags` with the
 *     parsed rows. Server reports `{ inserted: 2, skipped: 1 }` (one tag
 *     already existed). Sonner toast surfaces both counts via
 *     `formatSuccessMessage` (Imported 2 BGG tag(s) (1 skipped as duplicate)).
 *  4. Dialog closes on success; the trigger button stays focused so a
 *     follow-up paste is one Tab away.
 *
 * Backend wire format and dedupe semantics: `BggImportResult` (Sprint 1
 * Task 17) + `MechanicGoldenBggTagRepository.seenInBatch` HashSet — see
 * `apps/web/src/lib/parsers/bgg-tsv.ts` for the matching client-side
 * dedupe. The spec runs against fully-mocked routes — no live DB, no
 * BGG fetch — so it's hermetic and stable in CI.
 *
 * Feature flag: requires `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true`
 * at server start. Forwarded by `playwright.config.ts` webServer env.
 */

import { test, expect, type Page, type Route } from '@playwright/test';

import { setupMockAuth } from '../fixtures/auth';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Stable UUID used as both the SharedGame id and the route param. `getById`
// in `sharedGamesClient` short-circuits to `null` for non-UUID strings and
// never fires the request, so we need a real-shape UUID for the page to
// hit our mock GET.
const SHARED_GAME_ID = '11111111-2222-3333-4444-555555555555';

const PASTE_BLOCK = 'Mechanism\tRole Selection\nMechanism\tVariable Phase Order\nTheme\tEconomic';

/**
 * Stubs the three endpoints the page touches:
 *   - `GET /shared-games/{id}` — minimal valid SharedGameDetail.
 *   - `GET /admin/mechanic-extractor/golden/{id}` — empty bundle (no claims,
 *     no tags) so the page renders quickly without us caring about list state.
 *   - `POST /admin/mechanic-extractor/golden/{id}/bgg-tags` — accepts the
 *     parsed rows and returns the canonical `{ inserted, skipped }` shape.
 *     Returns `{ inserted: 2, skipped: 1 }` so the success-toast assertion
 *     exercises the duplicate-aware branch in `formatSuccessMessage`.
 *
 * Returns a `getSubmittedTags()` accessor so the test can verify the
 * client posted the parser-deduped row set rather than the raw paste.
 */
async function mockBggImporterRoutes(page: Page) {
  let submittedTags: Array<{ category: string; name: string }> | null = null;
  let postCount = 0;

  await page
    .context()
    .route(`${API_BASE}/api/v1/shared-games/${SHARED_GAME_ID}`, async (route: Route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: SHARED_GAME_ID,
            bggId: 13,
            title: 'Catan',
            yearPublished: 1995,
            description: '',
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.32,
            averageRating: 7.1,
            imageUrl: '',
            thumbnailUrl: '',
            rules: null,
            status: 'Published',
            createdBy: '00000000-0000-0000-0000-000000000001',
            modifiedBy: null,
            createdAt: '2026-04-25T00:00:00Z',
            modifiedAt: null,
            faqs: [],
            erratas: [],
            designers: [],
            publishers: [],
            categories: [],
            mechanics: [],
          }),
        });
        return;
      }
      await route.continue();
    });

  await page
    .context()
    .route(
      `${API_BASE}/api/v1/admin/mechanic-extractor/golden/${SHARED_GAME_ID}`,
      async (route: Route) => {
        if (route.request().method() === 'GET') {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              sharedGameId: SHARED_GAME_ID,
              versionHash: 'h-mock-1',
              claims: [],
              bggTags: [],
            }),
          });
          return;
        }
        await route.continue();
      }
    );

  await page
    .context()
    .route(
      `${API_BASE}/api/v1/admin/mechanic-extractor/golden/${SHARED_GAME_ID}/bgg-tags`,
      async (route: Route) => {
        if (route.request().method() === 'POST') {
          const body = route.request().postDataJSON() as {
            tags: Array<{ category: string; name: string }>;
          } | null;
          submittedTags = body?.tags ?? [];
          postCount += 1;
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ inserted: 2, skipped: 1 }),
          });
          return;
        }
        await route.continue();
      }
    );

  return {
    getSubmittedTags: () => submittedTags,
    getPostCount: () => postCount,
  };
}

test.describe('Admin · Mechanic Extractor · BGG bulk import', () => {
  test('admin pastes BGG tags, sees deduped preview, and the success toast surfaces both counts', async ({
    page,
  }) => {
    await setupMockAuth(page, 'Admin', 'admin@meepleai.dev');
    const mocks = await mockBggImporterRoutes(page);

    await page.goto(`/admin/knowledge-base/mechanic-extractor/golden/${SHARED_GAME_ID}`);
    await page.waitForLoadState('networkidle');

    // Open the importer — the trigger lives in the page header next to "New claim".
    await page.getByTestId('bgg-importer-trigger').click();

    const dialog = page.getByTestId('bgg-importer-paste-dialog');
    await expect(dialog).toBeVisible();

    // Submit button starts disabled (no parsed rows yet).
    const insertButton = page.getByRole('button', { name: /insert tags/i });
    await expect(insertButton).toBeDisabled();

    // Paste 3 unique TSV rows. The textarea is uncontrolled-from-Playwright's
    // perspective — `fill()` triggers the React onChange handler which runs
    // the parser synchronously.
    await page.getByTestId('bgg-importer-paste-textarea').fill(PASTE_BLOCK);

    // Live preview should show the parsed rows. The label flips from
    // "Insert tags" → "Insert 3 tag(s)" once the parser produces ≥1 row.
    await expect(page.getByTestId('bgg-importer-preview-table')).toBeVisible();
    await expect(page.getByText(/3 row\(s\) ready to import/i)).toBeVisible();
    const activeInsertButton = page.getByRole('button', { name: /insert 3 tag\(s\)/i });
    await expect(activeInsertButton).toBeEnabled();

    // Fire the import. Server returns { inserted: 2, skipped: 1 } so the
    // success toast text exercises `formatSuccessMessage`'s duplicate branch.
    await activeInsertButton.click();

    await expect(
      page.getByText(/imported 2 bgg tag\(s\) \(1 skipped as duplicate\)/i)
    ).toBeVisible();

    // Dialog closes on success.
    await expect(dialog).not.toBeVisible();

    // Verify the client posted exactly the 3 parsed rows (parser dedupes
    // happen client-side; here all 3 pasted rows are already unique).
    expect(mocks.getPostCount()).toBe(1);
    expect(mocks.getSubmittedTags()).toEqual([
      { category: 'Mechanism', name: 'Role Selection' },
      { category: 'Mechanism', name: 'Variable Phase Order' },
      { category: 'Theme', name: 'Economic' },
    ]);
  });

  test('in-paste duplicates are silently folded so the preview row count matches the POST body', async ({
    page,
  }) => {
    await setupMockAuth(page, 'Admin', 'admin@meepleai.dev');
    const mocks = await mockBggImporterRoutes(page);

    await page.goto(`/admin/knowledge-base/mechanic-extractor/golden/${SHARED_GAME_ID}`);
    await page.waitForLoadState('networkidle');

    await page.getByTestId('bgg-importer-trigger').click();
    await expect(page.getByTestId('bgg-importer-paste-dialog')).toBeVisible();

    // Same 3 unique rows + 1 in-paste duplicate of the first → parser folds
    // the duplicate, preview shows 3 rows, POST body carries 3 rows.
    await page
      .getByTestId('bgg-importer-paste-textarea')
      .fill(`${PASTE_BLOCK}\nMechanism\tRole Selection`);

    await expect(page.getByText(/3 row\(s\) ready to import/i)).toBeVisible();

    await page.getByRole('button', { name: /insert 3 tag\(s\)/i }).click();

    await expect(
      page.getByText(/imported 2 bgg tag\(s\) \(1 skipped as duplicate\)/i)
    ).toBeVisible();

    expect(mocks.getSubmittedTags()).toEqual([
      { category: 'Mechanism', name: 'Role Selection' },
      { category: 'Mechanism', name: 'Variable Phase Order' },
      { category: 'Theme', name: 'Economic' },
    ]);
  });

  test('malformed lines surface in the error list and do not block the well-formed rows', async ({
    page,
  }) => {
    await setupMockAuth(page, 'Admin', 'admin@meepleai.dev');
    await mockBggImporterRoutes(page);

    await page.goto(`/admin/knowledge-base/mechanic-extractor/golden/${SHARED_GAME_ID}`);
    await page.waitForLoadState('networkidle');

    await page.getByTestId('bgg-importer-trigger').click();

    // Two valid rows + one TAB-less line + one empty-category line.
    await page
      .getByTestId('bgg-importer-paste-textarea')
      .fill(
        'Mechanism\tRole Selection\nthis line has no tab\n\tName Without Category\nTheme\tEconomic'
      );

    await expect(page.getByTestId('bgg-importer-error-list')).toBeVisible();
    await expect(page.getByText(/2 line\(s\) skipped/i)).toBeVisible();

    // Two valid rows still parse and the preview + Insert label reflect that.
    await expect(page.getByText(/2 row\(s\) ready to import/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /insert 2 tag\(s\)/i })).toBeEnabled();
  });
});
