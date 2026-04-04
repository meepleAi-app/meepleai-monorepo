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

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({})),
}));

vi.mock('@/lib/api', () => ({
  api: {
    config: {
      getChatHistoryLimits: vi
        .fn()
        .mockResolvedValue({ free: 10, normal: 50, premium: 100, admin: 999 }),
      updateChatHistoryLimits: vi.fn().mockResolvedValue({}),
    },
  },
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('Configuration', () => {
  it('renders three tabs', async () => {
    const { default: ConfigPage } = await import('@/app/admin/(dashboard)/agents/config/page');
    render(<ConfigPage />, { wrapper: Wrapper });
    expect(screen.getByRole('tab', { name: /strategy/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /models/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /limits/i })).toBeInTheDocument();
  });
});
