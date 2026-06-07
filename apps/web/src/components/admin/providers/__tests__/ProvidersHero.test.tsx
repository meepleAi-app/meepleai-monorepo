import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ProvidersHero } from '../ProvidersHero';

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

describe('ProvidersHero (PR1 reduced — 2 KPI reali)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders 2 KPI: Servizi monitorati + Circuit health', async () => {
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
        state: 'Closed',
        tripCount: 0,
        lastTrippedAt: null,
        lastResetAt: null,
        lastError: null,
      },
      {
        serviceName: 'ollama-local',
        state: 'Open',
        tripCount: 5,
        lastTrippedAt: '2026-06-02T10:00:00Z',
        lastResetAt: null,
        lastError: 'timeout',
      },
    ]);

    renderWithQuery(<ProvidersHero />);

    await waitFor(() => {
      expect(screen.getByTestId('providers-hero')).toBeInTheDocument();
      expect(screen.getByTestId('providers-kpi-servizi-monitorati')).toHaveTextContent('3');
    });

    // Circuit health: 2 closed / 3 total + breakdown trend
    const health = screen.getByTestId('providers-kpi-circuit-health');
    expect(health).toHaveTextContent('2/3');
    expect(health).toHaveTextContent('1 open');
  });

  it('shows zero state when no circuit breakers registered', async () => {
    getCircuitBreakerStates.mockResolvedValue([]);
    renderWithQuery(<ProvidersHero />);

    await waitFor(() => {
      expect(screen.getByTestId('providers-kpi-servizi-monitorati')).toHaveTextContent('0');
    });
    expect(screen.getByTestId('providers-kpi-circuit-health')).toHaveTextContent('—');
  });

  it('no longer renders the deprecated BE-pending placeholder KPI', async () => {
    getCircuitBreakerStates.mockResolvedValue([]);
    renderWithQuery(<ProvidersHero />);

    await waitFor(() => {
      expect(screen.getByTestId('providers-hero')).toBeInTheDocument();
    });
    // 3 placeholder KPI (latency-p95, error-rate-24h, costo-24h) removed in PR1
    expect(screen.queryByTestId('providers-kpi-latency-p95')).not.toBeInTheDocument();
    expect(screen.queryByTestId('providers-kpi-error-rate-24h')).not.toBeInTheDocument();
    expect(screen.queryByTestId('providers-kpi-costo-24h')).not.toBeInTheDocument();
  });
});
