import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/analytics',
  useSearchParams: () => new URLSearchParams(),
}));

// Analytics page fetches directly via fetch(), not @/lib/api
const mockMetrics = {
  totalInvocations: 0,
  totalTokensUsed: 0,
  totalCost: 0,
  avgLatencyMs: 0,
  avgConfidenceScore: 0,
  userSatisfactionRate: 0,
  topQueries: [],
  costBreakdown: [],
  usageOverTime: [],
};

vi.stubGlobal(
  'fetch',
  vi.fn().mockResolvedValue({
    ok: true,
    json: () => Promise.resolve(mockMetrics),
  })
);

// Mock chart components to avoid canvas/resize-observer deps in tests
vi.mock('@/components/admin/agents/UsageChart', () => ({
  UsageChart: () => <div data-testid="usage-chart" />,
}));

vi.mock('@/components/admin/agents/CostBreakdownChart', () => ({
  CostBreakdownChart: () => <div data-testid="cost-chart" />,
}));

vi.mock('@/components/admin/agents/TopAgentsTable', () => ({
  TopAgentsTable: () => <div data-testid="top-agents-table" />,
}));

vi.mock('@/components/admin/agents/MetricsKpiCards', () => ({
  MetricsKpiCards: () => <div data-testid="kpi-cards" />,
}));

// Mock the legacy metrics client type import source
vi.mock('@/app/(authenticated)/admin/agents/metrics/client', () => ({
  AgentMetrics: {},
  TopAgent: {},
}));

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('Analytics Tabs', () => {
  it('renders three tabs', async () => {
    const { default: AnalyticsPage } =
      await import('@/app/admin/(dashboard)/agents/analytics/page');
    render(<AnalyticsPage />, { wrapper: Wrapper });
    expect(screen.getByRole('tab', { name: /overview/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /top agents/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /trends/i })).toBeInTheDocument();
  });
});
