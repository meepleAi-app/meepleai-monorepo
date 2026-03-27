import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/inspector',
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getRagExecutions: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }),
    getRagExecutionById: vi.fn().mockResolvedValue(null),
    getRagExecutionStats: vi.fn().mockResolvedValue({
      totalExecutions: 0,
      avgLatencyMs: 0,
      errorRate: 0,
      cacheHitRate: 0,
      totalCost: 0,
    }),
  }),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn().mockImplementation(() => ({})),
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('RAG Inspector', () => {
  it('renders three tabs', async () => {
    const { default: InspectorPage } =
      await import('@/app/admin/(dashboard)/agents/inspector/page');
    render(<InspectorPage />, { wrapper: Wrapper });
    expect(screen.getByRole('tab', { name: /esecuzioni/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /pipeline/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /waterfall/i })).toBeInTheDocument();
  });

  it('defaults to Esecuzioni tab', async () => {
    const { default: InspectorPage } =
      await import('@/app/admin/(dashboard)/agents/inspector/page');
    render(<InspectorPage />, { wrapper: Wrapper });
    const tab = screen.getByRole('tab', { name: /esecuzioni/i });
    expect(tab).toHaveAttribute('data-state', 'active');
  });
});
