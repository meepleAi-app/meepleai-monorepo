/**
 * MSW handlers for session endpoints
 *
 * Covers: /api/v1/sessions/* routes
 * - Session lifecycle: create, start, pause, resume, complete, archive
 * - Participants: add, remove
 * - Scoring: record, propose, confirm
 * - Tools: dice, cards, timer, notes
 */

import { http, HttpResponse } from 'msw';
import { createMockSession, createMockSessionParticipant } from '../../fixtures/common-fixtures';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// In-memory session store
interface SessionData {
  id: string;
  sessionCode: string;
  gameId: string;
  gameName?: string;
  status: 'Active' | 'Paused' | 'Finalized';
  participants: Array<{
    id: string;
    displayName: string;
    isOwner: boolean;
    totalScore: number;
  }>;
  notes: string[];
  createdAt: string;
  completedAt?: string;
}

let sessions: SessionData[] = [
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
  // GET /api/v1/sessions - List sessions
  http.get(`${API_BASE}/api/v1/sessions`, ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');

    let filtered = [...sessions];
    if (status) {
      filtered = filtered.filter(s => s.status === status);
    }

    return HttpResponse.json({
      items: filtered,
      totalCount: filtered.length,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  }),

  // GET /api/v1/sessions/:id - Get session detail
  http.get(`${API_BASE}/api/v1/sessions/:id`, ({ params }) => {
    const session = sessions.find(s => s.id === params.id);
    if (!session) {
      return HttpResponse.json({ error: 'Session not found' }, { status: 404 });
    }
    return HttpResponse.json(session);
  }),

  // POST /api/v1/sessions - Create session
  http.post(`${API_BASE}/api/v1/sessions`, async ({ request }) => {
    const body = (await request.json()) as {
      gameId: string;
      gameName?: string;
      participants?: string[];
    };

    const newSession: SessionData = {
      id: `session-${Date.now()}`,
      sessionCode: Math.random().toString(36).substring(2, 8).toUpperCase(),
      gameId: body.gameId,
      gameName: body.gameName,
      status: 'Active',
      participants: [
        { id: 'current-user', displayName: 'Current User', isOwner: true, totalScore: 0 },
      ],
      notes: [],
      createdAt: new Date().toISOString(),
    };

    sessions.push(newSession);
    return HttpResponse.json(newSession, { status: 201 });
  }),

  // POST /api/v1/sessions/:id/join - Join session by code
  http.post(`${API_BASE}/api/v1/sessions/:id/join`, async ({ params }) => {
    const session = sessions.find(s => s.id === params.id || s.sessionCode === params.id);
    if (!session) {
      return HttpResponse.json({ error: 'Session not found' }, { status: 404 });
    }
    const newParticipant = {
      id: `p-${Date.now()}`,
      displayName: 'Joined Player',
      isOwner: false,
      totalScore: 0,
    };
    session.participants.push(newParticipant);
    return HttpResponse.json(session);
  }),

  // POST /api/v1/sessions/:id/participants - Add participant
  http.post(`${API_BASE}/api/v1/sessions/:id/participants`, async ({ params, request }) => {
    const body = (await request.json()) as { playerId: string; displayName: string };
    const session = sessions.find(s => s.id === params.id);
    if (!session) {
      return HttpResponse.json({ error: 'Session not found' }, { status: 404 });
    }
    const newParticipant = {
      id: body.playerId,
      displayName: body.displayName,
      isOwner: false,
      totalScore: 0,
    };
    session.participants.push(newParticipant);
    return HttpResponse.json(newParticipant, { status: 201 });
  }),

  // DELETE /api/v1/sessions/:id/participants/:participantId - Remove participant
  http.delete(`${API_BASE}/api/v1/sessions/:id/participants/:participantId`, ({ params }) => {
    const session = sessions.find(s => s.id === params.id);
    if (!session) {
      return HttpResponse.json({ error: 'Session not found' }, { status: 404 });
    }
    session.participants = session.participants.filter(p => p.id !== params.participantId);
    return HttpResponse.json({ success: true });
  }),

  // POST /api/v1/sessions/:id/scores - Record score
  http.post(`${API_BASE}/api/v1/sessions/:id/scores`, async ({ params, request }) => {
    const body = (await request.json()) as { participantId: string; score: number };
    const session = sessions.find(s => s.id === params.id);
    if (!session) {
      return HttpResponse.json({ error: 'Session not found' }, { status: 404 });
    }
    const participant = session.participants.find(p => p.id === body.participantId);
    if (!participant) {
      return HttpResponse.json({ error: 'Participant not found' }, { status: 404 });
    }
    participant.totalScore += body.score;
    return HttpResponse.json({
      participantId: body.participantId,
      totalScore: participant.totalScore,
    });
  }),

  // POST /api/v1/sessions/:id/pause - Pause session
  http.post(`${API_BASE}/api/v1/sessions/:id/pause`, ({ params }) => {
    const session = sessions.find(s => s.id === params.id);
    if (!session) {
      return HttpResponse.json({ error: 'Session not found' }, { status: 404 });
    }
    session.status = 'Paused';
    return HttpResponse.json(session);
  }),

  // POST /api/v1/sessions/:id/resume - Resume session
  http.post(`${API_BASE}/api/v1/sessions/:id/resume`, ({ params }) => {
    const session = sessions.find(s => s.id === params.id);
    if (!session) {
      return HttpResponse.json({ error: 'Session not found' }, { status: 404 });
    }
    session.status = 'Active';
    return HttpResponse.json(session);
  }),

  // POST /api/v1/sessions/:id/complete - Complete session
  http.post(`${API_BASE}/api/v1/sessions/:id/complete`, async ({ params, request }) => {
    const body = (await request.json()) as { winnerName?: string };
    const session = sessions.find(s => s.id === params.id);
    if (!session) {
      return HttpResponse.json({ error: 'Session not found' }, { status: 404 });
    }
    session.status = 'Finalized';
    session.completedAt = new Date().toISOString();
    return HttpResponse.json(session);
  }),

  // POST /api/v1/sessions/:id/archive - Archive session
  http.post(`${API_BASE}/api/v1/sessions/:id/archive`, ({ params }) => {
    const session = sessions.find(s => s.id === params.id);
    if (!session) {
      return HttpResponse.json({ error: 'Session not found' }, { status: 404 });
    }
    session.status = 'Finalized';
    return HttpResponse.json(session);
  }),

  // POST /api/v1/sessions/:id/notes - Add note
  http.post(`${API_BASE}/api/v1/sessions/:id/notes`, async ({ params, request }) => {
    const body = (await request.json()) as { text: string };
    const session = sessions.find(s => s.id === params.id);
    if (!session) {
      return HttpResponse.json({ error: 'Session not found' }, { status: 404 });
    }
    session.notes.push(body.text);
    return HttpResponse.json({ text: body.text, createdAt: new Date().toISOString() });
  }),

  // POST /api/v1/sessions/:id/dice - Roll dice
  http.post(`${API_BASE}/api/v1/sessions/:id/dice`, async ({ request }) => {
    const body = (await request.json()) as { count?: number; sides?: number };
    const count = body.count || 2;
    const sides = body.sides || 6;
    const results = Array.from({ length: count }, () => Math.floor(Math.random() * sides) + 1);
    return HttpResponse.json({
      results,
      total: results.reduce((a, b) => a + b, 0),
    });
  }),

  // POST /api/v1/sessions/:id/cards/draw - Draw cards
  http.post(`${API_BASE}/api/v1/sessions/:id/cards/draw`, async ({ request }) => {
    const body = (await request.json()) as { count?: number };
    const count = body.count || 1;
    const suits = ['Hearts', 'Diamonds', 'Clubs', 'Spades'];
    const values = ['A', '2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K'];
    const cards = Array.from({ length: count }, () => ({
      suit: suits[Math.floor(Math.random() * suits.length)],
      value: values[Math.floor(Math.random() * values.length)],
    }));
    return HttpResponse.json({ cards });
  }),
];

export const resetSessionsState = () => {
  sessions = [
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
};

export const getSessionsState = () => sessions;
