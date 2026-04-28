/**
 * Visual contract — /shared-games/[id] route migrata vs mockup baseline.
 *
 * Issue #603 (Wave A.4 Shared Game Detail migration) · umbrella #579
 * V2 Migration Phase 1.
 *
 * Strategia (TDD red→green):
 *   - **Red** (corrente, pre-bootstrap): la baseline PNG non esiste ancora.
 *   - **Green**: route v2 attiva (`(public)/shared-games/[id]/page-client.tsx`)
 *     con hero + tabs (5 tabpanels) + sticky CTA + community contributors.
 *
 * Bootstrap baseline (one-time, post-migration):
 *   `gh workflow run 266963272 --ref feature/issue-603-shared-game-detail-fe-v2 \
 *     -f mode=bootstrap -f project_filter=both`
 *   Il runner Linux genera PNG canonical viewport reali (375×812 mobile,
 *   1440×900 desktop) e le carica come artifact.
 *
 * Strategia ID:
 *   - Fetch index `/api/v1/shared-games?pageSize=1` per ottenere primo ID
 *     seeded (stable cross-snapshot grazie a sort default).
 *   - Naviga a `/shared-games/{id}` con tab default "overview".
 *
 * Hybrid masking:
 *   Le zone marcate `data-dynamic` (rating live, contatori, contributors
 *   strip) sono mascherate per evitare flake.
 */
import { test, expect, type Page, type APIRequestContext } from '@playwright/test';

const SLUG = 'sp3-shared-game-detail';

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

test.describe('V2 Visual Migrated — /shared-games/[id] matches mockup baseline', () => {
  test.describe.configure({ retries: 0 });

  test('Shared game detail default tab matches sp3-shared-game-detail mockup', async ({
    page,
    request,
  }) => {
    const id = await fetchFirstSharedGameId(request);
    await page.goto(`/shared-games/${id}`, { waitUntil: 'networkidle' });
    await waitForDetailReady(page);

    await expect(page).toHaveScreenshot(`${SLUG}.png`, {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });
});
