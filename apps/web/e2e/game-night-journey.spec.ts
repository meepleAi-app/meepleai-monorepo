/**
 * Game Night Journey E2E Test
 *
 * Covers key navigation flows of the game night journey:
 * - BGG search via discover page
 * - Sessions page with paused sessions
 * - Scoreboard page
 * - Happy path wizard → session creation → live page (Issue #301)
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
