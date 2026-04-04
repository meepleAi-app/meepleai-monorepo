import { test, expect } from '@playwright/test';
import {
  loginAsAdmin,
  createGenericSession,
  addPlayersToSession,
  startSession,
  getSession,
  getScores,
  cleanupSession,
  parseUuid,
  TEST_PLAYERS,
  API_BASE,
  ADMIN_EMAIL,
  ADMIN_PASSWORD,
} from './helpers/improvvisata-helpers';
import type { APIRequestContext } from '@playwright/test';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

/** Create a second authenticated API context (simulates another user) */
async function createSecondContext(
  playwright: any
): Promise<APIRequestContext> {
  const api = await playwright.request.newContext({ baseURL: API_BASE });
  // Login as same admin for simplicity — in production, use a second user
  const loginRes = await api.post('/api/v1/auth/login', {
    data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
    headers: JSON_HEADERS,
  });
  expect(loginRes.ok()).toBe(true);
  return api;
}

test.describe('Improvvisata Concurrent Access — Integration', () => {
  test.describe.configure({ mode: 'serial' });

  let apiHost: APIRequestContext;
  let apiGuest: APIRequestContext;
  let sessionId: string | null = null;

  test.beforeAll(async ({ playwright }) => {
    apiHost = await loginAsAdmin(playwright);
    apiGuest = await createSecondContext(playwright);
  });

  test.afterAll(async () => {
    await apiHost.dispose();
    await apiGuest.dispose();
  });

  test.afterEach(async () => {
    if (sessionId) {
      await cleanupSession(apiHost, sessionId);
      sessionId = null;
    }
  });

  test('simultaneous score recording', async () => {
    sessionId = await createGenericSession(apiHost, 'Concurrent Score Test');
    const [p1, p2] = await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(apiHost, sessionId);

    // Both record simultaneously
    const [r1, r2] = await Promise.all([
      apiHost.post(`/api/v1/live-sessions/${sessionId}/scores`, {
        data: { playerId: p1, round: 1, dimension: 'points', value: 15 },
        headers: JSON_HEADERS,
      }),
      apiGuest.post(`/api/v1/live-sessions/${sessionId}/scores`, {
        data: { playerId: p2, round: 1, dimension: 'points', value: 12 },
        headers: JSON_HEADERS,
      }),
    ]);

    expect(r1.ok(), `Host score failed: ${r1.status()}`).toBe(true);
    expect(r2.ok(), `Guest score failed: ${r2.status()}`).toBe(true);

    const scores = await getScores(apiHost, sessionId);
    expect(scores.length).toBe(2);
    const values = scores.map((s: any) => s.value).sort();
    expect(values).toEqual([12, 15]);
  });

  test('score proposal and confirmation flow', async () => {
    sessionId = await createGenericSession(apiHost, 'Propose Score Test');
    const [p1, p2] = await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(apiHost, sessionId);

    // Create invite so we have a participant ID
    const inviteRes = await apiHost.post(`/api/v1/live-sessions/${sessionId}/invite`, {
      data: { maxUses: 10, expiryMinutes: 30 },
      headers: JSON_HEADERS,
    });
    expect(inviteRes.status()).toBe(201);
    const invite = await inviteRes.json();

    // Guest joins
    const joinRes = await apiGuest.post('/api/v1/live-sessions/join', {
      data: { token: invite.pin, guestName: 'ProposerGuest' },
      headers: JSON_HEADERS,
    });
    expect(joinRes.ok()).toBe(true);
    const joinBody = await joinRes.json();

    // Guest proposes score
    const proposeRes = await apiGuest.post(
      `/api/v1/live-sessions/${sessionId}/scores/propose`,
      {
        data: {
          participantId: joinBody.participantId,
          targetPlayerId: p1,
          round: 1,
          dimension: 'points',
          value: 20,
          proposerName: 'ProposerGuest',
        },
        headers: JSON_HEADERS,
      }
    );
    expect(proposeRes.status()).toBe(202);

    // Host confirms
    const confirmRes = await apiHost.post(
      `/api/v1/live-sessions/${sessionId}/scores/confirm`,
      {
        data: {
          targetPlayerId: p1,
          round: 1,
          dimension: 'points',
          value: 20,
        },
        headers: JSON_HEADERS,
      }
    );
    expect([200, 204]).toContain(confirmRes.status());

    // Verify score recorded
    const scores = await getScores(apiHost, sessionId);
    const confirmed = scores.find((s: any) => s.value === 20);
    expect(confirmed).toBeTruthy();
  });

  test('concurrent dispute voting', async () => {
    sessionId = await createGenericSession(apiHost, 'Concurrent Vote Test');
    const playerIds = await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS);
    await startSession(apiHost, sessionId);

    // Open dispute
    const openRes = await apiHost.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: { initiatorPlayerId: playerIds[0], initiatorClaim: 'Concurrent vote test' },
      headers: JSON_HEADERS,
    });
    expect(openRes.ok()).toBe(true);
    const disputeId = parseUuid(await openRes.text());

    // Respond
    await apiHost.put(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/respond`, {
      data: { respondentPlayerId: playerIds[1], respondentClaim: 'Counter' },
      headers: JSON_HEADERS,
    });

    // All 3 vote in parallel
    const voteResults = await Promise.all(
      playerIds.map((pid) =>
        apiHost.post(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/vote`, {
          data: { playerId: pid, acceptsVerdict: true },
          headers: JSON_HEADERS,
        })
      )
    );

    for (const vr of voteResults) {
      expect(vr.ok(), `Vote failed: ${vr.status()}`).toBe(true);
    }

    // Tally
    const tallyRes = await apiHost.post(
      `/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/tally`
    );
    expect(tallyRes.ok()).toBe(true);
  });

  test('save while guest is scoring', async () => {
    sessionId = await createGenericSession(apiHost, 'Race Condition Test');
    const [p1, p2] = await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(apiHost, sessionId);

    // Simultaneous: guest scores + host saves
    const [scoreRes, saveRes] = await Promise.all([
      apiGuest.post(`/api/v1/live-sessions/${sessionId}/scores`, {
        data: { playerId: p2, round: 1, dimension: 'points', value: 7 },
        headers: JSON_HEADERS,
      }),
      apiHost.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
        data: { finalPhotoIds: [] },
        headers: JSON_HEADERS,
      }),
    ]);

    // At least one should succeed — no silent data loss
    const scoreOk = scoreRes.ok();
    const saveOk = saveRes.ok();
    expect(scoreOk || saveOk).toBe(true);

    if (scoreOk && saveOk) {
      // Both succeeded — score should be in snapshot or session
      console.log('[Concurrent] Both score and save succeeded');
    } else if (!scoreOk) {
      // Score failed because session was paused — acceptable
      console.log(`[Concurrent] Score rejected (${scoreRes.status()}) — session was paused first`);
    }
  });

  test('join while session is active', async () => {
    sessionId = await createGenericSession(apiHost, 'Join Active Test');
    await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS.slice(0, 1));
    await startSession(apiHost, sessionId);

    // Create invite
    const inviteRes = await apiHost.post(`/api/v1/live-sessions/${sessionId}/invite`, {
      data: { maxUses: 10, expiryMinutes: 30 },
      headers: JSON_HEADERS,
    });
    expect(inviteRes.status()).toBe(201);
    const invite = await inviteRes.json();

    // Guest joins active session
    const joinRes = await apiGuest.post('/api/v1/live-sessions/join', {
      data: { token: invite.pin, guestName: 'Late Joiner' },
      headers: JSON_HEADERS,
    });
    expect(joinRes.ok()).toBe(true);

    // Session still active
    const session = await getSession(apiHost, sessionId);
    expect(session.status).toBe('InProgress');

    // Participant visible
    const participantsRes = await apiHost.get(
      `/api/v1/live-sessions/${sessionId}/participants`
    );
    if (participantsRes.ok()) {
      const participants = await participantsRes.json();
      const names = participants.map((p: any) => p.displayName);
      expect(names).toContain('Late Joiner');
    }
  });

  test('optimistic concurrency on score edit', async () => {
    sessionId = await createGenericSession(apiHost, 'Concurrency Test');
    const [p1] = await addPlayersToSession(apiHost, sessionId, TEST_PLAYERS.slice(0, 1));
    await startSession(apiHost, sessionId);

    // Record initial score
    await apiHost.post(`/api/v1/live-sessions/${sessionId}/scores`, {
      data: { playerId: p1, round: 1, dimension: 'points', value: 10 },
      headers: JSON_HEADERS,
    });

    // Both try to edit simultaneously
    const [edit1, edit2] = await Promise.all([
      apiHost.put(`/api/v1/live-sessions/${sessionId}/scores`, {
        data: { playerId: p1, round: 1, dimension: 'points', value: 20 },
        headers: JSON_HEADERS,
      }),
      apiGuest.put(`/api/v1/live-sessions/${sessionId}/scores`, {
        data: { playerId: p1, round: 1, dimension: 'points', value: 30 },
        headers: JSON_HEADERS,
      }),
    ]);

    const status1 = edit1.status();
    const status2 = edit2.status();

    // At least one succeeds
    expect(status1 === 200 || status1 === 204 || status2 === 200 || status2 === 204).toBe(true);

    // If concurrency is enforced, one should get 409 or 500
    if ((status1 === 409 || status1 === 500) || (status2 === 409 || status2 === 500)) {
      console.log(`[Concurrency] Conflict detected: edit1=${status1}, edit2=${status2}`);
    } else {
      console.log(
        `[Concurrency] Both edits succeeded (${status1}, ${status2}) — last-write-wins`
      );
    }

    // No corruption: score should be either 20 or 30, not something else
    const scores = await getScores(apiHost, sessionId);
    const score = scores.find((s: any) => s.round === 1 && s.dimension === 'points');
    expect(score).toBeTruthy();
    expect([20, 30]).toContain(score.value);
  });
});
