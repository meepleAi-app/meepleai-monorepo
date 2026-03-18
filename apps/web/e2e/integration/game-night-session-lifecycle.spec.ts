import { test, expect, APIRequestContext } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const ADMIN_EMAIL = process.env.ADMIN_EMAIL || 'admin@meepleai.app';
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'DMxspufvkZM3gRHjAHmd';

/** Create a session and return its ID (API returns bare UUID string) */
async function createSession(api: APIRequestContext, gameName: string): Promise<string> {
  const res = await api.post('/api/v1/live-sessions', {
    data: {
      gameName,
      visibility: 'Private',
      scoringDimensions: ['points'],
      agentMode: 'None',
    },
    headers: { 'Content-Type': 'application/json' },
  });
  expect(res.ok()).toBe(true);
  const body = await res.text();
  // API returns a bare UUID string (with quotes)
  const id = body.replace(/"/g, '');
  expect(id).toMatch(/^[0-9a-f-]{36}$/);
  return id;
}

/** Add a player and return its ID (API returns bare UUID) */
async function addPlayer(
  api: APIRequestContext,
  sessionId: string,
  name: string,
  color: string,
  role: string
): Promise<string> {
  const res = await api.post(`/api/v1/live-sessions/${sessionId}/players`, {
    data: { displayName: name, color, role },
    headers: { 'Content-Type': 'application/json' },
  });
  expect(res.ok()).toBe(true);
  return (await res.text()).replace(/"/g, '');
}

test.describe('Game Night Session Lifecycle — Integration', () => {
  test.describe.configure({ mode: 'serial' });

  let api: APIRequestContext;
  let sessionId: string | null = null;

  test.beforeAll(async ({ playwright }) => {
    api = await playwright.request.newContext({ baseURL: API_BASE });
    const loginRes = await api.post('/api/v1/auth/login', {
      data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
      headers: { 'Content-Type': 'application/json' },
    });
    expect(loginRes.ok()).toBe(true);
  });

  test.afterAll(async () => {
    await api.dispose();
  });

  test.afterEach(async () => {
    if (sessionId) {
      await api.post(`/api/v1/live-sessions/${sessionId}/complete`).catch(() => {});
      sessionId = null;
    }
  });

  test('should create a live session via API', async () => {
    sessionId = await createSession(api, 'Integration Test Game');

    // GET to verify full object
    const getRes = await api.get(`/api/v1/live-sessions/${sessionId}`);
    expect(getRes.ok()).toBe(true);
    const session = await getRes.json();
    expect(session.id).toBe(sessionId);
    expect(session.status).toBe('Created');
    expect(session.gameName).toBe('Integration Test Game');
  });

  test('should add players and start session', async () => {
    sessionId = await createSession(api, 'Player Test Game');
    await addPlayer(api, sessionId, 'Marco', 'Red', 'Host');
    await addPlayer(api, sessionId, 'Lucia', 'Blue', 'Player');

    const startRes = await api.post(`/api/v1/live-sessions/${sessionId}/start`);
    expect(startRes.ok()).toBe(true);

    const getRes = await api.get(`/api/v1/live-sessions/${sessionId}`);
    const updated = await getRes.json();
    expect(updated.status).toBe('InProgress');
    expect(updated.players.length).toBeGreaterThanOrEqual(2);
  });

  test('should record scores and verify totals', async () => {
    sessionId = await createSession(api, 'Score Test Game');
    const player1Id = await addPlayer(api, sessionId, 'Marco', 'Red', 'Host');
    const player2Id = await addPlayer(api, sessionId, 'Lucia', 'Blue', 'Player');
    await api.post(`/api/v1/live-sessions/${sessionId}/start`);

    const s1 = await api.post(`/api/v1/live-sessions/${sessionId}/scores`, {
      data: { playerId: player1Id, round: 1, dimension: 'points', value: 15 },
      headers: { 'Content-Type': 'application/json' },
    });
    expect(s1.ok()).toBe(true);

    const s2 = await api.post(`/api/v1/live-sessions/${sessionId}/scores`, {
      data: { playerId: player2Id, round: 1, dimension: 'points', value: 12 },
      headers: { 'Content-Type': 'application/json' },
    });
    expect(s2.ok()).toBe(true);

    const scoresRes = await api.get(`/api/v1/live-sessions/${sessionId}/scores`);
    expect(scoresRes.ok()).toBe(true);
    const scores = await scoresRes.json();
    expect(Array.isArray(scores)).toBe(true);
    expect(scores.length).toBe(2);
  });

  test('should pause and resume session', async () => {
    sessionId = await createSession(api, 'Pause Resume Test');
    await addPlayer(api, sessionId, 'Marco', 'Red', 'Host');
    await api.post(`/api/v1/live-sessions/${sessionId}/start`);

    const pauseRes = await api.post(`/api/v1/live-sessions/${sessionId}/pause`);
    expect(pauseRes.ok()).toBe(true);

    const afterPause = await api.get(`/api/v1/live-sessions/${sessionId}`);
    expect((await afterPause.json()).status).toBe('Paused');

    const resumeRes = await api.post(`/api/v1/live-sessions/${sessionId}/resume`);
    expect(resumeRes.ok()).toBe(true);

    const afterResume = await api.get(`/api/v1/live-sessions/${sessionId}`);
    expect((await afterResume.json()).status).toBe('InProgress');
  });

  test('should complete full session lifecycle', async () => {
    sessionId = await createSession(api, 'Full Lifecycle Test');
    await addPlayer(api, sessionId, 'Marco', 'Red', 'Host');

    await api.post(`/api/v1/live-sessions/${sessionId}/start`);
    await api.post(`/api/v1/live-sessions/${sessionId}/pause`);
    await api.post(`/api/v1/live-sessions/${sessionId}/resume`);

    const completeRes = await api.post(`/api/v1/live-sessions/${sessionId}/complete`);
    expect(completeRes.ok()).toBe(true);

    const getRes = await api.get(`/api/v1/live-sessions/${sessionId}`);
    expect((await getRes.json()).status).toBe('Completed');
    sessionId = null;
  });
});
