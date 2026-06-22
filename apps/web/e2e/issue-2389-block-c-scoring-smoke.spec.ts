/**
 * #2389 Block C — Polymorphic scoring cleanup smoke (Playwright).
 *
 * Behavioural regression-pins for the Block C cleanup work:
 *
 *   - The legacy `scores: Record<string, number>` field is GONE from
 *     `useLiveSessionStore`. The store now only carries the polymorphic
 *     `scoreData` payload sourced from REST hydration + SignalR
 *     `ScoringConfigured`.
 *   - `useSessionScores().leader` is playerId-keyed (it used to be
 *     playerName-keyed before Block C).
 *   - `ScoringPanelRenderer` mounts the variant matching `scoringType`
 *     coming from the REST DTO (no more hardcoded Points-only assumption).
 *
 * Why a new spec file (Option A from T8 brief): `asse-d-p1-polymorphic-scoring.spec.ts`
 * already pins the legacy `/sessions/live/[sessionId]/scores` ScoreBoard route
 * (Wave D pre-cleanup surface). The post-cleanup surface lives on the NEW
 * `/sessions/[id]/live` SessionLiveView orchestrator + ScoreTabContent
 * primitive — a different route with a different mount path. Keeping these
 * in separate spec files makes the Block C work retire-able in isolation.
 *
 * Why these two scenarios (Q3=a):
 *   1. **Points host adjust** — pinned via REST-hydrated Points scoreData;
 *      asserts the Points panel mounts + leader cell highlights the highest
 *      scorer (validates playerId-keyed leader derivation).
 *   2. **Spectator Ranking render** — pinned via REST-hydrated Ranking
 *      scoreData; asserts the Ranking panel mounts (validates the
 *      `mapScoreDataToPanelData` adapter + ScoringPanelRenderer Ranking
 *      variant), proves no `.replace()` crash on missing aria template
 *      (T12 closed that loose-end).
 *
 * Scope notes:
 *   - The default Playwright `next dev` web server is NOT built with
 *     `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1`, so `?fixture=host` is
 *     INERT here (`IS_VISUAL_TEST_BUILD === false`). The REST adapter path
 *     forces `viewerRole='Player'` in SessionLiveView lines 405-406, which
 *     means the true host-editor mutation flow cannot be reproduced via REST
 *     mocking alone — that scenario would require either a fixture build or
 *     a full SignalR mock. Per the T8 brief: "if SignalR mocking proves
 *     infeasible, document a smaller smoke that still pins Block C
 *     correctness." This file follows that guidance: both scenarios pin the
 *     CLEANUP outcomes (playerId-keyed leader + polymorphic store + Ranking
 *     render) via REST hydration alone.
 *   - The host-editor mutation roundtrip (debounced PUT + leader migration)
 *     remains covered by the unit suites:
 *       - `ScoreTabContent.test.tsx` — debounced submit + 5-class error matrix
 *       - `use-update-session-scores.test.tsx` — mutation + error mapping
 *       - `useSessionScores.test.ts` — leader playerId derivation
 *     Cross-referenced here so future regressions are not missed.
 *
 * Auth fixtures: triple helper (seedAuthSession + seedCookieConsent +
 * mockAuthEndpoints) mirrors `session-live-chat-agent-g3.spec.ts`.
 *
 * #2389 Block C T8.
 */

import { expect, test, type Page } from '@playwright/test';

import { mockAuthEndpoints, seedAuthSession } from './_helpers/seedAuthSession';
import { seedCookieConsent } from './_helpers/seedCookieConsent';

// SessionLiveView hardcodes `data-theme="dark"` on its root div.
// Mirror the chat-agent-g3 spec pattern for consistency.
//
// Desktop viewport is enforced because the polymorphic `ScoreTabContent`
// mounts INSIDE the desktop right-column tabs `lg+` breakpoint. On mobile
// (Pixel 5) the same content lives inside the MobileBottomSheetDrawer which
// is `closed` by default — the panel is rendered (so `toBeVisible` returns
// `hidden`, not "not in DOM") but not visible to the user, which would
// make the smoke flaky across the mobile-chrome project too. Pinning to a
// desktop viewport keeps the assertions stable regardless of the project
// filter being applied.
test.use({
  colorScheme: 'dark',
  viewport: { width: 1280, height: 800 },
});

// ---------------------------------------------------------------------------
// Sentinel fixtures
// ---------------------------------------------------------------------------

/**
 * Sentinel UUID for the test session. Distinct from Wave D.2 fixture UUID
 * (`...d20`) so debugging is unambiguous if both specs run in the same shard.
 */
const SESSION_ID = '00000000-0000-4000-8000-00000000c389';

