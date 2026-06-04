import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import * as api from '../lib/catalog-ingestion-api';
import { useCatalogSyncRuns } from './use-catalog-sync-runs';

vi.mock('../lib/catalog-ingestion-api');

function wrapper(client: QueryClient) {
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('useCatalogSyncRuns (infinite query)', () => {
  it('fetches first page with default pageSize=12 on mount', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 12,
      hasMore: false,
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRuns(), { wrapper: wrapper(client) });
    await waitFor(() => expect(spy).toHaveBeenCalledWith({ page: 1, pageSize: 12 }));
  });

  it('passes custom pageSize on first fetch', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 24,
      hasMore: false,
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRuns(24), { wrapper: wrapper(client) });
    await waitFor(() => expect(spy).toHaveBeenCalledWith({ page: 1, pageSize: 24 }));
  });

  it('exposes hasNextPage=true when first response has hasMore=true', async () => {
    vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
      items: [],
      total: 25,
      page: 1,
      pageSize: 12,
      hasMore: true,
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useCatalogSyncRuns(), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.hasNextPage).toBe(true));
  });

  it('exposes hasNextPage=false when first response has hasMore=false', async () => {
    vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
      items: [],
      total: 5,
      page: 1,
      pageSize: 12,
      hasMore: false,
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useCatalogSyncRuns(), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.hasNextPage).toBe(false));
  });

  it('fetchNextPage requests the next page param', async () => {
    const spy = vi
      .mocked(api.fetchCatalogSyncRuns)
      .mockResolvedValueOnce({
        items: [],
        total: 25,
        page: 1,
        pageSize: 12,
        hasMore: true,
      })
      .mockResolvedValueOnce({
        items: [],
        total: 25,
        page: 2,
        pageSize: 12,
        hasMore: false,
      });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const { result } = renderHook(() => useCatalogSyncRuns(), { wrapper: wrapper(client) });
    await waitFor(() => expect(result.current.hasNextPage).toBe(true));
    await act(async () => {
      await result.current.fetchNextPage();
    });
    await waitFor(() => expect(spy).toHaveBeenCalledWith({ page: 2, pageSize: 12 }));
    expect(result.current.hasNextPage).toBe(false);
  });
});
