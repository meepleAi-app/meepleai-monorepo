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

  it('renders primary tag only on the deepseek row (PR1 entity-token mapping)', async () => {
    renderWithQuery(<ProviderTable />);

    await waitFor(() => {
      expect(screen.getByTestId('provider-row-deepseek')).toBeInTheDocument();
    });
    expect(screen.getByTestId('provider-primary-tag-deepseek')).toHaveTextContent('primary');
    expect(screen.queryByTestId('provider-primary-tag-openrouter')).not.toBeInTheDocument();
    expect(screen.queryByTestId('provider-primary-tag-ollama-local')).not.toBeInTheDocument();
  });

  it('exposes aria-label on token + circuit status chips for screen readers (PR3 a11y)', async () => {
    renderWithQuery(<ProviderTable />);

    await waitFor(() => {
      expect(screen.getByTestId('provider-circuit-deepseek')).toBeInTheDocument();
    });
    // Circuit chips carry "Circuit breaker: {state}" aria-label
    expect(screen.getByTestId('provider-circuit-deepseek')).toHaveAttribute(
      'aria-label',
      expect.stringMatching(/circuit breaker:/i)
    );
    expect(screen.getByTestId('provider-circuit-openrouter')).toHaveAttribute(
      'aria-label',
      expect.stringMatching(/circuit breaker:/i)
    );
    // Token status chips: one per provider row (3 providers)
    const tokenChips = screen.getAllByLabelText(/token status:/i);
    expect(tokenChips.length).toBe(3);
  });

  it('renders entity-colored mark per provider (no shared gradient)', async () => {
    renderWithQuery(<ProviderTable />);

    await waitFor(() => {
      expect(screen.getByTestId('provider-mark-deepseek')).toBeInTheDocument();
    });
    const dsMark = screen.getByTestId('provider-mark-deepseek');
    const orMark = screen.getByTestId('provider-mark-openrouter');
    const ollMark = screen.getByTestId('provider-mark-ollama-local');

    // Each mark gets its tone class — bg-entity-{tool|chat|kb}
    expect(dsMark.className).toMatch(/bg-entity-tool/);
    expect(orMark.className).toMatch(/bg-entity-chat/);
    expect(ollMark.className).toMatch(/bg-entity-kb/);
  });
});
