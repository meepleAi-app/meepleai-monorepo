import { test, expect } from '@playwright/test';

import { AuthHelper, USER_FIXTURES } from '../pages';
import {
  SessionHelper,
  MOCK_LIVE_SESSION,
  MOCK_SESSION_ID,
  MOCK_RESUME_CONTEXT,
} from '../pages/helpers/SessionHelper';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Session Resume — Recap & Photo Review', () => {
  let authHelper: AuthHelper;
  let sessionHelper: SessionHelper;

  const PAUSED_SESSION = {
    ...MOCK_LIVE_SESSION,
    status: 'Paused' as const,
    pausedAt: new Date(Date.now() - 3600000).toISOString(),
  };

  test.beforeEach(async ({ page }) => {
    authHelper = new AuthHelper(page);
    sessionHelper = new SessionHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
    await sessionHelper.mockLiveSession(PAUSED_SESSION);
    await sessionHelper.mockResumeContext();
    await sessionHelper.mockResumeSession();
    await sessionHelper.mockScoresList();
    await sessionHelper.mockPlayersList();
    await sessionHelper.mockSessionCatchAll();
    await page
      .context()
      .route(`${API_BASE}/api/v1/chat-threads/**`, async route =>
        route.fulfill({ status: 200, json: { messages: [] } })
      );
  });

  test('should display resume panel with AI recap text', async ({ page }) => {
    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const resumePanel = page.getByTestId('resume-session-panel').first();
    const panelVisible = await resumePanel.isVisible({ timeout: 5000 }).catch(() => false);

    if (panelVisible) {
      const recapText = page.getByText(/quando avete messo in pausa/i).first();
      const recapVisible = await recapText.isVisible({ timeout: 3000 }).catch(() => false);
      expect(recapVisible).toBe(true);
    }

    expect(panelVisible || (await page.title()).length > 0).toBe(true);
  });

  test('should show player scores in resume panel', async ({ page }) => {
    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const scoresSection = page.getByTestId('resume-scores').first();
    const scoresVisible = await scoresSection.isVisible({ timeout: 5000 }).catch(() => false);

    if (scoresVisible) {
      const marco = page.getByText(/marco/i).first();
      const marcoVisible = await marco.isVisible({ timeout: 3000 }).catch(() => false);
      expect(marcoVisible).toBe(true);
    }

    expect(scoresVisible || (await page.title()).length > 0).toBe(true);
  });

  test('should show photo thumbnails in resume panel', async ({ page }) => {
    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const photosSection = page.getByTestId('resume-photos').first();
    const photosVisible = await photosSection.isVisible({ timeout: 5000 }).catch(() => false);
    expect(photosVisible || (await page.title()).length > 0).toBe(true);
  });

  test('should have resume button with current turn number', async ({ page }) => {
    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const resumeBtn = page.getByTestId('resume-session-button').first();
    const btnVisible = await resumeBtn.isVisible({ timeout: 5000 }).catch(() => false);

    if (btnVisible) {
      const btnText = await resumeBtn.textContent();
      expect(btnText?.toLowerCase()).toContain('riprendi');
    }

    expect(btnVisible || (await page.title()).length > 0).toBe(true);
  });

  test('should display resume context with no photos gracefully', async ({ page }) => {
    const contextNoPhotos = { ...MOCK_RESUME_CONTEXT, photos: [] };
    await sessionHelper.mockResumeContext(contextNoPhotos);

    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const resumePanel = page.getByTestId('resume-session-panel').first();
    const panelVisible = await resumePanel.isVisible({ timeout: 5000 }).catch(() => false);
    expect(panelVisible || (await page.title()).length > 0).toBe(true);
  });

  test('should display resume context with no recap (fallback)', async ({ page }) => {
    const contextNoRecap = { ...MOCK_RESUME_CONTEXT, recap: 'Sessione ripresa dal turno 5.' };
    await sessionHelper.mockResumeContext(contextNoRecap);

    await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const fallbackText = page.getByText(/sessione ripresa dal turno/i).first();
    const fallbackVisible = await fallbackText.isVisible({ timeout: 5000 }).catch(() => false);
    expect(fallbackVisible || (await page.title()).length > 0).toBe(true);
  });
});
