/**
 * MSW handlers for games endpoints
 *
 * Covers: /api/v1/games/* routes
 * - List games, get game details
 * - Create, update, delete games
 * - Game rules and specifications
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

const API_BASE = HANDLER_BASE;

// In-memory game store for stateful testing
let games = [
  mockChessGame(),
  mockTicTacToeGame(),
  createMockGame({ id: mockId(103), title: 'Monopoly' }),
];

export const gamesHandlers = [
  // GET /api/v1/games - List all games
  http.get(`${API_BASE}/api/v1/games`, () => {
    return HttpResponse.json(games, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // GET /api/v1/games/:id - Get game details
  http.get(`${API_BASE}/api/v1/games/:id`, ({ params }) => {
    const { id } = params;
    const game = games.find(g => g.id === id);

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

    games.push(newGame);

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

    const gameIndex = games.findIndex(g => g.id === id);

    if (gameIndex === -1) {
      return HttpResponse.json({ error: 'Game not found' }, { status: 404 });
    }

    games[gameIndex] = {
      ...games[gameIndex],
      title: body.title,
      updatedAt: new Date().toISOString(),
    };

    return HttpResponse.json(games[gameIndex], {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // DELETE /api/v1/games/:id - Delete game
  http.delete(`${API_BASE}/api/v1/games/:id`, ({ params }) => {
    const { id } = params;
    const gameIndex = games.findIndex(g => g.id === id);

    if (gameIndex === -1) {
      return HttpResponse.json({ error: 'Game not found' }, { status: 404 });
    }

    games.splice(gameIndex, 1);

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

    if (!games.find(g => g.id === id)) {
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

    if (!games.find(g => g.id === id)) {
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

// Helper to reset games state between tests
export const resetGamesState = () => {
  games = [
    mockChessGame(),
    mockTicTacToeGame(),
    createMockGame({ id: mockId(103), title: 'Monopoly' }),
  ];
};
