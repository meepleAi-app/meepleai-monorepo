import { describe, it, expect, vi, beforeEach } from 'vitest';
import { devPanelClient, DevPanelClientError } from '@/dev-tools/panel/api/devPanelClient';

describe('devPanelClient', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('getToggles parses successful response', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        toggles: { llm: true, embedding: false },
        knownServices: ['llm', 'embedding'],
      }),
      headers: new Headers(),
    });
    const result = await devPanelClient.getToggles();
    expect(result.toggles.llm).toBe(true);
    expect(result.knownServices).toEqual(['llm', 'embedding']);
  });

  it('getToggles sends X-Meepledev-Internal header', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ toggles: {}, knownServices: [] }),
      headers: new Headers(),
    });
    global.fetch = fetchMock;
    await devPanelClient.getToggles();
    const headers = fetchMock.mock.calls[0][1]?.headers as Record<string, string>;
    expect(headers['X-Meepledev-Internal']).toBe('1');
  });

  it('patchToggles posts batch body', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ updated: ['llm'], toggles: { llm: false, embedding: true } }),
      headers: new Headers(),
    });
    global.fetch = fetchMock;
    const result = await devPanelClient.patchToggles({ llm: false });
    expect(result.updated).toEqual(['llm']);
    expect(fetchMock.mock.calls[0][1]?.method).toBe('PATCH');
  });

  it('throws DevPanelClientError on 4xx response', async () => {
    global.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 400,
      json: async () => ({ error: 'unknown-service', message: 'Unknown' }),
      headers: new Headers({ 'X-Trace-Id': 'abc123' }),
    });
    await expect(devPanelClient.patchToggles({ xyz: true })).rejects.toThrow(DevPanelClientError);
  });

  it('throws DevPanelClientError on network error', async () => {
    global.fetch = vi.fn().mockRejectedValue(new TypeError('network failure'));
    await expect(devPanelClient.getToggles()).rejects.toThrow(DevPanelClientError);
  });

  it('resetToggles calls POST /reset', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ toggles: { llm: true }, knownServices: ['llm'] }),
      headers: new Headers(),
    });
    global.fetch = fetchMock;
    const result = await devPanelClient.resetToggles();
    expect(result.toggles.llm).toBe(true);
    expect(fetchMock.mock.calls[0][1]?.method).toBe('POST');
    expect(fetchMock.mock.calls[0][0]).toContain('/reset');
  });

  it('aborts fetch after timeout (5s)', async () => {
    vi.useFakeTimers();
    let abortCalled = false;
    global.fetch = vi.fn((_url, init) => {
      const signal = (init as RequestInit)?.signal;
      return new Promise((_resolve, reject) => {
        if (signal) {
          signal.addEventListener('abort', () => {
            abortCalled = true;
            reject(new DOMException('The operation was aborted.', 'AbortError'));
          });
        }
      });
    });
    const promise = devPanelClient.getToggles();
    vi.advanceTimersByTime(5500);
    await expect(promise).rejects.toThrow();
    expect(abortCalled).toBe(true);
    vi.useRealTimers();
  });
});
