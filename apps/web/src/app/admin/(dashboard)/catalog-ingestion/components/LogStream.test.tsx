import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { LogStream } from './LogStream';
import * as api from '../lib/catalog-ingestion-api';

vi.mock('../lib/catalog-ingestion-api');

function setup() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return {
    client,
    wrapper: ({ children }: { children: React.ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    ),
  };
}

describe('LogStream', () => {
  it('does not render when runId is null', () => {
    const { wrapper } = setup();
    render(<LogStream runId={null} onClose={vi.fn()} />, { wrapper });
    expect(screen.queryByRole('region')).not.toBeInTheDocument();
  });

  it('renders logs when runId is provided and BE returns data', async () => {
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r1',
      status: 'Success',
      errorCode: null,
      errorDetail: null,
      logsAvailable: true,
      logs: ['[2026-06-04 14:08:00] BGG sync started', '[2026-06-04 14:08:14] +12 items'],
      logsUnavailableReason: null,
    } as never);
    const { wrapper } = setup();
    render(<LogStream runId="r1" onClose={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/BGG sync started/)).toBeInTheDocument());
    expect(screen.getByText(/\+12 items/)).toBeInTheDocument();
  });

  it('shows errorCode + errorDetail when status=Failed', async () => {
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r2',
      status: 'Failed',
      errorCode: 'BGG_API_RATE_LIMIT_429',
      errorDetail: '4 retry esauriti',
      logsAvailable: true,
      logs: [],
      logsUnavailableReason: null,
    } as never);
    const { wrapper } = setup();
    render(<LogStream runId="r2" onClose={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText('BGG_API_RATE_LIMIT_429')).toBeInTheDocument());
    expect(screen.getByText(/4 retry esauriti/)).toBeInTheDocument();
  });

  it('shows "Logs not available" when logsAvailable=false', async () => {
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r3',
      status: 'Success',
      errorCode: null,
      errorDetail: null,
      logsAvailable: false,
      logs: [],
      logsUnavailableReason: null,
    } as never);
    const { wrapper } = setup();
    render(<LogStream runId="r3" onClose={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/Logs not available/i)).toBeInTheDocument());
  });

  it('shows "Run not found" on 404 (null result)', async () => {
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue(null);
    const { wrapper } = setup();
    render(<LogStream runId="missing" onClose={vi.fn()} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/Run not found/i)).toBeInTheDocument());
  });

  it('calls onClose when close button clicked', async () => {
    vi.mocked(api.fetchCatalogSyncRunLogs).mockResolvedValue({
      runId: 'r1',
      status: 'Success',
      errorCode: null,
      errorDetail: null,
      logsAvailable: true,
      logs: ['x'],
      logsUnavailableReason: null,
    } as never);
    const onClose = vi.fn();
    const { wrapper } = setup();
    render(<LogStream runId="r1" onClose={onClose} />, { wrapper });
    await waitFor(() => expect(screen.getByText(/x/)).toBeInTheDocument());
    await userEvent.click(screen.getByRole('button', { name: /Close logs/i }));
    expect(onClose).toHaveBeenCalled();
  });
});
