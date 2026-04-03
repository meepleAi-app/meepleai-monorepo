/**
 * MSW handlers for library endpoints (browser-safe)
 * Covers: /api/v1/library/*
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

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

let libraryItems: LibraryItem[] = [
  {
    id: 'lib-1',
    gameId: 'catan-1',
    name: 'Catan',
    status: 'owned',
    rating: 8,
    playCount: 15,
    addedAt: '2024-01-15T10:00:00Z',
    lastPlayedAt: '2024-12-01T18:30:00Z',
  },
  {
    id: 'lib-2',
    gameId: 'ticket-1',
    name: 'Ticket to Ride',
    status: 'owned',
    rating: 9,
    playCount: 22,
    addedAt: '2024-02-20T14:00:00Z',
    lastPlayedAt: '2024-12-15T20:00:00Z',
  },
  {
    id: 'lib-3',
    gameId: 'wingspan-1',
    name: 'Wingspan',
    status: 'wishlist',
    playCount: 0,
    addedAt: '2024-06-10T09:00:00Z',
  },
  {
    id: 'lib-4',
    gameId: 'scythe-1',
    name: 'Scythe',
    status: 'want_to_play',
    playCount: 2,
    addedAt: '2024-03-05T11:00:00Z',
    lastPlayedAt: '2024-08-20T15:00:00Z',
  },
];

export const libraryHandlers = [
  http.get(`${API_BASE}/api/v1/library/stats`, () => {
    return HttpResponse.json({
      totalGames: libraryItems.length,
      owned: libraryItems.filter(i => i.status === 'owned').length,
      wishlist: libraryItems.filter(i => i.status === 'wishlist').length,
      played: libraryItems.filter(i => i.status === 'played').length,
      wantToPlay: libraryItems.filter(i => i.status === 'want_to_play').length,
      totalPlays: libraryItems.reduce((sum, i) => sum + i.playCount, 0),
      averageRating:
        libraryItems.filter(i => i.rating).reduce((sum, i) => sum + (i.rating || 0), 0) /
        (libraryItems.filter(i => i.rating).length || 1),
    });
  }),

  http.get(`${API_BASE}/api/v1/library`, ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');
    const filtered = status ? libraryItems.filter(i => i.status === status) : [...libraryItems];
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
    const item = libraryItems.find(i => i.id === params.id);
    if (!item) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(item);
  }),

  http.post(`${API_BASE}/api/v1/library`, async ({ request }) => {
    const body = (await request.json()) as {
      gameId: string;
      name: string;
      status: LibraryItem['status'];
    };
    if (libraryItems.find(i => i.gameId === body.gameId)) {
      return HttpResponse.json({ error: 'Already in library' }, { status: 409 });
    }
    const newItem: LibraryItem = {
      id: `lib-${Date.now()}`,
      gameId: body.gameId,
      name: body.name,
      status: body.status,
      playCount: 0,
      addedAt: new Date().toISOString(),
    };
    libraryItems.push(newItem);
    return HttpResponse.json(newItem, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/library/:id`, async ({ params, request }) => {
    const idx = libraryItems.findIndex(i => i.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as Partial<LibraryItem>;
    libraryItems[idx] = { ...libraryItems[idx], ...body };
    return HttpResponse.json(libraryItems[idx]);
  }),

  http.delete(`${API_BASE}/api/v1/library/:id`, ({ params }) => {
    const idx = libraryItems.findIndex(i => i.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    libraryItems.splice(idx, 1);
    return HttpResponse.json({ success: true });
  }),

  http.post(`${API_BASE}/api/v1/library/:id/play`, async ({ params }) => {
    const idx = libraryItems.findIndex(i => i.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    libraryItems[idx] = {
      ...libraryItems[idx],
      playCount: libraryItems[idx].playCount + 1,
      lastPlayedAt: new Date().toISOString(),
    };
    return HttpResponse.json(libraryItems[idx]);
  }),
];

// Helper to reset state between tests
export const resetLibraryState = () => {
  libraryItems = [
    {
      id: 'lib-1',
      gameId: 'catan-1',
      name: 'Catan',
      status: 'owned',
      rating: 8,
      playCount: 15,
      addedAt: '2024-01-15T10:00:00Z',
      lastPlayedAt: '2024-12-01T18:30:00Z',
    },
    {
      id: 'lib-2',
      gameId: 'ticket-1',
      name: 'Ticket to Ride',
      status: 'owned',
      rating: 9,
      playCount: 22,
      addedAt: '2024-02-20T14:00:00Z',
      lastPlayedAt: '2024-12-15T20:00:00Z',
    },
    {
      id: 'lib-3',
      gameId: 'wingspan-1',
      name: 'Wingspan',
      status: 'wishlist',
      playCount: 0,
      addedAt: '2024-06-10T09:00:00Z',
    },
    {
      id: 'lib-4',
      gameId: 'scythe-1',
      name: 'Scythe',
      status: 'want_to_play',
      playCount: 2,
      addedAt: '2024-03-05T11:00:00Z',
      lastPlayedAt: '2024-08-20T15:00:00Z',
    },
  ];
};
