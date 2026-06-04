import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, waitFor, act } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useCatalogSyncStatus } from './use-catalog-sync-status';
import * as api from '../lib/catalog-ingestion-api';

vi.mock('../lib/catalog-ingestion-api');

function TestHarness({ onData }: { onData: (data: unknown) => void }) {
  const { data } = useCatalogSyncStatus();
  if (data) onData(data);
  return null;
}

function wrapper(client: QueryClient) {
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('useCatalogSyncStatus', () => {
  beforeEach(() => {
    Object.defineProperty(document, 'hidden', { value: false, configurable: true });
    vi.useFakeTimers({ shouldAdvanceTime: true });
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('fetches status on mount', async () => {
    const fetchSpy = vi.mocked(api.fetchCatalogSyncStatus).mockResolvedValue({
      status: 'idle',
      lastRun: null,
      currentRun: null,
      cumulative: { gamesTotal: 0 },
      nextScheduled: null,
    } as never);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const observed = vi.fn();
    render(<TestHarness onData={observed} />, { wrapper: wrapper(client) });
    await waitFor(() => expect(fetchSpy).toHaveBeenCalledTimes(1));
  });

  it('invalidates runs cache on running -> idle transition', async () => {
    const fetchSpy = vi
      .mocked(api.fetchCatalogSyncStatus)
      .mockResolvedValueOnce({ status: 'running' } as never)
      .mockResolvedValueOnce({ status: 'idle' } as never);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const invalidateSpy = vi.spyOn(client, 'invalidateQueries');
    const observed = vi.fn();

    render(<TestHarness onData={observed} />, { wrapper: wrapper(client) });
    await waitFor(() =>
      expect(observed).toHaveBeenCalledWith(expect.objectContaining({ status: 'running' }))
    );

    await act(async () => {
      await client.refetchQueries({ queryKey: ['catalog-sync-status'] });
    });
    await waitFor(() =>
      expect(observed).toHaveBeenCalledWith(expect.objectContaining({ status: 'idle' }))
    );

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['catalog-sync-runs'] });
  });
});
