/**
 * MSW handlers for library endpoints
 *
 * Covers: /api/v1/library/* routes
 * - List user library games
 * - Add/remove games from library
 * - Update game status (owned, wishlist, played)
 *
 * Issue #2760: MSW Infrastructure Setup
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// In-memory library store for stateful testing
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
  // GET /api/v1/library - List user library
  http.get(`${API_BASE}/api/v1/library`, ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');
    const sortBy = url.searchParams.get('sortBy') || 'addedAt';
    const sortOrder = url.searchParams.get('sortOrder') || 'desc';

    let filtered = [...libraryItems];

    // Filter by status
    if (status) {
      filtered = filtered.filter((item) => item.status === status);
    }

    // Sort
    filtered.sort((a, b) => {
      const aVal = a[sortBy as keyof LibraryItem] || '';
      const bVal = b[sortBy as keyof LibraryItem] || '';
      const comparison = String(aVal).localeCompare(String(bVal));
      return sortOrder === 'desc' ? -comparison : comparison;
    });

    // Paginate
    const start = (page - 1) * pageSize;
    const paginatedItems = filtered.slice(start, start + pageSize);

    return HttpResponse.json(
      {
        items: paginatedItems,
        totalCount: filtered.length,
        page,
        pageSize,
        totalPages: Math.ceil(filtered.length / pageSize),
      },
      {
        headers: {
          'X-Correlation-Id': `test-correlation-${Date.now()}`,
        },
      }
    );
  }),

  // GET /api/v1/library/:id - Get library item details
  http.get(`${API_BASE}/api/v1/library/:id`, ({ params }) => {
    const { id } = params;
    const item = libraryItems.find((i) => i.id === id);

    if (!item) {
      return HttpResponse.json({ error: 'Library item not found' }, { status: 404 });
    }

    return HttpResponse.json(item);
  }),

  // POST /api/v1/library - Add game to library
  http.post(`${API_BASE}/api/v1/library`, async ({ request }) => {
    const body = (await request.json()) as {
      gameId: string;
      name: string;
      status: LibraryItem['status'];
    };

    // Check if already in library
    const existing = libraryItems.find((i) => i.gameId === body.gameId);
    if (existing) {
      return HttpResponse.json({ error: 'Game already in library' }, { status: 409 });
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

  // PUT /api/v1/library/:id - Update library item
  http.put(`${API_BASE}/api/v1/library/:id`, async ({ params, request }) => {
    const { id } = params;
    const body = (await request.json()) as Partial<LibraryItem>;

    const index = libraryItems.findIndex((i) => i.id === id);
    if (index === -1) {
      return HttpResponse.json({ error: 'Library item not found' }, { status: 404 });
    }

    libraryItems[index] = {
      ...libraryItems[index],
      ...body,
    };

    return HttpResponse.json(libraryItems[index]);
  }),

  // DELETE /api/v1/library/:id - Remove from library
  http.delete(`${API_BASE}/api/v1/library/:id`, ({ params }) => {
    const { id } = params;
    const index = libraryItems.findIndex((i) => i.id === id);

    if (index === -1) {
      return HttpResponse.json({ error: 'Library item not found' }, { status: 404 });
    }

    libraryItems.splice(index, 1);

    return HttpResponse.json({ success: true });
  }),

  // POST /api/v1/library/:id/play - Log a play
  http.post(`${API_BASE}/api/v1/library/:id/play`, async ({ params }) => {
    const { id } = params;

    const index = libraryItems.findIndex((i) => i.id === id);
    if (index === -1) {
      return HttpResponse.json({ error: 'Library item not found' }, { status: 404 });
    }

    libraryItems[index] = {
      ...libraryItems[index],
      playCount: libraryItems[index].playCount + 1,
      lastPlayedAt: new Date().toISOString(),
    };

    return HttpResponse.json(libraryItems[index]);
  }),

  // GET /api/v1/library/stats - Get library statistics
  http.get(`${API_BASE}/api/v1/library/stats`, () => {
    const stats = {
      totalGames: libraryItems.length,
      owned: libraryItems.filter((i) => i.status === 'owned').length,
      wishlist: libraryItems.filter((i) => i.status === 'wishlist').length,
      played: libraryItems.filter((i) => i.status === 'played').length,
      wantToPlay: libraryItems.filter((i) => i.status === 'want_to_play').length,
      totalPlays: libraryItems.reduce((sum, i) => sum + i.playCount, 0),
      averageRating:
        libraryItems.filter((i) => i.rating).reduce((sum, i) => sum + (i.rating || 0), 0) /
          libraryItems.filter((i) => i.rating).length || 0,
    };

    return HttpResponse.json(stats);
  }),
];

// Helper to reset library state between tests
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

// Helper to add items for testing
export const addLibraryItem = (item: Partial<LibraryItem>) => {
  const newItem: LibraryItem = {
    id: `lib-${Date.now()}`,
    gameId: item.gameId || `game-${Date.now()}`,
    name: item.name || 'Test Game',
    status: item.status || 'owned',
    playCount: item.playCount || 0,
    addedAt: item.addedAt || new Date().toISOString(),
    ...item,
  };
  libraryItems.push(newItem);
  return newItem;
};
