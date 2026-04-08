/**
 * Game Night Soft Filter E2E Tests — MVP Hardening F1
 *
 * Tests the PDF-aware soft filter in the GameNightWizard game selector:
 *  1. Each game option shows a "AI pronto" or "Solo manuale" badge
 *  2. Selecting a non-indexed game shows an inline warning
 *  3. User can still confirm and proceed with a non-indexed game (soft filter,
 *     non-blocking)
 *
 * Uses page.route() for API mocking. The kb-status endpoint is mocked to return
 * different values per gameId to exercise both indexed and non-indexed paths.
 */

import { test, expect, Page } from '@playwright/test';

// ============================================================================
// Constants
// ============================================================================

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const WIZARD_URL = '/game-nights/new';

// ============================================================================
// Mock Data
// ============================================================================

const MOCK_USER = {
  user: {
    id: 'user-soft-filter-001',
    email: 'host@meepleai.dev',
    displayName: 'Soft Filter Host',
    role: 'User',
    tier: 'premium',
  },
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
};

const INDEXED_GAME = {
  id: 'game-catan-indexed',
  title: 'Catan',
  yearPublished: 1995,
  thumbnailUrl: null,
  imageUrl: null,
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  averageRating: 7.2,
  complexityRating: 2.3,
  bggId: 13,
  description: 'Trade, build, settle on the island of Catan.',
  status: 1,
};

const NON_INDEXED_GAME = {
  id: 'game-obscure-nonindexed',
  title: 'ObscureGame',
  yearPublished: 2020,
  thumbnailUrl: null,
  imageUrl: null,
  minPlayers: 2,
  maxPlayers: 4,
  playingTimeMinutes: 45,
  averageRating: 6.5,
  complexityRating: 2.0,
  bggId: 999999,
  description: 'An obscure game not yet indexed by RAG.',
  status: 1,
};

// ============================================================================
// Helpers
// ============================================================================

async function setupMockRoutes(page: Page) {
  // Auth
  await page.route(`${API_BASE}/api/v1/auth/session`, route =>
    route.fulfill({ status: 200, json: MOCK_USER })
  );
  await page.route(`${API_BASE}/api/v1/auth/me`, route =>
    route.fulfill({ status: 200, json: MOCK_USER })
  );

  // Feature flags — wizard depends on GameNight feature flag
  await page.route(`${API_BASE}/api/v1/system-config/features/**`, route =>
    route.fulfill({ status: 200, json: { enabled: true, key: 'Features.GameNight' } })
  );

  // Shared games catalog search — return both games
  await page.route(`${API_BASE}/api/v1/shared-games**`, route => {
    const url = new URL(route.request().url());
    if (url.pathname.startsWith('/api/v1/shared-games') && route.request().method() === 'GET') {
      return route.fulfill({
        status: 200,
        json: {
          items: [INDEXED_GAME, NON_INDEXED_GAME],
          totalCount: 2,
          page: 1,
          pageSize: 20,
        },
      });
    }
    return route.continue();
  });

  // KB status — indexed for Catan, not indexed for ObscureGame
  await page.route(`${API_BASE}/api/v1/games/${INDEXED_GAME.id}/knowledge-base`, route =>
    route.fulfill({
      status: 200,
      json: {
        gameId: INDEXED_GAME.id,
        isIndexed: true,
        documentCount: 3,
        coverageScore: 80,
        coverageLevel: 'Standard',
        suggestedQuestions: [],
      },
    })
  );

  await page.route(`${API_BASE}/api/v1/games/${NON_INDEXED_GAME.id}/knowledge-base`, route =>
    route.fulfill({
      status: 200,
      json: {
        gameId: NON_INDEXED_GAME.id,
        isIndexed: false,
        documentCount: 0,
        coverageScore: 0,
        coverageLevel: 'None',
        suggestedQuestions: [],
      },
    })
  );

  // Catch-all for unmatched API requests (200 empty)
  await page.route(`${API_BASE}/api/**`, route => route.fulfill({ status: 200, json: {} }));
}

async function searchCatan(page: Page) {
  const input = page.getByTestId('game-search-input');
  await input.fill('catan');
  await input.press('Enter');
  await expect(page.getByTestId('game-search-results')).toBeVisible({ timeout: 5000 });
}

