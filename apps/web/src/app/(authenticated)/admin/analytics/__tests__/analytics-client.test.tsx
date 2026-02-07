/**
 * AdminPageClient (Analytics) Component Tests - Issue #2208
 *
 * Tests for analytics dashboard with:
 * - Dashboard metrics display
 * - Filter controls (days, role)
 * - Auto-refresh functionality
 * - Export functionality (CSV, JSON)
 * - Error handling
 * - Chart rendering
 *
 * Coverage target: 90%+ statements, 75%+ branches
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AdminPageClient } from '../client';
import * as apiModule from '@/lib/api';

// Mock next/navigation for App Router
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    back: vi.fn(),
  }),
  usePathname: () => '/admin/analytics',
}));

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// Mock useAuthUser hook
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: vi.fn(() => ({
    user: {
      id: 'user-123',
      email: 'admin@example.com',
      role: 'Admin',
      displayName: 'Admin User',
    },
    loading: false,
  })),
}));

// Mock AdminAuthGuard to just render children
vi.mock('@/components/admin/AdminAuthGuard', () => ({
  AdminAuthGuard: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

// Mock recharts to avoid SVG rendering issues in tests
vi.mock('recharts', () => ({
  LineChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="line-chart">{children}</div>
  ),
  Line: () => <div data-testid="line" />,
  XAxis: () => <div data-testid="x-axis" />,
  YAxis: () => <div data-testid="y-axis" />,
  CartesianGrid: () => <div data-testid="cartesian-grid" />,
  Tooltip: () => <div data-testid="tooltip" />,
  Legend: () => <div data-testid="legend" />,
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="responsive-container">{children}</div>
  ),
}));

// Mock the API module
vi.mock('@/lib/api', async importOriginal => {
  const original = await importOriginal<typeof import('@/lib/api')>();
  return {
    ...original,
    api: {
      ...original.api,
      admin: {
        ...original.api.admin,
        getAnalytics: vi.fn(),
      },
    },
  };
});

// Mock URL.createObjectURL and revokeObjectURL
vi.stubGlobal('URL', {
  ...URL,
  createObjectURL: vi.fn(() => 'blob:mock-url'),
  revokeObjectURL: vi.fn(),
});

// Mock fetch - will be set up in beforeEach
let mockFetch: ReturnType<typeof vi.fn>;

const mockAnalyticsData = {
  metrics: {
    totalUsers: 1247,
    activeSessions: 42,
    apiRequestsToday: 3456,
    totalPdfDocuments: 847,
    totalChatMessages: 15234,
    averageConfidenceScore: 0.942,
    totalRagRequests: 18547,
    totalTokensUsed: 15700000,
    tokenBalanceEur: 157.50,
    tokenLimitEur: 200.00,
    dbStorageGb: 2.45,
    dbStorageLimitGb: 10.00,
    dbGrowthMbPerDay: 15.3,
    cacheHitRatePercent: 87.5,
    cacheHitRateTrendPercent: 2.3,
  },
  userTrend: [
    { date: '2025-12-01', count: 100, averageValue: null },
    { date: '2025-12-02', count: 120, averageValue: null },
    { date: '2025-12-03', count: 115, averageValue: null },
  ],
  sessionTrend: [
    { date: '2025-12-01', count: 50, averageValue: null },
    { date: '2025-12-02', count: 65, averageValue: null },
    { date: '2025-12-03', count: 58, averageValue: null },
  ],
  apiRequestTrend: [
    { date: '2025-12-01', count: 1000, averageValue: 150 },
    { date: '2025-12-02', count: 1200, averageValue: 160 },
    { date: '2025-12-03', count: 1100, averageValue: 155 },
  ],
  pdfUploadTrend: [
    { date: '2025-12-01', count: 25, averageValue: 10 },
    { date: '2025-12-02', count: 30, averageValue: 12 },
    { date: '2025-12-03', count: 28, averageValue: 11 },
  ],
  chatMessageTrend: [
    { date: '2025-12-01', count: 500, averageValue: null },
    { date: '2025-12-02', count: 600, averageValue: null },
    { date: '2025-12-03', count: 550, averageValue: null },
  ],
  generatedAt: '2025-12-19T10:00:00Z',
};

describe('AdminPageClient (Analytics)', () => {
  const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ shouldAdvanceTime: true });
    // Create fresh mock for each test
    mockFetch = vi.fn();
    vi.stubGlobal('fetch', mockFetch);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('Rendering', () => {
    it('renders loading state initially', () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      render(<AdminPageClient />);

      // Loading skeleton should be visible
      expect(document.querySelector('.animate-pulse')).toBeInTheDocument();
    });

    it('renders page title after data loads', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });
    });

    it('renders back navigation link', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('← Back to Users')).toBeInTheDocument();
      });
    });

    it('renders last updated timestamp', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText(/Last updated:/)).toBeInTheDocument();
      });
    });
  });

  describe('Metrics Display', () => {
    it('displays all 8 metric cards', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Total Users')).toBeInTheDocument();
        expect(screen.getByText('Active Sessions')).toBeInTheDocument();
        expect(screen.getByText('API Requests Today')).toBeInTheDocument();
        expect(screen.getByText('Total PDF Documents')).toBeInTheDocument();
        expect(screen.getByText('Total Chat Messages')).toBeInTheDocument();
        expect(screen.getByText('Avg Confidence Score')).toBeInTheDocument();
        expect(screen.getByText('Total RAG Requests')).toBeInTheDocument();
        expect(screen.getByText('Total Tokens Used')).toBeInTheDocument();
      });
    });

    it('formats numbers with locale formatting', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        // Total Users: 1247 -> 1,247 or 1.247 depending on locale
        expect(screen.getByText(/1[,.\s]?247/)).toBeInTheDocument();
      });
    });

    it('formats confidence score as percentage', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        // 0.942 -> 94.2%
        expect(screen.getByText('94.2%')).toBeInTheDocument();
      });
    });

    it('displays emoji icons for each metric', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('👥')).toBeInTheDocument(); // Total Users
        expect(screen.getByText('🔗')).toBeInTheDocument(); // Active Sessions
        expect(screen.getByText('📊')).toBeInTheDocument(); // API Requests
        expect(screen.getByText('📄')).toBeInTheDocument(); // PDF Documents
      });
    });
  });

  describe('Filter Controls', () => {
    it('renders time period selector with default value', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        const selects = screen.getAllByRole('combobox');
        const timeSelect = selects.find(s => (s as HTMLSelectElement).value === '30');
        expect(timeSelect).toBeInTheDocument();
      });
    });

    it('renders role filter selector', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        const selects = screen.getAllByRole('combobox');
        const roleSelect = selects.find(s => (s as HTMLSelectElement).value === 'all');
        expect(roleSelect).toBeInTheDocument();
      });
    });

    it('changes time period and refetches data', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      // Find time period select (first select with value '30')
      const selects = screen.getAllByRole('combobox');
      const timeSelect = selects.find(s => (s as HTMLSelectElement).value === '30');

      if (timeSelect) {
        await user.selectOptions(timeSelect, '7');

        await waitFor(() => {
          expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(2);
        });
      }
    });

    it('changes role filter and refetches data', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      // Find role filter select (select with value 'all')
      const selects = screen.getAllByRole('combobox');
      const roleSelect = selects.find(s => (s as HTMLSelectElement).value === 'all');

      if (roleSelect) {
        await user.selectOptions(roleSelect, 'Admin');

        await waitFor(() => {
          expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledWith(
            expect.objectContaining({ roleFilter: 'Admin' })
          );
        });
      }
    });
  });

  describe('Refresh Functionality', () => {
    it('renders refresh button', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        // Use exact name match to avoid matching "auto-refresh"
        expect(screen.getByRole('button', { name: /^refresh$/i })).toBeInTheDocument();
      });
    });

    it('refetches data on refresh button click', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      // Use exact name match to avoid matching "auto-refresh"
      const refreshButton = screen.getByRole('button', { name: /^refresh$/i });
      await user.click(refreshButton);

      await waitFor(() => {
        expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(2);
      });
    });

    it('renders auto-refresh toggle button', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /auto-refresh on/i })).toBeInTheDocument();
      });
    });

    it('toggles auto-refresh on button click', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      const autoRefreshButton = screen.getByRole('button', { name: /auto-refresh on/i });
      await user.click(autoRefreshButton);

      expect(screen.getByRole('button', { name: /auto-refresh off/i })).toBeInTheDocument();
    });

    it('auto-refreshes every 30 seconds when enabled', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      // Initial call
      expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(1);

      // Advance time by 30 seconds
      await vi.advanceTimersByTimeAsync(30000);

      await waitFor(() => {
        expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(2);
      });
    });

    it('stops auto-refresh when disabled', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      // Disable auto-refresh
      const autoRefreshButton = screen.getByRole('button', { name: /auto-refresh on/i });
      await user.click(autoRefreshButton);

      // Clear call count
      vi.mocked(apiModule.api.admin.getAnalytics).mockClear();

      // Advance time by 30 seconds
      await vi.advanceTimersByTimeAsync(30000);

      // Should not have been called again
      expect(apiModule.api.admin.getAnalytics).not.toHaveBeenCalled();
    });
  });

  describe('Export Functionality', () => {
    it('renders export CSV button', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /export csv/i })).toBeInTheDocument();
      });
    });

    it('renders export JSON button', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /export json/i })).toBeInTheDocument();
      });
    });

    it('calls export API endpoint on CSV export click', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
      mockFetch.mockResolvedValue({
        ok: true,
        blob: () => Promise.resolve(new Blob(['test,data'], { type: 'text/csv' })),
      });

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      const exportButton = screen.getByRole('button', { name: /export csv/i });
      await user.click(exportButton);

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalled();
        const [url, options] = mockFetch.mock.calls[0];
        expect(url).toContain('/api/v1/admin/analytics/export');
        expect(options.method).toBe('POST');
        expect(options.body).toContain('"format":"csv"');
      });
    });

    it('calls export API endpoint on JSON export click', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
      mockFetch.mockResolvedValue({
        ok: true,
        blob: () => Promise.resolve(new Blob(['{"test":"data"}'], { type: 'application/json' })),
      });

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      const exportButton = screen.getByRole('button', { name: /export json/i });
      await user.click(exportButton);

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalled();
        const [url, options] = mockFetch.mock.calls[0];
        expect(url).toContain('/api/v1/admin/analytics/export');
        expect(options.method).toBe('POST');
        expect(options.body).toContain('"format":"json"');
      });
    });

    it('shows error toast on export failure', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
      mockFetch.mockResolvedValue({
        ok: false,
        statusText: 'Internal Server Error',
      });

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      const exportButton = screen.getByRole('button', { name: /export csv/i });
      await user.click(exportButton);

      await waitFor(() => {
        expect(screen.getByText(/export failed/i)).toBeInTheDocument();
      });
    });
  });

  describe('Chart Rendering', () => {
    it('renders all 5 chart cards', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('User Registrations')).toBeInTheDocument();
        expect(screen.getByText('Session Creations')).toBeInTheDocument();
        expect(screen.getByText('API Requests')).toBeInTheDocument();
        expect(screen.getByText('PDF Uploads')).toBeInTheDocument();
        expect(screen.getByText('Chat Messages')).toBeInTheDocument();
      });
    });

    it('renders chart components inside ResponsiveContainer', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        const containers = screen.getAllByTestId('responsive-container');
        expect(containers.length).toBeGreaterThanOrEqual(5);
      });
    });

    it('renders LineChart with correct structure', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        const charts = screen.getAllByTestId('line-chart');
        expect(charts.length).toBeGreaterThanOrEqual(5);
      });
    });
  });

  describe('Error Handling', () => {
    it('displays error state on API failure', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockRejectedValue(new Error('Network error'));

      render(<AdminPageClient />);

      await waitFor(() => {
        // ErrorDisplay component should be shown
        expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
      });
    });

    it('retries data fetch on retry button click', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics)
        .mockRejectedValueOnce(new Error('First failure'))
        .mockResolvedValueOnce(mockAnalyticsData);

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
      });

      const retryButton = screen.getByRole('button', { name: /retry/i });
      await user.click(retryButton);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
        expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(2);
      });
    });
  });

  describe('Toast Notifications', () => {
    it('shows success toast on successful export', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
      mockFetch.mockResolvedValue({
        ok: true,
        blob: () => Promise.resolve(new Blob(['test'], { type: 'text/csv' })),
      });

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      const exportButton = screen.getByRole('button', { name: /export csv/i });
      await user.click(exportButton);

      await waitFor(() => {
        expect(screen.getByText(/analytics exported as csv/i)).toBeInTheDocument();
      });
    });

    it('auto-dismisses toast after 5 seconds', async () => {
      vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
      mockFetch.mockResolvedValue({
        ok: false,
        statusText: 'Error',
      });

      render(<AdminPageClient />);

      await waitFor(() => {
        expect(screen.getByText('Analytics Dashboard')).toBeInTheDocument();
      });

      const exportButton = screen.getByRole('button', { name: /export csv/i });
      await user.click(exportButton);

      await waitFor(() => {
        expect(screen.getByText(/export failed/i)).toBeInTheDocument();
      });

      // Advance time by 5 seconds
      await vi.advanceTimersByTimeAsync(5000);

      await waitFor(() => {
        expect(screen.queryByText(/export failed/i)).not.toBeInTheDocument();
      });
    });
  });

  describe('Authentication', () => {
    it('renders null when user is not authenticated', async () => {
      // Re-mock useAuthUser to return null user
      const { useAuthUser } = await import('@/components/auth/AuthProvider');
      vi.mocked(useAuthUser).mockReturnValue({ user: null, loading: false });

      const { container } = render(<AdminPageClient />);

      expect(container.firstChild).toBeNull();

      // Reset mock
      vi.mocked(useAuthUser).mockReturnValue({
        user: {
          id: 'user-123',
          email: 'admin@example.com',
          role: 'Admin',
          displayName: 'Admin User',
        },
        loading: false,
      });
    });
  });
});
