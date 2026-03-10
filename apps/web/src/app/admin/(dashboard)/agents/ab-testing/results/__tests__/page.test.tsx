import React from 'react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import AbTestAnalyticsPage from '../page';

// ────── Mocks ──────

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/admin/agents/ab-testing/results',
}));

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

vi.mock('../../../NavConfig', () => ({
  AgentsNavConfig: () => null,
}));

// Mock recharts to avoid SVG rendering issues in jsdom
vi.mock('recharts', () => ({
  BarChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="bar-chart">{children}</div>
  ),
  Bar: () => null,
  XAxis: () => null,
  YAxis: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  RadarChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="radar-chart">{children}</div>
  ),
  Radar: () => null,
  PolarGrid: () => null,
  PolarAngleAxis: () => null,
  PolarRadiusAxis: () => null,
}));

const mockGetAbTestAnalytics = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAbTestAnalytics: (...args: unknown[]) => mockGetAbTestAnalytics(...args),
    },
  },
}));

// ────── Fixtures ──────

const mockAnalytics = {
  totalTests: 15,
  completedTests: 12,
  totalCost: 0.156,
  modelWinRates: [
    { modelId: 'openai/gpt-4o', wins: 6, total: 12, winRate: 0.5 },
    {
      modelId: 'anthropic/claude-3.5-sonnet',
      wins: 4,
      total: 12,
      winRate: 0.333,
    },
  ],
  modelAvgScores: [
    {
      modelId: 'openai/gpt-4o',
      avgAccuracy: 4.2,
      avgCompleteness: 3.8,
      avgClarity: 4.5,
      avgTone: 4.0,
      avgOverall: 4.125,
      evaluationCount: 12,
    },
    {
      modelId: 'anthropic/claude-3.5-sonnet',
      avgAccuracy: 4.0,
      avgCompleteness: 4.1,
      avgClarity: 4.3,
      avgTone: 4.2,
      avgOverall: 4.15,
      evaluationCount: 12,
    },
  ],
};

const emptyAnalytics = {
  totalTests: 0,
  completedTests: 0,
  totalCost: 0,
  modelWinRates: [],
  modelAvgScores: [],
};

// ────── Helpers ──────

function createWrapper() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
}

// ────── Tests ──────

describe('AbTestAnalyticsPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows loading state initially', () => {
    // Never resolve so the query stays in loading state
    mockGetAbTestAnalytics.mockReturnValue(new Promise(() => {}));

    render(<AbTestAnalyticsPage />, { wrapper: createWrapper() });

    // The Loader2 spinner renders with the animate-spin class
    const spinner = document.querySelector('.animate-spin');
    expect(spinner).toBeTruthy();
  });

  it('shows empty state with "No data yet" when totalTests is 0', async () => {
    mockGetAbTestAnalytics.mockResolvedValue(emptyAnalytics);

    render(<AbTestAnalyticsPage />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByText('No data yet')).toBeInTheDocument();
    });

    // "Create First Test" link pointing to the new test page
    const createLink = screen.getByText('Create First Test').closest('a');
    expect(createLink).toHaveAttribute('href', '/admin/agents/ab-testing/new');
  });

  it('shows KPI cards with correct values when data exists', async () => {
    mockGetAbTestAnalytics.mockResolvedValue(mockAnalytics);

    render(<AbTestAnalyticsPage />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByText('Total Tests')).toBeInTheDocument();
    });

    // Total Tests = 15
    expect(screen.getByText('15')).toBeInTheDocument();
    // Completed = 12 (also appears in evaluation count cells, so use getAllByText)
    const twelveElements = screen.getAllByText('12');
    expect(twelveElements.length).toBeGreaterThanOrEqual(1);
    // Total Cost = $0.1560
    expect(screen.getByText('$0.1560')).toBeInTheDocument();
    // Completion Rate = Math.round((12/15)*100) = 80%
    expect(screen.getByText('80%')).toBeInTheDocument();
  });

  it('renders page title "A/B Test Analytics"', async () => {
    mockGetAbTestAnalytics.mockResolvedValue(mockAnalytics);

    render(<AbTestAnalyticsPage />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByText('A/B Test Analytics')).toBeInTheDocument();
    });
  });

  it('shows "New Test" link button', async () => {
    mockGetAbTestAnalytics.mockResolvedValue(mockAnalytics);

    render(<AbTestAnalyticsPage />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByText('New Test')).toBeInTheDocument();
    });

    const newTestLink = screen.getByText('New Test').closest('a');
    expect(newTestLink).toHaveAttribute('href', '/admin/agents/ab-testing/new');
  });

  it('shows leaderboard table with model names when data exists', async () => {
    mockGetAbTestAnalytics.mockResolvedValue(mockAnalytics);

    render(<AbTestAnalyticsPage />, { wrapper: createWrapper() });

    await waitFor(() => {
      expect(screen.getByText('Model Leaderboard')).toBeInTheDocument();
    });

    // Model short names (after the "/" split)
    expect(screen.getByText('Model Leaderboard')).toBeInTheDocument();

    // Table headers
    expect(screen.getByText('Avg Score')).toBeInTheDocument();
    expect(screen.getByText('Accuracy')).toBeInTheDocument();
    expect(screen.getByText('Completeness')).toBeInTheDocument();
    expect(screen.getByText('Clarity')).toBeInTheDocument();
    expect(screen.getByText('Tone')).toBeInTheDocument();
    expect(screen.getByText('Evaluations')).toBeInTheDocument();

    // Model names appear in the table rows (short names extracted from modelId)
    // gpt-4o appears in both the table and possibly the chart legend; use getAllByText
    const gpt4oElements = screen.getAllByText('gpt-4o');
    expect(gpt4oElements.length).toBeGreaterThanOrEqual(1);

    const claudeElements = screen.getAllByText('claude-3.5-sonnet');
    expect(claudeElements.length).toBeGreaterThanOrEqual(1);
  });
});
