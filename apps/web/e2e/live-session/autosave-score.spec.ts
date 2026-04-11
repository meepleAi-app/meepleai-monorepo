/**
 * Live Session — Autosave Indicator + Score Persistence E2E tests (Issue #325)
 *
 * Covers two scenarios:
 *   1. AutosaveIndicator renders on the live scores page
 *      (/sessions/live/{id}/scores) with data-testid="autosave-indicator"
 *
 *   2. Score update via SessionQuickActions.upsertScore sends the correct
 *      payload to POST /api/v1/sessions/{id}/scores-with-diary
 *
 * Mock boundary: HTTP layer only.
 *
 * Related: Session Flow v2.1 (#365/#369), ScoreBoard.tsx, AutosaveIndicator.tsx
 */

import { expect, test } from '@playwright/test';

import { AuthHelper, USER_FIXTURES } from '../pages';

// ── UUID Constants ─────────────────────────────────────────────────────────
const C = {
  SESSION_ID: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  GAME_ID: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  PARTICIPANT_ID: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
  // Must match USER_FIXTURES.user.id for identity checks
  USER_ID: 'user-test-1',
} as const;

const NOW = '2026-01-01T10:00:00.000Z';

// ── Shared mock helpers ────────────────────────────────────────────────────

/** Mock GET /live-sessions/{id} → InProgress session with one player */
async function mockLiveSession(page: import('@playwright/test').Page) {
  await page.context().route(`**/api/v1/live-sessions/${C.SESSION_ID}`, route => {
    if (route.request().method() !== 'GET') return route.continue();
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: C.SESSION_ID,
        sessionCode: 'TEST01',
        gameId: C.GAME_ID,
        gameName: 'Test Game',
        createdByUserId: C.USER_ID,
        status: 'InProgress',
        visibility: 'Private',
        groupId: null,
        createdAt: NOW,
        startedAt: NOW,
        pausedAt: null,
        completedAt: null,
        updatedAt: NOW,
        lastSavedAt: null,
        currentTurnIndex: 0,
        currentTurnPlayerId: C.PARTICIPANT_ID,
        agentMode: 'None',
        chatSessionId: null,
        notes: null,
        players: [
          {
            id: C.PARTICIPANT_ID,
            displayName: 'Player One',
            color: '#FF6B6B',
            role: 'Player',
            totalScore: 0,
            currentRank: 1,
            isActive: true,
          },
        ],
        teams: [],
        roundScores: [],
        scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
      }),
    });
  });
}

/** Mock GET /live-sessions/{id}/scores → empty list */
async function mockLiveSessionScores(page: import('@playwright/test').Page) {
  await page
    .context()
    .route(`**/api/v1/live-sessions/${C.SESSION_ID}/scores**`, route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
    );
}

/** Mock GET /sessions/current → active session matching C.SESSION_ID */
async function mockCurrentSession(page: import('@playwright/test').Page) {
  await page.context().route('**/api/v1/sessions/current', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        sessionId: C.SESSION_ID,
        gameId: C.GAME_ID,
        status: 'Active',
        sessionCode: 'TEST01',
        sessionDate: NOW,
        updatedAt: null,
        gameNightEventId: null,
      }),
    })
  );
}

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Live Session — Autosave + Score', () => {
  test.use({ viewport: { width: 1280, height: 800 } });

  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
    await page.goto('about:blank');
    await page.evaluate(() => localStorage.clear());

    // Suppress SignalR WebSocket connections (live session hub)
    await page.context().route('**/hubs/**', route => route.abort('failed'));
  });

  // ════════════════════════════════════════════════════════════════════════════
  // Scenario 1 — AutosaveIndicator renders on the live scores page
  //
  // Validates:
  //   - data-testid="autosave-indicator" is present when navigating to
  //     /sessions/live/{id}/scores
  //   - Default idle state label is visible
  // ════════════════════════════════════════════════════════════════════════════

  test('autosave indicator renders on live scores page', async ({ page }) => {
    // Suppress ContextualHand initialization
    await page
      .context()
      .route('**/api/v1/sessions/current', route => route.fulfill({ status: 204, body: '' }));

    // Suppress SSE streams
    await page.context().route('**/api/v1/game-sessions/**', route => route.abort('failed'));

    await page.goto(`/sessions/live/${C.SESSION_ID}/scores`);
    await page.waitForLoadState('networkidle');

    const indicator = page.getByTestId('autosave-indicator');
    await expect(indicator).toBeVisible({ timeout: 5_000 });
    await expect(indicator).toHaveAttribute('aria-label', 'Pronto');
  });

  // ════════════════════════════════════════════════════════════════════════════
  // Scenario 2 — Score update via SessionQuickActions sends correct payload
  //
  // Validates:
  //   - POST /api/v1/sessions/{id}/scores-with-diary is called when the user
  //     fills in a participant + score value and clicks "Aggiorna"
  //   - The payload contains the correct participantId and newValue
  // ════════════════════════════════════════════════════════════════════════════

  test('score update via SessionQuickActions sends correct payload', async ({ page }) => {
    const scoreCalls: { participantId: string; newValue: number }[] = [];

    await mockCurrentSession(page);
    await mockLiveSession(page);
    await mockLiveSessionScores(page);

    // Block SSE streams
    await page
      .context()
      .route(`**/api/v1/sessions/${C.SESSION_ID}/diary/stream`, route => route.abort('failed'));
    await page
      .context()
      .route(`**/api/v1/game-sessions/${C.SESSION_ID}/stream`, route => route.abort('failed'));

    // Capture POST /sessions/{id}/scores-with-diary
    await page
      .context()
      .route(`**/api/v1/sessions/${C.SESSION_ID}/scores-with-diary`, async route => {
        if (route.request().method() !== 'POST') return route.continue();
        const body = route.request().postDataJSON() as {
          participantId: string;
          newValue: number;
        };
        scoreCalls.push({ participantId: body.participantId, newValue: body.newValue });
        return route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            diaryEntryId: '11111111-1111-4111-8111-111111111111',
            participantId: body.participantId,
            newValue: body.newValue,
            round: 1,
          }),
        });
      });

    await page.goto(`/sessions/${C.SESSION_ID}`);
    await page.waitForLoadState('networkidle');

    // SessionQuickActions renders only when ContextualHand has an active session
    // and participants.length > 0
    const quickActions = page.getByTestId('session-quick-actions');
    await expect(quickActions).toBeVisible({ timeout: 10_000 });

    // Fill score form — selects use aria-label
    await page.getByRole('combobox', { name: 'Giocatore per punteggio' }).selectOption({
      value: C.PARTICIPANT_ID,
    });
    await page.getByRole('spinbutton', { name: 'Valore punteggio' }).fill('42');

    // Click "Aggiorna"
    await page.getByRole('button', { name: 'Aggiorna' }).click();

    // Assert POST was called with correct payload
    await expect
      .poll(() => scoreCalls.length, { timeout: 5_000, message: 'scores-with-diary not called' })
      .toBe(1);

    expect(scoreCalls[0]).toMatchObject({
      participantId: C.PARTICIPANT_ID,
      newValue: 42,
    });
  });
});
