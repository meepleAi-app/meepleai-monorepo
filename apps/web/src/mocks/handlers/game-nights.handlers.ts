/**
 * MSW handlers for game night endpoints (browser-safe)
 * Covers: /api/v1/game-nights/*
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface GameNight {
  id: string;
  title: string;
  date: string;
  location?: string;
  status: 'Planned' | 'InProgress' | 'Completed' | 'Cancelled';
  organizerId: string;
  participants: Array<{
    id: string;
    userId: string;
    displayName: string;
    rsvpStatus: 'Pending' | 'Accepted' | 'Declined';
  }>;
  playlist: Array<{ id: string; gameId: string; gameName: string; order: number }>;
  createdAt: string;
}

const gameNights: GameNight[] = [
  {
    id: 'gn-1',
    title: 'Friday Game Night',
    date: '2026-04-11T19:00:00Z',
    location: 'Casa di Alice',
    status: 'Planned',
    organizerId: 'user-1',
    participants: [
      { id: 'gnp-1', userId: 'user-1', displayName: 'Alice', rsvpStatus: 'Accepted' },
      { id: 'gnp-2', userId: 'user-2', displayName: 'Bob', rsvpStatus: 'Pending' },
    ],
    playlist: [
      { id: 'pl-1', gameId: 'catan-1', gameName: 'Catan', order: 1 },
      { id: 'pl-2', gameId: 'wingspan-1', gameName: 'Wingspan', order: 2 },
    ],
    createdAt: '2026-04-01T10:00:00Z',
  },
];

export const gameNightsHandlers = [
  http.get(`${API_BASE}/api/v1/game-nights`, () => {
    return HttpResponse.json({
      items: gameNights,
      totalCount: gameNights.length,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  }),

  http.get(`${API_BASE}/api/v1/game-nights/:id`, ({ params }) => {
    const night = gameNights.find(n => n.id === params.id);
    if (!night) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(night);
  }),

  http.post(`${API_BASE}/api/v1/game-nights`, async ({ request }) => {
    const body = (await request.json()) as { title: string; date: string; location?: string };
    const newNight: GameNight = {
      id: `gn-${Date.now()}`,
      title: body.title,
      date: body.date,
      location: body.location,
      status: 'Planned',
      organizerId: 'user-1',
      participants: [],
      playlist: [],
      createdAt: new Date().toISOString(),
    };
    gameNights.push(newNight);
    return HttpResponse.json(newNight, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/game-nights/:id`, async ({ params, request }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as Partial<GameNight>;
    gameNights[idx] = { ...gameNights[idx], ...body };
    return HttpResponse.json(gameNights[idx]);
  }),

  http.delete(`${API_BASE}/api/v1/game-nights/:id`, ({ params }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    gameNights.splice(idx, 1);
    return HttpResponse.json({ success: true });
  }),

  http.post(`${API_BASE}/api/v1/game-nights/:id/invite`, async ({ params, request }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as { userId: string; displayName: string };
    const participant = {
      id: `gnp-${Date.now()}`,
      userId: body.userId,
      displayName: body.displayName,
      rsvpStatus: 'Pending' as const,
    };
    gameNights[idx].participants.push(participant);
    return HttpResponse.json(participant, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/game-nights/:id/rsvp`, async ({ params, request }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as { status: 'Accepted' | 'Declined' };
    const pIdx = gameNights[idx].participants.findIndex(p => p.userId === 'user-1');
    if (pIdx !== -1) gameNights[idx].participants[pIdx].rsvpStatus = body.status;
    return HttpResponse.json(gameNights[idx]);
  }),

  http.post(`${API_BASE}/api/v1/game-nights/:id/playlist`, async ({ params, request }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as { gameId: string; gameName: string };
    const item = {
      id: `pl-${Date.now()}`,
      gameId: body.gameId,
      gameName: body.gameName,
      order: gameNights[idx].playlist.length + 1,
    };
    gameNights[idx].playlist.push(item);
    return HttpResponse.json(item, { status: 201 });
  }),
];

// Helper to reset game night state between tests
export const resetGameNightsState = () => {
  gameNights.splice(0, gameNights.length, {
    id: 'gn-1',
    title: 'Friday Game Night',
    date: '2026-04-11T19:00:00Z',
    location: 'Casa di Alice',
    status: 'Planned',
    organizerId: 'user-1',
    participants: [
      { id: 'gnp-1', userId: 'user-1', displayName: 'Alice', rsvpStatus: 'Accepted' },
      { id: 'gnp-2', userId: 'user-2', displayName: 'Bob', rsvpStatus: 'Pending' },
    ],
    playlist: [
      { id: 'pl-1', gameId: 'catan-1', gameName: 'Catan', order: 1 },
      { id: 'pl-2', gameId: 'wingspan-1', gameName: 'Wingspan', order: 2 },
    ],
    createdAt: '2026-04-01T10:00:00Z',
  });
};