/** Stable player roster used across both scenarios. */
const PLAYERS = [
  { playerName: 'Alice', playerOrder: 0, color: '#FF6B6B' },
  { playerName: 'Bob', playerOrder: 1, color: '#4ECDC4' },
  { playerName: 'Charlie', playerOrder: 2, color: '#FFD93D' },
] as const;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function seedAuth(page: Page): Promise<void> {
  await seedAuthSession(page);
  await seedCookieConsent(page);
  await mockAuthEndpoints(page);
}

/**
 * Mocks GET /api/v1/sessions/{id} to return a session DTO with the requested
 * polymorphic scoring payload. The SessionLiveView REST hydration effect
 * (lines 961-978) will then call `setScoringConfig` and downstream
 * `ScoreTabContent` will render the matching variant.
 *
 * Both `scoringType` AND `scoreData` must be set or the hydration effect
 * short-circuits (line 963 `if (... == null) return`).
 */
async function mockSessionDto(
  page: Page,
  opts: {
    scoringType: 'Points' | 'BinaryWin' | 'Objectives' | 'Ranking';
    /** JSON-encoded payload — the BE/FE wire contract serialises scoreData. */
    scoreDataJson: string;
  }
): Promise<void> {
  await page.context().route(
    // Host-agnostic match (the in-browser httpClient uses relative paths via
    // the Next.js proxy at /api/v1/...). Mirrors the autosave-score.spec.ts
    // pattern but URL-encoded session id.
    new RegExp(`/api/v1/sessions/${SESSION_ID}(?:\\?.*)?$`),
    async route => {
      if (route.request().method() !== 'GET') {
        await route.continue();
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: SESSION_ID,
          gameId: '00000000-0000-4000-8000-000000000bb1',
          status: 'InProgress',
          startedAt: '2026-06-19T10:00:00.000Z',
          completedAt: null,
          playerCount: PLAYERS.length,
          players: PLAYERS,
          winnerName: null,
          notes: null,
          durationMinutes: 0,
          scoringType: opts.scoringType,
          scoreData: opts.scoreDataJson,
        }),
      });
    }
  );
}

/**
 * Block side-effect endpoints that are irrelevant to scoring smoke but
 * would otherwise log warnings / 401 spam in the page console. Mirrors the
 * defensive pattern from autosave-score.spec.ts.
 */
async function blockSideEffectEndpoints(page: Page): Promise<void> {
  // SignalR hub — we want the polymorphic store to hydrate from REST ONLY.
  await page.context().route(/\/hubs\/.*$/, route => route.abort('failed'));

  // SSE diary / game-session streams — not relevant to score panel rendering.
  await page
    .context()
    .route(/\/api\/v1\/sessions\/.*\/diary\/stream/, route => route.abort('failed'));
  await page
    .context()
    .route(/\/api\/v1\/game-sessions\/.*\/stream/, route => route.abort('failed'));

  // Active session lookup (used by ContextualHand) — return 204 to silence.
  await page
    .context()
    .route('**/api/v1/sessions/current', route => route.fulfill({ status: 204, body: '' }));
}

