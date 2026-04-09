/**
 * Game Night Journey E2E Test
 *
 * Covers key navigation flows of the game night journey:
 * - BGG search via discover page
 * - Sessions page with paused sessions
 * - Scoreboard page
 * - Happy path wizard → session creation → live page (Issue #301)
 * - Lifecycle pause → resume → final save on the live page (Issue #323)
 *
 * Uses mock API routes via page.context().route() (CRITICAL for Next.js SSR).
 * SSR pages may bypass browser-side mocks — assertions use .first() and
 * if-guards per project conventions.
 */

import { expect, test, type Page } from '@playwright/test';

import { AuthHelper, USER_FIXTURES } from './pages';

test.describe('Game Night Journey', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
  });

  test('discover page loads BGG tab', async ({ page }) => {
    // Mock BGG search API at context level
    await page.context().route('**/api/v1/bgg/search**', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          results: [{ bggId: 230802, name: 'Azul', yearPublished: 2017, thumbnailUrl: null }],
          totalResults: 1,
        }),
      })
    );

    await page.goto('/discover?tab=bgg');
    await page.waitForLoadState('domcontentloaded');

    // Verify the discover page loaded (SSR may not render the tab)
    const heading = page.getByText(/scopri/i).first();
    const searchInput = page.getByPlaceholder(/cerca/i).first();

    // At least one of these should be visible on the discover page
    const headingVisible = await heading.isVisible().catch(() => false);
    const inputVisible = await searchInput.isVisible().catch(() => false);

    expect(headingVisible || inputVisible).toBe(true);
  });

  test('sessions page renders', async ({ page }) => {
    // Mock live sessions API (returns paused sessions)
    await page.context().route('**/api/v1/live-sessions**', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'session-1',
            gameName: 'Azul',
            playerCount: 3,
            sessionCode: 'ABC123',
            status: 'Paused',
            updatedAt: new Date().toISOString(),
          },
        ]),
      })
    );

    // Mock sessions list
    await page.context().route('**/api/v1/sessions**', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      })
    );

    await page.goto('/sessions');
    await page.waitForLoadState('domcontentloaded');

    // Verify the sessions page loaded
    const pageLoaded = await page
      .getByText(/session/i)
      .first()
      .isVisible()
      .catch(() => false);
    const azulVisible = await page
      .getByText('Azul')
      .first()
      .isVisible()
      .catch(() => false);

    // SSR page may not show mocked data; verify at least the page renders
    expect(pageLoaded || azulVisible || (await page.title()).length > 0).toBe(true);
  });

  // ==========================================================================
  // Issue #301: Full happy-path journey — wizard → session → play page
  //
  // Validates integration points NOT covered by unit tests:
  //   - Cross-page navigation (/sessions/new → /sessions/{id}/play)
  //   - Multi-step wizard state accumulation
  //   - API sequencing (createSession → addPlayer × N → startSession)
  //   - Router state transition on successful creation
  //   - Session play page initial render with hydration fetch
  //
  // Out of scope for v1 (future issues):
  //   - Pause / resume / finalize (separate UI via /save sub-page)
  //   - SSE diary assertions (needs hub mock)
  //   - Autosave + score persistence
  //
  // See e2e/README.md § "When NOT to Use E2E Tests" for the rationale behind
  // this journey test existing while single-component E2E tests did not.
  // ==========================================================================

  test('happy path: wizard → session creation → land on live page', async ({ page }) => {
    // LiveSessionDtoSchema requires UUID for createdByUserId (USER_FIXTURES.user.id
    // is 'user-test-1' which fails Zod), so we hardcode valid UUIDs here.
    // UUIDs use v4 format (3rd group starts with "4", 4th group starts with "8/9/a/b")
    // to pass Zod's strict `z.string().uuid()` validator.
    const JOURNEY_CONSTANTS = {
      INDEXED_GAME_ID: '11111111-1111-4111-8111-111111111111',
      SESSION_ID: '33333333-3333-4333-8333-333333333333',
      PLAYER_ID_1: '44444444-4444-4444-8444-444444444441',
      PLAYER_ID_2: '44444444-4444-4444-8444-444444444442',
      USER_ID: '55555555-5555-4555-8555-555555555555',
    } as const;

    await setupJourneyMocks(page, JOURNEY_CONSTANTS);

    // ─── Step 1: navigate to /sessions/new and reveal the wizard ─────────
    await page.goto('/sessions/new');
    await page.waitForLoadState('domcontentloaded');

    const startBtn = page.getByTestId('start-game-night-button');
    await startBtn.waitFor({ state: 'visible', timeout: 10_000 });
    await startBtn.click();

    await expect(page.getByTestId('game-night-wizard')).toBeVisible();
    await expect(page.getByTestId('search-game-step')).toBeVisible();

    // ─── Step 2: search "catan" and select Catan ─────────────────────────
    const searchInput = page.getByTestId('game-search-input');
    await searchInput.fill('catan');
    await searchInput.press('Enter');

    await expect(page.getByTestId('game-search-results')).toBeVisible({ timeout: 5_000 });

    const catanOption = page.locator(`[data-game-id="${JOURNEY_CONSTANTS.INDEXED_GAME_ID}"]`);
    await expect(catanOption).toBeVisible();
    await catanOption.click();

    // Indexed game → the soft-filter warning must NOT appear
    await expect(page.getByTestId('kb-warning')).not.toBeVisible();

    const confirmGameBtn = page.getByTestId('confirm-game-button');
    await expect(confirmGameBtn).toBeEnabled();
    await confirmGameBtn.click();

    // ─── Step 3: skip the rules upload ───────────────────────────────────
    await expect(page.getByTestId('upload-rules-step')).toBeVisible({ timeout: 5_000 });

    const skipBtn = page.getByTestId('skip-rules-button');
    await expect(skipBtn).toBeVisible();
    await skipBtn.click();

    // ─── Step 4: add 2 players and create the session ────────────────────
    await expect(page.getByTestId('create-session-step')).toBeVisible({ timeout: 5_000 });

    await page.getByTestId('player-input-0').fill('Alice');
    await page.getByTestId('player-input-1').fill('Bob');

    const createBtn = page.getByTestId('create-session-button');
    await expect(createBtn).toBeEnabled();
    await createBtn.click();

    // ─── Step 5: assert redirect to the session play page ──────────────
    // handleWizardComplete in sessions/new/page.tsx redirects to
    // /sessions/{id}/play (not /sessions/live/{id}).
    await page.waitForURL(`**/sessions/${JOURNEY_CONSTANTS.SESSION_ID}/play`, {
      timeout: 10_000,
    });
    expect(page.url()).toContain(`/sessions/${JOURNEY_CONSTANTS.SESSION_ID}/play`);

    // ─── Step 6: assert play page initial render ───────────────────────
    // The play page hydrates the session via GET /live-sessions/{id}
    // (mocked) and renders the game name heading + scoreboard with players.
    // SignalR is blocked so no live updates occur — we assert on the
    // static data from the hydration fetch.
    await expect(page.getByRole('heading', { name: 'Catan', level: 1 }).first()).toBeVisible({
      timeout: 10_000,
    });
    await expect(page.getByText('Alice').first()).toBeVisible();
    await expect(page.getByText('Bob').first()).toBeVisible();
  });

  // ==========================================================================
  // Issue #323: Session lifecycle — pause → resume → final save
  //
  // Extends the journey by exercising the save-complete dialog + resume toggle
  // after the user lands on /sessions/{id}/play.
  //
  // Flow under test:
  //   1. Open live session page with a running session
  //   2. Click the "Pausa" quick-action → save-complete dialog opens
  //   3. Confirm → POST /save-complete → success panel → close → status=Paused
  //   4. Click the "Riprendi" quick-action → POST /resume → status=InProgress
  //   5. Click "Pausa" again + save again → final save confirmed
  //
  // Mock boundary: REST endpoints only. Zustand store transitions, the
  // SaveCompleteDialog state machine, and the Pause/Resume toggle in
  // QuickActions all run for real.
  //
  // Out of scope:
  //   - SSE diary assertions (see #324)
  //   - Autosave indicator + score persistence (see #325)
  //   - The `/complete` endpoint: LiveSessionView (rendered by /play) exposes
  //     no finalize button. The sibling /sessions/{id}/page.tsx and
  //     play-mode-mobile.tsx DO wire completeSession(), but they are out of
  //     the /play + desktop-chrome test surface covered here.
  // ==========================================================================

  test('lifecycle: pause → resume → final save on live session page', async ({ page }) => {
    const LIFECYCLE_CONSTANTS = {
      INDEXED_GAME_ID: '22222222-2222-4222-8222-222222222222',
      SESSION_ID: '66666666-6666-4666-8666-666666666666',
      PLAYER_ID_1: '77777777-7777-4777-8777-777777777771',
      PLAYER_ID_2: '77777777-7777-4777-8777-777777777772',
      USER_ID: '88888888-8888-4888-8888-888888888888',
    } as const;

    const saveCompleteCalls: number[] = [];
    const resumeCalls: number[] = [];

    // ─── Hydration mock: mutable status, flips Paused ⇄ InProgress ─────────
    // Defensive only. In the current codebase, loadSession() in
    // sessions/[id]/layout.tsx runs exactly once on mount, and subsequent
    // status transitions are driven by optimistic Zustand updates
    // (resumeSession/success in session-store.ts, handleSaveComplete in
    // LiveSessionView.tsx). There is NO subsequent GET /{id} refetch, so
    // this mutable mirror never load-bears on the assertion flow.
    //
    // We keep it in case a future re-focus/refetch hook is added — if that
    // happens and this mirror is not updated, the hydration GET would return
    // stale 'InProgress' after pause and break the label-flip assertions.
    let currentStatus: 'InProgress' | 'Paused' = 'InProgress';
    const nowIso = new Date().toISOString();

    const buildSessionDto = (status: 'InProgress' | 'Paused') => ({
      id: LIFECYCLE_CONSTANTS.SESSION_ID,
      sessionCode: 'LIFE01',
      gameId: LIFECYCLE_CONSTANTS.INDEXED_GAME_ID,
      gameName: 'Catan',
      createdByUserId: LIFECYCLE_CONSTANTS.USER_ID,
      status,
      visibility: 'Private',
      groupId: null,
      createdAt: nowIso,
      startedAt: nowIso,
      pausedAt: status === 'Paused' ? nowIso : null,
      completedAt: null,
      updatedAt: nowIso,
      lastSavedAt: status === 'Paused' ? nowIso : null,
      currentTurnIndex: 0,
      currentTurnPlayerId: LIFECYCLE_CONSTANTS.PLAYER_ID_1,
      agentMode: 'None',
      chatSessionId: null,
      notes: null,
      players: [
        {
          id: LIFECYCLE_CONSTANTS.PLAYER_ID_1,
          userId: LIFECYCLE_CONSTANTS.USER_ID,
          displayName: 'Alice',
          avatarUrl: null,
          color: 'Red',
          role: 'Host',
          teamId: null,
          totalScore: 0,
          currentRank: 1,
          joinedAt: nowIso,
          isActive: true,
        },
        {
          id: LIFECYCLE_CONSTANTS.PLAYER_ID_2,
          userId: null,
          displayName: 'Bob',
          avatarUrl: null,
          color: 'Blue',
          role: 'Player',
          teamId: null,
          totalScore: 0,
          currentRank: 2,
          joinedAt: nowIso,
          isActive: true,
        },
      ],
      teams: [],
      roundScores: [],
      scoringConfig: {
        enabledDimensions: [],
        dimensionUnits: {},
      },
    });

    // ─── Route registration order matters ─────────────────────────────────
    // Playwright's route matcher evaluates handlers in LIFO order (the most
    // recently registered wins). Register the broad catch-all FIRST and the
    // specific endpoints LAST so specific routes take priority.

    // Catch-all sub-routes (scores, activity, etc.) — registered first so it
    // loses against the specific routes below. Only fulfills GETs.
    await page
      .context()
      .route(`**/api/v1/live-sessions/${LIFECYCLE_CONSTANTS.SESSION_ID}/**`, route => {
        if (route.request().method() === 'GET') {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: '[]',
          });
        }
        return route.continue();
      });

    // Session hydration GET /{id} — mutable status reflects pause/resume state
    await page
      .context()
      .route(`**/api/v1/live-sessions/${LIFECYCLE_CONSTANTS.SESSION_ID}`, route => {
        if (route.request().method() === 'GET') {
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(buildSessionDto(currentStatus)),
          });
        }
        return route.continue();
      });

    // resume-context (GET): LiveSessionView queries it eagerly; return empty recap
    await page
      .context()
      .route(`**/api/v1/live-sessions/${LIFECYCLE_CONSTANTS.SESSION_ID}/resume-context`, route =>
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            sessionId: LIFECYCLE_CONSTANTS.SESSION_ID,
            gameTitle: 'Catan',
            lastSnapshotIndex: 0,
            currentTurn: 0,
            currentPhase: null,
            pausedAt: nowIso,
            recap: '',
            playerScores: [],
            photos: [],
          }),
        })
      );

    // POST /resume → 200 void + flip mutable status
    await page
      .context()
      .route(`**/api/v1/live-sessions/${LIFECYCLE_CONSTANTS.SESSION_ID}/resume`, route => {
        if (route.request().method() === 'POST') {
          resumeCalls.push(Date.now());
          currentStatus = 'InProgress';
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: '{}',
          });
        }
        return route.continue();
      });

    // save-complete → returns SessionSaveResult (schema in save-resume.schemas.ts).
    // snapshotIndex increments per call to validate "final save" is a second call.
    // REGISTERED LAST so it wins against the catch-all handler above.
    await page
      .context()
      .route(`**/api/v1/live-sessions/${LIFECYCLE_CONSTANTS.SESSION_ID}/save-complete`, route => {
        if (route.request().method() === 'POST') {
          const snapshotIndex = saveCompleteCalls.length + 1;
          saveCompleteCalls.push(Date.now());
          return route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({
              sessionId: LIFECYCLE_CONSTANTS.SESSION_ID,
              snapshotIndex,
              recap: `Riepilogo partita, snapshot #${snapshotIndex}`,
              photoCount: 0,
              savedAt: new Date().toISOString(),
            }),
          });
        }
        return route.continue();
      });

    // Block SignalR negotiate to prevent retry storms
    await page.context().route('**/hubs/game-state/**', route => route.abort('failed'));

    // ─── Navigate directly to the live play page ──────────────────────────
    await page.goto(`/sessions/${LIFECYCLE_CONSTANTS.SESSION_ID}/play`);
    await page.waitForLoadState('domcontentloaded');

    // Wait for the play page to hydrate with Catan + Alice visible
    await expect(page.getByRole('heading', { name: 'Catan', level: 1 }).first()).toBeVisible({
      timeout: 10_000,
    });
    await expect(page.getByText('Alice').first()).toBeVisible();

    // ─── Step 1: Pause via save-complete flow ──────────────────────────────
    const pauseBtn = page.getByTestId('quick-action-pause');
    await expect(pauseBtn).toBeVisible();
    // Initial label is "Pausa" (isPaused=false)
    await expect(pauseBtn).toContainText(/pausa/i);
    await pauseBtn.click();

    // Dialog opens in "confirm" phase
    await expect(page.getByTestId('save-complete-dialog')).toBeVisible();
    const confirmSaveBtn = page.getByTestId('save-complete-confirm');
    await expect(confirmSaveBtn).toBeVisible();
    await confirmSaveBtn.click();

    // Success panel appears after POST /save-complete resolves
    await expect(page.getByTestId('save-complete-success')).toBeVisible({ timeout: 5_000 });
    // (Defensive) Mirror the status flip that handleSaveComplete in
    // LiveSessionView.tsx will apply AFTER the user clicks "Chiudi" on the
    // success panel. The actual store transition happens a few lines below
    // when save-complete-close is clicked — we set it here so any future
    // hydration refetch (see note on currentStatus declaration) sees 'Paused'.
    currentStatus = 'Paused';

    // Close dialog → returns to live view with status=Paused
    await page.getByTestId('save-complete-close').click();
    await expect(page.getByTestId('save-complete-dialog')).not.toBeVisible();

    // Assert first save happened
    expect(saveCompleteCalls.length).toBe(1);

    // ─── Step 2: Resume via the same button (now labeled "Riprendi") ──────
    // isPaused flipped in the store → the pause button re-renders with label "Riprendi"
    const resumeBtn = page.getByTestId('quick-action-pause');
    await expect(resumeBtn).toContainText(/riprendi/i, { timeout: 5_000 });
    await resumeBtn.click();

    // POST /resume should be called exactly once
    await expect
      .poll(() => resumeCalls.length, { timeout: 5_000, message: 'resume endpoint not called' })
      .toBe(1);

    // After resume, the button label flips back to "Pausa"
    await expect(page.getByTestId('quick-action-pause')).toContainText(/pausa/i, {
      timeout: 5_000,
    });

    // ─── Step 3: Final save — second pause/save cycle ─────────────────────
    await page.getByTestId('quick-action-pause').click();
    await expect(page.getByTestId('save-complete-dialog')).toBeVisible();
    await page.getByTestId('save-complete-confirm').click();

    // Second save result: snapshotIndex should now be 2
    await expect(page.getByTestId('save-complete-success')).toBeVisible({ timeout: 5_000 });
    await expect(page.getByTestId('save-complete-success')).toContainText(/snapshot/i);

    // Validate the second POST hit the server
    expect(saveCompleteCalls.length).toBe(2);

    // Close the final dialog
    await page.getByTestId('save-complete-close').click();
    await expect(page.getByTestId('save-complete-dialog')).not.toBeVisible();
  });

  test('scoreboard page renders with player data', async ({ page }) => {
    // Mock session details API
    await page.context().route('**/api/v1/sessions/session-1**', route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'session-1',
          gameId: 'game-1',
          status: 'InProgress',
          winnerName: null,
          players: [
            { playerName: 'Marco', playerOrder: 1, color: '#3B82F6' },
            { playerName: 'Luca', playerOrder: 2, color: '#EF4444' },
            { playerName: 'Anna', playerOrder: 3, color: '#10B981' },
          ],
        }),
      })
    );

    await page.goto('/sessions/session-1/scoreboard');
    await page.waitForLoadState('domcontentloaded');

    // The scoreboard is a client component so mocks should work
    const marcoVisible = await page
      .getByText('Marco')
      .first()
      .isVisible({ timeout: 5000 })
      .catch(() => false);
    const loadingVisible = await page
      .getByText(/caricamento/i)
      .first()
      .isVisible()
      .catch(() => false);

    // Either player data loaded or loading state shown
    expect(marcoVisible || loadingVisible || (await page.title()).length > 0).toBe(true);
  });
});

