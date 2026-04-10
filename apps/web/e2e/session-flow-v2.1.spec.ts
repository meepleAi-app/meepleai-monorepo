/**
 * Session Flow v2.1 — Playwright E2E tests (Issue #373)
 *
 * Covers the ContextualHand sidebar + GamePickerDialog flows:
 *   1. Happy path: idle sidebar → GamePicker → create session → navigate to session detail
 *   2. KB not ready: game item is disabled in the picker
 *   3. Sidebar lifecycle: active session → pause → resume via sidebar slot
 *   4. GameNight diary UNION: GET /game-nights/{id}/diary surfaces events from both sessions
 *
 * Mock boundary: HTTP layer only. Zustand stores, Zod validators, router, and
 * Playwright route matchers all run for real.
 *
 * Sidebar uses `hidden md:flex` — tests run at 1280×800 (desktop viewport).
 * SignalR / SSE diary stream is aborted to prevent retry storms.
 *
 * Related PRs: #365 (backend), #369 (frontend)
 */

import { expect, test } from '@playwright/test';

import { AuthHelper, USER_FIXTURES } from './pages';

// ── UUID Constants ─────────────────────────────────────────────────────────
// v4 format (3rd group "4xxx", 4th group starts with "8/9/a/b")
const C = {
  GAME_ID_READY: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  GAME_ID_NOT_READY: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  GAME_ID_READY_2: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc',
  SESSION_ID: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
  SESSION_ID_2: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
  GAME_NIGHT_ID: 'ffffffff-ffff-4fff-8fff-ffffffffffff',
  ENTRY_ID_1: '11111111-1111-4111-8111-111111111111',
  ENTRY_ID_2: '22222222-2222-4222-8222-222222222222',
  ENTRY_ID_3: '33333333-3333-4333-8333-333333333333',
  USER_ID: '55555555-5555-4555-8555-555555555555',
  PARTICIPANT_ID: '66666666-6666-4666-8666-666666666661',
} as const;

const NOW = new Date().toISOString();

// ── Library entry factory ──────────────────────────────────────────────────
function makeLibraryEntry(gameId: string, title: string, publisher = 'Test Publisher') {
  return {
    id: gameId,
    userId: C.USER_ID,
    gameId,
    gameTitle: title,
    gamePublisher: publisher,
    gameYearPublished: 2020,
    gameIconUrl: null,
    gameImageUrl: null,
    addedAt: NOW,
    notes: null,
    isFavorite: false,
    currentState: 'Owned',
    stateChangedAt: null,
    stateNotes: null,
    hasKb: true,
    kbCardCount: 2,
    kbIndexedCount: 2,
    kbProcessingCount: 0,
    ownershipDeclaredAt: null,
    hasRagAccess: true,
    agentIsOwned: true,
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 60,
    complexityRating: 2.5,
    averageRating: 7.5,
    privateGameId: null,
    isPrivateGame: false,
    canProposeToCatalog: false,
  };
}

