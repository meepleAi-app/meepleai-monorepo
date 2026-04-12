/**
 * Tests that MSW handlers read from the scenario bridge when it is installed.
 *
 * These tests verify the fix for issue #366: previously the handlers used
 * hardcoded factory data and ignored the scenario, so switching scenarios
 * via `NEXT_PUBLIC_DEV_SCENARIO` was cosmetic.
 *
 * Strategy: install a stub bridge with known data, call the handlers
 * directly, and assert the responses reflect the bridge state (not the
 * hardcoded fallback).
 */
import { HttpRequest, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  clearScenarioBridge,
  setScenarioBridge,
  type ScenarioBridge,
} from '@/mocks/scenarioBridge';

// Import handlers after bridge helpers so modules evaluate cleanly
import { gamesHandlers } from '@/mocks/handlers/games.handlers';
import { libraryHandlers } from '@/mocks/handlers/library.handlers';
import { sessionsHandlers } from '@/mocks/handlers/sessions.handlers';

const BASE = 'http://localhost:8080';

function makeBridge(overrides: Partial<ScenarioBridge> = {}): ScenarioBridge {
  return {
    getGames: () => [],
    getSessions: () => [],
    getChatHistory: () => [],
    getLibrary: () => ({ ownedGameIds: [], wishlistGameIds: [] }),
    getScenarioName: () => 'test-scenario',
    addGame: vi.fn(),
    updateGame: vi.fn(),
    removeGame: vi.fn(),
    toggleOwned: vi.fn(),
    toggleWishlist: vi.fn(),
    ...overrides,
  };
}

/**
 * Invoke an MSW handler directly and return the parsed JSON body.
 */
async function callHandler(
  handlers: ReturnType<typeof gamesHandlers.slice>,
  method: string,
  url: string
): Promise<{ status: number; body: unknown }> {
  const request = new Request(url, { method });
  for (const handler of handlers) {
    const result = await handler.run({ request: request as unknown as HttpRequest });
    if (result?.response) {
      const body = await result.response.clone().json();
      return { status: result.response.status, body };
    }
  }
  throw new Error(`No handler matched ${method} ${url}`);
}

describe('MSW handlers × scenario bridge (issue #366)', () => {
  beforeEach(() => {
    clearScenarioBridge();
  });

  afterEach(() => {
    clearScenarioBridge();
  });

  describe('empty scenario', () => {
    it('GET /api/v1/games returns [] when bridge reports no games', async () => {
      setScenarioBridge(makeBridge({ getScenarioName: () => 'empty' }));
      const res = await callHandler(gamesHandlers, 'GET', `${BASE}/api/v1/games`);
      expect(res.status).toBe(200);
      const body = res.body as { games: unknown[]; total: number };
      expect(body.games).toEqual([]);
      expect(body.total).toBe(0);
    });

    it('GET /api/v1/library returns empty items when bridge library is empty', async () => {
      setScenarioBridge(makeBridge({ getScenarioName: () => 'empty' }));
      const res = await callHandler(libraryHandlers, 'GET', `${BASE}/api/v1/library`);
      expect(res.status).toBe(200);
      const body = res.body as { items: unknown[]; totalCount: number };
      expect(body.items).toEqual([]);
      expect(body.totalCount).toBe(0);
    });

    it('GET /api/v1/sessions returns empty when bridge has no sessions', async () => {
      setScenarioBridge(makeBridge({ getScenarioName: () => 'empty' }));
      const res = await callHandler(sessionsHandlers, 'GET', `${BASE}/api/v1/sessions`);
      expect(res.status).toBe(200);
      const body = res.body as { items: unknown[]; totalCount: number };
      expect(body.items).toEqual([]);
      expect(body.totalCount).toBe(0);
    });
  });

  describe('small-library scenario', () => {
    const smallLibraryBridge: ScenarioBridge = makeBridge({
      getScenarioName: () => 'small-library',
      getGames: () => [
        { id: 'g1', title: 'Wingspan', averageRating: 8.1 },
        { id: 'g2', title: 'Scythe', averageRating: 8.3 },
      ],
      getLibrary: () => ({
        ownedGameIds: ['g1'],
        wishlistGameIds: ['g2'],
      }),
      getSessions: () => [
        {
          id: 'MOCK-00000000-0000-0000-0000-00000000s001',
          gameId: 'g1',
          startedAt: '2026-04-01T18:00:00Z',
        },
      ],
    });

    it('GET /api/v1/games returns bridge games (not hardcoded)', async () => {
      setScenarioBridge(smallLibraryBridge);
      const res = await callHandler(gamesHandlers, 'GET', `${BASE}/api/v1/games`);
      expect(res.status).toBe(200);
      const { games } = res.body as { games: Array<{ id: string; title: string }> };
      expect(games).toHaveLength(2);
      expect(games[0].title).toBe('Wingspan');
      expect(games[1].title).toBe('Scythe');
    });

    it('GET /api/v1/library derives owned + wishlist from scenario', async () => {
      setScenarioBridge(smallLibraryBridge);
      const res = await callHandler(libraryHandlers, 'GET', `${BASE}/api/v1/library`);
      expect(res.status).toBe(200);
      const body = res.body as {
        items: Array<{ gameId: string; gameTitle: string; currentState: string }>;
        totalCount: number;
      };
      expect(body.totalCount).toBe(2);
      expect(body.items.find(i => i.gameId === 'g1')?.currentState).toBe('Owned');
      expect(body.items.find(i => i.gameId === 'g1')?.gameTitle).toBe('Wingspan');
      expect(body.items.find(i => i.gameId === 'g2')?.currentState).toBe('Wishlist');
    });

    it('GET /api/v1/sessions enriches bridge sessions with game name', async () => {
      setScenarioBridge(smallLibraryBridge);
      const res = await callHandler(sessionsHandlers, 'GET', `${BASE}/api/v1/sessions`);
      expect(res.status).toBe(200);
      const body = res.body as {
        items: Array<{ id: string; gameName?: string; status: string }>;
      };
      expect(body.items).toHaveLength(1);
      expect(body.items[0].gameName).toBe('Wingspan');
      expect(body.items[0].status).toBe('Active');
    });
  });

  describe('no bridge installed (fallback)', () => {
    it('GET /api/v1/games returns the hardcoded fallback array', async () => {
      // Bridge NOT installed
      const res = await callHandler(gamesHandlers, 'GET', `${BASE}/api/v1/games`);
      expect(res.status).toBe(200);
      const { games } = res.body as { games: Array<{ title: string }> };
      expect(games.length).toBeGreaterThan(0);
      // Default fallback contains Chess
      expect(games.some(g => g.title === 'Chess')).toBe(true);
    });
  });
});