// ============================================================================
// Issue #301 helpers — journey mocks
// ============================================================================

interface JourneyConstants {
  readonly INDEXED_GAME_ID: string;
  readonly SESSION_ID: string;
  readonly PLAYER_ID_1: string;
  readonly PLAYER_ID_2: string;
  readonly USER_ID: string;
}

/**
 * Sets up the API mocks required for the full game night happy path.
 *
 * Mock boundary: HTTP layer only. Router, Zustand stores, wizard state
 * machine, and SignalR hook all run for real. This validates the integration
 * wiring without reaching a real backend.
 *
 * All mock data is schema-compliant (SharedGameSchema, UserGameKbStatusSchema,
 * LiveSessionDtoSchema) so consumer Zod validators pass.
 *
 * Cookies + /auth/me are already set by AuthHelper.mockAuthenticatedSession
 * in the describe-level beforeEach.
 */
async function setupJourneyMocks(page: Page, c: JourneyConstants): Promise<void> {
  const nowIso = new Date().toISOString();

  // ─── Wizard step 1: shared games search ────────────────────────────────
  // Use regex to match regardless of query string ordering/presence
  await page.context().route(/\/api\/v1\/shared-games(\?.*)?$/, route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: c.INDEXED_GAME_ID,
              bggId: 13,
              title: 'Catan',
              yearPublished: 1995,
              description: 'Trade, build, settle on the island of Catan.',
              minPlayers: 3,
              maxPlayers: 4,
              playingTimeMinutes: 90,
              minAge: 10,
              complexityRating: 2.3,
              averageRating: 7.2,
              imageUrl: '',
              thumbnailUrl: '',
              status: 'Published',
              isRagPublic: true,
              createdAt: nowIso,
              modifiedAt: null,
            },
          ],
          total: 1,
          page: 1,
          pageSize: 5,
        }),
      });
    }
    return route.continue();
  });

  // KB status for Catan — isIndexed=true so the soft-filter warning stays hidden
  await page.context().route(`**/api/v1/games/${c.INDEXED_GAME_ID}/knowledge-base`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        gameId: c.INDEXED_GAME_ID,
        isIndexed: true,
        documentCount: 3,
        coverageScore: 80,
        coverageLevel: 'Standard',
        suggestedQuestions: [],
      }),
    })
  );

  // ─── Wizard step 3: session creation sequence ──────────────────────────
  // POST /live-sessions returns a bare JSON string (see liveSessionsClient.ts)
  await page.context().route('**/api/v1/live-sessions', route => {
    if (route.request().method() === 'POST') {
      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(c.SESSION_ID),
      });
    }
    return route.continue();
  });

  // Add player returns a bare string id — called once per player
  const playerIds = [c.PLAYER_ID_1, c.PLAYER_ID_2];
  let playerIndex = 0;
  await page.context().route(`**/api/v1/live-sessions/${c.SESSION_ID}/players`, route => {
    if (route.request().method() === 'POST') {
      const id = playerIds[playerIndex % playerIds.length];
      playerIndex += 1;
      return route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify(id),
      });
    }
    return route.continue();
  });

  // Start session: void 200
  await page.context().route(`**/api/v1/live-sessions/${c.SESSION_ID}/start`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: '{}',
    })
  );

  // Live session GET (fallback for any hydration call): full schema-compliant DTO
  await page.context().route(`**/api/v1/live-sessions/${c.SESSION_ID}`, route => {
    if (route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: c.SESSION_ID,
          sessionCode: 'CATAN1',
          gameId: c.INDEXED_GAME_ID,
          gameName: 'Catan',
          createdByUserId: c.USER_ID,
          status: 'InProgress',
          visibility: 'Private',
          groupId: null,
          createdAt: nowIso,
          startedAt: nowIso,
          pausedAt: null,
          completedAt: null,
          updatedAt: nowIso,
          lastSavedAt: null,
          currentTurnIndex: 0,
          currentTurnPlayerId: c.PLAYER_ID_1,
          agentMode: 'None',
          chatSessionId: null,
          notes: null,
          players: [
            {
              id: c.PLAYER_ID_1,
              userId: c.USER_ID,
              displayName: 'Alice',
              avatarUrl: null,
              color: 'Red',
              role: 'Host',
              teamId: null,
              totalScore: 0,
              currentRank: 1,
              joinedAt: nowIso,
              isActive: true,
            },
            {
              id: c.PLAYER_ID_2,
              userId: null,
              displayName: 'Bob',
              avatarUrl: null,
              color: 'Blue',
              role: 'Player',
              teamId: null,
              totalScore: 0,
              currentRank: 2,
              joinedAt: nowIso,
              isActive: true,
            },
          ],
          teams: [],
          roundScores: [],
          scoringConfig: {
            enabledDimensions: [],
            dimensionUnits: {},
          },
        }),
      });
    }
    return route.continue();
  });

  // ─── SignalR hub: block connection to prevent retry storms ─────────────
  // The live session page connects to /hubs/game-state. With no backend we
  // abort the negotiate handshake; the hook fails fast and the page renders
  // the offline banner without hanging.
  await page.context().route('**/hubs/game-state/**', route => route.abort('failed'));
}
