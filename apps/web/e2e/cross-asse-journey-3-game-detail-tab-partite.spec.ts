/**
 * Cross-Asse Journey #3 — Game Detail "Storico partite" rail (sessions tab).
 *
 * Anna (user) navigates to `/games/${gameId}` → clicks the **Sessioni** tab →
 * verifies the `GameDetailSessionsRail` renders correctly with:
 *   - T4.1: happy path 5-cap (BE Take(5)) — rail shows up to 5 cards + viewAll visible
 *   - T4.2: boundary 0 sessions — rail empty-state (`data-empty=true`), no viewAll
 *   - T4.3: boundary 3 sessions — 3 cards + viewAll IS visible (no threshold guard)
 *   - T4.4: filter persistence — URL query params preserved after viewAll navigation
 *
 * **Initial state** (DEC-C-1 journey3):
 *   - 0 GameNight
 *   - 1 library game (seeded via Real BE `seedLibraryGame` factory, DEC-C-8)
 *   - 15 sessionCount (informational only — NOT seeded as UserGameSessionEntity)
 *
 * **PIVOT-3**: Mission brief assumed `seedSession` → `UserGameSessionEntity`.
 *   Reality: `seedSession` creates `GameNightSessionEntity` (different table/entity).
 *   `UserGameSessionEntity` has no TestRunId column and no seeding factory.
 *   Fix: mock `GET /api/v1/library/games/${gameId}` to return synthetic
 *   `recentSessions[]` payload. `seedLibraryGame` provides the valid `gameId`
 *   consumed by the mock URL pattern. (P193 architectural-pivot-documented-inline)
 *
 * **PIVOT-4**: `GET /api/v1/library/games/${gameId}` endpoint extracts userId from
 *   the real JWT session. The `mockAuthEndpoints` helper returns the static
 *   `ANNA_PERSONA.userId` while `seedLibraryGame` creates a different seeded user.
 *   Fix: mock the library detail endpoint entirely; no real BE call needed.
 *
 * **PIVOT-1** (mission brief correction): `GameDetailSessionsRail` shows the viewAll
 *   link whenever `sessions.length > 0` — there is no configurable threshold.
 *   T4.3 must assert viewAll IS visible (not absent) when N=3.
 *
 * **PIVOT-2** (mission brief correction): BE `GetGameDetailQueryHandler.cs:81` uses
 *   `Take(5)`. T4.1 assertions use `≤5` (capped) not `15`.
 *
 * **testid contract**:
 *   - Sessions tab button: `id="game-detail-tab-sessions"` (tabIdFor('sessions'))
 *   - Sessions panel: `data-slot="game-detail-panel-sessions"`
 *   - Sessions rail: `data-slot="game-detail-sessions-rail"` + `data-empty={isEmpty}`
 *   - Session cards: `data-slot="game-detail-session-card"`
 *   - View-all link: `data-slot="game-detail-sessions-view-all"` (href=`/games/${id}/sessions`)
 *   - Empty state: `data-slot="game-detail-sessions-empty"`
 *
 * **Spec ref**: DEC-C-1 + DEC-C-3 (verify-only, no rail refactor) + DEC-C-4 (non-blocking CI)
 *               + DEC-C-8 Real BE seedLibraryGame (Macro 3a `789b3a301`)
 *               + DEC-C-10 PIVOT seedLibraryGame as game-id anchor
 *
 * **CI gate**: non-blocking on main-dev (DEC-C-4). Blocking on main-staging.
 *
 * Issue #1929 Task C Macro 4 — Asse D P4 follow-up.
 */
import { test, expect, type Page, type Route } from '@playwright/test';

import { ANNA_PERSONA, buildAnnaInitialState } from './_helpers/annaPersona';
import { assertExactUrl } from './_helpers/dataAssertionUtils';
import { withRetry } from './_helpers/resilienceWrappers';
import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';
import { cleanupTestEntities, newTestRunId, seedLibraryGame } from './_helpers/seedEntities';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

