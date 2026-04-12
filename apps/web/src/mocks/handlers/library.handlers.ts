/**
 * MSW handlers for library endpoints (browser-safe)
 * Covers: /api/v1/library/*
 *
 * Response shapes match the real API Zod schemas:
 * - UserLibraryEntrySchema  (library.schemas.ts)
 * - UserLibraryStatsSchema  (library.schemas.ts)
 * - PaginatedLibraryResponseSchema (library.schemas.ts)
 *
 * Data source precedence:
 * 1. Scenario bridge — when installed, derives the library from
 *    `scenario.library.ownedGameIds` + `wishlistGameIds` crossed with
 *    `scenario.games`. Metadata is synthesized.
 * 2. Local fallback — used by unit tests that don't install the bridge.
 */
import { http, HttpResponse } from 'msw';

import { mockId, HANDLER_BASE } from '../data/factories';
import { getScenarioBridge } from '../scenarioBridge';
import { guardScenarioSwitching } from './_shared';

const API_BASE = HANDLER_BASE;

// Matches UserLibraryEntrySchema from library.schemas.ts
interface LibraryItem {
  id: string;
  userId: string;
  gameId: string;
  gameTitle: string;
  gamePublisher: string | null;
  addedAt: string;
  isFavorite: boolean;
  currentState: 'Nuovo' | 'InPrestito' | 'Wishlist' | 'Owned';
  hasKb: boolean;
  kbCardCount: number;
  kbIndexedCount: number;
  kbProcessingCount: number;
  hasRagAccess: boolean;
  agentIsOwned: boolean;
  isPrivateGame: boolean;
  canProposeToCatalog: boolean;
  averageRating?: number | null;
  gameImageUrl?: string | null;
  gameIconUrl?: string | null;
}

