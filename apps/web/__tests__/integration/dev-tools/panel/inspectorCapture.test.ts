import { describe, it, expect, beforeAll, afterAll, afterEach } from 'vitest';
import { setupServer } from 'msw/node';
import { http, HttpResponse } from 'msw';
import {
  installFetchInterceptor,
  uninstallFetchInterceptor,
} from '@/dev-tools/panel/hooks/useFetchInterceptor';
import { createRequestInspectorStore } from '@/dev-tools/panel/stores/requestInspectorStore';

// Single MSW server instance to avoid "already patched" errors
const server = setupServer(
  // Simulated mock endpoint (adds X-Meeple-Mock header)
  http.get('http://test.local/api/v1/games', () =>
    HttpResponse.json([{ id: 'mock-game-1' }], {
      headers: { 'X-Meeple-Mock': 'msw-games' },
    })
  ),

  // Regular endpoint (no mock header)
  http.get('http://test.local/api/v1/public', () => HttpResponse.json({ ok: true }))
);

describe('Inspector capture integration', () => {
  let store: ReturnType<typeof createRequestInspectorStore>;

  beforeAll(() => {
    server.listen({ onUnhandledRequest: 'bypass' });
    store = createRequestInspectorStore();
    installFetchInterceptor(store);
  });

  afterEach(() => {
    server.resetHandlers();
    store.getState().clear();
  });

  afterAll(() => {
    server.close();
    uninstallFetchInterceptor();
  });

  it('captures fetch with X-Meeple-Mock header and marks isMock=true', async () => {
    await fetch('http://test.local/api/v1/games');

    const entries = store.getState().entries;
    expect(entries).toHaveLength(1);

    const entry = entries[0];
    expect(entry.method).toBe('GET');
    expect(entry.url).toBe('http://test.local/api/v1/games');
    expect(entry.status).toBe(200);
    expect(entry.isMock).toBe(true);
    expect(entry.mockSource).toBe('msw-games');
    expect(entry.durationMs).toBeGreaterThanOrEqual(0);
    expect(entry.id).toBeTruthy();
  });

  it('skips internal requests (X-Meepledev-Internal: 1)', async () => {
    // Simulate a dev-tools internal call (e.g. fetching backend toggles)
    await fetch('http://test.local/api/v1/public', {
      headers: { 'X-Meepledev-Internal': '1' },
    });

    // Nothing should be recorded
    const entries = store.getState().entries;
    expect(entries).toHaveLength(0);
  });
});
