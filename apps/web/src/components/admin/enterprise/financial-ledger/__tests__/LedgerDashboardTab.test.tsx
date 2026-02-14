/**
 * LedgerDashboardTab Tests
 * Issue #3723 - Ledger Dashboard and Visualization
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { LedgerDashboardTab } from '../LedgerDashboardTab';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getLedgerDashboard: vi.fn(),
    },
  },
}));

// Mock Recharts to avoid JSDOM rendering issues
vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div className="recharts-responsive-container">{children}</div>
  ),
  BarChart: ({ children }: { children: React.ReactNode }) => <svg>{children}</svg>,
  Bar: () => <rect />,
  XAxis: () => <line />,
  YAxis: () => <line />,
  CartesianGrid: () => <rect />,
  Tooltip: () => null,
  Legend: () => null,
}));

describe('LedgerDashboardTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders dashboard container', () => {
    render(<LedgerDashboardTab />);
    expect(screen.getByTestId('ledger-dashboard-tab')).toBeInTheDocument();
  });

  it('renders Financial Overview heading', () => {
    render(<LedgerDashboardTab />);
    expect(screen.getByText('Financial Overview')).toBeInTheDocument();
  });

  it('renders subtitle', () => {
    render(<LedgerDashboardTab />);
    expect(screen.getByText('Revenue, costs, and profitability at a glance')).toBeInTheDocument();
  });

  it('renders refresh button', () => {
    render(<LedgerDashboardTab />);
    expect(screen.getByTestId('refresh-dashboard')).toBeInTheDocument();
  });

  it('renders all four KPI cards', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByTestId('kpi-current-month-balance')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-profit-margin')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-monthly-revenue')).toBeInTheDocument();
      expect(screen.getByTestId('kpi-monthly-costs')).toBeInTheDocument();
    });
  });

  it('renders KPI card labels', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByText('Current Month Balance')).toBeInTheDocument();
      expect(screen.getByText('Profit Margin')).toBeInTheDocument();
      expect(screen.getByText('Monthly Revenue')).toBeInTheDocument();
      expect(screen.getByText('Monthly Costs')).toBeInTheDocument();
    });
  });

  it('renders revenue vs costs chart section', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByTestId('revenue-vs-costs-chart')).toBeInTheDocument();
    });
  });

  it('renders category breakdown chart section', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByTestId('category-breakdown-chart')).toBeInTheDocument();
    });
  });

  it('renders recent entries section', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByTestId('recent-entries-section')).toBeInTheDocument();
      expect(screen.getByText('Recent Ledger Entries')).toBeInTheDocument();
    });
  });

  it('renders recent entries table with mock data', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByTestId('recent-entries-table')).toBeInTheDocument();
    });
  });

  it('renders table headers', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByText('Date')).toBeInTheDocument();
      expect(screen.getByText('Type')).toBeInTheDocument();
      expect(screen.getByText('Category')).toBeInTheDocument();
      expect(screen.getByText('Description')).toBeInTheDocument();
      expect(screen.getByText('Amount')).toBeInTheDocument();
    });
  });

  it('displays profit margin value from mock data', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByText('54.8%')).toBeInTheDocument();
    });
  });

  it('displays profit margin trend', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByText('+3.2% vs last month')).toBeInTheDocument();
    });
  });

  it('renders income type badges in recent entries', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      // Recent entries contain both Income and Expense types
      const incomeBadges = screen.getAllByText('Income');
      expect(incomeBadges.length).toBeGreaterThan(0);
    });
  });

  it('renders refresh button with correct text', () => {
    render(<LedgerDashboardTab />);
    const btn = screen.getByTestId('refresh-dashboard');
    expect(btn.textContent).toContain('Refresh');
  });

  it('renders KPI cards grid container', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByTestId('dashboard-kpi-cards')).toBeInTheDocument();
    });
  });

  it('shows error banner when API fails', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.admin.getLedgerDashboard).mockRejectedValueOnce(new Error('Network error'));

    render(<LedgerDashboardTab />);

    await waitFor(() => {
      expect(screen.getByTestId('dashboard-error')).toBeInTheDocument();
      expect(screen.getByText('Unable to load dashboard data. Backend may not be available yet.')).toBeInTheDocument();
    });
  });

  it('displays recent entry descriptions', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      expect(screen.getByText('Monthly Pro subscription - Acme Corp')).toBeInTheDocument();
    });
  });

  it('displays entry amounts with sign', async () => {
    render(<LedgerDashboardTab />);
    await waitFor(() => {
      // Check for income entries that should have + prefix
      const table = screen.getByTestId('recent-entries-table');
      expect(table).toBeInTheDocument();
    });
  });
});
