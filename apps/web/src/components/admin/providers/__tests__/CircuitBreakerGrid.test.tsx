import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { CircuitBreakerGrid } from '../CircuitBreakerGrid';

const getCircuitBreakerStates = vi.fn();
const getLlmSystemConfig = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getCircuitBreakerStates: (...args: unknown[]) => getCircuitBreakerStates(...args),
      getLlmSystemConfig: (...args: unknown[]) => getLlmSystemConfig(...args),
    },
  },
}));

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

const DEFAULT_CONFIG = {
  circuitBreakerFailureThreshold: 5,
  circuitBreakerOpenDurationSeconds: 30,
  circuitBreakerSuccessThreshold: 1,
  dailyBudgetUsd: 50,
  monthlyBudgetUsd: 1000,
  fallbackChainJson: '[]',
  source: 'database' as const,
  lastUpdatedAt: null,
  lastUpdatedByUserId: null,
};

describe('CircuitBreakerGrid', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getLlmSystemConfig.mockResolvedValue(DEFAULT_CONFIG);
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

  describe('PR2 stat-grid layout + cooldown bar', () => {
    it('renders 2x2 stat-grid with Trips, Soglia, Ultima apertura, Reset', async () => {
      getCircuitBreakerStates.mockResolvedValue([
        {
          serviceName: 'deepseek',
          state: 'Closed',
          tripCount: 2,
          lastTrippedAt: null,
          lastResetAt: null,
          lastError: null,
        },
      ]);

      renderWithQuery(<CircuitBreakerGrid />);

      await waitFor(() => {
        expect(screen.getByTestId('circuit-card-statgrid-deepseek')).toBeInTheDocument();
      });
      const grid = screen.getByTestId('circuit-card-statgrid-deepseek');
      expect(grid).toHaveTextContent(/Trips/i);
      expect(grid).toHaveTextContent(/Soglia/i);
      expect(grid).toHaveTextContent(/Ultima apertura/i);
      expect(grid).toHaveTextContent(/Reset/i);
      // Soglia value from config
      expect(grid).toHaveTextContent('5');
    });

    it('renders policy meta in header when config loaded', async () => {
      getCircuitBreakerStates.mockResolvedValue([
        {
          serviceName: 'x',
          state: 'Closed',
          tripCount: 0,
          lastTrippedAt: null,
          lastResetAt: null,
          lastError: null,
        },
      ]);

      renderWithQuery(<CircuitBreakerGrid />);

      await waitFor(() => {
        expect(screen.getByTestId('cb-policy-meta')).toBeInTheDocument();
      });
      expect(screen.getByTestId('cb-policy-meta')).toHaveTextContent('5 fail');
      expect(screen.getByTestId('cb-policy-meta')).toHaveTextContent('cooldown 30s');
    });

    it('renders cooldown bar with countdown for Open state', async () => {
      // Use real wall clock: lastTrippedAt = now - 10s. With 30s cooldown,
      // ~20s should remain (give or take a few ms for test setup latency).
      const trippedMs = Date.now() - 10_000;
      getCircuitBreakerStates.mockResolvedValue([
        {
          serviceName: 'openrouter',
          state: 'Open',
          tripCount: 5,
          lastTrippedAt: new Date(trippedMs).toISOString(),
          lastResetAt: null,
          lastError: null,
        },
      ]);

      renderWithQuery(<CircuitBreakerGrid />);

      await waitFor(() => {
        expect(screen.getByTestId('cb-cooldown-bar')).toBeInTheDocument();
      });

      const remaining = screen.getByTestId('cb-cooldown-remaining');
      // 30 - 10 ≈ 20s (Math.ceil) — allow ±3s for test setup overhead
      const secs = Number(remaining.textContent?.replace('s', ''));
      expect(secs).toBeGreaterThanOrEqual(17);
      expect(secs).toBeLessThanOrEqual(20);
    });

    it('does NOT render cooldown bar for Closed state', async () => {
      getCircuitBreakerStates.mockResolvedValue([
        {
          serviceName: 'deepseek',
          state: 'Closed',
          tripCount: 0,
          lastTrippedAt: '2026-06-02T09:00:00Z',
          lastResetAt: '2026-06-02T09:30:00Z',
          lastError: null,
        },
      ]);

      renderWithQuery(<CircuitBreakerGrid />);

      await waitFor(() => {
        expect(screen.getByTestId('circuit-card-deepseek')).toBeInTheDocument();
      });
      expect(screen.queryByTestId('cb-cooldown-bar')).not.toBeInTheDocument();
    });

    it('exposes aria-label on state chip + issue counter for screen readers (PR3 a11y)', async () => {
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
          lastTrippedAt: null,
          lastResetAt: null,
          lastError: null,
        },
      ]);

      renderWithQuery(<CircuitBreakerGrid />);

      await waitFor(() => {
        expect(screen.getByTestId('circuit-card-deepseek')).toBeInTheDocument();
      });

      // State chips have "Circuit breaker state: {Open|Closed|...}" aria-label
      expect(screen.getByLabelText(/circuit breaker state: closed/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/circuit breaker state: open/i)).toBeInTheDocument();
      // Issue counter has role=status with descriptive aria-label
      expect(screen.getByLabelText(/circuit breaker.* require attention/i)).toBeInTheDocument();
    });

    it('does NOT render cooldown bar when lastTrippedAt is null even for Open state', async () => {
      getCircuitBreakerStates.mockResolvedValue([
        {
          serviceName: 'x',
          state: 'Open',
          tripCount: 1,
          lastTrippedAt: null, // edge case
          lastResetAt: null,
          lastError: null,
        },
      ]);

      renderWithQuery(<CircuitBreakerGrid />);

      await waitFor(() => {
        expect(screen.getByTestId('circuit-card-x')).toBeInTheDocument();
      });
      expect(screen.queryByTestId('cb-cooldown-bar')).not.toBeInTheDocument();
    });
  });

  afterEach(() => {
    vi.useRealTimers();
  });
});
