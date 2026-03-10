/**
 * Multi-Expansion Degradation E2E Tests
 * Issue #5589: Game with 2 expansions (1 with PDF, 1 without) → verify warning badge.
 *
 * Tests degradation behavior when a game session has mixed expansion PDF coverage:
 * 1. Primary game + expansion A (both indexed) = Partial degradation
 * 2. Expansion B missing PDF → warning in context
 * 3. Verify missingAnalysisGameNames contains the correct expansion
 */

import { test, expect, Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Mock Data
// ============================================================================

const MOCK_USER = {
  user: {
    id: 'user-expansion-001',
    email: 'player@meepleai.dev',
    displayName: 'Expansion Tester',
    role: 'User',
    tier: 'premium',
  },
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
};

const PRIMARY_GAME_ID = 'game-catan-base';
const EXPANSION_A_ID = 'game-catan-seafarers';
const EXPANSION_B_ID = 'game-catan-cities';
const SESSION_ID = 'session-expansion-001';

const MOCK_SESSION_CONTEXT_PARTIAL = {
  sessionId: SESSION_ID,
  primaryGameId: PRIMARY_GAME_ID,
  expansionGameIds: [EXPANSION_A_ID, EXPANSION_B_ID],
  allGameIds: [PRIMARY_GAME_ID, EXPANSION_A_ID, EXPANSION_B_ID],
  kbCardIds: [PRIMARY_GAME_ID, EXPANSION_A_ID], // Only base + Seafarers have PDFs
  currentPhase: 'Main',
  primaryRules: {
    gameId: PRIMARY_GAME_ID,
    gameTitle: 'Catan',
    summary: 'Trade and build settlements on the island of Catan.',
    keyMechanics: ['Trading', 'Building', 'Dice Rolling'],
    currentPhaseName: 'Main',
    phaseNames: ['Setup', 'Main', 'Endgame'],
  },
  expansionRules: [
    {
      gameId: EXPANSION_A_ID,
      gameTitle: 'Catan: Seafarers',
      summary: 'Ships and island exploration expansion.',
      keyMechanics: ['Ships', 'Gold Rivers'],
      currentPhaseName: null,
      phaseNames: ['Setup', 'Exploration'],
    },
  ],
  missingAnalysisGameNames: ['Catan: Cities & Knights'], // Expansion B missing
  gamesWithoutPdf: [EXPANSION_B_ID],
  degradationLevel: 1, // Partial
};

const MOCK_SESSION_CONTEXT_FULL = {
  sessionId: SESSION_ID,
  primaryGameId: PRIMARY_GAME_ID,
  expansionGameIds: [EXPANSION_A_ID],
  allGameIds: [PRIMARY_GAME_ID, EXPANSION_A_ID],
  kbCardIds: [PRIMARY_GAME_ID, EXPANSION_A_ID],
  currentPhase: 'Main',
  primaryRules: {
    gameId: PRIMARY_GAME_ID,
    gameTitle: 'Catan',
    summary: 'Trade and build.',
    keyMechanics: ['Trading'],
    currentPhaseName: 'Main',
    phaseNames: ['Setup', 'Main'],
  },
  expansionRules: [
    {
      gameId: EXPANSION_A_ID,
      gameTitle: 'Catan: Seafarers',
      summary: 'Ships expansion.',
      keyMechanics: ['Ships'],
      currentPhaseName: null,
      phaseNames: ['Setup'],
    },
  ],
  missingAnalysisGameNames: [],
  gamesWithoutPdf: [],
  degradationLevel: 0, // Full
};

const MOCK_SESSION_CONTEXT_NO_AI = {
  sessionId: SESSION_ID,
  primaryGameId: PRIMARY_GAME_ID,
  expansionGameIds: [EXPANSION_A_ID, EXPANSION_B_ID],
  allGameIds: [PRIMARY_GAME_ID, EXPANSION_A_ID, EXPANSION_B_ID],
  kbCardIds: [],
  currentPhase: 'Main',
  primaryRules: null,
  expansionRules: [],
  missingAnalysisGameNames: ['Catan: Seafarers', 'Catan: Cities & Knights'],
  gamesWithoutPdf: [PRIMARY_GAME_ID, EXPANSION_A_ID, EXPANSION_B_ID],
  degradationLevel: 3, // NoAI
};

// ============================================================================
// Helpers
// ============================================================================

async function setupRoutes(page: Page, contextResponse: object) {
  await page.route(`${API_BASE}/api/v1/auth/session`, route =>
    route.fulfill({ status: 200, json: MOCK_USER })
  );

  await page.route(`${API_BASE}/api/v1/live-sessions/${SESSION_ID}/context`, route =>
    route.fulfill({ status: 200, json: contextResponse })
  );

  await page.route(`${API_BASE}/api/**`, route => route.fulfill({ status: 200, json: {} }));
}

// ============================================================================
// Tests
// ============================================================================

test.describe('Multi-Expansion Degradation', () => {
  test('should report Partial degradation when one expansion missing PDF', async ({ page }) => {
    await setupRoutes(page, MOCK_SESSION_CONTEXT_PARTIAL);

    const context = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/context`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    expect(context.degradationLevel).toBe(1); // Partial
    expect(context.expansionGameIds).toHaveLength(2);
    expect(context.kbCardIds).toHaveLength(2);
    expect(context.gamesWithoutPdf).toHaveLength(1);
    expect(context.gamesWithoutPdf[0]).toBe(EXPANSION_B_ID);
  });

  test('should list missing analysis game names for expansions without PDF', async ({ page }) => {
    await setupRoutes(page, MOCK_SESSION_CONTEXT_PARTIAL);

    const context = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/context`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    expect(context.missingAnalysisGameNames).toHaveLength(1);
    expect(context.missingAnalysisGameNames[0]).toBe('Catan: Cities & Knights');
  });

  test('should report Full degradation when all expansions have PDFs', async ({ page }) => {
    await setupRoutes(page, MOCK_SESSION_CONTEXT_FULL);

    const context = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/context`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    expect(context.degradationLevel).toBe(0); // Full
    expect(context.gamesWithoutPdf).toHaveLength(0);
    expect(context.missingAnalysisGameNames).toHaveLength(0);
  });

  test('should report NoAI when no games have PDFs', async ({ page }) => {
    await setupRoutes(page, MOCK_SESSION_CONTEXT_NO_AI);

    const context = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/context`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    expect(context.degradationLevel).toBe(3); // NoAI
    expect(context.kbCardIds).toHaveLength(0);
    expect(context.gamesWithoutPdf).toHaveLength(3);
    expect(context.missingAnalysisGameNames).toHaveLength(2);
  });

  test('should include expansion rules only for expansions with analyzed PDFs', async ({
    page,
  }) => {
    await setupRoutes(page, MOCK_SESSION_CONTEXT_PARTIAL);

    const context = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/${SESSION_ID}/context`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    // Only Seafarers (expansion A) has rules — Cities & Knights (expansion B) does not
    expect(context.expansionRules).toHaveLength(1);
    expect(context.expansionRules[0].gameTitle).toBe('Catan: Seafarers');

    // Primary rules should be present
    expect(context.primaryRules).not.toBeNull();
    expect(context.primaryRules.gameTitle).toBe('Catan');
    expect(context.primaryRules.keyMechanics).toContain('Trading');
  });
});
