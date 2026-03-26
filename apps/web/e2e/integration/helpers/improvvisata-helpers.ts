import { expect, APIRequestContext } from '@playwright/test';

export const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export const ADMIN_EMAIL = process.env.ADMIN_EMAIL || 'admin@meepleai.app';
export const ADMIN_PASSWORD = process.env.ADMIN_PASSWORD || 'DMxspufvkZM3gRHjAHmd';

const JSON_HEADERS = { 'Content-Type': 'application/json' };

/** Parse bare UUID string from API response (strips quotes) */
export function parseUuid(text: string): string {
  const id = text.replace(/"/g, '');
  return id;
}

/** Login as admin and return authenticated API context */
export async function loginAsAdmin(playwright: any): Promise<APIRequestContext> {
  const api = await playwright.request.newContext({ baseURL: API_BASE });
  const loginRes = await api.post('/api/v1/auth/login', {
    data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
    headers: JSON_HEADERS,
  });
  expect(loginRes.ok(), `Admin login failed: ${loginRes.status()}`).toBe(true);
  return api;
}

/** Create a session via generic endpoint. Returns session ID (bare UUID). */
export async function createGenericSession(
  api: APIRequestContext,
  gameName: string,
  options?: { visibility?: string; scoringDimensions?: string[]; agentMode?: string }
): Promise<string> {
  const res = await api.post('/api/v1/live-sessions', {
    data: {
      gameName,
      visibility: options?.visibility ?? 'Private',
      scoringDimensions: options?.scoringDimensions ?? ['points'],
      agentMode: options?.agentMode ?? 'None',
    },
    headers: JSON_HEADERS,
  });
  expect(res.ok(), `Create session failed: ${res.status()} ${await res.text()}`).toBe(true);
  return parseUuid(await res.text());
}

export interface PlayerDef {
  displayName: string;
  color: string;
  role: string;
}

/** Add players to a session. Returns array of player IDs. */
export async function addPlayersToSession(
  api: APIRequestContext,
  sessionId: string,
  players: PlayerDef[]
): Promise<string[]> {
  const ids: string[] = [];
  for (const p of players) {
    const res = await api.post(`/api/v1/live-sessions/${sessionId}/players`, {
      data: { displayName: p.displayName, color: p.color, role: p.role },
      headers: JSON_HEADERS,
    });
    expect(res.ok(), `Add player ${p.displayName} failed: ${res.status()}`).toBe(true);
    ids.push(parseUuid(await res.text()));
  }
  return ids;
}

/** Start a session */
export async function startSession(api: APIRequestContext, sessionId: string): Promise<void> {
  const res = await api.post(`/api/v1/live-sessions/${sessionId}/start`);
  expect(res.ok(), `Start session failed: ${res.status()}`).toBe(true);
}

/** Record a score entry */
export async function recordScore(
  api: APIRequestContext,
  sessionId: string,
  playerId: string,
  round: number,
  value: number,
  dimension = 'points'
): Promise<void> {
  const res = await api.post(`/api/v1/live-sessions/${sessionId}/scores`, {
    data: { playerId, round, dimension, value },
    headers: JSON_HEADERS,
  });
  expect(res.ok(), `Record score failed: ${res.status()}`).toBe(true);
}

/** Get session details */
export async function getSession(api: APIRequestContext, sessionId: string): Promise<any> {
  const res = await api.get(`/api/v1/live-sessions/${sessionId}`);
  expect(res.ok()).toBe(true);
  return res.json();
}

/** Get session scores */
export async function getScores(api: APIRequestContext, sessionId: string): Promise<any[]> {
  const res = await api.get(`/api/v1/live-sessions/${sessionId}/scores`);
  expect(res.ok()).toBe(true);
  return res.json();
}

/** Cleanup: resume (try both paths) then complete session, swallow errors */
export async function cleanupSession(api: APIRequestContext, sessionId: string): Promise<void> {
  // Try improvvisata resume first (for sessions paused via /game-night/sessions/{id}/save)
  await api.post(`/api/v1/game-night/sessions/${sessionId}/resume`).catch(() => {});
  // Fallback: generic resume (for sessions paused via /live-sessions/{id}/pause)
  await api.post(`/api/v1/live-sessions/${sessionId}/resume`).catch(() => {});
  await api.post(`/api/v1/live-sessions/${sessionId}/complete`).catch(() => {});
}

/** Standard test players */
export const TEST_PLAYERS: PlayerDef[] = [
  { displayName: 'Marco', color: 'Red', role: 'Host' },
  { displayName: 'Lucia', color: 'Blue', role: 'Player' },
  { displayName: 'Giovanni', color: 'Green', role: 'Player' },
];
