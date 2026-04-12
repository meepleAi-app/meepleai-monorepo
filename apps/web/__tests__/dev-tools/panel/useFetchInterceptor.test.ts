import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
  installFetchInterceptor,
  uninstallFetchInterceptor,
  readInternalHeader,
} from '@/dev-tools/panel/hooks/useFetchInterceptor';
import { createRequestInspectorStore } from '@/dev-tools/panel/stores/requestInspectorStore';

// Helper: make a minimal Response
function makeResponse(status: number, headers: Record<string, string> = {}): Response {
  return new Response(null, { status, headers });
}

describe('useFetchInterceptor', () => {
  let store: ReturnType<typeof createRequestInspectorStore>;
  let realFetch: typeof fetch;

  beforeEach(() => {
    store = createRequestInspectorStore();
    realFetch = window.fetch;
    // Ensure clean state before each test
    uninstallFetchInterceptor();
  });

  afterEach(() => {
    uninstallFetchInterceptor();
    window.fetch = realFetch;
  });

  it('monkey-patches window.fetch after install', () => {
    const original = window.fetch;
    installFetchInterceptor(store);
    expect(window.fetch).not.toBe(original);
  });

  it('install is idempotent (calling twice keeps same wrapper)', () => {
    installFetchInterceptor(store);
    const firstWrapper = window.fetch;
    installFetchInterceptor(store);
    expect(window.fetch).toBe(firstWrapper);
  });

  it('records method, url, status, durationMs, isMock on successful fetch', async () => {
    window.fetch = vi.fn().mockResolvedValue(makeResponse(200, { 'X-Meeple-Mock': 'msw-game' }));
    installFetchInterceptor(store);

    await window.fetch('https://example.com/api/games', { method: 'GET' });

    const entries = store.getState().entries;
    expect(entries).toHaveLength(1);
    const entry = entries[0];
    expect(entry.method).toBe('GET');
    expect(entry.url).toBe('https://example.com/api/games');
    expect(entry.status).toBe(200);
    expect(entry.durationMs).toBeGreaterThanOrEqual(0);
    expect(entry.isMock).toBe(true);
    expect(entry.mockSource).toBe('msw-game');
  });

  it('skips recording when X-Meepledev-Internal: 1 is set (plain object form)', async () => {
    const mockFetch = vi.fn().mockResolvedValue(makeResponse(200));
    window.fetch = mockFetch;
    installFetchInterceptor(store);

    await window.fetch('https://example.com/api/dev/toggles', {
      headers: { 'X-Meepledev-Internal': '1' },
    });

    expect(store.getState().entries).toHaveLength(0);
  });

  it('skips recording when X-Meepledev-Internal: 1 set via Headers object', async () => {
    const mockFetch = vi.fn().mockResolvedValue(makeResponse(200));
    window.fetch = mockFetch;
    installFetchInterceptor(store);

    const headers = new Headers({ 'X-Meepledev-Internal': '1' });
    await window.fetch('https://example.com/api/dev/toggles', { headers });

    expect(store.getState().entries).toHaveLength(0);
  });

  it('skips recording when X-Meepledev-Internal: 1 set via array form', async () => {
    const mockFetch = vi.fn().mockResolvedValue(makeResponse(200));
    window.fetch = mockFetch;
    installFetchInterceptor(store);

    await window.fetch('https://example.com/api/dev/toggles', {
      headers: [['X-Meepledev-Internal', '1']],
    });

    expect(store.getState().entries).toHaveLength(0);
  });

  it('records status=0 and re-throws on network error', async () => {
    const networkError = new TypeError('Failed to fetch');
    window.fetch = vi.fn().mockRejectedValue(networkError);
    installFetchInterceptor(store);

    await expect(window.fetch('https://example.com/api/games')).rejects.toThrow('Failed to fetch');

    const entries = store.getState().entries;
    expect(entries).toHaveLength(1);
    expect(entries[0].status).toBe(0);
    expect(entries[0].isMock).toBe(false);
  });

  it('re-throws AbortError unchanged', async () => {
    const abortError = new DOMException('The user aborted a request.', 'AbortError');
    window.fetch = vi.fn().mockRejectedValue(abortError);
    installFetchInterceptor(store);

    await expect(window.fetch('https://example.com/api/games')).rejects.toThrow(
      'The user aborted a request.'
    );

    const entries = store.getState().entries;
    expect(entries).toHaveLength(1);
    expect(entries[0].status).toBe(0);
  });

  it('uninstall restores original window.fetch', () => {
    const originalBeforeInstall = window.fetch;
    installFetchInterceptor(store);
    expect(window.fetch).not.toBe(originalBeforeInstall);

    uninstallFetchInterceptor();
    expect(window.fetch).toBe(originalBeforeInstall);
  });
});

describe('readInternalHeader', () => {
  it('returns false when no headers provided', () => {
    expect(readInternalHeader()).toBe(false);
    expect(readInternalHeader(null)).toBe(false);
    expect(readInternalHeader({})).toBe(false);
  });

  it('detects header in plain object form', () => {
    expect(readInternalHeader({ headers: { 'X-Meepledev-Internal': '1' } })).toBe(true);
    expect(readInternalHeader({ headers: { 'x-meepledev-internal': '1' } })).toBe(true);
    expect(readInternalHeader({ headers: { 'X-Meepledev-Internal': '0' } })).toBe(false);
  });

  it('detects header in Headers object form', () => {
    const headers = new Headers({ 'X-Meepledev-Internal': '1' });
    expect(readInternalHeader({ headers })).toBe(true);
  });

  it('detects header in array form', () => {
    expect(readInternalHeader({ headers: [['X-Meepledev-Internal', '1']] })).toBe(true);
    expect(readInternalHeader({ headers: [['x-meepledev-internal', '1']] })).toBe(true);
    expect(readInternalHeader({ headers: [['X-Meepledev-Internal', '0']] })).toBe(false);
  });
});
