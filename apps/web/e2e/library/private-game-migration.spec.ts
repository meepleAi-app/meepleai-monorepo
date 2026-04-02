/**
 * LIB-17: Private Game Migration Flow
 * Plan: 2026-04-01-library-improvements.md — Task 17
 *
 * Tests the PrivateGame → Propose → Admin Approve → Migration flow:
 * - LinkToCatalog: sessions preserved on shared game (3 partite)
 * - KeepPrivate: sessions remain on private game (1 partita)
 *
 * These are mock-based tests: they verify frontend rendering given
 * mocked API responses, consistent with wishlist.spec.ts and play-history.spec.ts.
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const PRIVATE_GAME_ID = 'private-game-abc';
const SHARED_GAME_ID = 'shared-game-xyz';
const PRIVATE_GAME_TITLE = 'Mio Gioco Test';

async function setupMockAuth(page: Page) {
  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'user-123',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
        },
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }),
    });
  });
}

async function setupLinkToCatalogMocks(page: Page) {
  await setupMockAuth(page);

  const sessions = [
    {
      id: 'session-1',
      gameId: SHARED_GAME_ID,
      playedAt: new Date(Date.now() - 86400000 * 3).toISOString(),
      durationMinutes: 60,
      players: ['Test User'],
    },
    {
      id: 'session-2',
      gameId: SHARED_GAME_ID,
      playedAt: new Date(Date.now() - 86400000 * 2).toISOString(),
      durationMinutes: 45,
      players: ['Test User'],
    },
    {
      id: 'session-3',
      gameId: SHARED_GAME_ID,
      playedAt: new Date(Date.now() - 86400000).toISOString(),
      durationMinutes: 90,
      players: ['Test User'],
    },
  ];

  // Shared game page — game detail with migrated sessions
  await page.route(`${API_BASE}/api/v1/games/${SHARED_GAME_ID}**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: SHARED_GAME_ID,
        title: PRIVATE_GAME_TITLE,
        publisher: 'Test Publisher',
        playCount: sessions.length,
        averageRating: 8.5,
      }),
    });
  });

  // Sessions endpoint for the shared game
  await page.route(`${API_BASE}/api/v1/library/${SHARED_GAME_ID}/sessions**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        sessions,
        totalCount: sessions.length,
      }),
    });
  });

  // Library listing — shows game as shared after migration
  await page.route(`${API_BASE}/api/v1/library**`, async route => {
    if (route.request().url().includes('/private')) {
      await route.continue();
      return;
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [
          {
            id: SHARED_GAME_ID,
            title: PRIVATE_GAME_TITLE,
            type: 'shared',
            sessionCount: sessions.length,
          },
        ],
        totalCount: 1,
      }),
    });
  });

  // Fallback for any other game/catalog routes
  await page.route(`${API_BASE}/api/v1/games**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });
}

async function setupKeepPrivateMocks(page: Page) {
  await setupMockAuth(page);

  const sessions = [
    {
      id: 'session-a',
      privateGameId: PRIVATE_GAME_ID,
      playedAt: new Date(Date.now() - 86400000).toISOString(),
      durationMinutes: 75,
      players: ['Test User'],
    },
  ];

  // Private game detail endpoint
  await page.route(`${API_BASE}/api/v1/library/private-games/${PRIVATE_GAME_ID}**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: PRIVATE_GAME_ID,
        title: PRIVATE_GAME_TITLE,
        sessions,
        sessionCount: sessions.length,
      }),
    });
  });

  // Private game page route (alternative path pattern used by frontend)
  await page.route(`${API_BASE}/api/v1/library/private/${PRIVATE_GAME_ID}**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: PRIVATE_GAME_ID,
        title: PRIVATE_GAME_TITLE,
        sessions,
        sessionCount: sessions.length,
      }),
    });
  });

  // Fallback for library listing (private game still in library)
  await page.route(`${API_BASE}/api/v1/library**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        items: [
          {
            id: PRIVATE_GAME_ID,
            title: PRIVATE_GAME_TITLE,
            type: 'private',
            sessionCount: sessions.length,
          },
        ],
        totalCount: 1,
      }),
    });
  });

  // Fallback for any other game/catalog routes
  await page.route(`${API_BASE}/api/v1/games**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([]),
    });
  });
}

test.describe('PrivateGame migration flow', () => {
  test('sessioni rimangono dopo migrazione LinkToCatalog', async ({ page }) => {
    await setupLinkToCatalogMocks(page);

    // Navigate to the shared game page (result of LinkToCatalog migration)
    await page.goto(`/library/games/${SHARED_GAME_ID}`);
    await page.waitForLoadState('networkidle');

    // The game title should be visible
    await expect(page.getByText(PRIVATE_GAME_TITLE).first()).toBeVisible({ timeout: 5000 });

    // The session count (3 partite) or the sessions list should be visible.
    // The exact label depends on the frontend implementation; we check for either
    // the numeric count or a recognizable session-related text.
    await expect(page.getByText(/3\s*part|3\s*session|3\s*gioc/i).first()).toBeVisible({
      timeout: 5000,
    });

    // Navigate to the library listing and confirm game appears as shared
    await page.goto('/library');
    await page.waitForLoadState('networkidle');

    await expect(page.getByText(PRIVATE_GAME_TITLE).first()).toBeVisible({ timeout: 5000 });
  });

  test('KeepPrivate mantiene le sessioni sul gioco privato', async ({ page }) => {
    await setupKeepPrivateMocks(page);

    // Navigate to the private game page (game kept private after migration)
    await page.goto(`/library/private/${PRIVATE_GAME_ID}`);
    await page.waitForLoadState('networkidle');

    // The game title should be visible
    await expect(page.getByText(PRIVATE_GAME_TITLE).first()).toBeVisible({ timeout: 5000 });

    // The session count (1 partita) or the sessions list should be visible.
    await expect(page.getByText(/1\s*part|1\s*session|1\s*gioc/i).first()).toBeVisible({
      timeout: 5000,
    });
  });
});
