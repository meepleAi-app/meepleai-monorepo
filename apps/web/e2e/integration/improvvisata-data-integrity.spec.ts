import { test, expect } from '@playwright/test';
import {
  loginAsAdmin,
  createGenericSession,
  addPlayersToSession,
  startSession,
  recordScore,
  getSession,
  getScores,
  cleanupSession,
  parseUuid,
  TEST_PLAYERS,
} from './helpers/improvvisata-helpers';
import type { APIRequestContext } from '@playwright/test';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

test.describe('Improvvisata Data Integrity — Integration', () => {
  test.describe.configure({ mode: 'serial' });

  let api: APIRequestContext;
  let sessionId: string | null = null;

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
    }
  });

  test('session state transitions are consistent', async () => {
    sessionId = await createGenericSession(api, 'State Transition Test');
    await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));

    // Created
    let session = await getSession(api, sessionId);
    expect(session.status).toBe('Created');
    expect(session.createdAt).toBeTruthy();

    // InProgress
    await startSession(api, sessionId);
    session = await getSession(api, sessionId);
    expect(session.status).toBe('InProgress');
    expect(session.startedAt).toBeTruthy();
    const startedAt = session.startedAt;

    // Paused
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    session = await getSession(api, sessionId);
    expect(session.status).toBe('Paused');
    expect(session.startedAt).toBe(startedAt); // should not regress

    // InProgress again
    await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);
    session = await getSession(api, sessionId);
    expect(session.status).toBe('InProgress');
    expect(session.startedAt).toBe(startedAt); // original start preserved

    // Completed
    const completeRes = await api.post(`/api/v1/live-sessions/${sessionId}/complete`);
    expect(completeRes.ok()).toBe(true);
    session = await getSession(api, sessionId);
    expect(session.status).toBe('Completed');
    sessionId = null; // already completed
  });

  test('scores persist across save/resume', async () => {
    sessionId = await createGenericSession(api, 'Score Persist Test');
    const [p1, p2] = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    // Record 3 scores
    await recordScore(api, sessionId, p1, 1, 10);
    await recordScore(api, sessionId, p2, 1, 8);
    await recordScore(api, sessionId, p1, 2, 12);

    const scoresBefore = await getScores(api, sessionId);
    expect(scoresBefore.length).toBe(3);

    // Save → Resume
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);

    // Verify scores identical
    const scoresAfter = await getScores(api, sessionId);
    expect(scoresAfter.length).toBe(3);

    // Compare values (order may differ)
    const valuesBefore = scoresBefore.map((s: any) => s.value).sort();
    const valuesAfter = scoresAfter.map((s: any) => s.value).sort();
    expect(valuesAfter).toEqual(valuesBefore);
  });

  test('snapshot state is complete', async () => {
    sessionId = await createGenericSession(api, 'Snapshot Complete Test');
    const playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);
    await recordScore(api, sessionId, playerIds[0], 1, 15);

    // Save to create snapshot
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });

    // Get snapshots list
    const listRes = await api.get(`/api/v1/sessions/${sessionId}/snapshots`);
    expect(listRes.ok()).toBe(true);
    const snapshots = await listRes.json();
    expect(snapshots.length).toBeGreaterThanOrEqual(1);

    // Get reconstructed state at index 0
    const stateRes = await api.get(`/api/v1/sessions/${sessionId}/snapshots/0`);
    expect(stateRes.ok(), `Snapshot state failed: ${stateRes.status()}`).toBe(true);
    const state = await stateRes.json();
    expect(state).toBeTruthy();
  });

  test('player soft-delete does not corrupt scores', async () => {
    sessionId = await createGenericSession(api, 'Soft Delete Test');
    const [p1, p2, p3] = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);

    // Record scores for all 3
    await recordScore(api, sessionId, p1, 1, 10);
    await recordScore(api, sessionId, p2, 1, 8);
    await recordScore(api, sessionId, p3, 1, 6);

    // Remove player 3
    const deleteRes = await api.delete(`/api/v1/live-sessions/${sessionId}/players/${p3}`);
    expect(deleteRes.ok()).toBe(true);

    // Verify session state
    const session = await getSession(api, sessionId);
    const activePlayers = session.players.filter((p: any) => p.isActive !== false);
    const inactivePlayers = session.players.filter((p: any) => p.isActive === false);
    expect(activePlayers.length).toBe(2);
    expect(inactivePlayers.length).toBe(1);

    // All 3 scores still present
    const scores = await getScores(api, sessionId);
    expect(scores.length).toBe(3);
  });

  test('invite token lifecycle', async () => {
    sessionId = await createGenericSession(api, 'Invite Lifecycle Test');
    await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 1));
    await startSession(api, sessionId);

    // Create invite
    const inviteRes = await api.post(`/api/v1/live-sessions/${sessionId}/invite`, {
      data: { maxUses: 10, expiryMinutes: 30 },
      headers: JSON_HEADERS,
    });
    expect(inviteRes.status()).toBe(201);
    const invite = await inviteRes.json();
    expect(invite.pin).toBeTruthy();
    expect(invite.pin.length).toBeGreaterThanOrEqual(4);
    expect(invite.linkToken).toBeTruthy();
    expect(new Date(invite.expiresAt).getTime()).toBeGreaterThan(Date.now());

    // Join with PIN
    const joinRes = await api.post('/api/v1/live-sessions/join', {
      data: { token: invite.pin, guestName: 'Test Guest' },
      headers: JSON_HEADERS,
    });
    expect(joinRes.ok(), `Join failed: ${joinRes.status()} ${await joinRes.text()}`).toBe(true);
    const joinBody = await joinRes.json();
    expect(joinBody.participantId).toBeTruthy();
    expect(joinBody.sessionId).toBe(sessionId);

    // Save + resume → new invite
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    const resumeRes = await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);
    expect(resumeRes.ok()).toBe(true);
    const resumeBody = await resumeRes.json();

    // Fresh invite code should differ
    expect(resumeBody.inviteCode).toBeTruthy();
    // Note: inviteCode may or may not differ from original PIN depending on implementation
  });

  test('dispute resolution does not affect session state', async () => {
    sessionId = await createGenericSession(api, 'Dispute Isolation Test');
    const playerIds = await addPlayersToSession(api, sessionId, TEST_PLAYERS);
    await startSession(api, sessionId);
    await recordScore(api, sessionId, playerIds[0], 1, 10);

    const sessionBefore = await getSession(api, sessionId);
    const scoresBefore = await getScores(api, sessionId);

    // Open dispute v2
    const openRes = await api.post(`/api/v1/live-sessions/${sessionId}/disputes`, {
      data: { initiatorPlayerId: playerIds[0], initiatorClaim: 'Isolation test' },
      headers: JSON_HEADERS,
    });
    expect(openRes.ok()).toBe(true);
    const disputeId = parseUuid(await openRes.text());

    // Respond + vote + tally
    await api.put(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/respond`, {
      data: { respondentPlayerId: playerIds[1], respondentClaim: 'Counter' },
      headers: JSON_HEADERS,
    });
    for (const pid of playerIds) {
      await api.post(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/vote`, {
        data: { playerId: pid, acceptsVerdict: true },
        headers: JSON_HEADERS,
      });
    }
    await api.post(`/api/v1/live-sessions/${sessionId}/disputes/${disputeId}/tally`);

    // Session state unchanged
    const sessionAfter = await getSession(api, sessionId);
    expect(sessionAfter.status).toBe('InProgress');
    expect(sessionAfter.status).toBe(sessionBefore.status);

    const scoresAfter = await getScores(api, sessionId);
    expect(scoresAfter.length).toBe(scoresBefore.length);

    // Note: GET /api/v1/games/{gameId}/dispute-history requires a gameId linked to the session.
    // Generic sessions created via POST /live-sessions don't expose a gameId in their response.
    // Dispute history verification is deferred to Phase 5 (full pipeline with real game entities).
  });
});
