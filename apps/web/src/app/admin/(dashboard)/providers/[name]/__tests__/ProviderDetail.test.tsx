import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ProviderDetail } from '../ProviderDetail';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getProviderQuota: vi.fn().mockResolvedValue(null),
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
});
