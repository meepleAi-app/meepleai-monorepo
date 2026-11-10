import { test, expect } from '@playwright/test';

import {
  loginAsAdmin,
  createGenericSession,
  addPlayersToSession,
  startSession,
  cleanupSession,
  TEST_PLAYERS,
} from './helpers/improvvisata-helpers';

import type { APIRequestContext } from '@playwright/test';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

test.describe('Improvvisata Disputes — Integration', () => {
  test.describe.configure({ mode: 'serial' });
  // AI arbitro test needs longer timeout
  test.setTimeout(60_000);

  let api: APIRequestContext;
  let sessionId: string | null = null;
  let playerIds: string[] = [];

  test.beforeAll(async ({ playwright }) => {
    api = await loginAsAdmin(playwright);
  });

  test.afterAll(async () => {
    await api.dispose();
  });

  test.afterEach(async () => {
    if (sessionId) {
      await cleanupSession(api, sessionId);
      sessionId = null;
      playerIds = [];
    }
  });

  // --- AI Arbitro (Improvvisata-specific) ---

  test('AI arbitro returns verdict for rule dispute', async () => {
    sessionId = await createGenericSession(api, 'Arbitro Test Game');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    const disputeRes = await api.post(`/api/v1/game-night/sessions/${sessionId}/disputes`, {
      data: {
        description: 'Can I build a settlement on an intersection adjacent to another settlement?',
        raisedByPlayerName: 'Marco',
      },
      headers: JSON_HEADERS,
    });
    expect(
      disputeRes.ok(),
      `AI Arbitro failed: ${disputeRes.status()} ${await disputeRes.text()}`
    ).toBe(true);
    const verdict = await disputeRes.json();

    expect(verdict.id).toBeTruthy();
    expect(verdict.verdict).toBeTruthy();
    expect(typeof verdict.verdict).toBe('string');
    expect(Array.isArray(verdict.ruleReferences)).toBe(true);
  });
});
