/**
 * MSW handlers for player endpoints (browser-safe)
 * Covers: /api/v1/players/*
 */
import { http, HttpResponse } from 'msw';

import { mockId, HANDLER_BASE } from '../data/factories';

const API_BASE = HANDLER_BASE;

interface Player {
  id: string;
  displayName: string;
  isActive: boolean;
  gamesPlayed: number;
  totalWins: number;
  createdAt: string;
}

const players: Player[] = [
  {
    id: mockId(1001),
    displayName: 'Alice',
    isActive: true,
    gamesPlayed: 15,
    totalWins: 8,
    createdAt: '2024-01-10T10:00:00Z',
  },
  {
    id: mockId(1002),
    displayName: 'Bob',
    isActive: true,
    gamesPlayed: 12,
    totalWins: 5,
    createdAt: '2024-01-15T10:00:00Z',
  },
  {
    id: mockId(1003),
    displayName: 'Carol',
    isActive: true,
    gamesPlayed: 20,
    totalWins: 11,
    createdAt: '2024-02-01T10:00:00Z',
  },
];

export const playersHandlers = [
  http.get(`${API_BASE}/api/v1/players`, () => {
    const active = players.filter(p => p.isActive);
    return HttpResponse.json({
      items: active,
      totalCount: active.length,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    });
  }),

  http.get(`${API_BASE}/api/v1/players/:id`, ({ params }) => {
    const player = players.find(p => p.id === params.id);
    if (!player) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(player);
  }),

  http.post(`${API_BASE}/api/v1/players`, async ({ request }) => {
    const body = (await request.json()) as { displayName: string };
    const newPlayer: Player = {
      id: mockId(Math.floor(Math.random() * 9000) + 1000),
      displayName: body.displayName,
      isActive: true,
      gamesPlayed: 0,
      totalWins: 0,
      createdAt: new Date().toISOString(),
    };
    players.push(newPlayer);
    return HttpResponse.json(newPlayer, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/players/:id`, async ({ params, request }) => {
    const idx = players.findIndex(p => p.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as Partial<Player>;
    players[idx] = { ...players[idx], ...body };
    return HttpResponse.json(players[idx]);
  }),

  http.delete(`${API_BASE}/api/v1/players/:id`, ({ params }) => {
    const idx = players.findIndex(p => p.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    players[idx] = { ...players[idx], isActive: false };
    return HttpResponse.json({ success: true });
  }),
];

// Helper to reset player state between tests
export const resetPlayersState = () => {
  players.splice(
    0,
    players.length,
    {
      id: mockId(1001),
      displayName: 'Alice',
      isActive: true,
      gamesPlayed: 15,
      totalWins: 8,
      createdAt: '2024-01-10T10:00:00Z',
    },
    {
      id: mockId(1002),
      displayName: 'Bob',
      isActive: true,
      gamesPlayed: 12,
      totalWins: 5,
      createdAt: '2024-01-15T10:00:00Z',
    },
    {
      id: mockId(1003),
      displayName: 'Carol',
      isActive: true,
      gamesPlayed: 20,
      totalWins: 11,
      createdAt: '2024-02-01T10:00:00Z',
    }
  );
};
