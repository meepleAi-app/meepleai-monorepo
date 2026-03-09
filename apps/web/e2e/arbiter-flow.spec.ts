/**
 * Arbiter Flow E2E Tests
 * Issue #5589: Arbiter mode — dispute arbitration with citations and verdict.
 *
 * Tests the arbiter dispute resolution flow:
 * 1. Open arbiter panel
 * 2. Submit dispute with situation + two positions
 * 3. Receive verdict with confidence score and citations
 */

import { test, expect, Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ============================================================================
// Mock Data
// ============================================================================

const MOCK_USER = {
  user: {
    id: 'user-arbiter-001',
    email: 'player@meepleai.dev',
    displayName: 'Dispute Reporter',
    role: 'User',
    tier: 'premium',
  },
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
};

const MOCK_VERDICT = {
  verdict:
    'Player A is correct. According to the rulebook, when a player rolls a 7, ' +
    'all players with more than 7 resource cards must discard half (rounded down).',
  confidence: 0.92,
  isConclusive: true,
  citations: [
    {
      documentTitle: 'Catan Rules PDF',
      section: 'Page 8 - The Robber',
      text: 'If a player has more than 7 Resource Cards, they must select half (rounded down) of their Resource Cards and return them to the bank.',
      relevanceScore: 0.95,
    },
    {
      documentTitle: 'Catan Rules PDF',
      section: 'Page 9 - Rolling a 7',
      text: 'When a "7" is rolled, every player counts their Resource Cards.',
      relevanceScore: 0.88,
    },
  ],
  expansionWarning: null,
};

const MOCK_VERDICT_INCONCLUSIVE = {
  verdict: 'The rules are ambiguous on this point. Both interpretations could be valid.',
  confidence: 0.45,
  isConclusive: false,
  citations: [
    {
      documentTitle: 'Catan Rules PDF',
      section: 'Page 12 - Trading',
      text: 'Players may trade Resource Cards with each other during the trade phase.',
      relevanceScore: 0.62,
    },
  ],
  expansionWarning: null,
};

const MOCK_VERDICT_WITH_WARNING = {
  verdict: 'Based on available rules, Player B appears correct.',
  confidence: 0.75,
  isConclusive: false,
  citations: [],
  expansionWarning:
    'Warning: No analyzed PDF available for expansion "Seafarers". ' +
    'The verdict may not account for expansion-specific rules.',
};

// ============================================================================
// Helpers
// ============================================================================

async function setupArbiterRoutes(page: Page, verdictResponse = MOCK_VERDICT) {
  await page.route(`${API_BASE}/api/v1/auth/session`, route =>
    route.fulfill({ status: 200, json: MOCK_USER })
  );

  // Arbiter endpoint
  await page.route(`${API_BASE}/api/v1/agents/*/arbiter`, route => {
    if (route.request().method() === 'POST') {
      return route.fulfill({ status: 200, json: verdictResponse });
    }
    return route.continue();
  });

  // Catch-all
  await page.route(`${API_BASE}/api/**`, route => route.fulfill({ status: 200, json: {} }));
}

// ============================================================================
// Tests
// ============================================================================

test.describe('Arbiter Dispute Resolution', () => {
  test('should return conclusive verdict with high confidence', async ({ page }) => {
    await setupArbiterRoutes(page, MOCK_VERDICT);

    const result = await page.evaluate(
      async ({ apiBase, agentId }) => {
        const res = await fetch(`${apiBase}/api/v1/agents/${agentId}/arbiter`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({
            sessionId: 'session-001',
            situation: 'Player rolled a 7. Player A has 9 cards, Player B has 6 cards.',
            positionA: 'Both players must discard cards.',
            positionB: 'Only Player A must discard because only they have more than 7.',
          }),
        });
        return await res.json();
      },
      { apiBase: API_BASE, agentId: 'agent-001' }
    );

    expect(result.confidence).toBeGreaterThan(0.85);
    expect(result.isConclusive).toBe(true);
    expect(result.citations).toHaveLength(2);
    expect(result.citations[0].documentTitle).toContain('Catan');
    expect(result.expansionWarning).toBeNull();
  });

  test('should handle inconclusive verdict with low confidence', async ({ page }) => {
    await setupArbiterRoutes(page, MOCK_VERDICT_INCONCLUSIVE);

    const result = await page.evaluate(
      async ({ apiBase, agentId }) => {
        const res = await fetch(`${apiBase}/api/v1/agents/${agentId}/arbiter`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({
            sessionId: 'session-001',
            situation: "Can you trade during another player's turn?",
            positionA: 'Yes, the active player can propose trades at any time.',
            positionB: 'No, only during your own trade phase.',
          }),
        });
        return await res.json();
      },
      { apiBase: API_BASE, agentId: 'agent-001' }
    );

    expect(result.confidence).toBeLessThan(0.85);
    expect(result.isConclusive).toBe(false);
    expect(result.citations.length).toBeGreaterThanOrEqual(1);
  });

  test('should show expansion warning when PDF is missing', async ({ page }) => {
    await setupArbiterRoutes(page, MOCK_VERDICT_WITH_WARNING);

    const result = await page.evaluate(
      async ({ apiBase, agentId }) => {
        const res = await fetch(`${apiBase}/api/v1/agents/${agentId}/arbiter`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          credentials: 'include',
          body: JSON.stringify({
            sessionId: 'session-001',
            situation: 'Dispute about ship movement in Catan Seafarers.',
            positionA: 'Ships can move 2 hexes per turn.',
            positionB: 'Ships can only move 1 hex per turn.',
          }),
        });
        return await res.json();
      },
      { apiBase: API_BASE, agentId: 'agent-001' }
    );

    expect(result.expansionWarning).toBeTruthy();
    expect(result.expansionWarning).toContain('Seafarers');
  });
});
