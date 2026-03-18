import { test, expect, APIRequestContext } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const ADMIN_EMAIL = process.env.ADMIN_EMAIL || 'admin@meepleai.app';
const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'DMxspufvkZM3gRHjAHmd';

async function createSession(api: APIRequestContext, gameName: string): Promise<string> {
  const res = await api.post('/api/v1/live-sessions', {
    data: { gameName, visibility: 'Private', scoringDimensions: ['points'], agentMode: 'None' },
    headers: { 'Content-Type': 'application/json' },
  });
  expect(res.ok()).toBe(true);
  return (await res.text()).replace(/"/g, '');
}

test.describe('Game Night Invite & Join — Integration', () => {
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

  test('should create session and get session code', async () => {
    sessionId = await createSession(api, 'Invite Test Game');

    const getRes = await api.get(`/api/v1/live-sessions/${sessionId}`);
    expect(getRes.ok()).toBe(true);
    const session = await getRes.json();
    expect(session.sessionCode).toBeTruthy();
    expect(typeof session.sessionCode).toBe('string');
    expect(session.sessionCode.length).toBeGreaterThanOrEqual(4);
  });

  test('should look up session by code', async () => {
    sessionId = await createSession(api, 'Code Lookup Test');

    const getRes = await api.get(`/api/v1/live-sessions/${sessionId}`);
    const session = await getRes.json();

    const lookupRes = await api.get(`/api/v1/live-sessions/code/${session.sessionCode}`);
    expect(lookupRes.ok()).toBe(true);
    const found = await lookupRes.json();
    expect(found.id).toBe(sessionId);
  });

  test('should add players and verify in session', async () => {
    sessionId = await createSession(api, 'Join Test Game');

    const hostRes = await api.post(`/api/v1/live-sessions/${sessionId}/players`, {
      data: { displayName: 'Host Player', color: 'Red', role: 'Host' },
      headers: { 'Content-Type': 'application/json' },
    });
    expect(hostRes.ok(), `Host add failed: ${hostRes.status()} ${await hostRes.text()}`).toBe(true);

    const guestRes = await api.post(`/api/v1/live-sessions/${sessionId}/players`, {
      data: { displayName: 'Guest Player', color: 'Blue', role: 'Player' },
      headers: { 'Content-Type': 'application/json' },
    });
    expect(guestRes.ok(), `Guest add failed: ${guestRes.status()} ${await guestRes.text()}`).toBe(
      true
    );

    // Use players endpoint instead of session GET (which may not include players inline)
    const playersRes = await api.get(`/api/v1/live-sessions/${sessionId}/players`);
    if (playersRes.ok()) {
      const players = await playersRes.json();
      expect(Array.isArray(players)).toBe(true);
      const names = players.map((p: { displayName: string }) => p.displayName);
      expect(names).toContain('Host Player');
      expect(names).toContain('Guest Player');
    } else {
      // Fallback: verify via session GET
      const getRes = await api.get(`/api/v1/live-sessions/${sessionId}`);
      expect(getRes.ok()).toBe(true);
      const session = await getRes.json();
      expect(session.players.length).toBeGreaterThanOrEqual(2);
    }
  });

  test('should remove player from session', async () => {
    sessionId = await createSession(api, 'Remove Player Test');

    await api.post(`/api/v1/live-sessions/${sessionId}/players`, {
      data: { displayName: 'Stays', color: 'Red', role: 'Host' },
      headers: { 'Content-Type': 'application/json' },
    });

    const p2Res = await api.post(`/api/v1/live-sessions/${sessionId}/players`, {
      data: { displayName: 'Gets Removed', color: 'Blue', role: 'Player' },
      headers: { 'Content-Type': 'application/json' },
    });
    const player2Id = (await p2Res.text()).replace(/"/g, '');

    const removeRes = await api.delete(`/api/v1/live-sessions/${sessionId}/players/${player2Id}`);
    expect(removeRes.ok()).toBe(true);

    const getRes = await api.get(`/api/v1/live-sessions/${sessionId}`);
    const updated = await getRes.json();
    // Player deletion may be soft-delete (isActive=false) or hard-delete
    const activePlayers = updated.players.filter(
      (p: { isActive?: boolean }) => p.isActive !== false
    );
    const activeNames = activePlayers.map((p: { displayName: string }) => p.displayName);
    expect(activeNames).toContain('Stays');
    expect(activeNames).not.toContain('Gets Removed');
  });
});
