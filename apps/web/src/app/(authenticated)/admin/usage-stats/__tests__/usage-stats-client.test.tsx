/**
 * UsageStatsClient Tests (Issue #3728)
 *
 * Tests for the App Usage Dashboard:
 * - Loading / error states
 * - KPI cards rendering
 * - Period selector
 * - DAU/MAU trend chart
 * - Peak hours heatmap
 * - Feature adoption funnel
 * - Retention cohort table
 * - Session duration chart
 * - Geo distribution table
 * - Accessibility
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { UsageStatsClient } from '../client';

// Mock recharts to avoid canvas/SVG rendering issues in tests
vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="responsive-container">{children}</div>
  ),
  BarChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="bar-chart">{children}</div>
  ),
  LineChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="line-chart">{children}</div>
  ),
  Line: () => <div data-testid="line" />,
  Bar: () => <div data-testid="bar" />,
  XAxis: () => <div data-testid="x-axis" />,
  YAxis: () => <div data-testid="y-axis" />,
  CartesianGrid: () => <div data-testid="cartesian-grid" />,
  Tooltip: () => <div data-testid="tooltip" />,
  Cell: () => <div data-testid="cell" />,
  Legend: () => <div data-testid="legend" />,
}));

// Mock the hook
vi.mock('@/hooks/queries', () => ({
  useAppUsageStats: vi.fn(),
}));

import { useAppUsageStats } from '@/hooks/queries';

const mockRefetch = vi.fn();

const mockData = {
  engagement: {
    dau: 1247,
    mau: 8932,
    dauMauRatio: 0.14,
    avgSessionDurationMinutes: 12.4,
    totalSessions: 34521,
    bounceRate: 0.23,
  },
  retentionCohorts: [
    { cohortDate: '2026-01-13', cohortSize: 450, day1: 72, day7: 45, day14: 32, day30: 21 },
    { cohortDate: '2026-01-20', cohortSize: 520, day1: 68, day7: 41, day14: 29, day30: 18 },
  ],
  featureAdoption: [
    { featureName: 'AI Chat', uniqueUsers: 3420, totalUsages: 18750, adoptionRate: 38.3 },
    { featureName: 'PDF Upload', uniqueUsers: 2180, totalUsages: 5920, adoptionRate: 24.4 },
  ],
  geoDistribution: [
    { country: 'United States', countryCode: 'US', users: 3210, percentage: 35.9 },
    { country: 'Germany', countryCode: 'DE', users: 1450, percentage: 16.2 },
  ],
  sessionDurationDistribution: [
    { label: '0-1 min', count: 4521, percentage: 13.1 },
    { label: '1-5 min', count: 8932, percentage: 25.9 },
  ],
  dauMauTrend: [
    { date: '2026-02-01', dau: 1100, mau: 8200 },
    { date: '2026-02-02', dau: 1250, mau: 8400 },
    { date: '2026-02-03', dau: 1180, mau: 8500 },
  ],
  peakHours: [
    { hour: 9, dayOfWeek: 0, value: 50 },
    { hour: 10, dayOfWeek: 0, value: 65 },
    { hour: 18, dayOfWeek: 0, value: 80 },
    { hour: 9, dayOfWeek: 5, value: 30 },
    { hour: 3, dayOfWeek: 3, value: 5 },
  ],
  generatedAt: '2026-02-11T10:00:00Z',
};

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

function setupLoaded() {
  vi.mocked(useAppUsageStats).mockReturnValue({
    data: mockData,
    isLoading: false,
    isError: false,
    error: null,
    isFetching: false,
    refetch: mockRefetch,
  } as unknown as ReturnType<typeof useAppUsageStats>);
}

describe('UsageStatsClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ==== Loading State ====
  describe('loading state', () => {
    it('should show loading spinner when data is loading', () => {
      vi.mocked(useAppUsageStats).mockReturnValue({
        data: null,
        isLoading: true,
        isError: false,
        error: null,
        isFetching: true,
        refetch: mockRefetch,
      } as ReturnType<typeof useAppUsageStats>);

      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByTestId('usage-stats-loading')).toBeInTheDocument();
      expect(screen.getByText('Loading usage stats...')).toBeInTheDocument();
    });

    it('should not show KPI cards while loading', () => {
      vi.mocked(useAppUsageStats).mockReturnValue({
        data: null,
        isLoading: true,
        isError: false,
        error: null,
        isFetching: true,
        refetch: mockRefetch,
      } as ReturnType<typeof useAppUsageStats>);

      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.queryByTestId('usage-stats-kpis')).not.toBeInTheDocument();
    });
  });

  // ==== Error State ====
  describe('error state', () => {
    it('should show error message on fetch failure', () => {
      vi.mocked(useAppUsageStats).mockReturnValue({
        data: null,
        isLoading: false,
        isError: true,
        error: new Error('Network error'),
        isFetching: false,
        refetch: mockRefetch,
      } as ReturnType<typeof useAppUsageStats>);

      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(screen.getByText(/Failed to load usage stats/)).toBeInTheDocument();
    });
  });

  // ==== KPI Cards ====
  describe('KPI cards', () => {
    beforeEach(setupLoaded);

    it('should render 6 KPI cards', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByTestId('usage-stats-kpis')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-dau')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-mau')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-dau-mau-ratio')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-avg-session')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-total-sessions')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-bounce-rate')).toBeInTheDocument();
    });

    it('should display DAU value', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const dauCard = screen.getByTestId('kpi-dau');
      expect(within(dauCard).getByText('Daily Active Users')).toBeInTheDocument();
    });

    it('should display MAU value', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const mauCard = screen.getByTestId('kpi-mau');
      expect(within(mauCard).getByText('Monthly Active Users')).toBeInTheDocument();
    });

    it('should display DAU/MAU ratio as percentage', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const ratioCard = screen.getByTestId('kpi-dau-mau-ratio');
      expect(within(ratioCard).getByText('14.0%')).toBeInTheDocument();
      expect(within(ratioCard).getByText('Stickiness')).toBeInTheDocument();
    });

    it('should display average session duration', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const sessionCard = screen.getByTestId('kpi-avg-session');
      expect(within(sessionCard).getByText('12.4m')).toBeInTheDocument();
    });

    it('should display bounce rate', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const bounceCard = screen.getByTestId('kpi-bounce-rate');
      expect(within(bounceCard).getByText('23.0%')).toBeInTheDocument();
    });
  });

  // ==== Period Selector ====
  describe('period selector', () => {
    beforeEach(setupLoaded);

    it('should render period selector with 3 options', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByText('Last 7 days')).toBeInTheDocument();
      expect(screen.getByText('Last 30 days')).toBeInTheDocument();
      expect(screen.getByText('Last 90 days')).toBeInTheDocument();
    });

    it('should default to 30d period', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const selected = screen.getByRole('radio', { checked: true });
      expect(selected).toHaveTextContent('Last 30 days');
    });

    it('should call hook with new period when changed', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      fireEvent.click(screen.getByText('Last 7 days'));

      expect(useAppUsageStats).toHaveBeenCalledWith('7d');
    });

    it('should call hook with 90d when selected', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      fireEvent.click(screen.getByText('Last 90 days'));

      expect(useAppUsageStats).toHaveBeenCalledWith('90d');
    });
  });

  // ==== DAU/MAU Trend Chart ====
  describe('DAU/MAU trend chart', () => {
    beforeEach(setupLoaded);

    it('should render DAU/MAU trend chart section', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByTestId('dau-mau-trend-chart')).toBeInTheDocument();
    });

    it('should display chart title', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const chartSection = screen.getByTestId('dau-mau-trend-chart');
      expect(within(chartSection).getByText('DAU / MAU Trend')).toBeInTheDocument();
    });

    it('should render line chart mock', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const chartSection = screen.getByTestId('dau-mau-trend-chart');
      expect(within(chartSection).getByTestId('line-chart')).toBeInTheDocument();
    });
  });

  // ==== Peak Hours Heatmap ====
  describe('peak hours heatmap', () => {
    beforeEach(setupLoaded);

    it('should render peak hours heatmap section', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByTestId('peak-hours-heatmap')).toBeInTheDocument();
    });

    it('should display heatmap title', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const heatmap = screen.getByTestId('peak-hours-heatmap');
      expect(within(heatmap).getByText('Peak Activity Hours')).toBeInTheDocument();
    });

    it('should render 7 day rows', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      for (let i = 0; i < 7; i++) {
        expect(screen.getByTestId(`heatmap-row-${i}`)).toBeInTheDocument();
      }
    });

    it('should show day labels', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const heatmap = screen.getByTestId('peak-hours-heatmap');
      expect(within(heatmap).getByText('Mon')).toBeInTheDocument();
      expect(within(heatmap).getByText('Fri')).toBeInTheDocument();
      expect(within(heatmap).getByText('Sun')).toBeInTheDocument();
    });

    it('should display Low/High legend', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const heatmap = screen.getByTestId('peak-hours-heatmap');
      expect(within(heatmap).getByText('Low')).toBeInTheDocument();
      expect(within(heatmap).getByText('High')).toBeInTheDocument();
    });

    it('should have gridcell roles with aria-labels', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const gridcells = screen.getAllByRole('gridcell');
      expect(gridcells.length).toBeGreaterThan(0);
      expect(gridcells[0]).toHaveAttribute('aria-label');
    });
  });

  // ==== Feature Adoption Funnel ====
  describe('feature adoption funnel', () => {
    beforeEach(setupLoaded);

    it('should render feature adoption funnel section', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByTestId('feature-adoption-funnel')).toBeInTheDocument();
    });

    it('should display funnel title', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const funnel = screen.getByTestId('feature-adoption-funnel');
      expect(within(funnel).getByText('Feature Adoption Funnel')).toBeInTheDocument();
    });

    it('should render funnel items sorted by adoption rate descending', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const item0 = screen.getByTestId('funnel-item-0');
      const item1 = screen.getByTestId('funnel-item-1');

      // AI Chat (38.3%) should come first, PDF Upload (24.4%) second
      expect(within(item0).getByText('AI Chat')).toBeInTheDocument();
      expect(within(item0).getByText('38.3%')).toBeInTheDocument();
      expect(within(item1).getByText('PDF Upload')).toBeInTheDocument();
    });

    it('should display user counts', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const funnel = screen.getByTestId('feature-adoption-funnel');
      expect(within(funnel).getByText(/3,?420 users/)).toBeInTheDocument();
      expect(within(funnel).getByText(/2,?180 users/)).toBeInTheDocument();
    });
  });

  // ==== Session Duration Chart ====
  describe('session duration chart', () => {
    beforeEach(setupLoaded);

    it('should render session duration chart section', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByTestId('session-duration-chart')).toBeInTheDocument();
      expect(screen.getByText('Session Duration Distribution')).toBeInTheDocument();
    });
  });

  // ==== Retention Cohort Table ====
  describe('retention cohort table', () => {
    beforeEach(setupLoaded);

    it('should render retention cohort table', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByTestId('retention-cohort-table')).toBeInTheDocument();
      expect(screen.getByText('Retention Cohorts')).toBeInTheDocument();
    });

    it('should display cohort headers', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const table = screen.getByTestId('retention-cohort-table');
      expect(within(table).getByText('Day 1')).toBeInTheDocument();
      expect(within(table).getByText('Day 7')).toBeInTheDocument();
      expect(within(table).getByText('Day 14')).toBeInTheDocument();
      expect(within(table).getByText('Day 30')).toBeInTheDocument();
    });

    it('should render cohort rows', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const table = screen.getByTestId('retention-cohort-table');
      expect(within(table).getByText('450')).toBeInTheDocument();
      expect(within(table).getByText('520')).toBeInTheDocument();
    });

    it('should display retention percentages', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const table = screen.getByTestId('retention-cohort-table');
      expect(within(table).getByText('72%')).toBeInTheDocument();
      expect(within(table).getByText('45%')).toBeInTheDocument();
    });
  });

  // ==== Geo Distribution ====
  describe('geo distribution table', () => {
    beforeEach(setupLoaded);

    it('should render geo distribution table', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByTestId('geo-distribution-table')).toBeInTheDocument();
      expect(screen.getByText('Geographic Distribution')).toBeInTheDocument();
    });

    it('should display countries', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByText('United States')).toBeInTheDocument();
      expect(screen.getByText('Germany')).toBeInTheDocument();
    });

    it('should display user percentages', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByText('35.9%')).toBeInTheDocument();
      expect(screen.getByText('16.2%')).toBeInTheDocument();
    });

    it('should have progress bars with aria labels', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const progressBars = screen.getAllByRole('progressbar');
      expect(progressBars.length).toBe(2);
      expect(progressBars[0]).toHaveAttribute('aria-label', 'United States: 35.9%');
    });
  });

  // ==== Refresh Button ====
  describe('refresh button', () => {
    it('should call refetch when clicked', () => {
      setupLoaded();
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      fireEvent.click(screen.getByLabelText('Refresh data'));

      expect(mockRefetch).toHaveBeenCalledTimes(1);
    });

    it('should be disabled while fetching', () => {
      vi.mocked(useAppUsageStats).mockReturnValue({
        data: mockData,
        isLoading: false,
        isError: false,
        error: null,
        isFetching: true,
        refetch: mockRefetch,
      } as unknown as ReturnType<typeof useAppUsageStats>);

      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByLabelText('Refresh data')).toBeDisabled();
    });
  });

  // ==== Timestamp ====
  describe('data generation timestamp', () => {
    it('should show generated at timestamp', () => {
      setupLoaded();
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByTestId('usage-stats-generated-at')).toBeInTheDocument();
    });
  });

  // ==== Accessibility ====
  describe('accessibility', () => {
    beforeEach(setupLoaded);

    it('should have role="radiogroup" on period selector', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByRole('radiogroup', { name: 'Time period' })).toBeInTheDocument();
    });

    it('should have role="radio" on period buttons', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const radios = screen.getAllByRole('radio');
      expect(radios).toHaveLength(3);
    });

    it('should have role="table" on data tables', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const tables = screen.getAllByRole('table');
      expect(tables.length).toBeGreaterThanOrEqual(2);
    });

    it('should have role="alert" on error state', () => {
      vi.mocked(useAppUsageStats).mockReturnValue({
        data: null,
        isLoading: false,
        isError: true,
        error: new Error('Network error'),
        isFetching: false,
        refetch: mockRefetch,
      } as ReturnType<typeof useAppUsageStats>);

      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    it('should have aria-label on refresh button', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByLabelText('Refresh data')).toBeInTheDocument();
    });

    it('should have sr-only label for distribution bar column', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByText('Distribution bar')).toHaveClass('sr-only');
    });

    it('should have aria-labels on heatmap grid cells', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      const gridcells = screen.getAllByRole('gridcell');
      expect(gridcells.length).toBeGreaterThan(0);
      // Check first cell has proper aria-label format
      expect(gridcells[0].getAttribute('aria-label')).toMatch(/\w+ \d+:00: \d+ sessions/);
    });
  });

  // ==== Mock Data Fallback ====
  describe('mock data fallback', () => {
    it('should use mock data when API returns null', () => {
      vi.mocked(useAppUsageStats).mockReturnValue({
        data: null,
        isLoading: false,
        isError: false,
        error: null,
        isFetching: false,
        refetch: mockRefetch,
      } as unknown as ReturnType<typeof useAppUsageStats>);

      render(<UsageStatsClient />, { wrapper: createWrapper() });

      // Should still render all sections from mock data
      expect(screen.getByTestId('usage-stats-kpis')).toBeInTheDocument();
      expect(screen.getByTestId('dau-mau-trend-chart')).toBeInTheDocument();
      expect(screen.getByTestId('peak-hours-heatmap')).toBeInTheDocument();
      expect(screen.getByTestId('feature-adoption-funnel')).toBeInTheDocument();
    });
  });

  // ==== Page Title ====
  describe('page title', () => {
    beforeEach(setupLoaded);

    it('should display the dashboard title', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByText('App Usage Dashboard')).toBeInTheDocument();
    });

    it('should display the dashboard description', () => {
      render(<UsageStatsClient />, { wrapper: createWrapper() });

      expect(screen.getByText(/DAU\/MAU trends, peak hours/)).toBeInTheDocument();
    });
  });
});