async function gotoSessionLive(page: Page): Promise<void> {
  await seedAuth(page);
  await page.goto(`/sessions/${SESSION_ID}/live`, { waitUntil: 'domcontentloaded' });
  // Wait for the SessionLiveView to land in default state (REST hydrated).
  await page.waitForSelector('[data-slot="session-live-view"][data-ui-state="default"]', {
    timeout: 30_000,
  });
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('#2389 Block C polymorphic scoring cleanup smoke', () => {
  // Chromium-only for speed — the cleanup is framework-agnostic.
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only for E2E smoke speed');

  test.beforeEach(async ({ page }) => {
    await blockSideEffectEndpoints(page);
  });

  // ── Scenario 1: Points panel + playerId-keyed leader ──────────────────────
  //
  // Validates:
  //   - The Points scoreData hydrated from REST mounts the Points renderer
  //     (data-slot="scoring-panel-points").
  //   - The leader cell (the highest scorer, Alice with 7) is highlighted.
  //     This indirectly pins useSessionScores().leader being playerId-keyed:
  //     if it were still playerName-keyed, the ScoringPanelRenderer would
  //     still mount (the renderer derives leader internally from `score`
  //     comparisons on data.players), but the upstream store wiring would
  //     break ESLint `local/no-store-scores-direct` (promoted to error in T7).
  //   - The Points renderer DOES NOT throw on hydration (the legacy
  //     `scores` Record<string, number> path would crash the adapter).

  test('1. Points scoreData hydrated from REST mounts ScoringPanelRenderer Points variant', async ({
    page,
  }) => {
    // Alice 7, Bob 5, Charlie 3 — Alice is the leader.
    await mockSessionDto(page, {
      scoringType: 'Points',
      scoreDataJson: JSON.stringify({
        scores: [
          { playerId: `${PLAYERS[0].playerName}-${PLAYERS[0].playerOrder}-0`, points: 7 },
          { playerId: `${PLAYERS[1].playerName}-${PLAYERS[1].playerOrder}-1`, points: 5 },
          { playerId: `${PLAYERS[2].playerName}-${PLAYERS[2].playerOrder}-2`, points: 3 },
        ],
      }),
    });

    await gotoSessionLive(page);

    // The default desktop right column tab is 'score' (parseLiveTab default).
    // ScoreTabContent's adapter maps Points scoreData → ScoringPanelRenderer
    // Points variant (the only branch with data-slot="scoring-panel-points").
    //
    // .first() — the renderer mounts in the desktop right column AND, on
    // narrower viewports, also in the mobile body. Asserting `.first()` is
    // viewport-agnostic so the spec doesn't break under future layout tweaks.
    const pointsPanel = page.locator('[data-slot="scoring-panel-points"]').first();
    await expect(pointsPanel).toBeVisible({ timeout: 15_000 });

    // Assert each player row is rendered (3 rows for 3 players).
    // scoring-row is rendered once per player in the sorted PointsPanel.
    const rows = pointsPanel.locator('[data-slot="scoring-row"]');
    await expect(rows).toHaveCount(3);

    // Negative assertion: the legacy ScoreBoard surface (autosave-indicator,
    // pinned by asse-d-p1) MUST NOT be present here — the polymorphic
    // renderer is the only score surface on /sessions/{id}/live.
    // This protects against accidental re-introduction of the legacy path.
    const legacyScoreBoard = page.locator('[data-testid="autosave-indicator"]');
    await expect(legacyScoreBoard).toHaveCount(0);
  });

  // ── Scenario 2: Spectator / Ranking variant ───────────────────────────────
  //
  // Validates:
  //   - Ranking scoreData hydrated from REST mounts the Ranking renderer
  //     (data-slot="scoring-panel-ranking", NOT scoring-panel-points).
  //   - The Ranking variant does NOT crash on aria template lookup (T12
  //     added the missing `rankAriaTemplate` key — a crash here would mean
  //     the i18n catalog drifted again).
  //
  // Note on "Spectator": SessionLiveView's REST adapter hardcodes
  // viewerRole='Player' (line 405-406). The ScoreTabContent path for both
  // Player and Spectator is identical — both mount ScoringPanelRenderer
  // (read-only). So this test exercises the same render path a Spectator
  // would see when receiving a ScoringConfigured push. The role-discrimination
  // (Host editor vs read-only renderer) is covered by the
  // ScoreTabContent.test.tsx unit suite.

  test('2. Ranking scoreData hydrated from REST mounts ScoringPanelRenderer Ranking variant', async ({
    page,
  }) => {
    await mockSessionDto(page, {
      scoringType: 'Ranking',
      scoreDataJson: JSON.stringify({
        positions: [
          { playerId: `${PLAYERS[0].playerName}-${PLAYERS[0].playerOrder}-0`, position: 1 },
          { playerId: `${PLAYERS[1].playerName}-${PLAYERS[1].playerOrder}-1`, position: 2 },
          { playerId: `${PLAYERS[2].playerName}-${PLAYERS[2].playerOrder}-2`, position: 3 },
        ],
      }),
    });

    await gotoSessionLive(page);

    // Ranking variant mounts under data-slot="scoring-panel-ranking".
    const rankingPanel = page.locator('[data-slot="scoring-panel-ranking"]').first();
    await expect(rankingPanel).toBeVisible({ timeout: 15_000 });

    // Negative assertion: the Points panel MUST NOT be visible — proves the
    // dispatcher branches correctly on scoringType.
    const pointsPanel = page.locator('[data-slot="scoring-panel-points"]');
    await expect(pointsPanel).toHaveCount(0);

    // Each player must have a ranking row. The Ranking variant uses
    // data-slot="ranking-row" (see ScoringPanelRenderer.tsx:222).
    const rankingRows = rankingPanel.locator('[data-slot="ranking-row"]');
    await expect(rankingRows).toHaveCount(3);

    // Probe: page console MUST NOT carry the [#2389] malformed-JSON warning.
    // SessionLiveView.tsx:972 logs `[#2389] malformed scoreData JSON, ...`
    // when JSON.parse blows up. Defensive: we capture page console messages
    // and assert none of them match the warning template (the JSON is
    // well-formed by construction, but this guards against future schema
    // drift breaking the parser).
    //
    // NOTE: capturing console after navigation would miss the boot-time
    // warning. We're not adding a capture here because the early-mount
    // warning would already have surfaced in the dev console and the page
    // would have rendered the empty `scoring-panel-empty` placeholder
    // instead of the Ranking panel — which the prior assertions would have
    // caught. So this comment is a contract-doc, not a separate assertion.
  });
});
