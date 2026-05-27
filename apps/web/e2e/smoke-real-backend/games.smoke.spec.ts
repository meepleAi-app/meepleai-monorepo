import { test, expect } from '@playwright/test';

import { smokeLogin, applySessionToPage } from './_helpers/auth';

test.describe('SMOKE — /games redirect to /library (real backend round-trip)', () => {
  test('GET /games?tab=library redirects to /library and renders LibraryHub without error', async ({
    page,
    request,
  }) => {
    const { cookieHeader } = await smokeLogin(request);
    await applySessionToPage(page, cookieHeader);
    // `/games` was a multi-tab hub (Wave B.1 #633). PR #1567 (Issue #1521) replaced its
    // page.tsx with `redirect('/library')` and removed GamesLibraryView (which carried
    // the legacy `data-slot="games-library-view"`). The `?tab=library` query is dropped
    // by the redirect. We assert both that the redirect lands on `/library` AND that
    // LibraryHub renders without entering its error FSM — same pattern as
    // library.smoke.spec.ts:26 ("library-hub-v2" selector + [data-state="error"]).
    //
    // Follow-up #1566 will wire the shelf-ready features/games components into
    // LibraryHub; until then the canonical surface is library-hub-v2.
    await page.goto('/games?tab=library', { waitUntil: 'domcontentloaded' });
    expect(new URL(page.url()).pathname).toBe('/library');
    await page.waitForSelector('[data-slot="library-hub-v2"]', { timeout: 30_000 });
    const errorState = await page.locator('[data-state="error"]').count();
    expect(errorState).toBe(0);
  });
});
