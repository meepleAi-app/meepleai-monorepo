/**
 * MSW handlers for session endpoints (browser-safe)
 * Covers: /api/v1/sessions/*
 *
 * Data source precedence:
 * 1. Scenario bridge — derives sessions from `scenario.sessions` cross-joined
 *    with `scenario.games` for the `gameName`.
 * 2. Local fallback — used by unit tests without the bridge.
 *
 * NOTE: The scenario session shape is minimal (`{id, gameId, startedAt}`); the
 * API response shape is enriched (sessionCode, participants, etc.). Missing
 * fields are synthesized: sessionCode from `id.slice(-6)`, participants `[]`.
 * CRUD writes currently mutate only the fallback array; when the bridge is
 * active, writes are applied on top of the derived list in-memory per request,
 * which means scenario reload resets any CRUD mutations. This matches the
 * "scenario is the source of truth" contract from issue #366.
 */
import { http, HttpResponse } from 'msw';

import { mockId, HANDLER_BASE } from '../data/factories';
import { getScenarioBridge } from '../scenarioBridge';
import { guardScenarioSwitching } from './_shared';

const API_BASE = HANDLER_BASE;

interface SessionData {
  id: string;
  sessionCode: string;
  gameId: string;
  gameName?: string;
  status: 'Active' | 'Paused' | 'Finalized';
  participants: Array<{ id: string; displayName: string; isOwner: boolean; totalScore: number }>;
  notes: string[];
  createdAt: string;
  completedAt?: string;
}

function sessionsFromScenario(): SessionData[] | null {
  const bridge = getScenarioBridge();
  if (!bridge) return null;
  const gamesById = new Map(bridge.getGames().map(g => [g.id, g]));
  return bridge.getSessions().map(s => ({
    id: s.id,
    sessionCode: s.id.slice(-6).toUpperCase(),
    gameId: s.gameId,
    gameName: gamesById.get(s.gameId)?.title,
    status: 'Active' as const,
    participants: [],
    notes: [],
    createdAt: s.startedAt ?? new Date().toISOString(),
  }));
}

function currentSessions(): SessionData[] {
  return sessionsFromScenario() ?? fallbackSessions;
}

const fallbackSessions: SessionData[] = [
  {
    id: mockId(601),
    sessionCode: 'ABC123',
    gameId: mockId(101),
    gameName: 'Chess',
    status: 'Active',
    participants: [
      { id: mockId(701), displayName: 'Alice', isOwner: true, totalScore: 0 },
      { id: mockId(702), displayName: 'Bob', isOwner: false, totalScore: 0 },
    ],
    notes: [],
    createdAt: '2024-01-15T10:00:00Z',
  },
];

export const sessionsHandlers = [
  http.get(`${API_BASE}/api/v1/sessions`, ({ request }) => {
    const guard = guardScenarioSwitching();
    if (guard) return guard;
    const items = currentSessions();
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const filtered = status ? items.filter(s => s.status === status) : [...items];
    return HttpResponse.json({
      items: filtered,
      totalCount: filtered.length,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  }),

  http.get(`${API_BASE}/api/v1/sessions/:id`, ({ params }) => {
    const session = currentSessions().find(s => s.id === params.id);
    if (!session) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(session);
  }),

  http.post(`${API_BASE}/api/v1/sessions`, async ({ request }) => {
    const body = (await request.json()) as { gameId: string; gameName?: string };
    const newSession: SessionData = {
      id: mockId(Math.floor(Math.random() * 9000) + 1000),
      sessionCode: Math.random().toString(36).substring(2, 8).toUpperCase(),
      gameId: body.gameId,
      gameName: body.gameName,
      status: 'Active',
      participants: [],
      notes: [],
      createdAt: new Date().toISOString(),
    };
    fallbackSessions.push(newSession);
    return HttpResponse.json(newSession, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/sessions/:id/pause`, ({ params }) => {
    const idx = fallbackSessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    fallbackSessions[idx] = { ...fallbackSessions[idx], status: 'Paused' };
    return HttpResponse.json(fallbackSessions[idx]);
  }),

  http.put(`${API_BASE}/api/v1/sessions/:id/resume`, ({ params }) => {
    const idx = fallbackSessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    fallbackSessions[idx] = { ...fallbackSessions[idx], status: 'Active' };
    return HttpResponse.json(fallbackSessions[idx]);
  }),

  http.put(`${API_BASE}/api/v1/sessions/:id/complete`, ({ params }) => {
    const idx = fallbackSessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    fallbackSessions[idx] = {
      ...fallbackSessions[idx],
      status: 'Finalized',
      completedAt: new Date().toISOString(),
    };
    return HttpResponse.json(fallbackSessions[idx]);
  }),

  http.post(`${API_BASE}/api/v1/sessions/:id/participants`, async ({ params, request }) => {
    const idx = fallbackSessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as { displayName: string };
    const participant = {
      id: mockId(Math.floor(Math.random() * 9000) + 1000),
      displayName: body.displayName,
      isOwner: false,
      totalScore: 0,
    };
    fallbackSessions[idx].participants.push(participant);
    return HttpResponse.json(participant, { status: 201 });
  }),

  http.delete(
    `${API_BASE}/api/v1/sessions/:sessionId/participants/:participantId`,
    ({ params }) => {
      const idx = fallbackSessions.findIndex(s => s.id === params.sessionId);
      if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
      fallbackSessions[idx].participants = fallbackSessions[idx].participants.filter(
        p => p.id !== params.participantId
      );
      return HttpResponse.json({ success: true });
    }
  ),

  http.post(`${API_BASE}/api/v1/sessions/:id/notes`, async ({ params, request }) => {
    const idx = fallbackSessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as { content: string };
    fallbackSessions[idx].notes.push(body.content);
    return HttpResponse.json({ success: true });
  }),

  http.delete(`${API_BASE}/api/v1/sessions/:id`, ({ params }) => {
    const idx = fallbackSessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    fallbackSessions.splice(idx, 1);
    return HttpResponse.json({ success: true });
  }),
];

// Helper to reset session state between tests
export const resetSessionsState = () => {
  fallbackSessions.splice(0, fallbackSessions.length, {
    id: mockId(601),
    sessionCode: 'ABC123',
    gameId: mockId(101),
    gameName: 'Chess',
    status: 'Active',
    participants: [
      { id: mockId(701), displayName: 'Alice', isOwner: true, totalScore: 0 },
      { id: mockId(702), displayName: 'Bob', isOwner: false, totalScore: 0 },
    ],
    notes: [],
    createdAt: '2024-01-15T10:00:00Z',
  });
};
