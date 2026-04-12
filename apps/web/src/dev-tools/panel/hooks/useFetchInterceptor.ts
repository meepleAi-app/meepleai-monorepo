/**
 * useFetchInterceptor — monkey-patches window.fetch to capture every HTTP
 * request into the requestInspectorStore ring buffer.
 *
 * Design goals:
 *  - Idempotent: calling install twice has no effect (module-level sentinel).
 *  - StrictMode-safe: works with React 18 double-invocation in dev.
 *  - Internal-request skip: skips requests that carry X-Meepledev-Internal: 1.
 *  - Mock detection: marks entries with isMock=true when X-Meeple-Mock header present.
 */

import { generateId } from '../stores/requestInspectorStore';

import type { RequestInspectorState } from '../stores/requestInspectorStore';
import type { StoreApi } from 'zustand/vanilla';

let installed = false;
let originalFetch: typeof fetch | null = null;

type InspectorStore = StoreApi<RequestInspectorState>;

/**
 * Reads the X-Meepledev-Internal header from a fetch RequestInit.
 * Supports Headers object, [string, string][] array, and plain Record forms.
 */
export function readInternalHeader(init?: RequestInit | null): boolean {
  if (!init?.headers) return false;

  const headers = init.headers;

  if (headers instanceof Headers) {
    return headers.get('X-Meepledev-Internal') === '1';
  }

  if (Array.isArray(headers)) {
    return headers.some(
      ([name, value]) => name.toLowerCase() === 'x-meepledev-internal' && value === '1'
    );
  }

  // Plain object / Record<string, string>
  const record = headers as Record<string, string>;
  for (const key of Object.keys(record)) {
    if (key.toLowerCase() === 'x-meepledev-internal' && record[key] === '1') {
      return true;
    }
  }

  return false;
}

/**
 * Reads a response header from the response.
 */
function readResponseHeader(response: Response, name: string): string | null {
  try {
    return response.headers.get(name);
  } catch {
    return null;
  }
}

export function installFetchInterceptor(store: InspectorStore): void {
  if (installed) return;
  installed = true;

  originalFetch = window.fetch;

  window.fetch = async function interceptedFetch(
    input: RequestInfo | URL,
    init?: RequestInit
  ): Promise<Response> {
    // Skip internal dev-tools requests
    if (readInternalHeader(init)) {
      return originalFetch!(input, init);
    }

    const method =
      init?.method?.toUpperCase() ??
      (typeof input === 'object' && 'method' in input ? (input as Request).method : 'GET');

    const url =
      typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.toString()
          : (input as Request).url;

    const startTime = performance.now();
    const id = generateId();
    const timestamp = Date.now();

    try {
      const response = await originalFetch!(input, init);
      const durationMs = Math.round(performance.now() - startTime);

      const mockSource = readResponseHeader(response, 'X-Meeple-Mock');
      const isMock = mockSource !== null;

      store.getState().record({
        id,
        timestamp,
        method,
        url,
        status: response.status,
        durationMs,
        isMock,
        mockSource: mockSource ?? undefined,
      });

      return response;
    } catch (error) {
      const durationMs = Math.round(performance.now() - startTime);

      store.getState().record({
        id,
        timestamp,
        method,
        url,
        status: 0,
        durationMs,
        isMock: false,
      });

      throw error;
    }
  };
}

export function uninstallFetchInterceptor(): void {
  if (!installed || !originalFetch) return;
  window.fetch = originalFetch;
  originalFetch = null;
  installed = false;
}
