/**
 * Tests for `useGetParagraph` — issue #1303.
 *
 * Covers AC-1, AC-2, AC-3, AC-6, AC-8, AC-9. Schema ACs (4a/4b) live in
 * `apps/web/src/lib/gamebook/__tests__/schemas.test.ts`. Type-contract
 * AC-7 lives in `useGetParagraph.types.test.tsx`.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  getCapturedGamebookRequests,
  resetCapturedGamebookRequests,
} from '@/__tests__/mocks/handlers/gamebook.handlers';

import { __resetDeprecationWarnings, useGetParagraph } from '../useGetParagraph';

// ---------------------------------------------------------------------------
// Test harness — fresh QueryClient per test so cache assertions are isolated
// ---------------------------------------------------------------------------

function makeWrapper(client: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

function freshQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, gcTime: Infinity, staleTime: Infinity },
    },
  });
}

const BATCH_ID = '11111111-2222-4333-8444-555555555555';

beforeEach(() => {
  resetCapturedGamebookRequests();
  __resetDeprecationWarnings();
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ---------------------------------------------------------------------------
// AC-1 — by-paragraph endpoint dispatch
// ---------------------------------------------------------------------------

describe('AC-1 — by-paragraph dispatch', () => {
  it('issues GET /paragraphs/by-paragraph/{N} when paragraphRef.type === "paragraph"', async () => {
    const { result } = renderHook(
      () =>
        useGetParagraph({
          batchId: BATCH_ID,
          paragraphRef: { type: 'paragraph', value: 42 },
        }),
      { wrapper: makeWrapper(freshQueryClient()) }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const captured = getCapturedGamebookRequests();
    expect(captured).toHaveLength(1);
    expect(captured[0].path).toBe(
      `/api/v1/photo-batches/${BATCH_ID}/paragraphs/by-paragraph/42`
    );
    expect(result.current.data?.paragraphNumber).toBe(42);
    expect(result.current.data?.pageNumber).toBe(7); // mocked physical page
  });
});

// ---------------------------------------------------------------------------
// AC-2 — by-page endpoint dispatch
// ---------------------------------------------------------------------------

describe('AC-2 — by-page dispatch', () => {
  it('issues GET /paragraphs/{N} when paragraphRef.type === "page"', async () => {
    const { result } = renderHook(
      () =>
        useGetParagraph({
          batchId: BATCH_ID,
          paragraphRef: { type: 'page', value: 5 },
        }),
      { wrapper: makeWrapper(freshQueryClient()) }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const captured = getCapturedGamebookRequests();
    expect(captured).toHaveLength(1);
    expect(captured[0].path).toBe(
      `/api/v1/photo-batches/${BATCH_ID}/paragraphs/5`
    );
    expect(result.current.data?.paragraphNumber).toBeNull();
    expect(result.current.data?.pageNumber).toBe(5);
  });
});

// ---------------------------------------------------------------------------
// AC-3 — legacy pageNumber shape + one-shot deprecation warning
// ---------------------------------------------------------------------------

describe('AC-3 — legacy pageNumber backward-compat', () => {
  it('returns the same data as AC-2 and emits exactly one console.warn per session', async () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => undefined);

    const client = freshQueryClient();
    const wrapper = makeWrapper(client);

    // First invocation — should fire the warn exactly once.
    const { result: r1 } = renderHook(
      () => useGetParagraph({ batchId: BATCH_ID, pageNumber: 5 }),
      { wrapper }
    );
    await waitFor(() => expect(r1.current.isSuccess).toBe(true));

    // Second invocation with the SAME (batchId, pageNumber) — warn must NOT fire again.
    const { result: r2 } = renderHook(
      () => useGetParagraph({ batchId: BATCH_ID, pageNumber: 5 }),
      { wrapper }
    );
    await waitFor(() => expect(r2.current.isSuccess).toBe(true));

    const deprecationCalls = warnSpy.mock.calls.filter(
      args => typeof args[0] === 'string' && args[0].includes('[useGetParagraph]')
    );
    expect(deprecationCalls).toHaveLength(1);

    // Sanity: data shape matches the explicit `type: 'page'` shape.
    expect(r1.current.data?.pageNumber).toBe(5);
    expect(r1.current.data?.paragraphNumber).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// AC-6 — query-key isolation between page and paragraph lookups
// ---------------------------------------------------------------------------

describe('AC-6 — query-key isolation', () => {
  it('stores two distinct cache entries for type:page,value:5 vs type:paragraph,value:5', async () => {
    const client = freshQueryClient();
    const wrapper = makeWrapper(client);

    const { result: byPage } = renderHook(
      () =>
        useGetParagraph({
          batchId: BATCH_ID,
          paragraphRef: { type: 'page', value: 5 },
        }),
      { wrapper }
    );
    await waitFor(() => expect(byPage.current.isSuccess).toBe(true));

    const { result: byPara } = renderHook(
      () =>
        useGetParagraph({
          batchId: BATCH_ID,
          paragraphRef: { type: 'paragraph', value: 5 },
        }),
      { wrapper }
    );
    await waitFor(() => expect(byPara.current.isSuccess).toBe(true));

    const cacheEntries = client.getQueryCache().getAll();
    expect(cacheEntries).toHaveLength(2);

    // Keys must carry the discriminator segment so a future namespace
    // collision can't sneak past code review.
    const keys = cacheEntries.map(e => e.queryKey);
    expect(keys.some(k => Array.isArray(k) && k.includes('byPage'))).toBe(true);
    expect(keys.some(k => Array.isArray(k) && k.includes('byParagraph'))).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// AC-8 — disabled flow when batchId is undefined
// ---------------------------------------------------------------------------

describe('AC-8 — disabled flow', () => {
  it('does NOT issue a network request when batchId is undefined', async () => {
    const { result } = renderHook(
      () =>
        useGetParagraph({
          batchId: undefined,
          paragraphRef: { type: 'paragraph', value: 1 },
        }),
      { wrapper: makeWrapper(freshQueryClient()) }
    );

    // Let the event loop settle so any spurious fetch would have fired.
    await new Promise(r => setTimeout(r, 50));

    expect(getCapturedGamebookRequests()).toHaveLength(0);
    expect(result.current.fetchStatus).toBe('idle');
  });

  it('does NOT issue a network request when paragraphRef.value <= 0', async () => {
    const { result } = renderHook(
      () =>
        useGetParagraph({
          batchId: BATCH_ID,
          paragraphRef: { type: 'page', value: 0 },
        }),
      { wrapper: makeWrapper(freshQueryClient()) }
    );

    await new Promise(r => setTimeout(r, 50));

    expect(getCapturedGamebookRequests()).toHaveLength(0);
    expect(result.current.fetchStatus).toBe('idle');
  });
});

// ---------------------------------------------------------------------------
// AC-9 — hint? forwarded as ?hint=<urlencoded> for both endpoints
// ---------------------------------------------------------------------------

describe('AC-9 — hint forwarding', () => {
  it('forwards hint as ?hint= query param on the by-page route', async () => {
    const { result } = renderHook(
      () =>
        useGetParagraph({
          batchId: BATCH_ID,
          paragraphRef: { type: 'page', value: 5 },
          hint: 'chapter 3',
        }),
      { wrapper: makeWrapper(freshQueryClient()) }
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const captured = getCapturedGamebookRequests();
    expect(captured).toHaveLength(1);
    expect(captured[0].searchParams.hint).toBe('chapter 3');
  });

  it('forwards hint as ?hint= query param on the by-paragraph route', async () => {
    const { result } = renderHook(
      () =>
        useGetParagraph({
          batchId: BATCH_ID,
          paragraphRef: { type: 'paragraph', value: 42 },
          hint: 'the forest',
        }),
      { wrapper: makeWrapper(freshQueryClient()) }
    );
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const captured = getCapturedGamebookRequests();
    expect(captured).toHaveLength(1);
    expect(captured[0].searchParams.hint).toBe('the forest');
  });
});
