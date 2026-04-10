/**
 * MSW handlers for library endpoints (browser-safe)
 * Covers: /api/v1/library/*
 *
 * Data source precedence:
 * 1. Scenario bridge — when installed, derives the library from
 *    `scenario.library.ownedGameIds` + `wishlistGameIds` crossed with
 *    `scenario.games`. Metadata (rating, playCount, addedAt) is synthesized.
 * 2. Local fallback — used by unit tests that don't install the bridge.
 */
import { http, HttpResponse } from 'msw';

import { mockId, HANDLER_BASE } from '../data/factories';
import { getScenarioBridge } from '../scenarioBridge';

const API_BASE = HANDLER_BASE;

interface LibraryItem {
  id: string;
  gameId: string;
  name: string;
  status: 'owned' | 'wishlist' | 'played' | 'want_to_play';
  rating?: number;
  playCount: number;
  notes?: string;
  addedAt: string;
  lastPlayedAt?: string;
}

/**
 * Build library items from scenario state when the bridge is active.
 * Returns null if the bridge is not installed.
 */
function libraryFromScenario(): LibraryItem[] | null {
  const bridge = getScenarioBridge();
  if (!bridge) return null;

  const { ownedGameIds, wishlistGameIds } = bridge.getLibrary();
  const gamesById = new Map(bridge.getGames().map(g => [g.id, g]));
  const items: LibraryItem[] = [];
  let counter = 200;

  for (const gameId of ownedGameIds) {
    const game = gamesById.get(gameId);
    items.push({
      id: mockId(++counter),
      gameId,
      name: game?.title ?? 'Unknown',
      status: 'owned',
      rating: typeof game?.averageRating === 'number' ? Math.round(game.averageRating) : undefined,
      playCount: 0,
      addedAt: new Date().toISOString(),
    });
  }

  for (const gameId of wishlistGameIds) {
    const game = gamesById.get(gameId);
    items.push({
      id: mockId(++counter),
      gameId,
      name: game?.title ?? 'Unknown',
      status: 'wishlist',
      playCount: 0,
      addedAt: new Date().toISOString(),
    });
  }

  return items;
}

function currentLibrary(): LibraryItem[] {
  return libraryFromScenario() ?? fallbackLibrary;
}

let fallbackLibrary: LibraryItem[] = [
  {
    id: mockId(201),
    gameId: mockId(101),
    name: 'Catan',
    status: 'owned',
    rating: 8,
    playCount: 15,
    addedAt: '2024-01-15T10:00:00Z',
    lastPlayedAt: '2024-12-01T18:30:00Z',
  },
  {
    id: mockId(202),
    gameId: mockId(102),
    name: 'Ticket to Ride',
    status: 'owned',
    rating: 9,
    playCount: 22,
    addedAt: '2024-02-20T14:00:00Z',
    lastPlayedAt: '2024-12-15T20:00:00Z',
  },
  {
    id: mockId(203),
    gameId: mockId(103),
    name: 'Wingspan',
    status: 'wishlist',
    playCount: 0,
    addedAt: '2024-06-10T09:00:00Z',
  },
  {
    id: mockId(204),
    gameId: mockId(104),
    name: 'Scythe',
    status: 'want_to_play',
    playCount: 2,
    addedAt: '2024-03-05T11:00:00Z',
    lastPlayedAt: '2024-08-20T15:00:00Z',
  },
];

export const libraryHandlers = [
  http.get(`${API_BASE}/api/v1/library/stats`, () => {
    const items = currentLibrary();
    const ratedItems = items.filter(i => i.rating);
    return HttpResponse.json({
      totalGames: items.length,
      owned: items.filter(i => i.status === 'owned').length,
      wishlist: items.filter(i => i.status === 'wishlist').length,
      played: items.filter(i => i.status === 'played').length,
      wantToPlay: items.filter(i => i.status === 'want_to_play').length,
      totalPlays: items.reduce((sum, i) => sum + i.playCount, 0),
      averageRating:
        ratedItems.reduce((sum, i) => sum + (i.rating || 0), 0) / (ratedItems.length || 1),
    });
  }),

  http.get(`${API_BASE}/api/v1/library`, ({ request }) => {
    const items = currentLibrary();
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');
    const filtered = status ? items.filter(i => i.status === status) : [...items];
    const start = (page - 1) * pageSize;
    return HttpResponse.json({
      items: filtered.slice(start, start + pageSize),
      totalCount: filtered.length,
      page,
      pageSize,
      totalPages: Math.ceil(filtered.length / pageSize),
    });
  }),

  http.get(`${API_BASE}/api/v1/library/:id`, ({ params }) => {
    const item = currentLibrary().find(i => i.id === params.id);
    if (!item) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(item);
  }),

  http.post(`${API_BASE}/api/v1/library`, async ({ request }) => {
    const body = (await request.json()) as {
      gameId: string;
      name: string;
      status: LibraryItem['status'];
    };
    if (fallbackLibrary.find(i => i.gameId === body.gameId)) {
      return HttpResponse.json({ error: 'Already in library' }, { status: 409 });
    }
    const newItem: LibraryItem = {
      id: mockId(Math.floor(Math.random() * 9000) + 1000),
      gameId: body.gameId,
      name: body.name,
      status: body.status,
      playCount: 0,
      addedAt: new Date().toISOString(),
    };
    fallbackLibrary.push(newItem);
    return HttpResponse.json(newItem, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/library/:id`, async ({ params, request }) => {
    const idx = fallbackLibrary.findIndex(i => i.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as Partial<LibraryItem>;
    fallbackLibrary[idx] = { ...fallbackLibrary[idx], ...body };
    return HttpResponse.json(fallbackLibrary[idx]);
  }),

  http.delete(`${API_BASE}/api/v1/library/:id`, ({ params }) => {
    const idx = fallbackLibrary.findIndex(i => i.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    fallbackLibrary.splice(idx, 1);
    return HttpResponse.json({ success: true });
  }),

  http.post(`${API_BASE}/api/v1/library/:id/play`, async ({ params }) => {
    const idx = fallbackLibrary.findIndex(i => i.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    fallbackLibrary[idx] = {
      ...fallbackLibrary[idx],
      playCount: fallbackLibrary[idx].playCount + 1,
      lastPlayedAt: new Date().toISOString(),
    };
    return HttpResponse.json(fallbackLibrary[idx]);
  }),
];

// Helper to reset state between tests
export const resetLibraryState = () => {
  fallbackLibrary = [
    {
      id: mockId(201),
      gameId: mockId(101),
      name: 'Catan',
      status: 'owned',
      rating: 8,
      playCount: 15,
      addedAt: '2024-01-15T10:00:00Z',
      lastPlayedAt: '2024-12-01T18:30:00Z',
    },
    {
      id: mockId(202),
      gameId: mockId(102),
      name: 'Ticket to Ride',
      status: 'owned',
      rating: 9,
      playCount: 22,
      addedAt: '2024-02-20T14:00:00Z',
      lastPlayedAt: '2024-12-15T20:00:00Z',
    },
    {
      id: mockId(203),
      gameId: mockId(103),
      name: 'Wingspan',
      status: 'wishlist',
      playCount: 0,
      addedAt: '2024-06-10T09:00:00Z',
    },
    {
      id: mockId(204),
      gameId: mockId(104),
      name: 'Scythe',
      status: 'want_to_play',
      playCount: 2,
      addedAt: '2024-03-05T11:00:00Z',
      lastPlayedAt: '2024-08-20T15:00:00Z',
    },
  ];
};
