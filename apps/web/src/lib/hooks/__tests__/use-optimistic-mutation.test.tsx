/**
 * Tests for useOptimisticMutation (Issue #951 AC-H2 scope note 2).
 *
 * Covers:
 *   - Optimistic write applied immediately before mutationFn resolves
 *   - Snapshot restore on error
 *   - onRollback consumer fires AFTER restore
 *   - Cache invalidation on success (invalidateOnSuccess flag)
 *   - reactive mutation.error surfacing on rollback
 */

import { QueryClient, QueryClientProvider, useQueryClient } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useOptimisticMutation } from '../use-optimistic-mutation';

interface CachedRoster {
  status: 'Pending' | 'Accepted' | 'Declined' | 'Maybe';
  count: number;
}

interface RsvpVariables {
  response: 'Accepted' | 'Declined' | 'Maybe';
}

const CACHE_KEY = ['game-nights', 'test-id', 'roster'] as const;

function createWrapper() {
  // gcTime: Infinity so cache entries with no observers survive setQueryData calls
  // during the test (we don't mount a useQuery consumer; we drive the cache directly).
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity, staleTime: 0 } },
  });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
  return { client, wrapper };
}

describe('useOptimisticMutation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('applies optimistic write before mutationFn resolves', async () => {
    const { client, wrapper } = createWrapper();
    client.setQueryData<CachedRoster>(CACHE_KEY, { status: 'Pending', count: 3 });

    // Use a deferred promise so the test can observe the cache during the in-flight mutation.
    let resolveFn!: (value: { ok: true }) => void;
    const networkPromise = new Promise<{ ok: true }>(resolve => {
      resolveFn = resolve;
    });
    const mutationFn = vi.fn(() => networkPromise);

    const { result } = renderHook(
      () =>
        useOptimisticMutation<{ ok: true }, Error, RsvpVariables, CachedRoster>({
          cacheKey: CACHE_KEY,
          mutationFn,
          applyOptimistic: (cached, { response }) => ({
            status: response,
            count: (cached?.count ?? 0) + 1,
          }),
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({ response: 'Accepted' });
    });

    // Optimistic value is visible immediately; network call has not resolved yet.
    expect(client.getQueryData<CachedRoster>(CACHE_KEY)).toEqual({
      status: 'Accepted',
      count: 4,
    });
    expect(mutationFn).toHaveBeenCalledOnce();

    await act(async () => {
      resolveFn({ ok: true });
      await networkPromise;
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
  });

  it('restores snapshot and surfaces error via mutation.error on rollback', async () => {
    const { client, wrapper } = createWrapper();
    const original: CachedRoster = { status: 'Pending', count: 3 };
    client.setQueryData<CachedRoster>(CACHE_KEY, original);

    const networkError = new Error('409 conflict');
    const mutationFn = vi.fn().mockRejectedValue(networkError);
    const onRollback = vi.fn();

    const { result } = renderHook(
      () =>
        useOptimisticMutation<{ ok: true }, Error, RsvpVariables, CachedRoster>({
          cacheKey: CACHE_KEY,
          mutationFn,
          applyOptimistic: cached => ({
            status: 'Accepted',
            count: (cached?.count ?? 0) + 1,
          }),
          onRollback,
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({ response: 'Accepted' });
    });

    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(client.getQueryData<CachedRoster>(CACHE_KEY)).toEqual(original);
    expect(onRollback).toHaveBeenCalledWith({ response: 'Accepted' }, networkError);
    expect(result.current.error).toBe(networkError);
  });

  it('invalidates the cache on success when invalidateOnSuccess=true (default)', async () => {
    const { client, wrapper } = createWrapper();
    client.setQueryData<CachedRoster>(CACHE_KEY, { status: 'Pending', count: 3 });
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries');

    const { result } = renderHook(
      () =>
        useOptimisticMutation<{ ok: true }, Error, RsvpVariables, CachedRoster>({
          cacheKey: CACHE_KEY,
          mutationFn: vi.fn().mockResolvedValue({ ok: true }),
          applyOptimistic: cached => ({
            status: 'Accepted',
            count: (cached?.count ?? 0) + 1,
          }),
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({ response: 'Accepted' });
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: CACHE_KEY });
  });

  it('skips invalidation when invalidateOnSuccess=false', async () => {
    const { client, wrapper } = createWrapper();
    client.setQueryData<CachedRoster>(CACHE_KEY, { status: 'Pending', count: 3 });
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries');

    const { result } = renderHook(
      () =>
        useOptimisticMutation<{ ok: true }, Error, RsvpVariables, CachedRoster>({
          cacheKey: CACHE_KEY,
          mutationFn: vi.fn().mockResolvedValue({ ok: true }),
          applyOptimistic: cached => ({
            status: 'Accepted',
            count: (cached?.count ?? 0) + 1,
          }),
          invalidateOnSuccess: false,
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({ response: 'Accepted' });
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(invalidateSpy).not.toHaveBeenCalledWith({ queryKey: CACHE_KEY });
  });

  it('skips invalidation on error path even when invalidateOnSuccess=true', async () => {
    const { client, wrapper } = createWrapper();
    client.setQueryData<CachedRoster>(CACHE_KEY, { status: 'Pending', count: 3 });
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries');

    const { result } = renderHook(
      () =>
        useOptimisticMutation<{ ok: true }, Error, RsvpVariables, CachedRoster>({
          cacheKey: CACHE_KEY,
          mutationFn: vi.fn().mockRejectedValue(new Error('boom')),
          applyOptimistic: cached => ({
            status: 'Accepted',
            count: (cached?.count ?? 0) + 1,
          }),
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({ response: 'Accepted' });
    });
    await waitFor(() => expect(result.current.isError).toBe(true));

    expect(invalidateSpy).not.toHaveBeenCalledWith({ queryKey: CACHE_KEY });
  });

  it('handles undefined initial cache by passing undefined to reducer', async () => {
    const { client, wrapper } = createWrapper();
    // Note: no setQueryData — cache is empty.

    const reducer = vi.fn(
      (cached: CachedRoster | undefined, vars: RsvpVariables): CachedRoster => ({
        status: vars.response,
        count: cached?.count ?? 0,
      })
    );

    const { result } = renderHook(
      () =>
        useOptimisticMutation<{ ok: true }, Error, RsvpVariables, CachedRoster>({
          cacheKey: CACHE_KEY,
          mutationFn: vi.fn().mockResolvedValue({ ok: true }),
          applyOptimistic: reducer,
        }),
      { wrapper }
    );

    await act(async () => {
      result.current.mutate({ response: 'Maybe' });
    });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    expect(reducer).toHaveBeenCalledWith(undefined, { response: 'Maybe' });
  });

  it('composes correctly with useQueryClient consumer in same tree', async () => {
    // Smoke: ensure the hook does not break adjacent useQueryClient consumers.
    const { wrapper } = createWrapper();

    const { result } = renderHook(
      () => ({
        mutation: useOptimisticMutation<{ ok: true }, Error, RsvpVariables, CachedRoster>({
          cacheKey: CACHE_KEY,
          mutationFn: vi.fn().mockResolvedValue({ ok: true }),
          applyOptimistic: () => ({ status: 'Maybe', count: 0 }),
        }),
        client: useQueryClient(),
      }),
      { wrapper }
    );

    expect(result.current.client).toBeDefined();
    expect(result.current.mutation.mutate).toBeInstanceOf(Function);
  });
});
