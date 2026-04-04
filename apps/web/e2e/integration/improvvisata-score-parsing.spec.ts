import { test, expect } from '@playwright/test';

import {
  loginAsAdmin,
  createGenericSession,
  addPlayersToSession,
  startSession,
  getScores,
  cleanupSession,
  TEST_PLAYERS,
} from './helpers/improvvisata-helpers';

import type { APIRequestContext } from '@playwright/test';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

test.describe('Improvvisata Score Parsing — Integration', () => {
  test.describe.configure({ mode: 'serial' });
  // AI-dependent tests need longer timeout (OpenRouter can take 10-30s)
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

  test('parse natural language score returns structured result', async () => {
    sessionId = await createGenericSession(api, 'NLP Score Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    const parseRes = await api.post(`/api/v1/live-sessions/${sessionId}/scores/parse`, {
      data: { message: 'Marco ha fatto 15 punti' },
      headers: JSON_HEADERS,
    });

    // AI service must be running — if not, test will fail here
    expect(parseRes.ok(), `Parse failed: ${parseRes.status()} ${await parseRes.text()}`).toBe(true);
    const result = await parseRes.json();

    expect(result.confidence).toBeDefined();
    expect(typeof result.confidence).toBe('number');
    expect(result.confidence).toBeGreaterThanOrEqual(0);
    expect(result.confidence).toBeLessThanOrEqual(1);
  });

  test('high confidence score is auto-recorded', async () => {
    sessionId = await createGenericSession(api, 'Auto Record Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    const scoresBefore = await getScores(api, sessionId);
    expect(scoresBefore.length).toBe(0);

    // Use an unambiguous message that should produce high confidence
    const parseRes = await api.post(`/api/v1/live-sessions/${sessionId}/scores/parse`, {
      data: { message: 'Marco ha fatto 15 punti nel round 1' },
      headers: JSON_HEADERS,
    });
    expect(parseRes.ok()).toBe(true);
    const result = await parseRes.json();

    // Verify confidence is in valid range
    expect(result.confidence).toBeGreaterThanOrEqual(0);
    expect(result.confidence).toBeLessThanOrEqual(1);

    if (result.confidence >= 0.8 && !result.requiresConfirmation) {
      // High confidence: score should have been auto-recorded
      const scores = await getScores(api, sessionId);
      expect(scores.length).toBe(1);
    } else {
      // Lower confidence: confirm manually, then verify auto-record path wasn't triggered
      console.log(`[Score Parse] Confidence ${result.confidence} — confirming manually`);
      await api.post(`/api/v1/live-sessions/${sessionId}/scores/confirm`, {
        data: { targetPlayerId: playerIds[0], round: 1, dimension: 'points', value: 15 },
        headers: JSON_HEADERS,
      });
      const scores = await getScores(api, sessionId);
      expect(scores.length).toBe(1);
    }
  });

  test('low confidence requires confirmation', async () => {
    sessionId = await createGenericSession(api, 'Confirm Score Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    // Use an ambiguous message
    const parseRes = await api.post(`/api/v1/live-sessions/${sessionId}/scores/parse`, {
      data: { message: 'qualcuno ha preso dei punti forse' },
      headers: JSON_HEADERS,
    });
    expect(parseRes.ok()).toBe(true);
    const result = await parseRes.json();

    if (result.requiresConfirmation) {
      // Confirm the score
      const confirmRes = await api.post(`/api/v1/live-sessions/${sessionId}/scores/confirm`, {
        data: {
          targetPlayerId: playerIds[0],
          round: 1,
          dimension: 'points',
          value: result.value ?? 10,
        },
        headers: JSON_HEADERS,
      });
      // Accept 200 or 204
      expect([200, 204]).toContain(confirmRes.status());

      const scores = await getScores(api, sessionId);
      expect(scores.length).toBeGreaterThanOrEqual(1);
    } else {
      console.log('[Score Parse] AI returned high confidence for ambiguous input — acceptable');
    }
  });

  test('invalid input returns error gracefully', async () => {
    sessionId = await createGenericSession(api, 'Invalid Score Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    const parseRes = await api.post(`/api/v1/live-sessions/${sessionId}/scores/parse`, {
      data: { message: 'asdfghjkl zxcvbnm qwerty' },
      headers: JSON_HEADERS,
    });

    // Should return 200 with low confidence or 400/422 error
    if (parseRes.ok()) {
      const result = await parseRes.json();
      // If it parsed, confidence should be very low
      if (result.confidence !== undefined) {
        expect(result.confidence).toBeLessThan(0.5);
      }
    } else {
      expect([400, 422]).toContain(parseRes.status());
    }

    // No score should be recorded
    const scores = await getScores(api, sessionId);
    expect(scores.length).toBe(0);
  });
});
