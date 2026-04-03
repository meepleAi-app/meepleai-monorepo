/**
 * MSW handlers for session endpoints (browser-safe)
 * Covers: /api/v1/sessions/*
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

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

const sessions: SessionData[] = [
  {
    id: 'session-1',
    sessionCode: 'ABC123',
    gameId: 'demo-chess',
    gameName: 'Chess',
    status: 'Active',
    participants: [
      { id: 'p1', displayName: 'Alice', isOwner: true, totalScore: 0 },
      { id: 'p2', displayName: 'Bob', isOwner: false, totalScore: 0 },
    ],
    notes: [],
    createdAt: '2024-01-15T10:00:00Z',
  },
];

export const sessionsHandlers = [
  http.get(`${API_BASE}/api/v1/sessions`, ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const filtered = status ? sessions.filter(s => s.status === status) : [...sessions];
    return HttpResponse.json({
      items: filtered,
      totalCount: filtered.length,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  }),

  http.get(`${API_BASE}/api/v1/sessions/:id`, ({ params }) => {
    const session = sessions.find(s => s.id === params.id);
    if (!session) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(session);
  }),

  http.post(`${API_BASE}/api/v1/sessions`, async ({ request }) => {
    const body = (await request.json()) as { gameId: string; gameName?: string };
    const newSession: SessionData = {
      id: `session-${Date.now()}`,
      sessionCode: Math.random().toString(36).substring(2, 8).toUpperCase(),
      gameId: body.gameId,
      gameName: body.gameName,
      status: 'Active',
      participants: [],
      notes: [],
      createdAt: new Date().toISOString(),
    };
    sessions.push(newSession);
    return HttpResponse.json(newSession, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/sessions/:id/pause`, ({ params }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    sessions[idx] = { ...sessions[idx], status: 'Paused' };
    return HttpResponse.json(sessions[idx]);
  }),

  http.put(`${API_BASE}/api/v1/sessions/:id/resume`, ({ params }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    sessions[idx] = { ...sessions[idx], status: 'Active' };
    return HttpResponse.json(sessions[idx]);
  }),

  http.put(`${API_BASE}/api/v1/sessions/:id/complete`, ({ params }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    sessions[idx] = {
      ...sessions[idx],
      status: 'Finalized',
      completedAt: new Date().toISOString(),
    };
    return HttpResponse.json(sessions[idx]);
  }),

  http.post(`${API_BASE}/api/v1/sessions/:id/participants`, async ({ params, request }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as { displayName: string };
    const participant = {
      id: `p-${Date.now()}`,
      displayName: body.displayName,
      isOwner: false,
      totalScore: 0,
    };
    sessions[idx].participants.push(participant);
    return HttpResponse.json(participant, { status: 201 });
  }),

  http.delete(
    `${API_BASE}/api/v1/sessions/:sessionId/participants/:participantId`,
    ({ params }) => {
      const idx = sessions.findIndex(s => s.id === params.sessionId);
      if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
      sessions[idx].participants = sessions[idx].participants.filter(
        p => p.id !== params.participantId
      );
      return HttpResponse.json({ success: true });
    }
  ),

  http.post(`${API_BASE}/api/v1/sessions/:id/notes`, async ({ params, request }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as { content: string };
    sessions[idx].notes.push(body.content);
    return HttpResponse.json({ success: true });
  }),

  http.delete(`${API_BASE}/api/v1/sessions/:id`, ({ params }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    sessions.splice(idx, 1);
    return HttpResponse.json({ success: true });
  }),
];

// Helper to reset session state between tests
export const resetSessionsState = () => {
  sessions.splice(0, sessions.length, {
    id: 'session-1',
    sessionCode: 'ABC123',
    gameId: 'demo-chess',
    gameName: 'Chess',
    status: 'Active',
    participants: [
      { id: 'p1', displayName: 'Alice', isOwner: true, totalScore: 0 },
      { id: 'p2', displayName: 'Bob', isOwner: false, totalScore: 0 },
    ],
    notes: [],
    createdAt: '2024-01-15T10:00:00Z',
  });
};
