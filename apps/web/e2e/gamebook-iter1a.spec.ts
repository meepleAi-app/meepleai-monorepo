/**
 * Gamebook Iter 1.A — E2E smoke tests
 *
 * @ci tag: synthetic mocked flow, runs in CI without real DB/auth
 * @dogfood tag: manual Aaron-validation only, skipped in CI
 *
 * Uses page.route() to mock backend — same pattern as add-game-wizard.spec.ts.
 */

import { test, expect } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const MOCK_USER = {
  user: {
    id: '22222222-2222-2222-2222-222222222222',
    email: 'aaron@meepleai.dev',
    displayName: 'Aaron',
    role: 'User',
  },
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
};

const nanolithGameId = '11111111-1111-1111-1111-111111111111';

const fakeCampaign = {
  id: '99999999-9999-9999-9999-999999999999',
  gameId: nanolithGameId,
  ownerUserId: '22222222-2222-2222-2222-222222222222',
  title: 'E2E Smoke Campagna',
  currentParagraph: 0,
  history: [],
  lastReadAt: '2026-05-07T12:00:00Z',
  createdAt: '2026-05-07T12:00:00Z',
  updatedAt: '2026-05-07T12:00:00Z',
};

test.describe('Iter 1.A — Gamebook campaign play page', () => {
  test('@ci play page renders title and paragraph badge', async ({ page }) => {
    // Mocked synthetic flow — does NOT require real DB/auth.
    // Exercises GamebookPlayShell wiring: useGamebookCampaign → data → render.

    // Catch-all for unmocked API calls
    await page.route(`${API_BASE}/api/**`, async route => {
      const method = route.request().method();
      if (method === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      }
    });

    // Auth
    await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_USER),
      });
    });

    await page.route(`${API_BASE}/api/v1/users/me`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: MOCK_USER.user.id,
          email: MOCK_USER.user.email,
          displayName: MOCK_USER.user.displayName,
          role: MOCK_USER.user.role,
          createdAt: '2026-01-01T00:00:00Z',
        }),
      });
    });

    // Campaign GET for play page
    await page.route(`${API_BASE}/api/v1/gamebook/campaigns/${fakeCampaign.id}`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(fakeCampaign),
      });
    });

    // Navigate directly to the play page (bypasses game-detail CTA for this test)
    await page.goto(`/library/games/${nanolithGameId}/play/${fakeCampaign.id}`);

    // Campaign title visible in shell header
    await expect(page.getByText(fakeCampaign.title)).toBeVisible({ timeout: 10000 });

    // Paragraph badge shows "§ —" for currentParagraph = 0
    await expect(page.getByText(/§\s*—/)).toBeVisible();
  });

  // eslint-disable-next-line playwright/no-skipped-test
  test.skip('@dogfood Aaron full setup tutorial flow with Nanolith real KB', () => {
    // Manual run by Aaron — NOT in CI. See design doc §3 N1.1 scenario.
    // Prerequisites:
    //   1. Nanolith Press Start + Rules indexed via `make seed-index`
    //   2. Nanolith Tutor agent active (admin panel → Agents)
    //   3. Aaron logged in as badsworm@gmail.com (superadmin)
    // Steps:
    //   1. Navigate to /library/games/<nanolith-real-id>
    //   2. Click "Nuova campagna libro game" CTA
    //   3. Fill title → submit → assert redirect to /play/<id>
    //   4. Ask 5 N1 setup queries → record in nanolith-dogfood-eval.gsheet
    //   5. Ask 5 N2 in-game rules queries → validate §0.4 confidence binning
  });
});