// ── Shared library mock helper ─────────────────────────────────────────────
async function mockLibrary(
  page: import('@playwright/test').Page,
  games: ReturnType<typeof makeLibraryEntry>[]
) {
  await page.context().route(/\/api\/v1\/library(\?.*)?$/, route => {
    if (route.request().method() !== 'GET') return route.continue();
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items: games, total: games.length, page: 1, pageSize: 200 }),
    });
  });
}

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Session Flow v2.1', () => {
  // Desktop viewport — sidebar is hidden on mobile (hidden md:flex)
  test.use({ viewport: { width: 1280, height: 800 } });

  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
  });

  // ════════════════════════════════════════════════════════════════════════════
  // Scenario 1 — Happy path
  //
  // Flow: idle sidebar → click "Nuova partita" → GamePickerDialog opens →
  //       select KB-ready game → POST /games/{id}/sessions → navigate to
  //       /sessions/{sessionId}
  //
  // Validates:
  //   - ContextualHandSidebar renders idle state with "Nuova partita" CTA
  //   - GamePickerDialog renders, fetches library, shows game
  //   - GamePickerItem is enabled when KB is ready
  //   - Clicking game triggers POST /games/{id}/sessions
  //   - Store transitions to active and router navigates to session detail
  // ════════════════════════════════════════════════════════════════════════════

  test('happy path: idle sidebar → GamePicker → create session → navigate', async ({ page }) => {
    // initialize() calls GET /sessions/current → 204 (no active session)
    await page
      .context()
      .route('**/api/v1/sessions/current', route => route.fulfill({ status: 204, body: '' }));

    // Library: one KB-ready game (Catan)
    await mockLibrary(page, [makeLibraryEntry(C.GAME_ID_READY, 'Catan')]);

    // KB readiness for Catan → ready
    await page.context().route(`**/api/v1/games/${C.GAME_ID_READY}/kb-readiness`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          isReady: true,
          state: 'Ready',
          readyPdfCount: 2,
          failedPdfCount: 0,
          warnings: [],
        }),
      })
    );

    // POST /games/{id}/sessions → CreateSessionResult
    await page.context().route(`**/api/v1/games/${C.GAME_ID_READY}/sessions`, route => {
      if (route.request().method() !== 'POST') return route.continue();
      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({
          sessionId: C.SESSION_ID,
          sessionCode: 'CATAN1',
          participants: [
            {
              id: C.PARTICIPANT_ID,
              userId: C.USER_ID,
              displayName: 'Host',
              isOwner: true,
              joinOrder: 1,
              finalRank: null,
              totalScore: 0,
            },
          ],
          gameNightEventId: C.GAME_NIGHT_ID,
          gameNightWasCreated: true,
          agentDefinitionId: null,
          toolkitId: null,
        }),
      });
    });

    // Block SSE diary stream to avoid retry storms
    await page
      .context()
      .route(`**/api/v1/sessions/${C.SESSION_ID}/diary/stream`, route => route.abort('failed'));

    // Stub session hydration so the detail page does not throw
    await page.context().route(`**/api/v1/live-sessions/${C.SESSION_ID}`, route => {
      if (route.request().method() !== 'GET') return route.continue();
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: C.SESSION_ID,
          sessionCode: 'CATAN1',
          gameId: C.GAME_ID_READY,
          gameName: 'Catan',
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
          players: [],
          teams: [],
          roundScores: [],
          scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
        }),
      });
    });

    // Block SignalR
    await page.context().route('**/hubs/**', route => route.abort('failed'));

    // Navigate to a page that renders the authenticated shell (sidebar included)
    await page.goto('/library');
    await page.waitForLoadState('domcontentloaded');

    // ── Assert sidebar rendered ────────────────────────────────────────────
    const sidebar = page.getByTestId('contextual-hand-sidebar');
    await sidebar.waitFor({ state: 'visible', timeout: 10_000 });

    // Idle state: "Nuova partita" button visible
    const newGameBtn = page.getByTestId('start-session-from-sidebar');
    await expect(newGameBtn).toBeVisible({ timeout: 5_000 });

    // ── Open picker ────────────────────────────────────────────────────────
    await newGameBtn.click();
    await expect(page.getByTestId('game-picker-dialog')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByTestId('game-picker-list')).toBeVisible();

    // ── Catan appears and is enabled ───────────────────────────────────────
    const catanItem = page.getByTestId(`game-picker-item-${C.GAME_ID_READY}`);
    await expect(catanItem).toBeVisible({ timeout: 5_000 });
    // KB-ready → button not disabled
    await expect(catanItem).toBeEnabled();

    // ── Select Catan → session created → navigate ──────────────────────────
    await catanItem.click();

    await page.waitForURL(`**/sessions/${C.SESSION_ID}`, { timeout: 10_000 });
    expect(page.url()).toContain(`/sessions/${C.SESSION_ID}`);
  });

  // ════════════════════════════════════════════════════════════════════════════
  // Scenario 2 — KB not ready
  //
  // When a game's KB is not ready (isReady=false), the picker item button must
  // be rendered in a disabled state so users cannot start a session without a
  // Knowledge Base.
  //
  // Validates:
  //   - GamePickerItem fetches GET /games/{id}/kb-readiness
  //   - When isReady=false, the button carries the `disabled` attribute
  // ════════════════════════════════════════════════════════════════════════════

  test('KB not ready: game item is disabled in picker', async ({ page }) => {
    // Idle sidebar
    await page
      .context()
      .route('**/api/v1/sessions/current', route => route.fulfill({ status: 204, body: '' }));

    // Library: one NOT-ready game
    await mockLibrary(page, [makeLibraryEntry(C.GAME_ID_NOT_READY, 'Twilight Imperium')]);

    // KB readiness → not ready (NoPdfs)
    await page.context().route(`**/api/v1/games/${C.GAME_ID_NOT_READY}/kb-readiness`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          isReady: false,
          state: 'NoPdfs',
          readyPdfCount: 0,
          failedPdfCount: 0,
          warnings: [],
        }),
      })
    );

    await page.goto('/library');
    await page.waitForLoadState('domcontentloaded');

    const sidebar = page.getByTestId('contextual-hand-sidebar');
    await sidebar.waitFor({ state: 'visible', timeout: 10_000 });

    await page.getByTestId('start-session-from-sidebar').click();
    await expect(page.getByTestId('game-picker-dialog')).toBeVisible({ timeout: 5_000 });

    // Game item visible
    const gameItem = page.getByTestId(`game-picker-item-${C.GAME_ID_NOT_READY}`);
    await expect(gameItem).toBeVisible({ timeout: 5_000 });

    // Wait for KB status to load (spinner disappears when fetch completes).
    // GamePickerItem sets disabled={!isReady && !isKbLoading} so the button
    // gains the disabled attribute only after the query resolves.
    await expect
      .poll(() => gameItem.getAttribute('disabled'), {
        timeout: 5_000,
        message: 'game item should be disabled after KB readiness resolves to not-ready',
      })
      .not.toBeNull();
  });

  // ════════════════════════════════════════════════════════════════════════════
  // Scenario 3 — Sidebar lifecycle: active → pause → resume
  //
  // When getCurrentSession returns an active session, the sidebar slot shows
  // the session card + "Pausa" CTA. Clicking pauses the session (POST /pause),
  // the label flips to "Riprendi". Clicking again resumes (POST /resume) and
  // the label returns to "Pausa".
  //
  // Validates:
  //   - Sidebar initializes from GET /sessions/current (active session)
  //   - ContextualHandSlot renders session slot with pause/resume toggle
  //   - POST /sessions/{id}/pause is called when "Pausa" clicked
  //   - POST /sessions/{id}/resume is called when "Riprendi" clicked
  //   - Zustand context flips active ⇄ paused on success
  // ════════════════════════════════════════════════════════════════════════════

  test('sidebar lifecycle: active session → pause → Riprendi → resume', async ({ page }) => {
    const pauseCalls: number[] = [];
    const resumeCalls: number[] = [];

    // GET /sessions/current → active session
    await page.context().route('**/api/v1/sessions/current', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          sessionId: C.SESSION_ID,
          gameId: C.GAME_ID_READY,
          status: 'Active',
          sessionCode: 'CATAN1',
          sessionDate: NOW,
          updatedAt: null,
          gameNightEventId: C.GAME_NIGHT_ID,
        }),
      })
    );

    // POST /pause
    await page.context().route(`**/api/v1/sessions/${C.SESSION_ID}/pause`, route => {
      if (route.request().method() !== 'POST') return route.continue();
      pauseCalls.push(Date.now());
      return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    // POST /resume
    await page.context().route(`**/api/v1/sessions/${C.SESSION_ID}/resume`, route => {
      if (route.request().method() !== 'POST') return route.continue();
      resumeCalls.push(Date.now());
      return route.fulfill({ status: 200, contentType: 'application/json', body: '{}' });
    });

    // Block SSE diary stream (active session triggers subscribeToDiary)
    await page
      .context()
      .route(`**/api/v1/sessions/${C.SESSION_ID}/diary/stream`, route => route.abort('failed'));

    await page.goto('/library');
    await page.waitForLoadState('domcontentloaded');

    const sidebar = page.getByTestId('contextual-hand-sidebar');
    await sidebar.waitFor({ state: 'visible', timeout: 10_000 });

    // ── Active: "Pausa" button visible ────────────────────────────────────
    // The session slot renders a toggle button with text "Pausa" or "Riprendi"
    const pauseBtn = sidebar.getByRole('button', { name: /^pausa$/i });
    await expect(pauseBtn).toBeVisible({ timeout: 5_000 });

    // ── Click Pausa → POST /pause called ──────────────────────────────────
    await pauseBtn.click();
    await expect
      .poll(() => pauseCalls.length, { timeout: 5_000, message: 'pause endpoint not called' })
      .toBe(1);

    // ── Context flipped to paused: "Riprendi" visible ─────────────────────
    const resumeBtn = sidebar.getByRole('button', { name: /^riprendi$/i });
    await expect(resumeBtn).toBeVisible({ timeout: 5_000 });

    // ── Click Riprendi → POST /resume called ──────────────────────────────
    await resumeBtn.click();
    await expect
      .poll(() => resumeCalls.length, { timeout: 5_000, message: 'resume endpoint not called' })
      .toBe(1);

    // ── Back to active: "Pausa" visible again ─────────────────────────────
    await expect(sidebar.getByRole('button', { name: /^pausa$/i })).toBeVisible({
      timeout: 5_000,
    });
  });

  // ════════════════════════════════════════════════════════════════════════════
  // Scenario 4 — GameNight diary UNION
  //
  // The GameNightDiaryPanel on /game-nights/{id} calls
  // GET /api/v1/game-nights/{id}/diary and renders one `data-testid="diary-entry"`
  // per event across all sessions in the night.
  //
  // Validates:
  //   - GameNightDiaryPanel is rendered when event status is Published/Completed
  //   - GET /game-nights/{id}/diary is called and returns diary entries
  //   - Entries from both sessions are rendered (3 diary-entry elements)
  // ════════════════════════════════════════════════════════════════════════════

  test('GameNight diary UNION: diary entries from both sessions rendered', async ({ page }) => {
    // GET /sessions/current → idle (not required for this test, just suppress call)
    await page
      .context()
      .route('**/api/v1/sessions/current', route => route.fulfill({ status: 204, body: '' }));

    // GET /game-nights/{id} → Published event (triggers diary panel)
    await page.context().route(`**/api/v1/game-nights/${C.GAME_NIGHT_ID}`, route => {
      if (route.request().method() !== 'GET') return route.continue();
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: C.GAME_NIGHT_ID,
          organizerId: C.USER_ID,
          organizerName: 'Host',
          title: 'Serata Giochi',
          description: null,
          scheduledAt: NOW,
          location: null,
          maxPlayers: null,
          gameIds: [C.GAME_ID_READY, C.GAME_ID_READY_2],
          status: 'Published',
          acceptedCount: 1,
          pendingCount: 0,
          totalInvited: 1,
          createdAt: NOW,
          updatedAt: null,
        }),
      });
    });

    // GET /game-nights/{id}/rsvps → empty list
    await page
      .context()
      .route(`**/api/v1/game-nights/${C.GAME_NIGHT_ID}/rsvps`, route =>
        route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
      );

    // GET /game-nights/{id}/diary → UNION of 3 events from 2 sessions
    const unionEntries = [
      {
        id: C.ENTRY_ID_1,
        sessionId: C.SESSION_ID,
        gameNightId: C.GAME_NIGHT_ID,
        eventType: 'SessionStarted',
        timestamp: new Date(Date.now() - 120_000).toISOString(),
        payload: null,
        createdBy: C.USER_ID,
        source: 'system',
      },
      {
        id: C.ENTRY_ID_2,
        sessionId: C.SESSION_ID_2,
        gameNightId: C.GAME_NIGHT_ID,
        eventType: 'SessionStarted',
        timestamp: new Date(Date.now() - 60_000).toISOString(),
        payload: null,
        createdBy: C.USER_ID,
        source: 'system',
      },
      {
        id: C.ENTRY_ID_3,
        sessionId: C.SESSION_ID_2,
        gameNightId: C.GAME_NIGHT_ID,
        eventType: 'GameCompleted',
        timestamp: new Date().toISOString(),
        payload: JSON.stringify({ winner: 'Alice' }),
        createdBy: C.USER_ID,
        source: 'system',
      },
    ];

    await page.context().route(`**/api/v1/game-nights/${C.GAME_NIGHT_ID}/diary**`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(unionEntries),
      })
    );

    // Shared games are fetched when event has gameIds (only for Draft)
    // Status=Published so useSharedGames is disabled — no mock needed.

    // Navigate to the game night detail page
    await page.goto(`/game-nights/${C.GAME_NIGHT_ID}`);
    await page.waitForLoadState('domcontentloaded');

    // Heading "Serata Giochi" confirms the page loaded
    await expect(page.getByRole('heading', { name: 'Serata Giochi' }).first()).toBeVisible({
      timeout: 10_000,
    });

    // Diary panel: 3 diary-entry elements (one per event from both sessions)
    const diaryEntries = page.getByTestId('diary-entry');
    await expect(diaryEntries).toHaveCount(3, { timeout: 5_000 });
  });
});
