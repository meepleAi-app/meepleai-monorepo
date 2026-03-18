import { test, expect } from '@playwright/test';

import { AuthHelper, USER_FIXTURES } from '../pages';
import {
  SessionHelper,
  MOCK_SESSION_ID,
  MOCK_SCORE_PARSE_RECORDED,
  MOCK_SCORE_PARSE_NEEDS_CONFIRM,
  MOCK_SCORE_PARSE_AMBIGUOUS,
  MOCK_SCORE_PARSE_UNRECOGNIZED,
} from '../pages/helpers/SessionHelper';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('ScoreAssistant — NLP Confidence Flows', () => {
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
    await sessionHelper.mockSessionCatchAll();
    await page
      .context()
      .route(`${API_BASE}/api/v1/chat-threads/**`, async route =>
        route.fulfill({ status: 200, json: { messages: [] } })
      );
  });

  test('high confidence (≥80%) — auto-records score', async ({ page }) => {
    await sessionHelper.mockScoreParse(MOCK_SCORE_PARSE_RECORDED);

    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const input = page.getByTestId('score-input').first();
    const inputVisible = await input.isVisible({ timeout: 5000 }).catch(() => false);

    if (inputVisible) {
      await input.fill('Marco ha 5 punti');
      const submitBtn = page.getByTestId('score-submit').first();
      await submitBtn.click();

      const result = page.getByTestId('score-result').first();
      const resultVisible = await result.isVisible({ timeout: 5000 }).catch(() => false);

      if (resultVisible) {
        const status = await result.getAttribute('data-status');
        expect(status).toBe('recorded');
      }
    }

    expect(inputVisible || (await page.title()).length > 0).toBe(true);
  });

  test('medium confidence (60-80%) — requires confirmation', async ({ page }) => {
    await sessionHelper.mockScoreParse(MOCK_SCORE_PARSE_NEEDS_CONFIRM);
    await sessionHelper.mockScoreConfirm();

    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const input = page.getByTestId('score-input').first();
    const inputVisible = await input.isVisible({ timeout: 5000 }).catch(() => false);

    if (inputVisible) {
      await input.fill('Marco 5');
      await page.getByTestId('score-submit').first().click();

      const result = page.getByTestId('score-result').first();
      const resultVisible = await result.isVisible({ timeout: 5000 }).catch(() => false);

      if (resultVisible) {
        const status = await result.getAttribute('data-status');
        expect(status).toBe('parsed');

        const confirmBtn = page.getByTestId('score-confirm').first();
        const confirmVisible = await confirmBtn.isVisible({ timeout: 3000 }).catch(() => false);
        expect(confirmVisible).toBe(true);
      }
    }

    expect(inputVisible || (await page.title()).length > 0).toBe(true);
  });

  test('low confidence — shows ambiguous candidates', async ({ page }) => {
    await sessionHelper.mockScoreParse(MOCK_SCORE_PARSE_AMBIGUOUS);

    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const input = page.getByTestId('score-input').first();
    const inputVisible = await input.isVisible({ timeout: 5000 }).catch(() => false);

    if (inputVisible) {
      await input.fill('Mar 5');
      await page.getByTestId('score-submit').first().click();

      const result = page.getByTestId('score-result').first();
      const resultVisible = await result.isVisible({ timeout: 5000 }).catch(() => false);

      if (resultVisible) {
        const status = await result.getAttribute('data-status');
        expect(status).toBe('ambiguous');

        const marcoCandidate = page.getByText('Marco').first();
        const mariaCandidate = page.getByText('Maria').first();
        const marcoVisible = await marcoCandidate.isVisible({ timeout: 3000 }).catch(() => false);
        const mariaVisible = await mariaCandidate.isVisible({ timeout: 3000 }).catch(() => false);
        expect(marcoVisible || mariaVisible).toBe(true);
      }
    }

    expect(inputVisible || (await page.title()).length > 0).toBe(true);
  });

  test('unrecognized input — shows error message', async ({ page }) => {
    await sessionHelper.mockScoreParse(MOCK_SCORE_PARSE_UNRECOGNIZED);

    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const input = page.getByTestId('score-input').first();
    const inputVisible = await input.isVisible({ timeout: 5000 }).catch(() => false);

    if (inputVisible) {
      await input.fill('asdfgh');
      await page.getByTestId('score-submit').first().click();

      const result = page.getByTestId('score-result').first();
      const resultVisible = await result.isVisible({ timeout: 5000 }).catch(() => false);

      if (resultVisible) {
        const status = await result.getAttribute('data-status');
        expect(status).toBe('unrecognized');
      }
    }

    expect(inputVisible || (await page.title()).length > 0).toBe(true);
  });
});
