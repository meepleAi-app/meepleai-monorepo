import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import * as api from '../../lib/catalog-seed-api';
import {
  useApproveSeed,
  useBulkEnqueueSeeds,
  useCatalogSeed,
  useCatalogSeeds,
  useEnqueueSeed,
  useRejectSeed,
  useWikidataSearch,
} from '../use-catalog-seeds';

vi.mock('../../lib/catalog-seed-api');

function wrapper(client: QueryClient) {
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

function makeClient() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

describe('useCatalogSeeds', () => {
  it('fetches the list with provided params', async () => {
    const spy = vi.mocked(api.listSeeds).mockResolvedValue({
      total: 0,
      skip: 0,
      take: 50,
      items: [],
    });
    const client = makeClient();
    renderHook(() => useCatalogSeeds({ status: 'Pending', skip: 0, take: 50 }), {
      wrapper: wrapper(client),
    });
    await waitFor(() => expect(spy).toHaveBeenCalledWith({ status: 'Pending', skip: 0, take: 50 }));
  });

  it('returns the list payload on success', async () => {
    vi.mocked(api.listSeeds).mockResolvedValue({
      total: 1,
      skip: 0,
      take: 50,
      items: [
        {
          id: 'd1',
          bggId: 13,
          wikidataQid: null,
          searchTermInput: null,
          status: 'Pending',
          errorMessage: null,
          resultingSharedGameId: null,
          createdByUserId: 'u1',
          createdAt: '2026-06-04T12:00:00Z',
          fetchedAt: null,
          approvedAt: null,
          approvedByUserId: null,
        },
      ],
    });
    const client = makeClient();
    const { result } = renderHook(() => useCatalogSeeds(), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.data?.items.length).toBe(1));
    expect(result.current.data?.items[0].bggId).toBe(13);
  });
});

describe('useCatalogSeed', () => {
  it('does not fetch when id is null', () => {
    const spy = vi.mocked(api.getSeedById).mockResolvedValue(null);
    const client = makeClient();
    renderHook(() => useCatalogSeed(null), { wrapper: wrapper(client) });
    expect(spy).not.toHaveBeenCalled();
  });

  it('fetches by id when provided', async () => {
    const spy = vi.mocked(api.getSeedById).mockResolvedValue(null);
    const client = makeClient();
    renderHook(() => useCatalogSeed('d1'), { wrapper: wrapper(client) });
    await waitFor(() => expect(spy).toHaveBeenCalledWith('d1'));
  });
});

describe('useEnqueueSeed', () => {
  it('invalidates the seeds list on success', async () => {
    const client = makeClient();
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries');
    vi.mocked(api.enqueueSeed).mockResolvedValue({
      id: 'd1',
      bggId: 13,
      status: 'Pending',
      isDuplicate: false,
    });
    const { result } = renderHook(() => useEnqueueSeed(), { wrapper: wrapper(client) });
    await act(async () => {
      await result.current.mutateAsync({ bggId: 13 });
    });
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['catalog-seeds'] });
  });
});

describe('useBulkEnqueueSeeds', () => {
  it('returns the bulk result on mutate', async () => {
    vi.mocked(api.bulkEnqueueSeeds).mockResolvedValue({
      total: 2,
      enqueued: 2,
      duplicates: 0,
      newDraftIds: ['d1', 'd2'],
    });
    const client = makeClient();
    const { result } = renderHook(() => useBulkEnqueueSeeds(), { wrapper: wrapper(client) });
    await act(async () => {
      const r = await result.current.mutateAsync({ bggIds: [13, 30549] });
      expect(r.enqueued).toBe(2);
    });
  });
});

describe('useApproveSeed', () => {
  it('forwards id to the api call', async () => {
    const spy = vi.mocked(api.approveSeed).mockResolvedValue({
      draftId: 'd1',
      resultingSharedGameId: 'sg-1',
    });
    const client = makeClient();
    const { result } = renderHook(() => useApproveSeed(), { wrapper: wrapper(client) });
    await act(async () => {
      await result.current.mutateAsync('d1');
    });
    expect(spy).toHaveBeenCalledWith('d1');
  });
});

describe('useRejectSeed', () => {
  it('forwards id+reason to the api call', async () => {
    const spy = vi.mocked(api.rejectSeed).mockResolvedValue(undefined);
    const client = makeClient();
    const { result } = renderHook(() => useRejectSeed(), { wrapper: wrapper(client) });
    await act(async () => {
      await result.current.mutateAsync({ id: 'd1', reason: 'dup' });
    });
    expect(spy).toHaveBeenCalledWith('d1', 'dup');
  });
});

describe('useWikidataSearch', () => {
  it('returns the SPARQL result on mutate', async () => {
    vi.mocked(api.wikidataSearch).mockResolvedValue({
      fields: {},
      errorMessage: null,
    });
    const client = makeClient();
    const { result } = renderHook(() => useWikidataSearch(), { wrapper: wrapper(client) });
    await act(async () => {
      const r = await result.current.mutateAsync('catan');
      expect(r.fields).toEqual({});
    });
  });
});
