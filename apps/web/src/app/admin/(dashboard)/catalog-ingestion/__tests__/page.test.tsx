import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import CatalogIngestionPage from '../page';
import * as api from '../lib/catalog-ingestion-api';

vi.mock('../lib/catalog-ingestion-api', async importOriginal => {
  const actual = await importOriginal<typeof api>();
  return {
    ...actual,
    fetchCatalogSyncStatus: vi.fn(),
    fetchCatalogSyncRuns: vi.fn(),
    fetchCatalogSyncRunLogs: vi.fn(),
    triggerCatalogSync: vi.fn(),
    useExcelImport: vi.fn().mockReturnValue({
      mutate: vi.fn(),
      reset: vi.fn(),
      isPending: false,
      isError: false,
      isSuccess: false,
      data: undefined,
      error: null,
    }),
  };
});

function setupMocks() {
  vi.mocked(api.fetchCatalogSyncStatus).mockResolvedValue({
    status: 'idle',
    lastRun: {
      id: 'r1',
      status: 'Success',
      provider: 'BggApi',
      title: 'BGG full sync',
      startedAt: '2026-06-04T14:08:00Z',
      completedAt: '2026-06-04T14:12:18Z',
      duration: '00:04:18',
      createdAt: '2026-06-04T14:08:00Z',
      itemsAdded: 12,
      itemsUpdated: 847,
      itemsFailed: 0,
      errorCode: null,
      errorDetail: null,
      triggeredByUserId: null,
    },
    currentRun: null,
    cumulative: { gamesTotal: 4812 },
    nextScheduled: null,
  } as never);

  vi.mocked(api.fetchCatalogSyncRuns).mockResolvedValue({
    items: [
      {
        id: 'r1',
        provider: 'BggApi',
        status: 'Success',
        title: 'BGG full sync',
        startedAt: '2026-06-04T14:08:00Z',
        completedAt: '2026-06-04T14:12:18Z',
        duration: '00:04:18',
        createdAt: '2026-06-04T14:08:00Z',
        itemsAdded: 12,
        itemsUpdated: 847,
        itemsFailed: 0,
        errorCode: null,
        errorDetail: null,
        triggeredByUserId: null,
      },
    ],
    total: 1,
    page: 1,
    pageSize: 12,
    hasMore: false,
  });
}

function wrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('CatalogIngestionPage', () => {
  it('renders header + hero + timeline + placeholder panels + export button', async () => {
    setupMocks();
    render(<CatalogIngestionPage />, { wrapper: wrapper() });

    await waitFor(() => expect(screen.getByText(/Catalog ingestion/i)).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('Idle')).toBeInTheDocument());
    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    expect(screen.getAllByText(/Queue pending/i).length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Failed items/i).length).toBeGreaterThan(0);
    expect(screen.getByRole('link', { name: /Export catalog/i })).toBeInTheDocument();
  });

  it('opens CSV modal when provider=CsvImport + Run sync now clicked', async () => {
    setupMocks();
    render(<CatalogIngestionPage />, { wrapper: wrapper() });

    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'CsvImport');
    await userEvent.click(screen.getByRole('button', { name: /Run sync now/i }));

    await waitFor(() => expect(screen.getByText(/CSV \/ Excel import/i)).toBeInTheDocument());
  });

  it('opens Manual modal when provider=Manual + Run sync now clicked', async () => {
    setupMocks();
    render(<CatalogIngestionPage />, { wrapper: wrapper() });

    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'Manual');
    await userEvent.click(screen.getByRole('button', { name: /Run sync now/i }));

    await waitFor(() => expect(screen.getByText(/Manual BGG assignment/i)).toBeInTheDocument());
  });

  it('opens LogStream when drill-down arrow clicked', async () => {
    setupMocks();
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r1',
      status: 'Success',
      errorCode: null,
      errorDetail: null,
      logsAvailable: true,
      logs: ['line1'],
    } as never);

    render(<CatalogIngestionPage />, { wrapper: wrapper() });

    await waitFor(() => expect(screen.getByText('BGG full sync')).toBeInTheDocument());
    await userEvent.click(screen.getByRole('button', { name: /Open logs for run r1/i }));

    await waitFor(() => expect(screen.getByText(/line1/)).toBeInTheDocument());
  });
});
