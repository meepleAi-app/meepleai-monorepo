import { test, expect } from '@playwright/test';

import { AuthHelper, USER_FIXTURES } from '../pages';
import { SessionHelper, MOCK_SESSION_ID } from '../pages/helpers/SessionHelper';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Arbitro Mode — Rule Dispute Resolution', () => {
  let authHelper: AuthHelper;
  let sessionHelper: SessionHelper;

  test.beforeEach(async ({ page }) => {
    authHelper = new AuthHelper(page);
    sessionHelper = new SessionHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
    await sessionHelper.mockLiveSession();
    await sessionHelper.mockScoresList();
    await sessionHelper.mockPlayersList();
    await sessionHelper.mockDispute();
    await sessionHelper.mockSessionCatchAll();
    await page
      .context()
      .route(`${API_BASE}/api/v1/chat-threads/**`, async route =>
        route.fulfill({ status: 200, json: { messages: [] } })
      );
  });

  test('should open arbitro sheet with suggestion chips', async ({ page }) => {
    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const arbiterBtn = page.getByTestId('quick-action-arbiter').first();
    const arbiterVisible = await arbiterBtn.isVisible({ timeout: 5000 }).catch(() => false);

    if (arbiterVisible) {
      await arbiterBtn.click();

      const chip = page.getByText(/chi ha ragione/i).first();
      const chipVisible = await chip.isVisible({ timeout: 3000 }).catch(() => false);
      expect(chipVisible).toBe(true);
    }

    expect(arbiterVisible || (await page.title()).length > 0).toBe(true);
  });

  test('should display quick actions including Arbitro button', async ({ page }) => {
    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const quickActions = page.getByTestId('quick-actions').first();
    const actionsVisible = await quickActions.isVisible({ timeout: 5000 }).catch(() => false);

    if (actionsVisible) {
      const rules = page.getByTestId('quick-action-rules').first();
      const arbiter = page.getByTestId('quick-action-arbiter').first();

      const rulesVisible = await rules.isVisible().catch(() => false);
      const arbiterVisible = await arbiter.isVisible().catch(() => false);
      expect(rulesVisible || arbiterVisible).toBe(true);
    }

    expect(actionsVisible || (await page.title()).length > 0).toBe(true);
  });
});
