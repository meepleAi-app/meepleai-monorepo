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
  TEST_PLAYERS,
} from './helpers/improvvisata-helpers';

import type { APIRequestContext } from '@playwright/test';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

test.describe('Improvvisata Save/Resume — Integration', () => {
  test.describe.configure({ mode: 'serial' });
  // Resume may invoke AI agent for recap generation
  test.setTimeout(60_000);

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

  test('save session creates PauseSnapshot', async () => {
    // Setup: create session with players and scores
    sessionId = await createGenericSession(api, 'Save Test Game');
    const [p1, p2] = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);
    await recordScore(api, sessionId, p1, 1, 15);
    await recordScore(api, sessionId, p2, 1, 12);

    // Act: save via improvvisata endpoint
    const saveRes = await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    expect(saveRes.ok(), `Save failed: ${saveRes.status()} ${await saveRes.text()}`).toBe(true);
    const saveBody = await saveRes.json();
    expect(saveBody.snapshotId).toBeTruthy();

    // Verify: session is Paused
    const session = await getSession(api, sessionId);
    expect(session.status).toBe('Paused');

    // Verify: snapshot exists
    const snapshotsRes = await api.get(`/api/v1/sessions/${sessionId}/snapshots`);
    expect(snapshotsRes.ok()).toBe(true);
    const snapshots = await snapshotsRes.json();
    expect(Array.isArray(snapshots)).toBe(true);
    expect(snapshots.length).toBeGreaterThanOrEqual(1);
  });

  test('resume restores state with recap', async () => {
    // Setup: create, start, score, save
    sessionId = await createGenericSession(api, 'Resume Test Game');
    const [p1] = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);
    await recordScore(api, sessionId, p1, 1, 20);

    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });

    // Act: resume via improvvisata endpoint
    const resumeRes = await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);
    expect(resumeRes.ok(), `Resume failed: ${resumeRes.status()} ${await resumeRes.text()}`).toBe(
      true
    );
    const resumeBody = await resumeRes.json();

    // Verify: response contains expected fields
    expect(resumeBody.sessionId).toBeTruthy();
    expect(resumeBody.inviteCode).toBeTruthy();
    expect(resumeBody.shareLink).toBeTruthy();
    // agentRecap may be null if AI service is unavailable — that's OK
    expect('agentRecap' in resumeBody).toBe(true);

    // Verify: session is InProgress again
    const session = await getSession(api, sessionId);
    expect(session.status).toBe('InProgress');

    // Supplementary: check resume-context endpoint
    const ctxRes = await api.get(`/api/v1/live-sessions/${sessionId}/resume-context`);
    if (ctxRes.ok()) {
      const ctx = await ctxRes.json();
      expect(ctx).toBeTruthy();
    }
  });

  test('multiple save/resume cycles preserve data', async () => {
    sessionId = await createGenericSession(api, 'Multi Cycle Test');
    const [p1, p2] = await addPlayersToSession(api, sessionId, TEST_PLAYERS.slice(0, 2));
    await startSession(api, sessionId);

    // Cycle 1: score → save → resume
    await recordScore(api, sessionId, p1, 1, 10);
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    const resume1 = await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);
    expect(resume1.ok()).toBe(true);
    const invite1 = (await resume1.json()).inviteCode;

    // Cycle 2: more scores → save → resume
    await recordScore(api, sessionId, p2, 1, 8);
    await recordScore(api, sessionId, p1, 2, 12);
    await api.post(`/api/v1/game-night/sessions/${sessionId}/save`, {
      data: { finalPhotoIds: [] },
      headers: JSON_HEADERS,
    });
    const resume2 = await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`);
    expect(resume2.ok()).toBe(true);
    const invite2 = (await resume2.json()).inviteCode;

    // Verify: fresh invite each resume
    expect(invite2).not.toBe(invite1);

    // Verify: all 3 scores persist
    const scores = await getScores(api, sessionId);
    expect(scores.length).toBe(3);
  });
});
