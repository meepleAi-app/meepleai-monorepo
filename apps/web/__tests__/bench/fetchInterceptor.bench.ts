/**
 * fetchInterceptor.bench.ts — Performance benchmark for the fetch interceptor.
 *
 * Compares baseline (plain fetch) vs. instrumented (intercepted fetch).
 * Run with: pnpm vitest bench __tests__/bench/fetchInterceptor.bench.ts
 */

import { bench, describe, beforeAll, afterAll } from 'vitest';
import {
  installFetchInterceptor,
  uninstallFetchInterceptor,
} from '@/dev-tools/panel/hooks/useFetchInterceptor';
import { createRequestInspectorStore } from '@/dev-tools/panel/stores/requestInspectorStore';

const ITERATIONS = 1000;

/**
 * Create a minimal fake fetch that resolves immediately with no I/O.
 * This isolates interceptor overhead from network latency.
 */
function makeFakeFetch(): typeof fetch {
  return () =>
    Promise.resolve(
      new Response(JSON.stringify({ ok: true }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );
}

describe('fetchInterceptor overhead', () => {
  let originalFetch: typeof fetch;

  beforeAll(() => {
    originalFetch = window.fetch;
  });

  afterAll(() => {
    // Always restore
    uninstallFetchInterceptor();
    window.fetch = originalFetch;
  });

  bench(
    `baseline — ${ITERATIONS} fetch calls (no interceptor)`,
    async () => {
      window.fetch = makeFakeFetch();
      for (let i = 0; i < ITERATIONS; i++) {
        await window.fetch(`https://bench.local/api/item-${i}`);
      }
    },
    { iterations: 5 }
  );

  bench(
    `instrumented — ${ITERATIONS} fetch calls (with interceptor)`,
    async () => {
      uninstallFetchInterceptor();
      window.fetch = makeFakeFetch();
      const store = createRequestInspectorStore();
      installFetchInterceptor(store);
      for (let i = 0; i < ITERATIONS; i++) {
        await window.fetch(`https://bench.local/api/item-${i}`);
      }
    },
    { iterations: 5 }
  );
});
