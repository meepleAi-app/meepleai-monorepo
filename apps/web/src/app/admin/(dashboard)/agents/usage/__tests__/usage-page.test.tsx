/**
 * Unit tests for /admin/agents/usage page (Issue #5077).
 * Covers KPI rendering, loading skeleton, and error state.
 */

import { screen, waitFor } from '@testing-library/react';
import { QueryClient } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import UsagePage from '../page';

// ─── Recharts mock (avoids canvas/SVG issues in jsdom) ────────────────────────

vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="responsive-container">{children}</div>
  ),
  ComposedChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="composed-chart">{children}</div>
  ),
  PieChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="pie-chart">{children}</div>
  ),
  Area: () => <div data-testid="area" />,
  Pie: () => <div data-testid="pie" />,
  Cell: () => <div data-testid="cell" />,
  XAxis: () => <div data-testid="x-axis" />,
  YAxis: () => <div data-testid="y-axis" />,
  CartesianGrid: () => <div data-testid="cartesian-grid" />,
  Tooltip: () => <div data-testid="tooltip" />,
  Legend: () => <div data-testid="legend" />,
}));

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockGetOpenRouterStatus = vi.hoisted(() => vi.fn());
const mockGetUsageTimeline = vi.hoisted(() => vi.fn());
const mockGetUsageCosts = vi.hoisted(() => vi.fn());
const mockGetUsageFreeQuota = vi.hoisted(() => vi.fn());
const mockGetRecentRequests = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getOpenRouterStatus: mockGetOpenRouterStatus,
    getUsageTimeline: mockGetUsageTimeline,
    getUsageCosts: mockGetUsageCosts,
    getUsageFreeQuota: mockGetUsageFreeQuota,
    getRecentRequests: mockGetRecentRequests,
  }),
  HttpClient: vi.fn(),
}));

vi.mock('@/lib/api/core/httpClient', () => ({
  HttpClient: vi.fn(() => ({})),
}));

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => vi.fn(),
}));

// ─── Fixtures ────────────────────────────────────────────────────────────────

const mockStatus = {
  balanceUsd: 4.5,
  dailySpendUsd: 0.0025,
  todayRequestCount: 42,
  currentRpm: 80,
  limitRpm: 200,
  utilizationPercent: 0.4,
  isThrottled: false,
  isFreeTier: false,
  rateLimitInterval: 'minute',
  lastUpdated: '2026-02-22T10:00:00Z',
};

const mockTimeline = {
  buckets: [],
  period: '24h',
  groupedByHour: true,
  totalRequests: 0,
  totalCostUsd: 0,
};

const mockCosts = {
  byModel: [],
  bySource: [],
  byTier: [],
  totalCostUsd: 0,
  totalRequests: 0,
  period: '7d',
};

const mockFreeQuota = {
  models: [],
  totalFreeRequestsToday: 0,
  generatedAt: '2026-02-22T10:00:00Z',
};

const mockRequests = {
  items: [],
  total: 0,
  page: 1,
  pageSize: 20,
  totalPages: 0,
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('UsagePage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetUsageTimeline.mockResolvedValue(mockTimeline);
    mockGetUsageCosts.mockResolvedValue(mockCosts);
    mockGetUsageFreeQuota.mockResolvedValue(mockFreeQuota);
    mockGetRecentRequests.mockResolvedValue(mockRequests);
  });

  it('renders page heading', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('Usage & Costs')).toBeInTheDocument();
    });
  });

  it('renders KPI card labels', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('Spend Today')).toBeInTheDocument();
    });

    expect(screen.getByText('Requests Today')).toBeInTheDocument();
    expect(screen.getByText('RPM')).toBeInTheDocument();
    expect(screen.getByText('Balance')).toBeInTheDocument();
  });

  it('displays formatted spend value', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    // dailySpendUsd = 0.0025 → "$0.0025"
    await waitFor(() => {
      expect(screen.getByText('$0.0025')).toBeInTheDocument();
    });
  });

  it('displays today request count', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('42')).toBeInTheDocument();
    });
  });

  it('shows RPM current / limit', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    // '80' appears in both the KPI card and the RateLimitGauge — either is fine
    await waitFor(() => {
      expect(screen.getAllByText('80').length).toBeGreaterThan(0);
    });

    expect(screen.getByText('/ 200')).toBeInTheDocument();
  });

  it('does not show throttled badge when not throttled', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('40.0% utilization')).toBeInTheDocument();
    });

    expect(screen.queryByText('Throttled')).not.toBeInTheDocument();
  });

  it('shows throttled badge when isThrottled = true', async () => {
    mockGetOpenRouterStatus.mockResolvedValue({ ...mockStatus, isThrottled: true });

    renderWithQuery(<UsagePage />);

    // Both the KPI card badge and the RateLimitGauge badge render "Throttled"
    await waitFor(() => {
      // Multiple components (KpiCards, RateLimitGauge) render "Throttled" — check at least one
      expect(screen.getAllByText('Throttled')[0]).toBeInTheDocument();
    });
  });

  it('shows Free Tier badge when isFreeTier = true', async () => {
    mockGetOpenRouterStatus.mockResolvedValue({ ...mockStatus, isFreeTier: true });

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('Free Tier')).toBeInTheDocument();
    });
  });

  it('shows loading skeletons initially', () => {
    mockGetOpenRouterStatus.mockReturnValue(new Promise(() => {})); // never resolves

    renderWithQuery(<UsagePage />);

    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows error banner on failure', async () => {
    mockGetOpenRouterStatus.mockRejectedValue(new Error('Redis unavailable'));

    // Component-level retry overrides test client defaults;
    // use a custom QueryClient with retryDelay: 0 to speed up retries.
    const fastClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
          staleTime: 0,
          refetchOnMount: false,
          refetchOnWindowFocus: false,
          refetchOnReconnect: false,
          retryDelay: 0,
        },
      },
    });

    renderWithQuery(<UsagePage />, { queryClient: fastClient });

    await waitFor(
      () => {
        expect(screen.getByRole('alert')).toBeInTheDocument();
      },
      { timeout: 10000 }
    );

    expect(screen.getByText(/Redis unavailable/i)).toBeInTheDocument();
  });

  it('shows charts section component headings', async () => {
    mockGetOpenRouterStatus.mockResolvedValue(mockStatus);

    renderWithQuery(<UsagePage />);

    await waitFor(() => {
      expect(screen.getByText('Request Timeline')).toBeInTheDocument();
    });

    expect(screen.getByText('Cost Breakdown')).toBeInTheDocument();
    expect(screen.getByText('Rate-Limit Utilization')).toBeInTheDocument();
    expect(screen.getByText('Free Tier Quota')).toBeInTheDocument();
    expect(screen.getAllByText('Recent Requests')[0]).toBeInTheDocument();
  });
});