/** Minimal valid GameDetailDto for the mock — only required fields + recentSessions. */
function makeGameDetailDto(gameId: string, userId: string, sessions: unknown[]) {
  return {
    id: '00000000-0000-4000-8000-000000000010',
    userId,
    gameId,
    gameTitle: 'Catan E2E Test',
    gamePublisher: 'KOSMOS E2E',
    gameYearPublished: 1995,
    gameDescription: 'Catan seeded for Journey #3 test',
    gameIconUrl: null,
    gameImageUrl: null,
    minPlayers: 3,
    maxPlayers: 4,
    playTimeMinutes: 90,
    complexityRating: null,
    averageRating: null,
    addedAt: '2026-01-01T00:00:00+00:00',
    notes: null,
    isFavorite: false,
    currentState: 'Nuovo',
    stateChangedAt: null,
    stateNotes: null,
    isAvailableForPlay: true,
    timesPlayed: sessions.length,
    lastPlayed: sessions.length > 0 ? '2026-06-01T10:00:00+00:00' : null,
    winRate: null,
    avgDuration: null,
    hasRagAccess: false,
    recentSessions: sessions,
    checklist: null,
    customAgentConfig: null,
    customPdf: null,
    customCoverR2Key: null,
  };
}

/** Build N synthetic LibraryGameSession DTOs. */
function makeSessionDtos(count: number) {
  return Array.from({ length: count }, (_, i) => ({
    id: `00000000-0000-4000-8000-${String(i + 1).padStart(12, '0')}`,
    playedAt: `2026-05-${String(i + 1).padStart(2, '0')}T18:00:00+00:00`,
    durationMinutes: 60 + i * 5,
    durationFormatted: `${60 + i * 5} min`,
    didWin: i % 2 === 0,
    players: 'Anna, Marco',
    notes: null,
  }));
}

/**
 * Mocks the library game detail endpoint with synthetic `recentSessions`.
 * Called once per test with the desired session count.
 */
async function mockLibraryGameDetail(page: Page, gameId: string, sessionCount: number) {
  const dto = makeGameDetailDto(gameId, ANNA_PERSONA.userId, makeSessionDtos(sessionCount));

  // Primary endpoint: GET /api/v1/library/games/{gameId}
  await page.route(
    new RegExp(`/api/v1/library/games/${gameId}(?:\\?.*)?$`),
    async (route: Route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(dto),
        });
        return;
      }
      await route.continue();
    }
  );

  // Prevent sharedGames fallback from overriding: return 404 so useLibraryGameDetail
  // falls through to the mocked gameDetail (which wins since it's non-null).
  await page.route(
    new RegExp(`/api/v1/shared-games/${gameId}(?:\\?.*)?$`),
    async (route: Route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({ status: 404, body: '' });
        return;
      }
      await route.continue();
    }
  );

  // Also mock library/games/{gameId}/status so hero variant stays 'own'.
  await page.route(
    new RegExp(`/api/v1/library/games/${gameId}/status(?:\\?.*)?$`),
    async (route: Route) => {
      if (route.request().method() === 'GET') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ inLibrary: true, isFavorite: false }),
        });
        return;
      }
      await route.continue();
    }
  );
}

// ─── Suite ────────────────────────────────────────────────────────────────────

