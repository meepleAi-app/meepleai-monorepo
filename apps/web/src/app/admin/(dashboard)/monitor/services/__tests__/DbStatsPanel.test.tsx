/**
 * DbStatsPanel Component Tests
 * Issue #135 — DB Stats Overview for Service Dashboard
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const mockGetResourceDatabaseMetrics = vi.hoisted(() => vi.fn());
const mockGetResourceDatabaseTopTables = vi.hoisted(() => vi.fn());
const mockVacuumDatabase = vi.hoisted(() => vi.fn());
const mockToast = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getResourceDatabaseMetrics: mockGetResourceDatabaseMetrics,
      getResourceDatabaseTopTables: mockGetResourceDatabaseTopTables,
      vacuumDatabase: mockVacuumDatabase,
    },
  },
}));

vi.mock('@/hooks/useToast', () => ({
  useToast: () => ({
    toast: mockToast,
  }),
}));

import { DbStatsPanel } from '../DbStatsPanel';

function createMockMetrics() {
  return {
    sizeBytes: 1073741824,
    sizeFormatted: '1.0 GB',
    growthLast7Days: 52428800,
    growthLast30Days: 209715200,
    growthLast90Days: 524288000,
    activeConnections: 15,
    maxConnections: 100,
    transactionsCommitted: 50000,
    transactionsRolledBack: 50,
    measuredAt: new Date().toISOString(),
  };
}

function createMockTables() {
  return [
    {
      tableName: 'games',
      sizeBytes: 52428800,
      sizeFormatted: '50 MB',
      rowCount: 1500,
      indexSizeBytes: 10485760,
      indexSizeFormatted: '10 MB',
      totalSizeBytes: 62914560,
      totalSizeFormatted: '60 MB',
    },
    {
      tableName: 'users',
      sizeBytes: 20971520,
      sizeFormatted: '20 MB',
      rowCount: 500,
      indexSizeBytes: 5242880,
      indexSizeFormatted: '5 MB',
      totalSizeBytes: 26214400,
      totalSizeFormatted: '25 MB',
    },
  ];
}

describe('DbStatsPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ==================== Loading State ====================

  it('renders loading state initially', () => {
    mockGetResourceDatabaseMetrics.mockReturnValue(new Promise(() => {}));
    mockGetResourceDatabaseTopTables.mockReturnValue(new Promise(() => {}));
    render(<DbStatsPanel />);

    expect(screen.getByTestId('db-stats-loading')).toBeInTheDocument();
  });

  // ==================== Data Display ====================

  it('renders KPI cards with metrics data', async () => {
    mockGetResourceDatabaseMetrics.mockResolvedValue(createMockMetrics());
    mockGetResourceDatabaseTopTables.mockResolvedValue(createMockTables());
    render(<DbStatsPanel />);

    await waitFor(() => {
      expect(screen.getByTestId('db-kpi-size')).toBeInTheDocument();
    });

    expect(screen.getByTestId('db-kpi-connections')).toBeInTheDocument();
    expect(screen.getByTestId('db-kpi-transactions')).toBeInTheDocument();
    expect(screen.getByTestId('db-kpi-measured')).toBeInTheDocument();

    // Check size value
    expect(screen.getByText('1.0 GB')).toBeInTheDocument();
  });

  it('renders top tables', async () => {
    mockGetResourceDatabaseMetrics.mockResolvedValue(createMockMetrics());
    mockGetResourceDatabaseTopTables.mockResolvedValue(createMockTables());
    render(<DbStatsPanel />);

    await waitFor(() => {
      expect(screen.getByTestId('db-top-tables')).toBeInTheDocument();
    });

    expect(screen.getByTestId('db-table-row-games')).toBeInTheDocument();
    expect(screen.getByTestId('db-table-row-users')).toBeInTheDocument();
  });

  // ==================== Refresh ====================

  it('refresh button fetches new data', async () => {
    const user = userEvent.setup();
    mockGetResourceDatabaseMetrics.mockResolvedValue(createMockMetrics());
    mockGetResourceDatabaseTopTables.mockResolvedValue(createMockTables());
    render(<DbStatsPanel />);

    await waitFor(() => {
      expect(screen.getByTestId('db-refresh-btn')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('db-refresh-btn'));

    // Initial load + refresh = 2 calls each
    await waitFor(() => {
      expect(mockGetResourceDatabaseMetrics).toHaveBeenCalledTimes(2);
    });
    expect(mockGetResourceDatabaseTopTables).toHaveBeenCalledTimes(2);
  });

  // ==================== Vacuum ====================

  it('vacuum button shows inline confirmation and executes on confirm', async () => {
    const user = userEvent.setup();
    mockGetResourceDatabaseMetrics.mockResolvedValue(createMockMetrics());
    mockGetResourceDatabaseTopTables.mockResolvedValue(createMockTables());
    mockVacuumDatabase.mockResolvedValue(undefined);
    render(<DbStatsPanel />);

    await waitFor(() => {
      expect(screen.getByTestId('db-vacuum-btn')).toBeInTheDocument();
    });

    // Click vacuum shows inline confirmation
    await user.click(screen.getByTestId('db-vacuum-btn'));
    expect(screen.getByTestId('db-vacuum-confirm')).toBeInTheDocument();

    // Confirm executes vacuum
    await user.click(screen.getByTestId('db-vacuum-confirm-yes'));
    await waitFor(() => {
      expect(mockVacuumDatabase).toHaveBeenCalledWith(false);
    });
  });

  it('vacuum confirmation cancel hides confirmation', async () => {
    const user = userEvent.setup();
    mockGetResourceDatabaseMetrics.mockResolvedValue(createMockMetrics());
    mockGetResourceDatabaseTopTables.mockResolvedValue(createMockTables());
    render(<DbStatsPanel />);

    await waitFor(() => {
      expect(screen.getByTestId('db-vacuum-btn')).toBeInTheDocument();
    });

    await user.click(screen.getByTestId('db-vacuum-btn'));
    expect(screen.getByTestId('db-vacuum-confirm')).toBeInTheDocument();

    // Cancel hides confirmation
    await user.click(screen.getByTestId('db-vacuum-confirm-no'));
    expect(screen.queryByTestId('db-vacuum-confirm')).not.toBeInTheDocument();
    expect(mockVacuumDatabase).not.toHaveBeenCalled();
  });

  // ==================== Empty State ====================

  it('shows empty state when no metrics', async () => {
    mockGetResourceDatabaseMetrics.mockResolvedValue(null);
    mockGetResourceDatabaseTopTables.mockResolvedValue([]);
    render(<DbStatsPanel />);

    await waitFor(() => {
      expect(screen.getByTestId('db-stats-empty')).toBeInTheDocument();
    });
  });

  // ==================== Error Handling ====================

  it('shows toast on API error', async () => {
    mockGetResourceDatabaseMetrics.mockRejectedValue(new Error('Network error'));
    mockGetResourceDatabaseTopTables.mockRejectedValue(new Error('Network error'));
    render(<DbStatsPanel />);

    await waitFor(() => {
      expect(mockToast).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'Failed to load database metrics',
          variant: 'destructive',
        })
      );
    });
  });
});
