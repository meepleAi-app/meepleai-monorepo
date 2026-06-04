import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import * as api from '../lib/catalog-ingestion-api';
import { useCatalogSyncRuns } from './use-catalog-sync-runs';

vi.mock('../lib/catalog-ingestion-api');

function wrapper(client: QueryClient) {
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('useCatalogSyncRuns', () => {
  it('fetches runs with default page=1 pageSize=12', async () => {
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

  it('passes custom page+pageSize', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
      items: [],
      total: 0,
      page: 3,
      pageSize: 24,
      hasMore: false,
    });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRuns(3, 24), { wrapper: wrapper(client) });
    await waitFor(() => expect(spy).toHaveBeenCalledWith({ page: 3, pageSize: 24 }));
  });
});
