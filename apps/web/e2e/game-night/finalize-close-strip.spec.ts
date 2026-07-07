/**
 * GameNight finalize → close-strip E2E (SI-3 #2634) — SKELETON (test.skip).
 *
 * Pins the end-to-end in-progress→completed close flow across the two surfaces the transition
 * touches:
 *   1. The night-live hub (/game-nights/{id}/live): the organizer, with every game done and none
 *      live, clicks "Concludi serata" → POST /complete → the read model refetches Completed → the
 *      LD-14 effect routes to /summary.
 *   2. The gamebook play page (/library/{gameId}/play/{campaignId}): after finalize, the invalidated
 *      spine read returns gameNightStatus:'Completed' → SerataSpineStrip renders "completata", no
 *      live badge, and the SerataResumeButton is gone (no backward transition on the FE).
 *
 * Kept `test.skip` (skeleton) — enable once the finalize CTA + cross-surface invalidation land in a
 * seeded/harnessed E2E environment. Uses page.context().route() (NOT page.route()) for Next.js
 * server-side fetch interception.
 */

import { test, expect, type Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const NIGHT_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const GAME_ID = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const CAMPAIGN_ID = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';

const MOCK_USER = {
  user: {
    id: 'user-organizer-001',
    email: 'organizer@meepleai.dev',
    displayName: 'Night Organizer',
    role: 'User',
    tier: 'premium',
  },
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
};

// A night mid-play: every planned game terminal, nothing live, status InProgress → the finalize
// CTA precondition. The organizer flag gates the CTA.
const LIVE_IN_PROGRESS = {
  id: NIGHT_ID,
  title: 'Serata Eldoria',
  status: 'InProgress',
  isViewerOrganizer: true,
  plannedLineup: [], // nothing left to start → nextGame === null
  currentSessionRoster: [],
  sessions: [
    {
      sessionId: '11111111-1111-4111-8111-111111111111',
      gameId: GAME_ID,
      gameTitle: 'Spirit Island',
      playOrder: 1,
      status: 'Completed',
      winnerId: null,
      winnerName: null,
      startedAt: new Date(Date.now() - 3 * 60 * 60 * 1000).toISOString(),
      completedAt: new Date(Date.now() - 60 * 60 * 1000).toISOString(),
    },
  ],
};

// Post-finalize spine: the owning GameNight is Completed with no live sitting → the strip pins the
// terminal state and drops the resume affordance (no FE backward flip).
const SPINE_COMPLETED = {
  gameNightId: NIGHT_ID,
  gameNightTitle: 'Serata Eldoria',
  organizerId: MOCK_USER.user.id,
  gameNightStatus: 'Completed',
  totalSessions: 1,
  completedSessions: 1,
  hasLiveSession: false,
  campaignStatus: 'Completed',
};

async function setupRoutes(page: Page) {
  await page
    .context()
    .route(`${API_BASE}/api/v1/auth/session`, route =>
      route.fulfill({ status: 200, json: MOCK_USER })
    );
  await page
    .context()
    .route(`${API_BASE}/api/v1/game-nights/${NIGHT_ID}/live`, route =>
      route.fulfill({ status: 200, json: LIVE_IN_PROGRESS })
    );
  // The finalize call — the flow under test.
  await page
    .context()
    .route(`${API_BASE}/api/v1/game-nights/${NIGHT_ID}/complete`, route =>
      route.fulfill({ status: 200, json: { gameNightId: NIGHT_ID, sessionsCompleted: 1 } })
    );
  await page
    .context()
    .route(`${API_BASE}/api/v1/gamebook/campaigns/${CAMPAIGN_ID}/spine`, route =>
      route.fulfill({ status: 200, json: SPINE_COMPLETED })
    );
}

test.describe('GameNight finalize → close strip (SI-3 #2634)', () => {
  test.skip('organizer concludes an in-progress night and is routed to the summary', async ({
    page,
  }) => {
    await setupRoutes(page);
    await page.goto(`/game-nights/${NIGHT_ID}/live`);

    await page.getByRole('button', { name: /Concludi serata/i }).click();

    // POST /complete → read model refetch → LD-14 routes to the summary.
    await expect(page).toHaveURL(new RegExp(`/game-nights/${NIGHT_ID}/summary`));
  });

  test.skip('the gamebook Serata strip reflects the Completed night after finalize', async ({
    page,
  }) => {
    await setupRoutes(page);
    await page.goto(`/library/${GAME_ID}/play/${CAMPAIGN_ID}`);

    const strip = page.getByTestId('serata-spine-strip');
    await expect(strip).toBeVisible();
    await expect(strip).toContainText('completata');
    await expect(page.getByTestId('serata-live-badge')).toHaveCount(0);
    // No backward transition on the FE — the resume affordance is gone once completed.
    await expect(page.getByRole('button', { name: /Riprendi/i })).toHaveCount(0);
  });
});
