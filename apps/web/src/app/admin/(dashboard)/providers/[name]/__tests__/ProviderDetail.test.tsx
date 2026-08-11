import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ProviderDetail } from '../ProviderDetail';

const getProviderQuota = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getProviderQuota,
      probeProvider: vi.fn(),
      listKnownProviders: vi.fn(),
    },
  },
}));

vi.mock('@/hooks/useAuthUser', () => ({
  useAuthUser: () => ({ user: null, loading: false }),
}));

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('ProviderDetail', () => {
  beforeEach(() => {
    getProviderQuota.mockReset();
    getProviderQuota.mockResolvedValue(null);
  });

  it('renders provider name + back link + Quota header', () => {
    renderWithQuery(<ProviderDetail name="openrouter" />);
    expect(screen.getByRole('heading', { name: 'openrouter' })).toBeInTheDocument();
    expect(screen.getByText(/torna alla lista/i)).toBeInTheDocument();
    expect(screen.getByText('Quota')).toBeInTheDocument();
  });

  it('shows "Probe richiede SuperAdmin" when not SuperAdmin', () => {
    renderWithQuery(<ProviderDetail name="deepseek" />);
    expect(screen.getByText(/Probe richiede privilegi SuperAdmin/i)).toBeInTheDocument();
  });

  it('shows explicit degraded message instead of an empty <dl> when quota fetch errors (#3045)', async () => {
    getProviderQuota.mockResolvedValue({
      providerName: 'deepseek',
      quotaSupported: true,
      tokenConfigured: true,
      usedUsd: null,
      limitUsd: null,
      remainingUsd: null,
      resetAt: null,
      errorCode: 'quota_fetch_failed',
      errorMessage: 'upstream 502',
      fetchedAt: '2026-01-01T00:00:00Z',
      cacheTtlSeconds: 0,
    });
    renderWithQuery(<ProviderDetail name="deepseek" />);

    expect(await screen.findByTestId('quota-degraded')).toHaveTextContent(
      /Sorgente non disponibile/
    );
    expect(screen.queryByText(/Aggiornato:/)).not.toBeInTheDocument();
  });

  it('renders the quota <dl> when data is real (errorCode null) (#3045 regression guard)', async () => {
    getProviderQuota.mockResolvedValue({
      providerName: 'deepseek',
      quotaSupported: true,
      tokenConfigured: true,
      usedUsd: 1.23,
      limitUsd: 10,
      remainingUsd: 8.77,
      resetAt: null,
      errorCode: null,
      errorMessage: null,
      fetchedAt: '2026-01-01T00:00:00Z',
      cacheTtlSeconds: 300,
    });
    renderWithQuery(<ProviderDetail name="deepseek" />);

    expect(await screen.findByText(/Aggiornato:/)).toBeInTheDocument();
    expect(screen.queryByTestId('quota-degraded')).not.toBeInTheDocument();
  });
});
