/**
 * Game Night Diary Events — Playwright E2E tests (Issue #324)
 *
 * Verifies that the GameNightDiaryPanel correctly renders diary entries
 * from the `GET /api/v1/game-nights/{id}/diary` endpoint, including:
 *   1. 3 game-night event types rendered as diary-entry elements
 *   2. Event labels from diary-utils EVENT_META are displayed correctly
 *   3. Empty state renders the "Nessun evento" message
 *
 * Mock boundary: HTTP layer only. React Query, diary-utils, and router run for real.
 *
 * Related: Session Flow v2.1 (#365/#369), diary-utils.ts EVENT_META
 */

import { expect, test } from '@playwright/test';

import { AuthHelper, USER_FIXTURES } from '../pages';

// ── UUID Constants ─────────────────────────────────────────────────────────
const C = {
  GAME_NIGHT_ID: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  SESSION_ID: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
  // Must match USER_FIXTURES.user.id so organizer identity checks pass
  USER_ID: 'user-test-1',
  ENTRY_ID_1: '11111111-1111-4111-8111-111111111111',
  ENTRY_ID_2: '22222222-2222-4222-8222-222222222222',
  ENTRY_ID_3: '33333333-3333-4333-8333-333333333333',
} as const;

// Fixed timestamps ensure deterministic descending sort in GameNightDiaryPanel
const T = {
  CREATED: '2026-01-01T10:00:00.000Z',
  GAME_ADDED: '2026-01-01T10:01:00.000Z',
  COMPLETED: '2026-01-01T10:02:00.000Z',
} as const;

// ── Shared mock helpers ────────────────────────────────────────────────────

async function mockGameNight(
  page: import('@playwright/test').Page,
  status: 'Published' | 'Completed' = 'Completed'
) {
  await page.context().route(`**/api/v1/game-nights/${C.GAME_NIGHT_ID}`, route => {
    if (route.request().method() !== 'GET') return route.continue();
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: C.GAME_NIGHT_ID,
        organizerId: C.USER_ID,
        organizerName: 'Host',
        title: 'Serata Test',
        description: null,
        scheduledAt: T.CREATED,
        location: null,
        maxPlayers: null,
        gameIds: [],
        status,
        acceptedCount: 1,
        pendingCount: 0,
        totalInvited: 1,
        createdAt: T.CREATED,
        updatedAt: null,
      }),
    });
  });
}

async function mockRsvps(page: import('@playwright/test').Page) {
  await page
    .context()
    .route(`**/api/v1/game-nights/${C.GAME_NIGHT_ID}/rsvps**`, route =>
      route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
    );
}

// ─────────────────────────────────────────────────────────────────────────────

test.describe('Game Night Diary Events', () => {
  test.use({ viewport: { width: 1280, height: 800 } });

  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);
    await page.goto('about:blank');
    await page.evaluate(() => localStorage.clear());

    // Suppress current session to avoid ContextualHand side effects
    await page
      .context()
      .route('**/api/v1/sessions/current', route => route.fulfill({ status: 204, body: '' }));
  });

  // ════════════════════════════════════════════════════════════════════════════
  // Scenario 1 — Diary panel renders game-night event types with correct labels
  //
  // Validates:
  //   - GET /game-nights/{id}/diary is called
  //   - 3x data-testid="diary-entry" elements are rendered
  //   - The most recent entry (gamenight_completed → "Serata completata") is
  //     displayed first (panel sorts entries by timestamp descending)
  // ════════════════════════════════════════════════════════════════════════════

  test('renders 3 diary entries with correct event labels', async ({ page }) => {
    await mockGameNight(page, 'Completed');
    await mockRsvps(page);

    // Entries sorted descending in the panel — newest (gamenight_completed) appears first
    const diaryEntries = [
      {
        id: C.ENTRY_ID_1,
        sessionId: C.SESSION_ID,
        gameNightId: C.GAME_NIGHT_ID,
        eventType: 'gamenight_created',
        timestamp: T.CREATED,
        payload: null,
        createdBy: C.USER_ID,
        source: 'system',
      },
      {
        id: C.ENTRY_ID_2,
        sessionId: C.SESSION_ID,
        gameNightId: C.GAME_NIGHT_ID,
        eventType: 'gamenight_game_added',
        timestamp: T.GAME_ADDED,
        payload: null,
        createdBy: C.USER_ID,
        source: 'system',
      },
      {
        id: C.ENTRY_ID_3,
        sessionId: C.SESSION_ID,
        gameNightId: C.GAME_NIGHT_ID,
        eventType: 'gamenight_completed',
        timestamp: T.COMPLETED,
        payload: null,
        createdBy: C.USER_ID,
        source: 'system',
      },
    ];

    await page.context().route(`**/api/v1/game-nights/${C.GAME_NIGHT_ID}/diary**`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(diaryEntries),
      })
    );

    await page.goto(`/game-nights/${C.GAME_NIGHT_ID}`);
    await page.waitForLoadState('networkidle');

    // Page title confirms navigation succeeded
    await expect(page.getByRole('heading', { name: 'Serata Test' }).first()).toBeVisible({
      timeout: 10_000,
    });

    // Wait for first entry to appear, then assert total count
    const entries = page.getByTestId('diary-entry');
    await expect(entries.first()).toBeVisible({ timeout: 5_000 });
    await expect(entries).toHaveCount(3, { timeout: 5_000 });

    // Most recent entry (gamenight_completed) is first due to descending sort
    await expect(entries.first()).toContainText('Serata completata');
  });

  // ════════════════════════════════════════════════════════════════════════════
  // Scenario 2 — Empty diary shows placeholder message
  //
  // Validates:
  //   - When GET /game-nights/{id}/diary returns [], the empty state is rendered
  //   - No diary-entry elements are present
  // ════════════════════════════════════════════════════════════════════════════

  test('shows empty state when diary has no events', async ({ page }) => {
    await mockGameNight(page, 'Completed');
    await mockRsvps(page);

    await page
      .context()
      .route(`**/api/v1/game-nights/${C.GAME_NIGHT_ID}/diary**`, route =>
        route.fulfill({ status: 200, contentType: 'application/json', body: '[]' })
      );

    await page.goto(`/game-nights/${C.GAME_NIGHT_ID}`);
    await page.waitForLoadState('networkidle');

    await expect(page.getByRole('heading', { name: 'Serata Test' }).first()).toBeVisible({
      timeout: 10_000,
    });

    // Empty state message must appear before asserting count=0, to ensure
    // React Query has settled and the loading state has resolved
    await expect(page.getByText(/nessun evento/i)).toBeVisible({ timeout: 5_000 });
    await expect(page.getByTestId('diary-entry')).toHaveCount(0, { timeout: 5_000 });
  });
});
