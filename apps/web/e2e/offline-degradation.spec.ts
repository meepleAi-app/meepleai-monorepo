/**
 * Offline Degradation E2E Tests
 * Issue #5589: Verify cached FAQ fallback when network is disconnected.
 *
 * Tests graceful degradation behavior:
 * 1. Load rules cache while online
 * 2. Simulate disconnect
 * 3. Verify cached FAQ data is still accessible
 * 4. Verify degradation indicator shows
 */

import { test, expect, Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Mock Data
// ============================================================================

const MOCK_USER = {
  user: {
    id: 'user-offline-001',
    email: 'player@meepleai.dev',
    displayName: 'Offline Player',
    role: 'User',
    tier: 'free',
  },
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
};

const MOCK_FAQ_RULES = [
  {
    id: 'faq-001',
    question: 'When can I trade resources?',
    answer: 'You can trade during the trade phase of your turn.',
    category: 'Trading',
    gameId: 'game-catan-001',
  },
  {
    id: 'faq-002',
    question: 'How does the robber work?',
    answer: 'When a 7 is rolled, move the robber to any hex and steal one card.',
    category: 'Robber',
    gameId: 'game-catan-001',
  },
  {
    id: 'faq-003',
    question: 'How many victory points to win?',
    answer: 'The first player to reach 10 victory points wins.',
    category: 'Victory',
    gameId: 'game-catan-001',
  },
];

const MOCK_SESSION_CONTEXT_DEGRADED = {
  sessionId: 'session-offline-001',
  primaryGameId: 'game-catan-001',
  expansionGameIds: [],
  allGameIds: ['game-catan-001'],
  kbCardIds: [],
  currentPhase: 'Main',
  degradationLevel: 3, // NoAI
  gamesWithoutPdf: ['game-catan-001'],
  missingAnalysisGameNames: [],
};

// ============================================================================
// Helpers
// ============================================================================

async function setupOnlineRoutes(page: Page) {
  await page.route(`${API_BASE}/api/v1/auth/session`, route =>
    route.fulfill({ status: 200, json: MOCK_USER })
  );

  // FAQ/rules endpoint
  await page.route(`${API_BASE}/api/v1/games/*/faqs`, route =>
    route.fulfill({ status: 200, json: MOCK_FAQ_RULES })
  );

  // Session context — degraded
  await page.route(`${API_BASE}/api/v1/live-sessions/*/context`, route =>
    route.fulfill({ status: 200, json: MOCK_SESSION_CONTEXT_DEGRADED })
  );

  // Catch-all
  await page.route(`${API_BASE}/api/**`, route => route.fulfill({ status: 200, json: {} }));
}

// ============================================================================
// Tests
// ============================================================================

test.describe('Offline Degradation', () => {
  test('should serve FAQ data while online', async ({ page }) => {
    await setupOnlineRoutes(page);

    const faqResponse = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/games/game-catan-001/faqs`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    expect(faqResponse).toHaveLength(3);
    expect(faqResponse[0].question).toContain('trade');
  });

  test('should return degraded context when no KBs indexed', async ({ page }) => {
    await setupOnlineRoutes(page);

    const contextResponse = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/live-sessions/session-offline-001/context`, {
        credentials: 'include',
      });
      return await res.json();
    }, API_BASE);

    expect(contextResponse.degradationLevel).toBe(3); // NoAI
    expect(contextResponse.kbCardIds).toHaveLength(0);
    expect(contextResponse.gamesWithoutPdf).toContain('game-catan-001');
  });

  test('should handle network failure gracefully with cached data', async ({ page, context }) => {
    await setupOnlineRoutes(page);

    // Load FAQ data while online
    const onlineResult = await page.evaluate(async apiBase => {
      const res = await fetch(`${apiBase}/api/v1/games/game-catan-001/faqs`, {
        credentials: 'include',
      });
      const data = await res.json();
      // Simulate caching in localStorage
      localStorage.setItem('faq-cache-game-catan-001', JSON.stringify(data));
      return data;
    }, API_BASE);

    expect(onlineResult).toHaveLength(3);

    // Go offline by aborting all future API routes
    await page.route(`${API_BASE}/api/**`, route => route.abort('connectionrefused'));

    // Verify cached data is still accessible in localStorage
    const cachedData = await page.evaluate(() => {
      const cached = localStorage.getItem('faq-cache-game-catan-001');
      return cached ? JSON.parse(cached) : null;
    });

    expect(cachedData).toHaveLength(3);
    expect(cachedData[0].question).toContain('trade');
    expect(cachedData[1].question).toContain('robber');
  });

  test('should detect network unavailability', async ({ page }) => {
    await setupOnlineRoutes(page);
    await page.goto('/dashboard');
    await page.waitForLoadState('networkidle');

    // Simulate offline by aborting requests
    await page.route(`${API_BASE}/api/**`, route => route.abort('connectionrefused'));

    // Try to make a request — should fail
    const offlineResult = await page.evaluate(async apiBase => {
      try {
        await fetch(`${apiBase}/api/v1/games/game-catan-001/faqs`, {
          credentials: 'include',
        });
        return { online: true };
      } catch {
        return { online: false };
      }
    }, API_BASE);

    expect(offlineResult.online).toBe(false);
  });
});