// ============================================================================
// Tests
// ============================================================================

test.describe('Game Night Wizard — PDF-aware soft filter (F1)', () => {
  test.beforeEach(async ({ page }) => {
    await setupMockRoutes(page);
  });

  test('shows "AI pronto" badge for indexed games and "Solo manuale" for non-indexed', async ({
    page,
  }) => {
    await page.goto(WIZARD_URL);
    await page.waitForLoadState('networkidle');

    await searchCatan(page);

    // Wait for both kb-status fetches to resolve (badges are conditionally rendered)
    const indexedOption = page.locator(`[data-game-id="${INDEXED_GAME.id}"]`);
    const nonIndexedOption = page.locator(`[data-game-id="${NON_INDEXED_GAME.id}"]`);

    await expect(indexedOption).toBeVisible();
    await expect(nonIndexedOption).toBeVisible();

    // Indexed game → "AI pronto" badge (emerald)
    await expect(indexedOption.getByText('AI pronto')).toBeVisible({ timeout: 5000 });
    await expect(
      indexedOption.locator('[data-testid="game-kb-badge"][data-indexed="true"]')
    ).toBeVisible();

    // Non-indexed game → "Solo manuale" badge (gray)
    await expect(nonIndexedOption.getByText('Solo manuale')).toBeVisible({ timeout: 5000 });
    await expect(
      nonIndexedOption.locator('[data-testid="game-kb-badge"][data-indexed="false"]')
    ).toBeVisible();
  });

  test('shows inline warning when selecting a non-indexed game', async ({ page }) => {
    await page.goto(WIZARD_URL);
    await page.waitForLoadState('networkidle');

    await searchCatan(page);

    // Warning should NOT be present before selection
    await expect(page.getByTestId('kb-warning')).not.toBeVisible();

    // Select non-indexed game
    const nonIndexedOption = page.locator(`[data-game-id="${NON_INDEXED_GAME.id}"]`);
    await nonIndexedOption.click();

    // Warning appears
    const warning = page.getByTestId('kb-warning');
    await expect(warning).toBeVisible({ timeout: 5000 });
    await expect(warning).toHaveAttribute('role', 'alert');
    await expect(warning).toContainText('Agente AI non disponibile');
    await expect(warning).toContainText('non è ancora indicizzato');
    await expect(warning).toContainText('usare gli strumenti manuali');

    // Confirm button is visible AND enabled (non-blocking — user can proceed)
    const confirmBtn = page.getByTestId('confirm-game-button');
    await expect(confirmBtn).toBeVisible();
    await expect(confirmBtn).toBeEnabled();
    await expect(confirmBtn).toContainText('Continua con ObscureGame');
  });

  test('does not show warning when selecting an indexed game', async ({ page }) => {
    await page.goto(WIZARD_URL);
    await page.waitForLoadState('networkidle');

    await searchCatan(page);

    // Select indexed game
    const indexedOption = page.locator(`[data-game-id="${INDEXED_GAME.id}"]`);
    await indexedOption.click();

    // No warning present
    await expect(page.getByTestId('kb-warning')).not.toBeVisible();

    // Confirm button shows with indexed game title
    const confirmBtn = page.getByTestId('confirm-game-button');
    await expect(confirmBtn).toBeVisible();
    await expect(confirmBtn).toBeEnabled();
    await expect(confirmBtn).toContainText('Continua con Catan');
  });

  test('clears warning when switching from non-indexed to indexed game', async ({ page }) => {
    await page.goto(WIZARD_URL);
    await page.waitForLoadState('networkidle');

    await searchCatan(page);

    // First: select non-indexed → warning visible
    await page.locator(`[data-game-id="${NON_INDEXED_GAME.id}"]`).click();
    await expect(page.getByTestId('kb-warning')).toBeVisible({ timeout: 5000 });

    // Then: switch to indexed → warning must disappear
    await page.locator(`[data-game-id="${INDEXED_GAME.id}"]`).click();
    await expect(page.getByTestId('kb-warning')).not.toBeVisible();
  });
});
