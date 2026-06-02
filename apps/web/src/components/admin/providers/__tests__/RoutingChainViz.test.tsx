import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { RoutingChainViz } from '../RoutingChainViz';

const getLlmSystemConfig = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getLlmSystemConfig: (...args: unknown[]) => getLlmSystemConfig(...args),
    },
  },
}));

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('RoutingChainViz', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders one node per fallback chain entry', async () => {
    getLlmSystemConfig.mockResolvedValue({
      circuitBreakerFailureThreshold: 5,
      circuitBreakerOpenDurationSeconds: 60,
      circuitBreakerSuccessThreshold: 1,
      dailyBudgetUsd: 50,
      monthlyBudgetUsd: 1000,
      fallbackChainJson: JSON.stringify([
        { provider: 'deepseek', model: 'deepseek-chat', priority: 'primary' },
        { provider: 'openrouter', model: 'anthropic/claude-3.5-sonnet', priority: 'secondary' },
        { provider: 'ollama-local', model: 'llama3.1:8b-instruct', priority: 'standby' },
      ]),
      source: 'database',
      lastUpdatedAt: '2026-06-02T10:00:00+00:00',
      lastUpdatedByUserId: '00000000-0000-0000-0000-000000000001',
    });

    renderWithQuery(<RoutingChainViz />);

    await waitFor(() => {
      expect(screen.getByTestId('routing-chain-node-0')).toBeInTheDocument();
      expect(screen.getByTestId('routing-chain-node-1')).toBeInTheDocument();
      expect(screen.getByTestId('routing-chain-node-2')).toBeInTheDocument();
    });

    expect(screen.getByTestId('routing-chain-node-0')).toHaveTextContent('primary');
    expect(screen.getByTestId('routing-chain-node-0')).toHaveTextContent('deepseek');
    expect(screen.getByTestId('routing-chain-node-2')).toHaveTextContent('standby');
  });

  it('shows empty state when fallbackChainJson is invalid', async () => {
    getLlmSystemConfig.mockResolvedValue({
      circuitBreakerFailureThreshold: 5,
      circuitBreakerOpenDurationSeconds: 60,
      circuitBreakerSuccessThreshold: 1,
      dailyBudgetUsd: 50,
      monthlyBudgetUsd: 1000,
      fallbackChainJson: 'not-valid-json',
      source: 'appsettings',
      lastUpdatedAt: null,
      lastUpdatedByUserId: null,
    });

    renderWithQuery(<RoutingChainViz />);

    await waitFor(() => {
      expect(screen.getByTestId('routing-chain-empty')).toBeInTheDocument();
    });
  });

  it('shows empty state when fallbackChainJson is empty array', async () => {
    getLlmSystemConfig.mockResolvedValue({
      circuitBreakerFailureThreshold: 5,
      circuitBreakerOpenDurationSeconds: 60,
      circuitBreakerSuccessThreshold: 1,
      dailyBudgetUsd: 50,
      monthlyBudgetUsd: 1000,
      fallbackChainJson: '[]',
      source: 'appsettings',
      lastUpdatedAt: null,
      lastUpdatedByUserId: null,
    });

    renderWithQuery(<RoutingChainViz />);

    await waitFor(() => {
      expect(screen.getByTestId('routing-chain-empty')).toBeInTheDocument();
    });
  });
});
