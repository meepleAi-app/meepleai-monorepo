/**
 * MSW handlers for games endpoints
 *
 * Covers: /api/v1/games/* routes
 * - List games, get game details
 * - Create, update, delete games
 * - Game rules and specifications
 *
 * Data source precedence:
 * 1. {@link getScenarioBridge} (runtime scenario store, if dev-tools installed)
 * 2. Local fallback array seeded from factory functions (used by unit tests
 *    that import handlers without the bridge)
 */

import { http, HttpResponse } from 'msw';

import {
  createMockGame,
  createMockRuleSpec,
  mockChessGame,
  mockTicTacToeGame,
  mockId,
  HANDLER_BASE,
} from '../data/factories';
import { getScenarioBridge, type BridgeMockGame } from '../scenarioBridge';
import { guardScenarioSwitching } from './_shared';

const API_BASE = HANDLER_BASE;

// Local fallback store for tests that don't install the bridge.
// When the scenario bridge is active this array is ignored completely.
let fallbackGames: BridgeMockGame[] = [
  mockChessGame(),
  mockTicTacToeGame(),
  createMockGame({ id: mockId(103), title: 'Monopoly' }),
];

function currentGames(): BridgeMockGame[] {
  const bridge = getScenarioBridge();
  return bridge ? bridge.getGames() : fallbackGames;
}

function findGame(id: string): BridgeMockGame | undefined {
  return currentGames().find(g => g.id === id);
}

function addGame(game: BridgeMockGame): void {
  const bridge = getScenarioBridge();
  if (bridge) {
    bridge.addGame(game);
  } else {
    fallbackGames.push(game);
  }
}

function updateGameInStore(id: string, patch: Partial<BridgeMockGame>): BridgeMockGame | null {
  const bridge = getScenarioBridge();
  if (bridge) {
    bridge.updateGame(id, patch);
    return findGame(id) ?? null;
  }
  const idx = fallbackGames.findIndex(g => g.id === id);
  if (idx === -1) return null;
  fallbackGames[idx] = { ...fallbackGames[idx], ...patch };
  return fallbackGames[idx];
}

function removeGameFromStore(id: string): boolean {
  const bridge = getScenarioBridge();
  if (bridge) {
    if (!findGame(id)) return false;
    bridge.removeGame(id);
    return true;
  }
  const idx = fallbackGames.findIndex(g => g.id === id);
  if (idx === -1) return false;
  fallbackGames.splice(idx, 1);
  return true;
}

export const gamesHandlers = [
  // GET /api/v1/games - List all games
  // Returns PaginatedGamesResponse: { games, total, page, pageSize, totalPages }
  http.get(`${API_BASE}/api/v1/games`, () => {
    const guard = guardScenarioSwitching();
    if (guard) return guard;
    const games = currentGames();
    return HttpResponse.json(
      {
        games,
        total: games.length,
        page: 1,
        pageSize: Math.max(games.length, 1),
        totalPages: 1,
      },
      {
        headers: {
          'X-Correlation-Id': `test-correlation-${Date.now()}`,
        },
      }
    );
  }),

  // GET /api/v1/games/:id - Get game details
  http.get(`${API_BASE}/api/v1/games/:id`, ({ params }) => {
    const { id } = params;
    const game = findGame(id as string);

    if (!game) {
      return HttpResponse.json({ error: 'Game not found' }, { status: 404 });
    }

    return HttpResponse.json(game, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/games - Create new game
  http.post(`${API_BASE}/api/v1/games`, async ({ request }) => {
    const body = (await request.json()) as { title: string };

    const newGame = createMockGame({
      id: mockId(Math.floor(Math.random() * 9000) + 1000),
      title: body.title,
    });

    addGame(newGame);

    return HttpResponse.json(newGame, {
      status: 201,
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // PUT /api/v1/games/:id - Update game
  http.put(`${API_BASE}/api/v1/games/:id`, async ({ params, request }) => {
    const { id } = params;
    const body = (await request.json()) as { title: string };

    const updated = updateGameInStore(id as string, {
      title: body.title,
      updatedAt: new Date().toISOString(),
    });

    if (!updated) {
      return HttpResponse.json({ error: 'Game not found' }, { status: 404 });
    }

    return HttpResponse.json(updated, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // DELETE /api/v1/games/:id - Delete game
  http.delete(`${API_BASE}/api/v1/games/:id`, ({ params }) => {
    const { id } = params;
    const removed = removeGameFromStore(id as string);

    if (!removed) {
      return HttpResponse.json({ error: 'Game not found' }, { status: 404 });
    }

    return HttpResponse.json(
      { success: true },
      {
        headers: {
          'X-Correlation-Id': `test-correlation-${Date.now()}`,
        },
      }
    );
  }),

  // GET /api/v1/games/:id/rules - Get game rules
  http.get(`${API_BASE}/api/v1/games/:id/rules`, ({ params }) => {
    const { id } = params;

    if (!findGame(id as string)) {
      return HttpResponse.json({ error: 'Game not found' }, { status: 404 });
    }

    const ruleSpec = createMockRuleSpec({ gameId: id as string });

    return HttpResponse.json(ruleSpec, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/games/:id/rules - Create/update game rules
  http.post(`${API_BASE}/api/v1/games/:id/rules`, async ({ params, request }) => {
    const { id } = params;
    const body = (await request.json()) as Record<string, unknown>;

    if (!findGame(id as string)) {
      return HttpResponse.json({ error: 'Game not found' }, { status: 404 });
    }

    const ruleSpec = createMockRuleSpec({
      gameId: id as string,
      ...(body as object),
    });

    return HttpResponse.json(ruleSpec, {
      status: 201,
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),
];

// Helper to reset games state between tests (only resets the fallback array;
// when the bridge is active, callers should reload the scenario instead)
export const resetGamesState = () => {
  fallbackGames = [
    mockChessGame(),
    mockTicTacToeGame(),
    createMockGame({ id: mockId(103), title: 'Monopoly' }),
  ];
};
