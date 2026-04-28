/**
 * V2 State Coverage — /shared-games/[id] route (Issue #603, Wave A.4).
 *
 * Captures 4 surface states: default, loading, error, empty-tab.
 * Uses test-only `?state=...` query param (guarded by
 * `NODE_ENV !== 'production'` in
 * `app/(public)/shared-games/[id]/page-client.tsx`).
 *
 * The 5th state from typical 5-state surfaces — `not-found` — is handled
 * server-side via Next.js `notFound()`, so it lives in a separate
 * `not-found.tsx` and is out of scope for this client-state suite.
 *
 * Snapshots written to `apps/web/e2e/v2-states/shared-game-detail.spec.ts-snapshots/`.
 * Run via CI bootstrap workflow (Linux x86-64 canonical baselines):
 *   `gh workflow run 266963272 --ref <branch> -f mode=bootstrap -f project_filter=both`
 */
import { test, expect, type Page, type APIRequestContext } from '@playwright/test';

async function fetchFirstSharedGameId(request: APIRequestContext): Promise<string> {
  const res = await request.get('/api/v1/shared-games?pageSize=1');
  expect(res.ok()).toBeTruthy();
  const body = (await res.json()) as { items: ReadonlyArray<{ id: string }> };
  expect(body.items.length).toBeGreaterThan(0);
  return body.items[0].id;
}

async function waitForDetailReady(page: Page): Promise<void> {
  await page.waitForSelector('[data-testid="shared-game-detail-page"]', { timeout: 30_000 });

  await page.evaluate(async () => {
    if (typeof document !== 'undefined' && 'fonts' in document) {
      await (document as Document & { fonts: { ready: Promise<void> } }).fonts.ready;
    }
  });

  await page.evaluate(
    () =>
      new Promise<void>(resolve => {
        requestAnimationFrame(() => requestAnimationFrame(() => resolve()));
      })
  );
}

test.describe('Shared game detail — state coverage', () => {
  test.describe.configure({ retries: 0 });

  test('default state', async ({ page, request }) => {
    const id = await fetchFirstSharedGameId(request);
    await page.goto(`/shared-games/${id}`, { waitUntil: 'networkidle' });
    await waitForDetailReady(page);
    await expect(page).toHaveScreenshot('shared-game-detail-default.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('loading state', async ({ page, request }) => {
    const id = await fetchFirstSharedGameId(request);
    await page.goto(`/shared-games/${id}?state=loading`, { waitUntil: 'networkidle' });
    await waitForDetailReady(page);
    await expect(page).toHaveScreenshot('shared-game-detail-loading.png', {
      fullPage: true,
      // Skeleton pulse animations — mask to avoid flake.
      mask: [page.locator('[data-dynamic]'), page.locator('.animate-pulse')],
    });
  });

  test('error state', async ({ page, request }) => {
    const id = await fetchFirstSharedGameId(request);
    await page.goto(`/shared-games/${id}?state=error`, { waitUntil: 'networkidle' });
    await waitForDetailReady(page);
    await expect(page).toHaveScreenshot('shared-game-detail-error.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('empty-tab state (no toolkits/agents/kbs)', async ({ page, request }) => {
    const id = await fetchFirstSharedGameId(request);
    await page.goto(`/shared-games/${id}?state=empty-tab`, { waitUntil: 'networkidle' });
    await waitForDetailReady(page);
    // EmptyState renders by default in toolkits tab when override active.
    // Click toolkits tab to make the empty state visible (default tab is
    // overview which always renders description).
    await page.locator('[role="tab"]').nth(1).click();
    await page.waitForTimeout(150);
    await expect(page).toHaveScreenshot('shared-game-detail-empty-tab.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
