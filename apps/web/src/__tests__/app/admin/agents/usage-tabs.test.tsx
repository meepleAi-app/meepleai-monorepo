import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/usage',
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getOpenRouterStatus: vi.fn().mockResolvedValue({}),
    getUsageTimeline: vi.fn().mockResolvedValue([]),
    getUsageCosts: vi.fn().mockResolvedValue({}),
    getUsageFreeQuota: vi.fn().mockResolvedValue({}),
    getRecentRequests: vi.fn().mockResolvedValue([]),
  }),
}));

// #1972 M4 batch: vitest v4 rejects arrow-mock used with `new`. Use empty class.
class MockHttpClient {}
vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: MockHttpClient,
}));

vi.mock('@/components/admin/agents/chat-history-filters', () => ({
  ChatHistoryFilters: () => <div data-testid="chat-filters">Filters</div>,
}));

vi.mock('@/components/admin/agents/chat-history-table', () => ({
  ChatHistoryTable: () => <div data-testid="chat-table">Table</div>,
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('Usage & Costs Tabs', () => {
  it('renders the OpenRouter and chat log tabs', async () => {
    const { default: UsagePage } = await import('@/app/admin/(dashboard)/agents/usage/page');
    render(<UsagePage />, { wrapper: Wrapper });
    expect(screen.getByRole('tab', { name: /openrouter/i })).toBeInTheDocument();
    // #4212: the «Saldo Token» tab read the token-tier tables, which nothing ever filled.
    expect(screen.queryByRole('tab', { name: /saldo token/i })).not.toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /log chat/i })).toBeInTheDocument();
  });
});
