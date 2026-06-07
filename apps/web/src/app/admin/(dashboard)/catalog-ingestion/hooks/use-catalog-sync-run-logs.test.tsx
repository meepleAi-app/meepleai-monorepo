import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import * as api from '../lib/catalog-ingestion-api';
import { useCatalogSyncRunLogs } from './use-catalog-sync-run-logs';

vi.mock('../lib/catalog-ingestion-api');

function wrapper(client: QueryClient) {
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('useCatalogSyncRunLogs', () => {
  it('does NOT fetch when runId is null', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRunLogs);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRunLogs(null), { wrapper: wrapper(client) });
    await new Promise(r => setTimeout(r, 50));
    expect(spy).not.toHaveBeenCalled();
  });

  it('fetches logs when runId is provided', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r1',
      status: 'Failed',
      errorCode: 'X',
      errorDetail: 'y',
      logsAvailable: true,
      logs: ['log1'],
      logsUnavailableReason: null,
    } as never);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRunLogs('r1'), { wrapper: wrapper(client) });
    await waitFor(() => expect(spy).toHaveBeenCalledWith('r1', 100));
  });

  it('uses custom tail value', async () => {
    const spy = vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r1',
      status: 'Success',
      errorCode: null,
      errorDetail: null,
      logsAvailable: true,
      logs: [],
      logsUnavailableReason: null,
    } as never);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    renderHook(() => useCatalogSyncRunLogs('r1', 50), { wrapper: wrapper(client) });
    await waitFor(() => expect(spy).toHaveBeenCalledWith('r1', 50));
  });
});
