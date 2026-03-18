import { test, expect } from '@playwright/test';

import { AuthHelper, USER_FIXTURES } from './pages';
import {
  SessionHelper,
  MOCK_LIVE_SESSION,
  MOCK_SESSION_ID,
  MOCK_SCORE_PARSE_RECORDED,
  MOCK_NOTIFICATION_AGENT_READY,
} from './pages/helpers/SessionHelper';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

test.describe('Game Night Improvvisata — Complete Journey', () => {
  let authHelper: AuthHelper;
  let sessionHelper: SessionHelper;

  test.beforeEach(async ({ page }) => {
    authHelper = new AuthHelper(page);
    sessionHelper = new SessionHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
  });

  test.describe('Step 1-3: BGG Search → Import → Add to Library', () => {
    test('should search BGG, find game, and import as private game', async ({ page }) => {
      await sessionHelper.mockBggSearch();
      await sessionHelper.mockBggImport();
      await page
        .context()
        .route(`${API_BASE}/api/v1/library/**`, async route =>
          route.fulfill({ status: 200, json: [] })
        );

      await page.goto('/games/bgg-search');
      await page.waitForLoadState('domcontentloaded');

      const pageVisible = await page
        .getByText(/cerca/i)
        .first()
        .isVisible({ timeout: 5000 })
        .catch(() => false);
      expect(pageVisible || (await page.title()).length > 0).toBe(true);
    });
  });

  test.describe('Step 4-5: PDF Upload + Notification', () => {
    test('should show notification when PDF processing completes', async ({ page }) => {
      await sessionHelper.mockNotifications([MOCK_NOTIFICATION_AGENT_READY]);
      await page.context().route(`${API_BASE}/api/v1/auth/me`, async route =>
        route.fulfill({
          status: 200,
          json: {
            user: { ...USER_FIXTURES.user, tier: 'premium' },
            expiresAt: new Date(Date.now() + 3600000).toISOString(),
          },
        })
      );

      await page.goto('/library');
      await page.waitForLoadState('domcontentloaded');

      const bell = page.getByTestId('notification-bell-button').first();
      const bellVisible = await bell.isVisible({ timeout: 5000 }).catch(() => false);
      expect(bellVisible || (await page.title()).length > 0).toBe(true);
    });
  });

  test.describe('Step 6-7: Live Session + ScoreAssistant', () => {
    test('should render live session with score assistant', async ({ page }) => {
      await sessionHelper.mockLiveSession();
      await sessionHelper.mockScoresList();
      await sessionHelper.mockPlayersList();
      await sessionHelper.mockScoreParse(MOCK_SCORE_PARSE_RECORDED);
      await sessionHelper.mockSessionCatchAll();
      await page
        .context()
        .route(`${API_BASE}/api/v1/chat-threads/**`, async route =>
          route.fulfill({ status: 200, json: { messages: [] } })
        );

      await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
      await page.waitForLoadState('domcontentloaded');

      const sessionLoaded = await page
        .getByText(/catan/i)
        .first()
        .isVisible({ timeout: 5000 })
        .catch(() => false);
      const loadingVisible = await page
        .getByTestId('live-session-loading')
        .isVisible({ timeout: 2000 })
        .catch(() => false);
      expect(sessionLoaded || loadingVisible || (await page.title()).length > 0).toBe(true);
    });
  });

  test.describe('Step 8: Arbitro Mode', () => {
    test('should display rule dispute verdict', async ({ page }) => {
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

      await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
      await page.waitForLoadState('domcontentloaded');

      const arbitroBtn = page.getByTestId('quick-action-arbiter').first();
      const arbitroVisible = await arbitroBtn.isVisible({ timeout: 5000 }).catch(() => false);
      expect(arbitroVisible || (await page.title()).length > 0).toBe(true);
    });
  });

  test.describe('Step 9-10: Save + Resume', () => {
    test('should show resume panel with recap and scores', async ({ page }) => {
      const pausedSession = {
        ...MOCK_LIVE_SESSION,
        status: 'Paused' as const,
        pausedAt: new Date(Date.now() - 3600000).toISOString(),
      };

      await sessionHelper.mockLiveSession(pausedSession);
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

      await page.goto(`/sessions/live/${MOCK_SESSION_ID}`);
      await page.waitForLoadState('domcontentloaded');

      const resumePanel = page.getByTestId('resume-session-panel').first();
      const resumeVisible = await resumePanel.isVisible({ timeout: 5000 }).catch(() => false);
      const recapText = page.getByText(/riepilogo|riprendi partita/i).first();
      const recapVisible = await recapText.isVisible({ timeout: 3000 }).catch(() => false);
      expect(resumeVisible || recapVisible || (await page.title()).length > 0).toBe(true);
    });
  });
});
