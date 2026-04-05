import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { LokiErrorViewer } from '../LokiErrorViewer';
import type { LogsApiResponse } from '@/lib/loki/types';

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({
    defaultOptions: {
      queries: { retry: false, refetchInterval: false, gcTime: 0 },
    },
  });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

describe('LokiErrorViewer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading state initially', () => {
    mockFetch.mockImplementation(() => new Promise(() => {}));
    render(<LokiErrorViewer />, { wrapper });
    expect(screen.getByTestId('loki-loading')).toBeInTheDocument();
  });

  it('shows unavailable message when Loki is not configured', async () => {
    const body: LogsApiResponse = { entries: [], lokiUnavailable: true };
    mockFetch.mockResolvedValueOnce({ ok: true, json: async () => body });
    render(<LokiErrorViewer />, { wrapper });
    await waitFor(() => expect(screen.getByTestId('loki-unavailable')).toBeInTheDocument());
  });

  it('shows empty state when no errors found', async () => {
    const body: LogsApiResponse = { entries: [] };
    mockFetch.mockResolvedValueOnce({ ok: true, json: async () => body });
    render(<LokiErrorViewer />, { wrapper });
    await waitFor(() => expect(screen.getByTestId('loki-empty')).toBeInTheDocument());
  });

  it('renders log entries with container and message', async () => {
    const body: LogsApiResponse = {
      entries: [
        {
          id: '0-0',
          timestamp: new Date().toISOString(),
          container: 'meepleai-api',
          message: 'ERROR: database connection failed',
          level: 'error',
        },
      ],
    };
    mockFetch.mockResolvedValueOnce({ ok: true, json: async () => body });
    render(<LokiErrorViewer />, { wrapper });
    await waitFor(() => {
      expect(screen.getByTestId('loki-table')).toBeInTheDocument();
      expect(screen.getByText('meepleai-api')).toBeInTheDocument();
      expect(screen.getByText(/database connection failed/i)).toBeInTheDocument();
    });
  });

  it('shows error state when fetch fails', async () => {
    mockFetch.mockRejectedValueOnce(new Error('Network error'));
    render(<LokiErrorViewer />, { wrapper });
    await waitFor(() => expect(screen.getByTestId('loki-error')).toBeInTheDocument());
  });
});
