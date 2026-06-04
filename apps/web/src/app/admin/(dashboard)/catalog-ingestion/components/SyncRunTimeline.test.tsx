import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import * as api from '../lib/catalog-ingestion-api';
import { SyncRunTimeline } from './SyncRunTimeline';

vi.mock('../lib/catalog-ingestion-api');

function setup(runs: api.CatalogSyncRunSummary[] = []) {
  vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
    items: runs,
    total: runs.length,
    page: 1,
    pageSize: 12,
    hasMore: false,
  });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    client,
    wrapper: ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  };
}

const sampleRun = (
  overrides: Partial<api.CatalogSyncRunSummary> = {}
): api.CatalogSyncRunSummary => ({
  id: 'r1',
  provider: 'BggApi',
  status: 'Success',
  title: 'BGG full sync',
  startedAt: '2026-06-04T14:08:00Z',
  completedAt: '2026-06-04T14:12:18Z',
  duration: '00:04:18', // TimeSpan string (was durationMs in plan)
  createdAt: '2026-06-04T14:08:00Z',
  itemsAdded: 12,
  itemsUpdated: 847,
  itemsFailed: 0,
  errorCode: null,
  errorDetail: null,
  triggeredByUserId: null,
  ...overrides,
});

describe('SyncRunTimeline', () => {
  it('renders header with success rate', async () => {
    const { wrapper } = setup([
      sampleRun({ id: 'r1', status: 'Success' }),
      sampleRun({ id: 'r2', status: 'Success' }),
      sampleRun({ id: 'r3', status: 'Failed' }),
    ]);
    render(<SyncRunTimeline onDrillDown={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/Sync history/)).toBeInTheDocument());
    expect(screen.getByText(/66.7%/)).toBeInTheDocument();
  });

  it('renders run rows with title, duration, counts', async () => {
    const { wrapper } = setup([sampleRun({ id: 'r1' })]);
    render(<SyncRunTimeline onDrillDown={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    expect(screen.getByText('4m 18s')).toBeInTheDocument();
    expect(screen.getByText('+12')).toBeInTheDocument();
    expect(screen.getByText('~847')).toBeInTheDocument();
  });

  it('applies failed-row tint for Failed status', async () => {
    const { wrapper } = setup([sampleRun({ id: 'r1', status: 'Failed' })]);
    render(<SyncRunTimeline onDrillDown={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    const row = screen.getByText('BGG full sync').closest('[data-testid="run-row"]');
    expect(row?.className).toMatch(/event/);
  });

  it('calls onDrillDown when › button clicked', async () => {
    const onDrillDown = vi.fn();
    const { wrapper } = setup([sampleRun({ id: 'r1' })]);
    render(<SyncRunTimeline onDrillDown={onDrillDown} />, { wrapper });
    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    await userEvent.click(screen.getByRole('button', { name: /Open logs for run r1/i }));
    expect(onDrillDown).toHaveBeenCalledWith('r1');
  });

  it('shows empty state when no runs', async () => {
    const { wrapper } = setup([]);
    render(<SyncRunTimeline onDrillDown={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/Nessun run/i)).toBeInTheDocument());
  });
});

describe('SyncRunTimeline — error state (#1880)', () => {
  function setupError(errorMessage = '500 Internal Server Error') {
    vi.mocked(api.fetchCatalogSyncRuns).mockRejectedValue(new Error(errorMessage));
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return {
      wrapper: ({ children }: { children: React.ReactNode }) => (
        <QueryClientProvider client={client}>{children}</QueryClientProvider>
      ),
    };
  }

  it('renders distinct error alert (NOT empty state) when /runs fetch fails', async () => {
    const { wrapper } = setupError('500 Internal Server Error');
    render(<SyncRunTimeline onDrillDown={vi.fn()} />, { wrapper });

    await waitFor(() =>
      expect(screen.getByText(/Impossibile caricare la timeline/i)).toBeInTheDocument()
    );
    expect(screen.getByText(/500 Internal Server Error/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Riprova/i })).toBeInTheDocument();
    // Critically: empty state semantics differ from error semantics
    expect(screen.queryByText(/Nessun run registrato/i)).not.toBeInTheDocument();
  });

  it('refetches when Retry clicked (recovers after transient error)', async () => {
    vi.mocked(api.fetchCatalogSyncRuns)
      .mockRejectedValueOnce(new Error('502 Bad Gateway'))
      .mockResolvedValue({
        items: [sampleRun({ id: 'r1' })],
        total: 1,
        page: 1,
        pageSize: 12,
        hasMore: false,
      });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    );

    render(<SyncRunTimeline onDrillDown={vi.fn()} />, { wrapper });
    await waitFor(() =>
      expect(screen.getByText(/Impossibile caricare la timeline/i)).toBeInTheDocument()
    );
    await userEvent.click(screen.getByRole('button', { name: /Riprova/i }));
    // After Retry the component transitions to the populated rendering; we do not
    // assert call-count because keepPreviousData + visibility refetch can fire extras.
    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    expect(screen.queryByText(/Impossibile caricare la timeline/i)).not.toBeInTheDocument();
  });
});
