import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest';
import { setupServer } from 'msw/node';
import { http, HttpResponse } from 'msw';
import { buildActiveHandlers, type HandlerGroup } from '@/dev-tools/mswHandlerRegistry';
import { computeGroupToggles } from '@/dev-tools/mockControlCore';

const authGroup: HandlerGroup = {
  name: 'auth',
  handlers: [
    http.get('http://test.local/api/v1/auth/me', () => HttpResponse.json({ id: 'MOCK-u' })),
  ],
};

const gamesGroup: HandlerGroup = {
  name: 'games',
  handlers: [
    http.get('http://test.local/api/v1/games', () => HttpResponse.json([{ id: 'MOCK-g1' }])),
  ],
};

const groups = [authGroup, gamesGroup];

// Single server instance for the whole file — avoids "already patched" XHR error
// when calling listen() more than once in the same process.
// We start with no handlers and use server.use() per test.
const server = setupServer();

describe('MSW group toggle integration', () => {
  beforeAll(() => {
    // Start with bypass so unregistered requests pass through by default
    server.listen({ onUnhandledRequest: 'bypass' });
  });

  afterEach(() => {
    // Reset to the baseline (no handlers) between tests
    server.resetHandlers();
  });

  afterAll(() => {
    server.close();
  });

  it('both groups enabled → both intercepted', async () => {
    const toggles = {
      groups: computeGroupToggles(['auth', 'games'], [], []),
      overrides: {},
    };
    // Register handlers for this test
    server.use(...buildActiveHandlers(groups, toggles));

    const authRes = await fetch('http://test.local/api/v1/auth/me');
    expect(authRes.ok).toBe(true);
    const gamesRes = await fetch('http://test.local/api/v1/games');
    expect(gamesRes.ok).toBe(true);
  });

  it('games disabled → games request fails, auth still works', async () => {
    const toggles = {
      groups: computeGroupToggles(['auth', 'games'], [], ['games']),
      overrides: {},
    };
    // Only auth handler is active; games handler excluded by toggle
    server.use(...buildActiveHandlers(groups, toggles));

    const authRes = await fetch('http://test.local/api/v1/auth/me');
    expect(authRes.ok).toBe(true);

    // games bypasses to network — no real server at test.local → rejects
    await expect(fetch('http://test.local/api/v1/games')).rejects.toThrow();
  });
});