test.describe('Cross-Asse Journey #3 — Game Detail "Storico partite" tab', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  let testRunId: string;
  let libraryGameId: string;

  test.beforeEach(async ({ page }, testInfo) => {
    testRunId = newTestRunId(testInfo.testId);

    // ① FE auth seeding — Anna as authenticated user
    await seedCookieConsent(page);
    await seedAuthSession(page, { role: ANNA_PERSONA.role });
    await mockAuthEndpoints(page, {
      role: ANNA_PERSONA.role,
      userId: ANNA_PERSONA.userId,
      email: ANNA_PERSONA.email,
      onboardingCompleted: ANNA_PERSONA.onboardingCompleted,
    });

    // ② BE entity seeding — journey3 initial state: 1 library game as gameId anchor
    const initial = buildAnnaInitialState('journey3');
    expect(initial.libraryGameCount).toBe(1);

    const libGame = await withRetry(
      () =>
        seedLibraryGame(page, {
          testRunId,
          ownerEmail: ANNA_PERSONA.email,
          title: 'Catan E2E Test',
          publisher: 'KOSMOS E2E',
          minPlayers: 3,
          maxPlayers: 4,
        }),
      { reason: 'seedLibraryGame journey3 beforeEach' }
    );
    libraryGameId = libGame.gameId;
  });

  test.afterEach(async ({ page }) => {
    if (testRunId) {
      await cleanupTestEntities(page, { testRunId });
    }
  });

  // ── T4.1: happy path — 5 sessions (BE Take(5) cap) → rail cards + viewAll ─

  /**
   * T4.1 — Anna navigates to game detail, clicks Sessioni tab.
   * Mock returns 5 sessions (BE slice cap).
   * Asserts: rail renders 5 cards, viewAll link visible, empty-state absent.
   *
   * PIVOT-2: BE Take(5) cap; original mission expected 15 visible — corrected.
   */
  test('sessions tab renders N=5 cards + viewAll visible', async ({ page }) => {
    // Arrange: mock library detail with 5 sessions (= BE slice cap)
    await mockLibraryGameDetail(page, libraryGameId, 5);

    // Act: navigate to game detail
    await page.goto(`/games/${libraryGameId}`);
    await expect(page.locator('[data-slot="game-detail-view"]')).toBeVisible({ timeout: 15_000 });

    // Click Sessioni tab
    const sessionsTab = page.locator('#game-detail-tab-sessions');
    await expect(sessionsTab).toBeVisible({ timeout: 5_000 });
    await sessionsTab.click();

    // Wait for panel to become visible (tab switch is synchronous but ARIA hidden toggle)
    const sessionsPanel = page.locator('[data-slot="game-detail-panel-sessions"]');
    await expect(sessionsPanel).not.toHaveAttribute('hidden', { timeout: 5_000 });

    // Rail mounts with data-empty=false
    const rail = sessionsPanel.locator('[data-slot="game-detail-sessions-rail"]');
    await expect(rail).toBeVisible({ timeout: 5_000 });
    await expect(rail).toHaveAttribute('data-empty', 'false');

    // 5 session cards
    const cards = rail.locator('[data-slot="game-detail-session-card"]');
    await expect(cards).toHaveCount(5, { timeout: 5_000 });

    // View-all link visible (href includes /sessions subroute)
    const viewAll = rail.locator('[data-slot="game-detail-sessions-view-all"]');
    await expect(viewAll).toBeVisible({ timeout: 3_000 });
    await expect(viewAll).toHaveAttribute('href', new RegExp(`/games/${libraryGameId}/sessions`));

    // Empty state MUST be absent
    await expect(rail.locator('[data-slot="game-detail-sessions-empty"]')).toBeHidden();
  });

  // ── T4.2: boundary 0 sessions — empty-state, no viewAll ──────────────────

  /**
   * T4.2 — Mock returns 0 sessions.
   * Asserts: rail `data-empty=true`, empty-state visible, viewAll absent.
   */
  test('sessions tab empty-state when N=0 (no viewAll link)', async ({ page }) => {
    // Arrange: mock library detail with 0 sessions
    await mockLibraryGameDetail(page, libraryGameId, 0);

    await page.goto(`/games/${libraryGameId}`);
    await expect(page.locator('[data-slot="game-detail-view"]')).toBeVisible({ timeout: 15_000 });

    const sessionsTab = page.locator('#game-detail-tab-sessions');
    await expect(sessionsTab).toBeVisible({ timeout: 5_000 });
    await sessionsTab.click();

    const sessionsPanel = page.locator('[data-slot="game-detail-panel-sessions"]');
    await expect(sessionsPanel).not.toHaveAttribute('hidden', { timeout: 5_000 });

    const rail = sessionsPanel.locator('[data-slot="game-detail-sessions-rail"]');
    await expect(rail).toBeVisible({ timeout: 5_000 });
    await expect(rail).toHaveAttribute('data-empty', 'true');

    // Empty state component visible
    const emptyState = rail.locator('[data-slot="game-detail-sessions-empty"]');
    await expect(emptyState).toBeVisible({ timeout: 3_000 });

    // No session cards
    const cards = rail.locator('[data-slot="game-detail-session-card"]');
    await expect(cards).toHaveCount(0);

    // View-all link MUST be absent (hidden branch in Rail: renders only when !isEmpty)
    const viewAll = rail.locator('[data-slot="game-detail-sessions-view-all"]');
    await expect(viewAll).toBeHidden();
  });

  // ── T4.3: boundary 3 sessions — 3 cards + viewAll IS visible ──────────────

  /**
   * T4.3 — Mock returns 3 sessions.
   * Asserts: 3 cards rendered, viewAll IS visible (no threshold — any N>0 shows it).
   *
   * PIVOT-1: mission brief incorrectly stated "no viewAll when N=3 (threshold non superato)".
   * Reality: GameDetailSessionsRail shows viewAll whenever sessions.length > 0.
   */
  test('sessions tab renders N=3 cards + viewAll visible (no threshold guard)', async ({
    page,
  }) => {
    // Arrange: mock library detail with 3 sessions
    await mockLibraryGameDetail(page, libraryGameId, 3);

    await page.goto(`/games/${libraryGameId}`);
    await expect(page.locator('[data-slot="game-detail-view"]')).toBeVisible({ timeout: 15_000 });

    const sessionsTab = page.locator('#game-detail-tab-sessions');
    await expect(sessionsTab).toBeVisible({ timeout: 5_000 });
    await sessionsTab.click();

    const sessionsPanel = page.locator('[data-slot="game-detail-panel-sessions"]');
    await expect(sessionsPanel).not.toHaveAttribute('hidden', { timeout: 5_000 });

    const rail = sessionsPanel.locator('[data-slot="game-detail-sessions-rail"]');
    await expect(rail).toBeVisible({ timeout: 5_000 });
    await expect(rail).toHaveAttribute('data-empty', 'false');

    // Exactly 3 cards
    const cards = rail.locator('[data-slot="game-detail-session-card"]');
    await expect(cards).toHaveCount(3, { timeout: 5_000 });

    // View-all IS visible (no threshold on Rail — any sessions > 0)
    const viewAll = rail.locator('[data-slot="game-detail-sessions-view-all"]');
    await expect(viewAll).toBeVisible({ timeout: 3_000 });

    // Empty state absent
    await expect(rail.locator('[data-slot="game-detail-sessions-empty"]')).toBeHidden();
  });

  // ── T4.4: viewAll navigation → /games/{id}/sessions ──────────────────────

  /**
   * T4.4 — Click viewAll from sessions tab.
   * Asserts: browser navigates to `/games/${gameId}/sessions`.
   *
   * Filter-persistence scope note: the viewAll link href is a static
   * `/games/{gameId}/sessions` (no query param forwarding in GameDetailView).
   * This test verifies navigation completes to the correct subroute.
   */
  test('viewAll link navigates to /games/{gameId}/sessions subroute', async ({ page }) => {
    // Arrange: mock library detail with 2 sessions so viewAll renders
    await mockLibraryGameDetail(page, libraryGameId, 2);

    await page.goto(`/games/${libraryGameId}`);
    await expect(page.locator('[data-slot="game-detail-view"]')).toBeVisible({ timeout: 15_000 });

    const sessionsTab = page.locator('#game-detail-tab-sessions');
    await expect(sessionsTab).toBeVisible({ timeout: 5_000 });
    await sessionsTab.click();

    const sessionsPanel = page.locator('[data-slot="game-detail-panel-sessions"]');
    await expect(sessionsPanel).not.toHaveAttribute('hidden', { timeout: 5_000 });

    const viewAll = sessionsPanel.locator('[data-slot="game-detail-sessions-view-all"]');
    await expect(viewAll).toBeVisible({ timeout: 5_000 });

    // Click viewAll → navigate to sessions subroute
    await withRetry(() => viewAll.click(), { reason: 'viewAll click journey3' });

    // Assert navigation to /games/{gameId}/sessions
    await page.waitForURL(new RegExp(`/games/${libraryGameId}/sessions`), { timeout: 10_000 });
    assertExactUrl(page.url(), new RegExp(`/games/${libraryGameId}/sessions(\\?.*)?$`));
  });
});
