import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { CircuitBreakerGrid } from '../CircuitBreakerGrid';

const getCircuitBreakerStates = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getCircuitBreakerStates: (...args: unknown[]) => getCircuitBreakerStates(...args),
    },
  },
}));

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('CircuitBreakerGrid', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders one card per circuit breaker state', async () => {
    getCircuitBreakerStates.mockResolvedValue([
      {
        serviceName: 'deepseek',
        state: 'Closed',
        tripCount: 0,
        lastTrippedAt: null,
        lastResetAt: null,
        lastError: null,
      },
      {
        serviceName: 'openrouter',
        state: 'Open',
        tripCount: 5,
        lastTrippedAt: '2026-06-02T10:00:00Z',
        lastResetAt: null,
        lastError: 'timeout 30s',
      },
    ]);

    renderWithQuery(<CircuitBreakerGrid />);

    await waitFor(() => {
      expect(screen.getByTestId('circuit-card-deepseek')).toBeInTheDocument();
      expect(screen.getByTestId('circuit-card-openrouter')).toBeInTheDocument();
    });

    expect(screen.getByTestId('circuit-card-deepseek')).toHaveTextContent('Closed');
    expect(screen.getByTestId('circuit-card-openrouter')).toHaveTextContent('Open');
    expect(screen.getByTestId('circuit-card-openrouter')).toHaveTextContent('timeout 30s');
  });

  it('shows empty state when no circuit breakers registered', async () => {
    getCircuitBreakerStates.mockResolvedValue([]);
    renderWithQuery(<CircuitBreakerGrid />);

    await waitFor(() => {
      expect(screen.getByTestId('circuit-grid-empty')).toBeInTheDocument();
    });
  });

  it('shows issue count chip when at least one breaker is not Closed', async () => {
    getCircuitBreakerStates.mockResolvedValue([
      {
        serviceName: 'a',
        state: 'Closed',
        tripCount: 0,
        lastTrippedAt: null,
        lastResetAt: null,
        lastError: null,
      },
      {
        serviceName: 'b',
        state: 'Open',
        tripCount: 1,
        lastTrippedAt: null,
        lastResetAt: null,
        lastError: null,
      },
      {
        serviceName: 'c',
        state: 'Half-Open',
        tripCount: 1,
        lastTrippedAt: null,
        lastResetAt: null,
        lastError: null,
      },
    ]);

    renderWithQuery(<CircuitBreakerGrid />);

    await waitFor(() => {
      expect(screen.getByText(/2 issue/i)).toBeInTheDocument();
    });
  });
});
