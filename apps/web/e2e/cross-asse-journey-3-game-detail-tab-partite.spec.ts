/**
 * Cross-Asse Journey #3 — Game Detail "Storico partite" rail (sessions tab).
 *
 * Anna (user) navigates to `/games/${gameId}` → clicks the **Sessioni** tab →
 * verifies the `GameDetailSessionsRail` renders correctly with:
 *   - T4.1: happy path 15-seed → 5-cap (BE Take(5)) — rail shows 5 cards + viewAll visible
 *   - T4.2: boundary 0 sessions — rail empty-state (`data-empty=true`), no viewAll
 *   - T4.3: boundary 3 sessions — 3 cards + viewAll IS visible (no threshold guard)
 *   - T4.4: viewAll navigation — URL changes to `/games/${id}/sessions`
 *
 * **Initial state** (DEC-C-1 journey3):
 *   - 0 GameNight
 *   - 1 library game (seeded via Real BE `seedLibraryGame` factory, DEC-C-8)
 *   - N UserGameSession rows seeded per-test via `seedUserGameSession` factory (DEC-C-10 REVISION)
 *
 * **DEC-C-10 REVISION** (user-locked sessione 43): supersedes PIVOT-3 (mock endpoint) and
 * PIVOT-4 (userId mismatch). Real BE wire via:
 *   - `seedLibraryGame` for the library entry (DEC-C-8, Macro 3a)
 *   - `seedUserGameSession` for per-test session counts (Macro 4 Phase B-E)
 *   - PIVOT-4 fix: `mockAuthEndpoints({ userId: libGame.ownerId })` → matches seeded user
 *
 * **PIVOT-1** (preserved): `GameDetailSessionsRail` shows the viewAll link whenever
 *   `sessions.length > 0` — there is no configurable threshold.
 *   T4.3 asserts viewAll IS visible (not absent) when N=3.
 *
 * **PIVOT-2** (preserved): BE `GetGameDetailQueryHandler.cs:81` uses `Take(5)`.
 *   T4.1 seeds 15 sessions but asserts exactly 5 cards (capped by BE).
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
 *               + DEC-C-10 REVISION Real BE seedUserGameSession (Macro 4 Phase B-E)
 *
 * **CI gate**: non-blocking on main-dev (DEC-C-4). Blocking on main-staging.
 *
 * Issue #1929 Task C Macro 4 — Asse D P4 follow-up.
 */
import { test, expect } from '@playwright/test';

import { ANNA_PERSONA, buildAnnaInitialState } from './_helpers/annaPersona';
import { assertExactUrl } from './_helpers/dataAssertionUtils';
import { withRetry } from './_helpers/resilienceWrappers';
import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';
import {
  cleanupTestEntities,
  newTestRunId,
  seedLibraryGame,
  seedUserGameSession,
} from './_helpers/seedEntities';

// ─── Suite ────────────────────────────────────────────────────────────────────

