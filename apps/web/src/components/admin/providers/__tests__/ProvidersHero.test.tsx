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

describe('ProvidersHero', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows 4 KPI boxes including service count from circuit breakers', async () => {
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

    // Eventually shows the service count
    await waitFor(() => {
      expect(screen.getByTestId('providers-hero')).toBeInTheDocument();
      expect(screen.getByTestId('providers-kpi-servizi-monitorati')).toHaveTextContent('3');
    });

    // Confirms tripped count appears in trend
    expect(screen.getByTestId('providers-kpi-servizi-monitorati')).toHaveTextContent(
      '1 circuit open'
    );
  });

  it('shows "—" for BE-pending metrics with explanatory tooltip', () => {
    getCircuitBreakerStates.mockResolvedValue([]);
    renderWithQuery(<ProvidersHero />);

    const errorRate = screen.getByTestId('providers-kpi-error-rate-24h');
    expect(errorRate).toHaveTextContent('—');
    expect(errorRate).toHaveAttribute('title', expect.stringMatching(/backend/i));
  });
});
