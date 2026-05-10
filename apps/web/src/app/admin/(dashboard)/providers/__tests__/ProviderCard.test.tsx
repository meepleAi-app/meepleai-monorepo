import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ProviderCard } from '../ProviderCard';

const getProviderQuota = vi.fn();
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getProviderQuota: (...args: unknown[]) => getProviderQuota(...args),
      probeProvider: vi.fn(),
      listKnownProviders: vi.fn(),
    },
  },
}));

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('ProviderCard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows remainingUsd when quotaSupported and tokenConfigured', async () => {
    getProviderQuota.mockResolvedValue({
      providerName: 'deepseek',
      quotaSupported: true,
      tokenConfigured: true,
      usedUsd: null,
      limitUsd: null,
      remainingUsd: 1.36,
      resetAt: null,
      errorCode: null,
      errorMessage: null,
      fetchedAt: '2026-05-10T13:50:26Z',
      cacheTtlSeconds: 300,
    });

    renderWithQuery(<ProviderCard name="deepseek" />);
    await waitFor(() => expect(screen.getByText(/\$1\.36/)).toBeInTheDocument());
    expect(screen.getByText('OK')).toBeInTheDocument();
  });

  it('shows "Quota n/d" badge when quotaSupported is false', async () => {
    getProviderQuota.mockResolvedValue({
      providerName: 'ollama-local',
      quotaSupported: false,
      tokenConfigured: false,
      usedUsd: null,
      limitUsd: null,
      remainingUsd: null,
      resetAt: null,
      errorCode: 'quota_not_supported',
      errorMessage: 'Provider does not expose a public quota API',
      fetchedAt: '2026-05-10T13:50:26Z',
      cacheTtlSeconds: 0,
    });

    renderWithQuery(<ProviderCard name="ollama-local" />);
    await waitFor(() => expect(screen.getByText('Quota n/d')).toBeInTheDocument());
    expect(screen.getByText(/non supportato dal provider/i)).toBeInTheDocument();
  });
});
