import { test, expect } from '@playwright/test';

import {
  loginAsAdmin,
  createGenericSession,
  addPlayersToSession,
  startSession,
  cleanupSession,
  parseUuid,
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

  // --- Dispute v2 (Structured flow) ---

  test('open structured dispute', async () => {
    sessionId = await createGenericSession(api, 'Dispute v2 Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);

    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: {
        initiatorPlayerId: playerIds[0],
        initiatorClaim: 'I claimed the longest road first',
      },
      headers: JSON_HEADERS,
    });
    const openBody = await openRes.text();
    expect(openRes.ok(), `Open dispute failed: ${openRes.status()} ${openBody}`).toBe(true);
    const disputeId = parseUuid(openBody);
    expect(disputeId).toMatch(/^[0-9a-f-]{36}$/);
  });

  test('respondent counter-claim', async () => {
    sessionId = await createGenericSession(api, 'Respond Dispute Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);

    // Open dispute
    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: {
        initiatorPlayerId: playerIds[0],
        initiatorClaim: 'I had longest road',
      },
      headers: JSON_HEADERS,
    });
    expect(openRes.ok()).toBe(true);
    const disputeId = parseUuid(await openRes.text());

    // Respond
    const respondRes = await api.put(
      `/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/respond`,
      {
        data: {
          respondentPlayerId: playerIds[1],
          respondentClaim: 'My road was longer at that point',
        },
        headers: JSON_HEADERS,
      }
    );
    expect(
      respondRes.ok(),
      `Respond failed: ${respondRes.status()} ${await respondRes.text()}`
    ).toBe(true);
  });

  test('cast votes on dispute', async () => {
    sessionId = await createGenericSession(api, 'Vote Dispute Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);

    // Open + respond
    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: { initiatorPlayerId: playerIds[0], initiatorClaim: 'Dispute for voting' },
      headers: JSON_HEADERS,
    });
    const disputeId = parseUuid(await openRes.text());

    await api.put(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/respond`, {
      data: { respondentPlayerId: playerIds[1], respondentClaim: 'Counter claim' },
      headers: JSON_HEADERS,
    });

    // All 3 players vote
    for (const pid of playerIds) {
      const voteRes = await api.post(
        `/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/vote`,
        {
          data: { playerId: pid, acceptsVerdict: pid === playerIds[0] },
          headers: JSON_HEADERS,
        }
      );
      expect(voteRes.ok(), `Vote failed for ${pid}: ${voteRes.status()}`).toBe(true);
    }
  });

  test('tally and resolve dispute', async () => {
    sessionId = await createGenericSession(api, 'Tally Dispute Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);

    // Open + respond + vote
    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: { initiatorPlayerId: playerIds[0], initiatorClaim: 'Tally test claim' },
      headers: JSON_HEADERS,
    });
    const disputeId = parseUuid(await openRes.text());

    await api.put(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/respond`, {
      data: { respondentPlayerId: playerIds[1], respondentClaim: 'Tally counter' },
      headers: JSON_HEADERS,
    });

    for (const pid of playerIds) {
      await api.post(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/vote`, {
        data: { playerId: pid, acceptsVerdict: true },
        headers: JSON_HEADERS,
      });
    }

    // Tally
    const tallyRes = await api.post(
      `/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/tally`
    );
    expect(tallyRes.ok(), `Tally failed: ${tallyRes.status()} ${await tallyRes.text()}`).toBe(true);
  });

  test('timeout fallback when respondent does not reply', async () => {
    sessionId = await createGenericSession(api, 'Timeout Dispute Test');
    playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: { initiatorPlayerId: playerIds[0], initiatorClaim: 'Timeout test' },
      headers: JSON_HEADERS,
    });
    const disputeId = parseUuid(await openRes.text());

    // Timeout without responding
    const timeoutRes = await api.post(
      `/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/timeout`
    );
    expect(
      timeoutRes.ok(),
      `Timeout failed: ${timeoutRes.status()} ${await timeoutRes.text()}`
    ).toBe(true);
  });
});
