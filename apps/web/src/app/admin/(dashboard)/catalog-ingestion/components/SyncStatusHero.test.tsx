import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { SyncStatusHero } from './SyncStatusHero';
import * as api from '../lib/catalog-ingestion-api';

vi.mock('../lib/catalog-ingestion-api');

function setup(initialStatus: Partial<api.CatalogSyncStatusResponse> = {}) {
  vi.mocked(api.fetchCatalogSyncStatus).mockResolvedValue({
    status: 'idle',
    lastRun: {
      id: 'r1',
      status: 'Success',
      provider: 'BggApi',
      title: 'BGG full sync',
      startedAt: '2026-06-04T14:00:00Z',
      completedAt: '2026-06-04T14:08:00Z',
      duration: '00:08:00',
      createdAt: '2026-06-04T14:00:00Z',
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
    ...initialStatus,
  } as never);
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    client,
    wrapper: ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  };
}

describe('SyncStatusHero — chip + stats rendering', () => {
  it('renders 🟢 Idle chip when status=idle && lastRun=Success', async () => {
    const { wrapper } = setup();
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText('Idle')).toBeInTheDocument());
    expect(screen.getByText(/Giochi importati totali/)).toBeInTheDocument();
    expect(screen.getByText('4.812')).toBeInTheDocument();
  });

  it('renders 🔴 Last sync failed chip + errorCode badge', async () => {
    const { wrapper } = setup({
      lastRun: {
        id: 'r1',
        status: 'Failed',
        provider: 'BggApi',
        title: 'BGG full sync',
        startedAt: '2026-06-04T14:00:00Z',
        completedAt: '2026-06-04T14:06:00Z',
        duration: '00:06:00',
        createdAt: '2026-06-04T14:00:00Z',
        itemsAdded: 3,
        itemsUpdated: 218,
        itemsFailed: 14,
        errorCode: 'BGG_API_RATE_LIMIT_429',
        errorDetail: '4 retry esauriti',
        triggeredByUserId: null,
      } as never,
    });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText('Last sync failed')).toBeInTheDocument());
    expect(screen.getByText('BGG_API_RATE_LIMIT_429')).toBeInTheDocument();
  });

  it('renders 🟠 Running chip when status=running', async () => {
    const { wrapper } = setup({
      status: 'running',
      currentRun: {
        id: 'rx',
        status: 'Running',
        provider: 'BggApi',
        title: 'BGG full sync',
        startedAt: 'now',
        completedAt: null,
        duration: null,
        createdAt: 'now',
        itemsAdded: 0,
        itemsUpdated: 0,
        itemsFailed: 0,
        errorCode: null,
        errorDetail: null,
        triggeredByUserId: null,
      } as never,
    });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText('Running')).toBeInTheDocument());
  });

  it('renders ⚪ Setup chip when status=never_run', async () => {
    const { wrapper } = setup({
      status: 'never_run',
      lastRun: null,
      cumulative: { gamesTotal: 0 },
    });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText('Setup')).toBeInTheDocument());
  });

  it('HIDES "Next scheduled" row when nextScheduled is null', async () => {
    const { wrapper } = setup({ nextScheduled: null });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText('Idle')).toBeInTheDocument());
    expect(screen.queryByText(/Next scheduled/)).not.toBeInTheDocument();
  });

  it('SHOWS "Next scheduled" row when present', async () => {
    const { wrapper } = setup({ nextScheduled: '2026-06-04T20:00:00Z' });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByText(/Next scheduled/)).toBeInTheDocument());
  });
});

describe('SyncStatusHero — provider config visibility', () => {
  it('SHOWS Batch size / Rate limit / Auto-retry when provider=BggApi', async () => {
    const { wrapper } = setup();
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByLabelText(/Batch size/i)).toBeInTheDocument());
    expect(screen.getByLabelText(/Rate limit/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Auto-retry/i)).toBeInTheDocument();
  });

  it('HIDES config when provider=CsvImport', async () => {
    const { wrapper } = setup();
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'CsvImport');
    expect(screen.queryByLabelText(/Batch size/i)).not.toBeInTheDocument();
  });

  it('HIDES config when provider=Manual', async () => {
    const { wrapper } = setup();
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'Manual');
    expect(screen.queryByLabelText(/Batch size/i)).not.toBeInTheDocument();
  });
});

describe('SyncStatusHero — trigger flow', () => {
  it('disables Run sync now when status=running', async () => {
    const { wrapper } = setup({
      status: 'running',
      currentRun: {
        id: 'rx',
        status: 'Running',
        provider: 'BggApi',
        title: 'BGG full sync',
        startedAt: 'now',
        completedAt: null,
        duration: null,
        createdAt: 'now',
        itemsAdded: 0,
        itemsUpdated: 0,
        itemsFailed: 0,
        errorCode: null,
        errorDetail: null,
        triggeredByUserId: null,
      } as never,
    });
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /Run sync now/i })).toBeDisabled()
    );
  });

  it('POSTs /trigger when BGG provider + button clicked', async () => {
    const spy = vi.mocked(api.triggerCatalogSync).mockResolvedValue({ runId: 'r2' } as never);
    const { wrapper } = setup();
    render(<SyncStatusHero />, { wrapper });
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /Run sync now/i })).toBeEnabled()
    );
    await userEvent.click(screen.getByRole('button', { name: /Run sync now/i }));
    await waitFor(() => expect(spy).toHaveBeenCalledWith('BggApi'));
  });

  it('emits onOpenCsvModal callback when CSV provider + button clicked', async () => {
    const onOpenCsvModal = vi.fn();
    const { wrapper } = setup();
    render(<SyncStatusHero onOpenCsvModal={onOpenCsvModal} />, { wrapper });
    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'CsvImport');
    await userEvent.click(screen.getByRole('button', { name: /Run sync now/i }));
    expect(onOpenCsvModal).toHaveBeenCalled();
  });

  it('emits onOpenManualModal callback when Manual provider + button clicked', async () => {
    const onOpenManualModal = vi.fn();
    const { wrapper } = setup();
    render(<SyncStatusHero onOpenManualModal={onOpenManualModal} />, { wrapper });
    await waitFor(() => expect(screen.getByLabelText(/Provider/i)).toBeInTheDocument());
    await userEvent.selectOptions(screen.getByLabelText(/Provider/i), 'Manual');
    await userEvent.click(screen.getByRole('button', { name: /Run sync now/i }));
    expect(onOpenManualModal).toHaveBeenCalled();
  });
});
