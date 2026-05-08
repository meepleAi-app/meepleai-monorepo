/**
 * Gamebook Resume Picker — E2E coverage 4 stati mockup G (issue #835).
 *
 * @ci tag: synthetic mocked flow, runs in CI without real DB/auth.
 * Mock useUserCampaigns via page.route() per coprire dispatch logic.
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

const NANOLITH_ID = '11111111-1111-1111-1111-111111111111';

function isoDaysAgo(days: number): string {
  const d = new Date();
  d.setUTCDate(d.getUTCDate() - days);
  return d.toISOString();
}

function buildCampaign(overrides: Record<string, unknown> = {}) {
  return {
    id: '99999999-9999-9999-9999-999999999999',
    gameId: NANOLITH_ID,
    ownerUserId: MOCK_USER.user.id,
    title: 'Veglia di Brace',
    currentParagraph: 289,
    history: [200, 250, 289],
    lastReadAt: isoDaysAgo(7),
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: isoDaysAgo(7),
    ...overrides,
  };
}

async function setupAuthMocks(page: import('@playwright/test').Page) {
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
}

async function mockCampaigns(page: import('@playwright/test').Page, payload: unknown[]) {
  await page.route(/\/api\/v1\/gamebook\/campaigns(\?gameId=.*)?$/, async route => {
    if (route.request().method() === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(payload),
      });
    } else {
      await route.continue();
    }
  });
}

test.describe('@ci Resume picker — 4 stati mockup G (#835)', () => {
  test('state-01-first-time when zero campaigns', async ({ page }) => {
    await setupAuthMocks(page);
    await mockCampaigns(page, []);

    await page.goto(`/library/games/${NANOLITH_ID}/play`);

    await expect(page.getByTestId('gamebook-resume-empty-first-time')).toBeVisible({
      timeout: 10000,
    });
  });

  test('state-02-single-resume when 1 fresh campaign', async ({ page }) => {
    await setupAuthMocks(page);
    await mockCampaigns(page, [buildCampaign({ lastReadAt: isoDaysAgo(7) })]);

    await page.goto(`/library/games/${NANOLITH_ID}/play`);

    await expect(page.getByTestId('gamebook-resume-hero')).toBeVisible({ timeout: 10000 });
    await expect(page.getByTestId('gamebook-resume-stale-warning')).toHaveCount(0);
  });

  test('state-03-multi-campaign when 2+ campaigns', async ({ page }) => {
    await setupAuthMocks(page);
    await mockCampaigns(page, [
      buildCampaign({ id: '11111111-aaaa-aaaa-aaaa-111111111111', title: 'Camp 1' }),
      buildCampaign({ id: '22222222-aaaa-aaaa-aaaa-222222222222', title: 'Camp 2' }),
    ]);

    await page.goto(`/library/games/${NANOLITH_ID}/play`);

    await expect(page.getByTestId('gamebook-resume-multi-list')).toBeVisible({ timeout: 10000 });
  });

  test('state-04-stale-warning when 1 stale campaign (>= 90 days)', async ({ page }) => {
    await setupAuthMocks(page);
    await mockCampaigns(page, [buildCampaign({ lastReadAt: isoDaysAgo(100) })]);

    await page.goto(`/library/games/${NANOLITH_ID}/play`);

    await expect(page.getByTestId('gamebook-resume-stale-warning')).toBeVisible({
      timeout: 10000,
    });
  });
});
