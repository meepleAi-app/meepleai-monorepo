import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ProviderTable } from '../ProviderTable';

const getProviderQuota = vi.fn();
const getCircuitBreakerStates = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getProviderQuota: (...args: unknown[]) => getProviderQuota(...args),
      getCircuitBreakerStates: (...args: unknown[]) => getCircuitBreakerStates(...args),
      probeProvider: vi.fn(),
    },
  },
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({
    data: { id: 'admin-id', email: 'admin@meepleai.test', role: 'admin', tier: 'plus' },
  }),
}));

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('ProviderTable', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getProviderQuota.mockResolvedValue({
      providerName: 'deepseek',
      quotaSupported: true,
      tokenConfigured: true,
      usedUsd: null,
      limitUsd: null,
      remainingUsd: 5.5,
      resetAt: null,
      errorCode: null,
      errorMessage: null,
      fetchedAt: '2026-06-02T10:00:00Z',
      cacheTtlSeconds: 300,
    });
    getCircuitBreakerStates.mockResolvedValue([
      {
        serviceName: 'deepseek-llm',
        state: 'Closed',
        tripCount: 0,
        lastTrippedAt: null,
        lastResetAt: null,
        lastError: null,
      },
      {
        serviceName: 'openrouter-llm',
        state: 'Half-Open',
        tripCount: 2,
        lastTrippedAt: null,
        lastResetAt: null,
        lastError: null,
      },
    ]);
  });

  it('renders one row per known provider with name + circuit state', async () => {
    renderWithQuery(<ProviderTable />);

    await waitFor(() => {
      expect(screen.getByTestId('providers-table')).toBeInTheDocument();
      expect(screen.getByTestId('provider-row-deepseek')).toBeInTheDocument();
      expect(screen.getByTestId('provider-row-openrouter')).toBeInTheDocument();
      expect(screen.getByTestId('provider-row-ollama-local')).toBeInTheDocument();
    });

    // Circuit chip wired from useCircuitBreakerStates (substring match) — wait for
    // breakers query to resolve and re-render before asserting chip content.
    await waitFor(() => {
      expect(screen.getByTestId('provider-circuit-deepseek')).toHaveTextContent('closed');
    });
    expect(screen.getByTestId('provider-circuit-openrouter')).toHaveTextContent('half-open');
    expect(screen.getByTestId('provider-circuit-ollama-local')).toHaveTextContent('unknown');
  });

  it('shows BE-pending placeholders for cross-provider metrics', async () => {
    renderWithQuery(<ProviderTable />);

    await waitFor(() => {
      const row = screen.getByTestId('provider-row-deepseek');
      // Latency, req 24h, errors all show "—"
      const cells = row.querySelectorAll('td');
      const placeholders = Array.from(cells).filter(c => c.textContent === '—');
      expect(placeholders.length).toBeGreaterThanOrEqual(3);
    });
  });
});