test.describe('Cross-Asse Journey #3 — Game Detail "Storico partite" tab', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for speed');

  let testRunId: string;
  let libraryGameId: string;
  let libraryEntryId: string;
  let ownerUserId: string;

  test.beforeEach(async ({ page }, testInfo) => {
    testRunId = newTestRunId(testInfo.testId);

    await seedCookieConsent(page);

    // ① BE entity seeding — journey3 initial state: 1 library game (Real BE DEC-C-8)
    const initial = buildAnnaInitialState('journey3');
    expect(initial.libraryGameCount).toBe(1);

    const libGame = await withRetry(
      () =>
        seedLibraryGame(page, {
          testRunId,
          ownerEmail: ANNA_PERSONA.email,
          title: 'Catan E2E Journey3',
          publisher: 'KOSMOS E2E',
          minPlayers: 3,
          maxPlayers: 4,
        }),
      { reason: 'seedLibraryGame journey3 beforeEach' }
    );
    libraryGameId = libGame.gameId;
    libraryEntryId = libGame.libraryEntryId;
    ownerUserId = libGame.ownerId;

    // ② FE auth seeding — DEC-C-10 PIVOT-4 fix: use libGame.ownerId (matches seeded user)
    await seedAuthSession(page, { role: ANNA_PERSONA.role });
    await mockAuthEndpoints(page, {
      role: ANNA_PERSONA.role,
      userId: ownerUserId, // ← PIVOT-4 fix: seeded user, not static ANNA_PERSONA.userId
      email: ANNA_PERSONA.email,
      onboardingCompleted: ANNA_PERSONA.onboardingCompleted,
    });
  });

  test.afterEach(async ({ page }) => {
    if (testRunId) {
      await cleanupTestEntities(page, { testRunId });
    }
  });

  // ── T4.1: happy path — 15 seeded → 5 BE-capped → rail cards + viewAll ─────

  /**
   * T4.1 — Anna navigates to game detail; 15 UserGameSessions seeded Real BE.
   * BE GetGameDetailQueryHandler.cs:81 caps at Take(5).
   * Asserts: rail renders exactly 5 cards (cap), viewAll link visible, empty-state absent.
   *
   * PIVOT-2 preserved: BE Take(5) cap; seed N=cap+1=6 → 5 visible (Adzic naming:
   * the invariant under test is the CAP, not the seed count — N>cap is what matters).
   * DEC-C-10 REVISION: Real BE seedUserGameSession (no mock routes).
   *
   * Follow-up #1955: T4.1 seed count reduced 15→6 (proves cap with minimal HTTP
   * round-trips for ~1.5-2.7s CI savings; previous N=15 was speculative defense
   * against unexpressed BE pagination defects, ruled out by Newman's review).
   */
  test('sessions tab caps at 5 cards even when BE has N=6 seeded (Take(5) invariant)', async ({
    page,
  }) => {
    // Arrange: seed N=cap+1=6 sessions (proves BE Take(5) cap with minimal seed overhead)
    for (let i = 0; i < 6; i++) {
      await withRetry(
        () =>
          seedUserGameSession(page, {
            testRunId,
            userLibraryEntryId: libraryEntryId,
            playedAt: new Date(2026, 4, i + 1).toISOString(),
            durationMinutes: 60 + i,
            didWin: i % 2 === 0,
            players: 'Anna, Marco',
          }),
        { reason: `seedUserGameSession journey3 T4.1 #${i + 1}/6` }
      );
    }

    // Act: navigate to game detail
    await page.goto(`/games/${libraryGameId}`);
    await expect(page.locator('[data-slot="game-detail-view"]')).toBeVisible({ timeout: 15_000 });

    // Click Sessioni tab
    const sessionsTab = page.locator('#game-detail-tab-sessions');
    await expect(sessionsTab).toBeVisible({ timeout: 5_000 });
    await sessionsTab.click();

    // Wait for panel to become visible
    const sessionsPanel = page.locator('[data-slot="game-detail-panel-sessions"]');
    await expect(sessionsPanel).not.toHaveAttribute('hidden', { timeout: 5_000 });

    // Rail mounts with data-empty=false
    const rail = sessionsPanel.locator('[data-slot="game-detail-sessions-rail"]');
    await expect(rail).toBeVisible({ timeout: 5_000 });
    await expect(rail).toHaveAttribute('data-empty', 'false');

    // Exactly 5 cards (BE Take(5) cap)
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
   * T4.2 — No UserGameSessions seeded (0). Library game exists but no sessions.
   * Asserts: rail `data-empty=true`, empty-state visible, viewAll absent.
   *
   * DEC-C-10 REVISION: No seed calls needed — beforeEach seeds only the library entry.
   */
  test('sessions tab empty-state when N=0 (no viewAll link)', async ({ page }) => {
    // Arrange: no seedUserGameSession calls — 0 sessions

    // Act: navigate to game detail
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
   * T4.3 — 3 UserGameSessions seeded Real BE.
   * Asserts: 3 cards rendered, viewAll IS visible (no threshold — any N>0 shows it).
   *
   * PIVOT-1 preserved: GameDetailSessionsRail shows viewAll whenever sessions.length > 0.
   * DEC-C-10 REVISION: Real BE seedUserGameSession.
   */
  test('sessions tab renders N=3 cards + viewAll visible (no threshold guard)', async ({
    page,
  }) => {
    // Arrange: seed exactly 3 sessions
    for (let i = 0; i < 3; i++) {
      await withRetry(
        () =>
          seedUserGameSession(page, {
            testRunId,
            userLibraryEntryId: libraryEntryId,
            playedAt: new Date(2026, 4, i + 1).toISOString(),
            durationMinutes: 90,
            didWin: i % 2 === 0,
          }),
        { reason: `seedUserGameSession journey3 T4.3 #${i + 1}/3` }
      );
    }

    // Act
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
   * DEC-C-10 REVISION: 2 sessions seeded Real BE so viewAll renders.
   */
  test('viewAll link navigates to /games/{gameId}/sessions subroute', async ({ page }) => {
    // Arrange: seed 2 sessions so viewAll renders
    for (let i = 0; i < 2; i++) {
      await withRetry(
        () =>
          seedUserGameSession(page, {
            testRunId,
            userLibraryEntryId: libraryEntryId,
            playedAt: new Date(2026, 4, i + 1).toISOString(),
            durationMinutes: 60,
          }),
        { reason: `seedUserGameSession journey3 T4.4 #${i + 1}/2` }
      );
    }

    // Act
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