const MOCK_USER_ID = mockId(1);

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
      userId: MOCK_USER_ID,
      gameId,
      gameTitle: game?.title ?? 'Unknown',
      gamePublisher: (game?.publisher as string | undefined) ?? null,
      addedAt: new Date().toISOString(),
      isFavorite: false,
      currentState: 'Owned',
      hasKb: false,
      kbCardCount: 0,
      kbIndexedCount: 0,
      kbProcessingCount: 0,
      hasRagAccess: false,
      agentIsOwned: true,
      isPrivateGame: false,
      canProposeToCatalog: false,
      averageRating: typeof game?.averageRating === 'number' ? game.averageRating : null,
    });
  }

  for (const gameId of wishlistGameIds) {
    const game = gamesById.get(gameId);
    items.push({
      id: mockId(++counter),
      userId: MOCK_USER_ID,
      gameId,
      gameTitle: game?.title ?? 'Unknown',
      gamePublisher: (game?.publisher as string | undefined) ?? null,
      addedAt: new Date().toISOString(),
      isFavorite: false,
      currentState: 'Wishlist',
      hasKb: false,
      kbCardCount: 0,
      kbIndexedCount: 0,
      kbProcessingCount: 0,
      hasRagAccess: false,
      agentIsOwned: true,
      isPrivateGame: false,
      canProposeToCatalog: false,
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
    userId: MOCK_USER_ID,
    gameId: mockId(101),
    gameTitle: 'Catan',
    gamePublisher: 'KOSMOS',
    addedAt: '2024-01-15T10:00:00Z',
    isFavorite: true,
    currentState: 'Owned',
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    hasRagAccess: false,
    agentIsOwned: true,
    isPrivateGame: false,
    canProposeToCatalog: false,
    averageRating: 8,
  },
  {
    id: mockId(202),
    userId: MOCK_USER_ID,
    gameId: mockId(102),
    gameTitle: 'Ticket to Ride',
    gamePublisher: 'Days of Wonder',
    addedAt: '2024-02-20T14:00:00Z',
    isFavorite: false,
    currentState: 'Owned',
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    hasRagAccess: false,
    agentIsOwned: true,
    isPrivateGame: false,
    canProposeToCatalog: false,
    averageRating: 9,
  },
  {
    id: mockId(203),
    userId: MOCK_USER_ID,
    gameId: mockId(103),
    gameTitle: 'Wingspan',
    gamePublisher: 'Stonemaier Games',
    addedAt: '2024-06-10T09:00:00Z',
    isFavorite: false,
    currentState: 'Wishlist',
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    hasRagAccess: false,
    agentIsOwned: true,
    isPrivateGame: false,
    canProposeToCatalog: false,
  },
  {
    id: mockId(204),
    userId: MOCK_USER_ID,
    gameId: mockId(104),
    gameTitle: 'Scythe',
    gamePublisher: 'Stonemaier Games',
    addedAt: '2024-03-05T11:00:00Z',
    isFavorite: false,
    currentState: 'Owned',
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    hasRagAccess: false,
    agentIsOwned: true,
    isPrivateGame: false,
    canProposeToCatalog: false,
    averageRating: 9,
  },
];

// ─── Stats must be registered BEFORE /library/:id to avoid matching "stats" as an ID ───

export const libraryHandlers = [
  // GET /api/v1/library/stats — matches UserLibraryStatsSchema
  http.get(`${API_BASE}/api/v1/library/stats`, () => {
    const guard = guardScenarioSwitching();
    if (guard) return guard;
    const items = currentLibrary();
    return HttpResponse.json({
      totalGames: items.length,
      favoriteGames: items.filter(i => i.isFavorite).length,
      privatePdfs: 0,
      nuovoCount: items.filter(i => i.currentState === 'Nuovo').length,
      inPrestitoCount: items.filter(i => i.currentState === 'InPrestito').length,
      wishlistCount: items.filter(i => i.currentState === 'Wishlist').length,
      ownedCount: items.filter(i => i.currentState === 'Owned').length,
    });
  }),

  // GET /api/v1/library — matches PaginatedLibraryResponseSchema
  http.get(`${API_BASE}/api/v1/library`, ({ request }) => {
    const guard = guardScenarioSwitching();
    if (guard) return guard;
    const items = currentLibrary();
    const url = new URL(request.url);
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');
    const totalCount = items.length;
    const totalPages = Math.max(Math.ceil(totalCount / pageSize), 1);
    const start = (page - 1) * pageSize;
    const pageItems = items.slice(start, start + pageSize);
    return HttpResponse.json({
      items: pageItems,
      page,
      pageSize,
      totalCount,
      totalPages,
      hasNextPage: page < totalPages,
      hasPreviousPage: page > 1,
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
      notes?: string | null;
      isFavorite?: boolean;
    };
    if (fallbackLibrary.find(i => i.gameId === body.gameId)) {
      return HttpResponse.json({ error: 'Already in library' }, { status: 409 });
    }
    const newItem: LibraryItem = {
      id: mockId(Math.floor(Math.random() * 9000) + 1000),
      userId: MOCK_USER_ID,
      gameId: body.gameId,
      gameTitle: 'Unknown Game',
      gamePublisher: null,
      addedAt: new Date().toISOString(),
      isFavorite: body.isFavorite ?? false,
      currentState: 'Owned',
      hasKb: false,
      kbCardCount: 0,
      kbIndexedCount: 0,
      kbProcessingCount: 0,
      hasRagAccess: false,
      agentIsOwned: true,
      isPrivateGame: false,
      canProposeToCatalog: false,
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
];

// Helper to reset state between tests
export const resetLibraryState = () => {
  fallbackLibrary = [
    {
      id: mockId(201),
      userId: MOCK_USER_ID,
      gameId: mockId(101),
      gameTitle: 'Catan',
      gamePublisher: 'KOSMOS',
      addedAt: '2024-01-15T10:00:00Z',
      isFavorite: true,
      currentState: 'Owned',
      hasKb: false,
      kbCardCount: 0,
      kbIndexedCount: 0,
      kbProcessingCount: 0,
      hasRagAccess: false,
      agentIsOwned: true,
      isPrivateGame: false,
      canProposeToCatalog: false,
      averageRating: 8,
    },
    {
      id: mockId(202),
      userId: MOCK_USER_ID,
      gameId: mockId(102),
      gameTitle: 'Ticket to Ride',
      gamePublisher: 'Days of Wonder',
      addedAt: '2024-02-20T14:00:00Z',
      isFavorite: false,
      currentState: 'Owned',
      hasKb: false,
      kbCardCount: 0,
      kbIndexedCount: 0,
      kbProcessingCount: 0,
      hasRagAccess: false,
      agentIsOwned: true,
      isPrivateGame: false,
      canProposeToCatalog: false,
      averageRating: 9,
    },
    {
      id: mockId(203),
      userId: MOCK_USER_ID,
      gameId: mockId(103),
      gameTitle: 'Wingspan',
      gamePublisher: 'Stonemaier Games',
      addedAt: '2024-06-10T09:00:00Z',
      isFavorite: false,
      currentState: 'Wishlist',
      hasKb: false,
      kbCardCount: 0,
      kbIndexedCount: 0,
      kbProcessingCount: 0,
      hasRagAccess: false,
      agentIsOwned: true,
      isPrivateGame: false,
      canProposeToCatalog: false,
    },
    {
      id: mockId(204),
      userId: MOCK_USER_ID,
      gameId: mockId(104),
      gameTitle: 'Scythe',
      gamePublisher: 'Stonemaier Games',
      addedAt: '2024-03-05T11:00:00Z',
      isFavorite: false,
      currentState: 'Owned',
      hasKb: false,
      kbCardCount: 0,
      kbIndexedCount: 0,
      kbProcessingCount: 0,
      hasRagAccess: false,
      agentIsOwned: true,
      isPrivateGame: false,
      canProposeToCatalog: false,
      averageRating: 9,
    },
  ];
};
