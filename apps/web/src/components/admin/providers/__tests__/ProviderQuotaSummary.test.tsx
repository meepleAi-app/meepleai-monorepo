import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ProviderQuotaSummary } from '../ProviderQuotaSummary';
import type { ProviderQuota } from '@/lib/api/schemas/providers';

const getProvidersQuota = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getProvidersQuota: (...args: unknown[]) => getProvidersQuota(...args),
    },
  },
}));

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

function quota(overrides: Partial<ProviderQuota> & { providerName: string }): ProviderQuota {
  return {
    providerName: overrides.providerName,
    quotaSupported: true,
    tokenConfigured: true,
    usedUsd: null,
    limitUsd: null,
    remainingUsd: null,
    resetAt: null,
    errorCode: null,
    errorMessage: null,
    fetchedAt: '2026-01-01T00:00:00Z',
    cacheTtlSeconds: 300,
    ...overrides,
  };
}

describe('ProviderQuotaSummary', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders remaining/used/limit numbers + total from a single aggregated response (#3043)', async () => {
    getProvidersQuota.mockResolvedValue([
      quota({ providerName: 'openrouter', usedUsd: 10, limitUsd: 40, remainingUsd: 30 }),
      quota({ providerName: 'deepseek', usedUsd: 4.5, limitUsd: 10, remainingUsd: 5.5 }),
    ]);

    renderWithQuery(<ProviderQuotaSummary />);

    await waitFor(() => expect(screen.getByTestId('quota-card-openrouter')).toBeInTheDocument());
    expect(screen.getByTestId('quota-card-deepseek')).toBeInTheDocument();
    // Numeri che la ProviderTable NON mostra (solo chip di stato).
    expect(screen.getByTestId('quota-card-openrouter')).toHaveTextContent('$30.00');
    expect(screen.getByTestId('quota-card-deepseek')).toHaveTextContent('$5.50');
    // Totale = 30 + 5.5.
    expect(screen.getByTestId('provider-quota-summary-total')).toHaveTextContent('$35.50');
  });

  it('shows loading state', () => {
    getProvidersQuota.mockReturnValue(new Promise(() => {}));
    renderWithQuery(<ProviderQuotaSummary />);
    expect(screen.getByTestId('provider-quota-summary-loading')).toBeInTheDocument();
  });

  it('shows error state with role=alert', async () => {
    getProvidersQuota.mockRejectedValue(new Error('boom'));
    renderWithQuery(<ProviderQuotaSummary />);
    await waitFor(() =>
      expect(screen.getByTestId('provider-quota-summary-error')).toBeInTheDocument()
    );
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('excludes a not-configured provider from the total and shows "—" (#3043)', async () => {
    getProvidersQuota.mockResolvedValue([
      quota({ providerName: 'openrouter', usedUsd: 10, limitUsd: 40, remainingUsd: 30 }),
      quota({ providerName: 'deepseek', tokenConfigured: false, errorCode: 'not_configured' }),
    ]);

    renderWithQuery(<ProviderQuotaSummary />);

    await waitFor(() => expect(screen.getByTestId('quota-card-deepseek')).toBeInTheDocument());
    expect(screen.getByTestId('quota-card-deepseek')).toHaveTextContent('no token');
    // Somma = solo openrouter (30), deepseek escluso.
    expect(screen.getByTestId('provider-quota-summary-total')).toHaveTextContent('$30.00');
  });
});
