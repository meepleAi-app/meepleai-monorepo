import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/config',
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getTierStrategyMatrix: vi.fn().mockResolvedValue({ tiers: [] }),
    getStrategyModelMappings: vi.fn().mockResolvedValue([]),
    getModelHealth: vi.fn().mockResolvedValue([]),
    getModelChangeHistory: vi.fn().mockResolvedValue([]),
  }),
}));

// #1972 M4 batch: vitest v4 rejects arrow-mock used with `new`. Use empty class.
class MockHttpClient {}
vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: MockHttpClient,
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('Configuration', () => {
  it('renders the strategy and models tabs', async () => {
    const { default: ConfigPage } = await import('@/app/admin/(dashboard)/agents/config/page');
    render(<ConfigPage />, { wrapper: Wrapper });
    expect(screen.getByRole('tab', { name: /strategy/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /models/i })).toBeInTheDocument();
    // #4212: the Limits tab saved chat-history keys nobody read; it is gone.
    expect(screen.queryByRole('tab', { name: /limits/i })).not.toBeInTheDocument();
  });
});
