import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ProvidersList } from '../ProvidersList';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getProviderQuota: vi.fn().mockResolvedValue(null),
      probeProvider: vi.fn(),
      listKnownProviders: vi.fn(),
    },
  },
}));

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('ProvidersList', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders one card per known provider', () => {
    renderWithQuery(<ProvidersList />);
    expect(screen.getByText('openrouter')).toBeInTheDocument();
    expect(screen.getByText('deepseek')).toBeInTheDocument();
    expect(screen.getByText('ollama-local')).toBeInTheDocument();
  });
});
