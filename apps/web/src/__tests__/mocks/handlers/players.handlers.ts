/**
 * MSW handlers for player endpoints
 *
 * Covers: /api/v1/players/* routes
 * - Player CRUD: create, read, update, soft-delete
 * - Player stats and history
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface Player {
  id: string;
  displayName: string;
  isActive: boolean;
  gamesPlayed: number;
  totalWins: number;
  createdAt: string;
}

let players: Player[] = [
  {
    id: 'player-1',
    displayName: 'Alice',
    isActive: true,
    gamesPlayed: 15,
    totalWins: 8,
    createdAt: '2024-01-10T10:00:00Z',
  },
  {
    id: 'player-2',
    displayName: 'Bob',
    isActive: true,
    gamesPlayed: 12,
    totalWins: 5,
    createdAt: '2024-01-15T10:00:00Z',
  },
];

export const playersHandlers = [
  // GET /api/v1/players
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

  // GET /api/v1/players/:id
  http.get(`${API_BASE}/api/v1/players/:id`, ({ params }) => {
    const player = players.find(p => p.id === params.id);
    if (!player) {
      return HttpResponse.json({ error: 'Player not found' }, { status: 404 });
    }
    return HttpResponse.json(player);
  }),

  // POST /api/v1/players - Create
  http.post(`${API_BASE}/api/v1/players`, async ({ request }) => {
    const body = (await request.json()) as { displayName: string };
    const newPlayer: Player = {
      id: `player-${Date.now()}`,
      displayName: body.displayName,
      isActive: true,
      gamesPlayed: 0,
      totalWins: 0,
      createdAt: new Date().toISOString(),
    };
    players.push(newPlayer);
    return HttpResponse.json(newPlayer, { status: 201 });
  }),

  // PUT /api/v1/players/:id - Update
  http.put(`${API_BASE}/api/v1/players/:id`, async ({ params, request }) => {
    const body = (await request.json()) as Partial<Player>;
    const index = players.findIndex(p => p.id === params.id);
    if (index === -1) {
      return HttpResponse.json({ error: 'Player not found' }, { status: 404 });
    }
    players[index] = { ...players[index], ...body };
    return HttpResponse.json(players[index]);
  }),

  // DELETE /api/v1/players/:id - Soft-delete
  http.delete(`${API_BASE}/api/v1/players/:id`, ({ params }) => {
    const index = players.findIndex(p => p.id === params.id);
    if (index === -1) {
      return HttpResponse.json({ error: 'Player not found' }, { status: 404 });
    }
    players[index].isActive = false;
    return HttpResponse.json({ success: true });
  }),
];

export const resetPlayersState = () => {
  players = [
    {
      id: 'player-1',
      displayName: 'Alice',
      isActive: true,
      gamesPlayed: 15,
      totalWins: 8,
      createdAt: '2024-01-10T10:00:00Z',
    },
    {
      id: 'player-2',
      displayName: 'Bob',
      isActive: true,
      gamesPlayed: 12,
      totalWins: 5,
      createdAt: '2024-01-15T10:00:00Z',
    },
  ];
};

export const getPlayersState = () => players;
