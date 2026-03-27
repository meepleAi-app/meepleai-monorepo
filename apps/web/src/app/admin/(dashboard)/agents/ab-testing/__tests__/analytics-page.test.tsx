import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import AbTestAnalyticsPage from '../results/page';

const mockGetAbTestAnalytics = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAbTestAnalytics: mockGetAbTestAnalytics,
    },
  },
}));

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  redirect: vi.fn(),
}));

// Mock recharts to avoid SVG rendering issues in jsdom
vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="responsive-container">{children}</div>
  ),
  BarChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="bar-chart">{children}</div>
  ),
  Bar: () => null,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
  RadarChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="radar-chart">{children}</div>
  ),
  Radar: () => null,
  PolarGrid: () => null,
  PolarAngleAxis: () => null,
  PolarRadiusAxis: () => null,
}));

const MOCK_ANALYTICS = {
  totalTests: 15,
  completedTests: 12,
  totalCost: 0.0456,
  modelWinRates: [
    { modelId: 'openai/gpt-4o', wins: 7, total: 12, winRate: 0.583 },
    { modelId: 'anthropic/claude-3-haiku', wins: 5, total: 12, winRate: 0.417 },
  ],
  modelAvgScores: [
    {
      modelId: 'openai/gpt-4o',
      avgAccuracy: 4.2,
      avgCompleteness: 4.0,
      avgClarity: 4.5,
      avgTone: 3.8,
      avgOverall: 4.125,
      evaluationCount: 7,
    },
    {
      modelId: 'anthropic/claude-3-haiku',
      avgAccuracy: 3.9,
      avgCompleteness: 4.3,
      avgClarity: 4.1,
      avgTone: 4.0,
      avgOverall: 4.075,
      evaluationCount: 5,
    },
  ],
};

const MOCK_EMPTY = {
  totalTests: 0,
  completedTests: 0,
  totalCost: 0,
  modelWinRates: [],
  modelAvgScores: [],
};

describe('AbTestAnalyticsPage', () => {
  const user = userEvent.setup();

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAbTestAnalytics.mockResolvedValue(MOCK_ANALYTICS);
  });

  it('renders page title and KPI cards', async () => {
    renderWithQuery(<AbTestAnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('Total Tests')).toBeInTheDocument();
    });

    expect(screen.getByText('A/B Test Analytics')).toBeInTheDocument();
    expect(screen.getByText('15')).toBeInTheDocument(); // total tests
    expect(screen.getByText('$0.0456')).toBeInTheDocument(); // cost
    expect(screen.getByText('80%')).toBeInTheDocument(); // completion rate
  });

  it('renders win rate chart', async () => {
    renderWithQuery(<AbTestAnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('Win Rate by Model')).toBeInTheDocument();
    });

    expect(screen.getByTestId('bar-chart')).toBeInTheDocument();
  });

  it('renders score radar chart', async () => {
    renderWithQuery(<AbTestAnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('Score Breakdown by Dimension')).toBeInTheDocument();
    });

    expect(screen.getByTestId('radar-chart')).toBeInTheDocument();
  });

  it('renders model leaderboard table', async () => {
    renderWithQuery(<AbTestAnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('Model Leaderboard')).toBeInTheDocument();
    });

    // Model names appear in both radar legend and leaderboard table
    expect(screen.getAllByText('gpt-4o').length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText('claude-3-haiku').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('4.13')).toBeInTheDocument(); // avgOverall for gpt-4o
  });

  it('shows empty state when no data', async () => {
    mockGetAbTestAnalytics.mockResolvedValue(MOCK_EMPTY);

    renderWithQuery(<AbTestAnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('No data yet')).toBeInTheDocument();
    });

    expect(
      screen.getByText('Run some A/B tests and evaluate them to see analytics here.')
    ).toBeInTheDocument();
    expect(screen.getByText('Create First Test')).toBeInTheDocument();
  });

  it('has date range filter inputs', async () => {
    renderWithQuery(<AbTestAnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('A/B Test Analytics')).toBeInTheDocument();
    });

    expect(screen.getByText('From')).toBeInTheDocument();
    expect(screen.getByText('To')).toBeInTheDocument();
  });

  it('passes date params to API when filters set', async () => {
    renderWithQuery(<AbTestAnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('A/B Test Analytics')).toBeInTheDocument();
    });

    // Initial call without dates
    expect(mockGetAbTestAnalytics).toHaveBeenCalledWith({
      dateFrom: undefined,
      dateTo: undefined,
    });

    const fromInput = screen.getAllByDisplayValue('')[0];
    await user.type(fromInput, '2026-01-01');

    await waitFor(() => {
      expect(mockGetAbTestAnalytics).toHaveBeenCalledWith(
        expect.objectContaining({ dateFrom: '2026-01-01' })
      );
    });
  });

  it('has link to create new test', async () => {
    renderWithQuery(<AbTestAnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('A/B Test Analytics')).toBeInTheDocument();
    });

    const newTestLink = screen.getByText('New Test').closest('a');
    expect(newTestLink).toHaveAttribute('href', '/admin/agents/ab-testing/new');
  });

  it('clears date filters when Clear button clicked', async () => {
    renderWithQuery(<AbTestAnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('Total Tests')).toBeInTheDocument();
    });

    const fromInput = screen.getByLabelText('From');
    await user.type(fromInput, '2026-01-01');

    await waitFor(() => {
      expect(screen.getByText('Clear')).toBeInTheDocument();
    });

    await user.click(screen.getByText('Clear'));

    await waitFor(() => {
      expect(screen.queryByText('Clear')).not.toBeInTheDocument();
    });
  });

  it('shows validation warning for invalid date range', async () => {
    renderWithQuery(<AbTestAnalyticsPage />);

    await waitFor(() => {
      expect(screen.getByText('Total Tests')).toBeInTheDocument();
    });

    const fromInput = screen.getByLabelText('From');
    const toInput = screen.getByLabelText('To');

    await user.type(toInput, '2026-01-01');
    await user.type(fromInput, '2026-12-31');

    await waitFor(() => {
      expect(screen.getByText('From date must be before To date')).toBeInTheDocument();
    });
  });
});
