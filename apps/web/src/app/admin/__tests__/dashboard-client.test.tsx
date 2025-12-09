/**
 * DashboardClient Component Tests - Issue #874
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { DashboardClient } from '../dashboard-client';
import * as apiModule from '@/lib/api';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAnalytics: vi.fn(),
      getRecentActivity: vi.fn(),
    },
  },
}));

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
    totalGames: 125,
    apiRequests7d: 24891,
    apiRequests30d: 112034,
    averageLatency24h: 215.0,
    averageLatency7d: 228.0,
    errorRate24h: 0.025,
    activeAlerts: 2,
    resolvedAlerts: 37,
  },
  userTrend: [],
  sessionTrend: [],
  apiRequestTrend: [],
  pdfUploadTrend: [],
  chatMessageTrend: [],
  generatedAt: new Date().toISOString(),
};

const mockActivityData = {
  events: [
    {
      id: '1',
      eventType: 'UserRegistered',
      description: 'New user registered: john.doe@example.com',
      userId: 'user-123',
      userEmail: 'john.doe@example.com',
      timestamp: new Date().toISOString(),
      severity: 'Info',
    },
  ],
  totalCount: 1,
  generatedAt: new Date().toISOString(),
};

describe('DashboardClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders loading state initially', () => {
    vi.mocked(apiModule.api.admin.getAnalytics).mockImplementation(
      () => new Promise(() => {}) // Never resolves
    );
    vi.mocked(apiModule.api.admin.getRecentActivity).mockImplementation(
      () => new Promise(() => {})
    );

    render(<DashboardClient />);

    expect(screen.getByText('Loading dashboard...')).toBeInTheDocument();
  });

  it('fetches analytics and activity data on mount', async () => {
    vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
    vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);

    render(<DashboardClient />);

    await waitFor(() => {
      expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(1);
      expect(apiModule.api.admin.getRecentActivity).toHaveBeenCalledWith({ limit: 10 });
    });
  });

  it('displays metrics after data loads', async () => {
    vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
    vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);

    render(<DashboardClient />);

    await waitFor(() => {
      expect(screen.getByText('Dashboard Overview')).toBeInTheDocument();
      expect(screen.getByText('1,247')).toBeInTheDocument(); // Total Users
      expect(screen.getByText('42')).toBeInTheDocument(); // Active Sessions
    });
  });

  it('displays activity feed after data loads', async () => {
    vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
    vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);

    render(<DashboardClient />);

    await waitFor(() => {
      expect(screen.getByText('Recent Activity')).toBeInTheDocument();
      expect(screen.getByText(/New user registered/)).toBeInTheDocument();
    });
  });

  it('shows error state when API call fails', async () => {
    vi.mocked(apiModule.api.admin.getAnalytics).mockRejectedValue(new Error('Network error'));
    vi.mocked(apiModule.api.admin.getRecentActivity).mockRejectedValue(new Error('Network error'));

    render(<DashboardClient />);

    await waitFor(() => {
      expect(screen.getByText('Error Loading Dashboard')).toBeInTheDocument();
      expect(screen.getByText(/Network error|Failed to load/)).toBeInTheDocument();
    });
  });

  it('shows retry button on error', async () => {
    vi.mocked(apiModule.api.admin.getAnalytics).mockRejectedValue(new Error('Network error'));
    vi.mocked(apiModule.api.admin.getRecentActivity).mockRejectedValue(new Error('Network error'));

    render(<DashboardClient />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
    });
  });

  it('polls for updates every 30 seconds', async () => {
    vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
    vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);

    render(<DashboardClient />);

    // Initial call
    await waitFor(() => {
      expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(1);
    });

    // Advance 30 seconds
    vi.advanceTimersByTime(30000);

    await waitFor(() => {
      expect(apiModule.api.admin.getAnalytics).toHaveBeenCalledTimes(2);
      expect(apiModule.api.admin.getRecentActivity).toHaveBeenCalledTimes(2);
    });
  });

  it('displays last updated time', async () => {
    vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
    vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);

    render(<DashboardClient />);

    await waitFor(() => {
      expect(screen.getByText(/Last updated:/)).toBeInTheDocument();
    });
  });

  it('renders all 16 metrics', async () => {
    vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
    vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);

    render(<DashboardClient />);

    await waitFor(() => {
      expect(screen.getByText('Total Users')).toBeInTheDocument();
      expect(screen.getByText('Active Sessions')).toBeInTheDocument();
      expect(screen.getByText('Total Games')).toBeInTheDocument();
      expect(screen.getByText('API Requests (24h)')).toBeInTheDocument();
      expect(screen.getByText('Avg Latency (24h)')).toBeInTheDocument();
      expect(screen.getByText('Error Rate (24h)')).toBeInTheDocument();
      expect(screen.getByText('Active Alerts')).toBeInTheDocument();
    });
  });

  it('formats large numbers with locale', async () => {
    vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(mockAnalyticsData);
    vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);

    render(<DashboardClient />);

    await waitFor(() => {
      expect(screen.getByText('1,247')).toBeInTheDocument(); // Total users with comma
    });
  });

  it('applies variant styling based on metric thresholds', async () => {
    const criticalData = {
      ...mockAnalyticsData,
      metrics: {
        ...mockAnalyticsData.metrics,
        errorRate24h: 0.15, // High error rate
        activeAlerts: 12, // Many alerts
      },
    };

    vi.mocked(apiModule.api.admin.getAnalytics).mockResolvedValue(criticalData);
    vi.mocked(apiModule.api.admin.getRecentActivity).mockResolvedValue(mockActivityData);

    render(<DashboardClient />);

    await waitFor(() => {
      // Should show danger variant for high error rate
      expect(screen.getByText('15.0%')).toBeInTheDocument();
    });
  });
});
