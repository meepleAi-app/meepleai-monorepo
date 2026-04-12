/**
 * MSW handlers for game night endpoints (browser-safe)
 * Covers: /api/v1/game-nights/*
 *
 * Data shape matches GameNightDtoSchema (game-nights.schemas.ts):
 * id, organizerId, organizerName, title, description, scheduledAt,
 * location, maxPlayers, gameIds, status, acceptedCount, pendingCount,
 * totalInvited, createdAt
 */
import { http, HttpResponse } from 'msw';

import { mockId, HANDLER_BASE } from '../data/factories';

const API_BASE = HANDLER_BASE;

interface GameNightDto {
  id: string;
  organizerId: string;
  organizerName: string;
  title: string;
  description: string | null;
  scheduledAt: string;
  location: string | null;
  maxPlayers: number | null;
  gameIds: string[];
  status: 'Draft' | 'Published' | 'Cancelled' | 'Completed';
  acceptedCount: number;
  pendingCount: number;
  totalInvited: number;
  createdAt: string;
}

const SEED: GameNightDto = {
  id: mockId(801),
  organizerId: mockId(1),
  organizerName: 'Test User',
  title: 'Friday Game Night',
  description: 'Serata di giochi da tavolo',
  scheduledAt: '2026-04-18T19:00:00Z',
  location: 'Casa di Alice',
  maxPlayers: 6,
  gameIds: [mockId(101), mockId(103)],
  status: 'Published',
  acceptedCount: 1,
  pendingCount: 1,
  totalInvited: 2,
  createdAt: '2026-04-01T10:00:00Z',
};

const gameNights: GameNightDto[] = [{ ...SEED }];

export const gameNightsHandlers = [
  // Upcoming game nights (all published)
  http.get(`${API_BASE}/api/v1/game-nights`, () => {
    return HttpResponse.json(gameNights);
  }),

  // My game nights (organized by current user)
  http.get(`${API_BASE}/api/v1/game-nights/mine`, () => {
    return HttpResponse.json(gameNights.filter(n => n.organizerId === mockId(1)));
  }),

  http.get(`${API_BASE}/api/v1/game-nights/:id/rsvps`, ({ params }) => {
    const night = gameNights.find(n => n.id === params.id);
    if (!night) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json([
      {
        id: mockId(901),
        userId: mockId(1),
        userName: 'Test User',
        status: 'Accepted',
        respondedAt: new Date().toISOString(),
        createdAt: night.createdAt,
      },
    ]);
  }),

  http.get(`${API_BASE}/api/v1/game-nights/:id`, ({ params }) => {
    const night = gameNights.find(n => n.id === params.id);
    if (!night) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(night);
  }),

  http.post(`${API_BASE}/api/v1/game-nights`, async ({ request }) => {
    const body = (await request.json()) as Partial<GameNightDto>;
    const newNight: GameNightDto = {
      id: mockId(Math.floor(Math.random() * 9000) + 1000),
      organizerId: mockId(1),
      organizerName: 'Test User',
      title: body.title ?? 'New Game Night',
      description: body.description ?? null,
      scheduledAt: body.scheduledAt ?? new Date().toISOString(),
      location: body.location ?? null,
      maxPlayers: body.maxPlayers ?? null,
      gameIds: body.gameIds ?? [],
      status: 'Draft',
      acceptedCount: 0,
      pendingCount: 0,
      totalInvited: 0,
      createdAt: new Date().toISOString(),
    };
    gameNights.push(newNight);
    return HttpResponse.json(newNight.id, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/game-nights/:id`, async ({ params, request }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as Partial<GameNightDto>;
    gameNights[idx] = { ...gameNights[idx], ...body };
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${API_BASE}/api/v1/game-nights/:id/publish`, ({ params }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    gameNights[idx].status = 'Published';
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${API_BASE}/api/v1/game-nights/:id/cancel`, ({ params }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    gameNights[idx].status = 'Cancelled';
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${API_BASE}/api/v1/game-nights/:id/invite`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${API_BASE}/api/v1/game-nights/:id/rsvp`, async ({ params, request }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as { response: string };
    if (body.response === 'Accepted') gameNights[idx].acceptedCount++;
    return new HttpResponse(null, { status: 204 });
  }),
];

export const resetGameNightsState = () => {
  gameNights.splice(0, gameNights.length, { ...SEED });
};
