import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AlertHistoryTab } from '../AlertHistoryTab';

// ============================================================================
// Test Utilities
// ============================================================================

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
        staleTime: 0,
      },
    },
  });
}

function renderWithProviders(component: React.ReactElement) {
  const queryClient = createTestQueryClient();
  return render(<QueryClientProvider client={queryClient}>{component}</QueryClientProvider>);
}

// ============================================================================
// Mock Data
// ============================================================================

const mockAlerts = [
  {
    id: 'alert-1',
    alertType: 'CpuUsage',
    severity: 'Critical',
    message: 'CPU usage exceeded 90% threshold',
    metadata: null,
    triggeredAt: '2026-03-15T10:00:00Z',
    resolvedAt: '2026-03-15T10:30:00Z',
    isActive: false,
    channelSent: { Email: true, Slack: true, PagerDuty: false },
  },
  {
    id: 'alert-2',
    alertType: 'MemoryUsage',
    severity: 'Warning',
    message: 'Memory usage at 80%',
    metadata: null,
    triggeredAt: '2026-03-15T12:00:00Z',
    resolvedAt: null,
    isActive: true,
    channelSent: { Email: true, Slack: false },
  },
];

// ============================================================================
// Setup
// ============================================================================

beforeEach(() => {
  vi.clearAllMocks();

  global.fetch = vi.fn(() =>
    Promise.resolve({
      ok: true,
      json: () => Promise.resolve(mockAlerts),
    } as Response)
  );
});

afterEach(() => {
  vi.restoreAllMocks();
});

// ============================================================================
// Tests
// ============================================================================

describe('AlertHistoryTab', () => {
  it('renders "Alert History" heading', async () => {
    renderWithProviders(<AlertHistoryTab />);

    await waitFor(() => {
      expect(screen.getByText('Alert History')).toBeInTheDocument();
    });
  });

  it('shows "No alerts" empty state when API returns empty array', async () => {
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: true,
        json: () => Promise.resolve([]),
      } as Response)
    );

    renderWithProviders(<AlertHistoryTab />);

    await waitFor(() => {
      expect(screen.getByText('No alerts')).toBeInTheDocument();
    });
  });

  it('renders alert rows when API returns data', async () => {
    renderWithProviders(<AlertHistoryTab />);

    await waitFor(() => {
      expect(screen.getByText('CpuUsage')).toBeInTheDocument();
    });

    expect(screen.getByText('MemoryUsage')).toBeInTheDocument();
    expect(screen.getByText('CPU usage exceeded 90% threshold')).toBeInTheDocument();
  });

  it('renders severity badges with correct labels', async () => {
    renderWithProviders(<AlertHistoryTab />);

    await waitFor(() => {
      // Critical appears in the dropdown option AND as badge — at least one match is enough
      expect(screen.getAllByText('Critical').length).toBeGreaterThanOrEqual(1);
    });

    expect(screen.getAllByText('Warning').length).toBeGreaterThanOrEqual(1);
  });

  it('shows active and resolved status correctly', async () => {
    renderWithProviders(<AlertHistoryTab />);

    await waitFor(() => {
      const statusCells = screen.getAllByText(/^(Active|Resolved)$/);
      expect(statusCells.length).toBeGreaterThanOrEqual(2);
    });
  });

  it('renders table columns', async () => {
    renderWithProviders(<AlertHistoryTab />);

    await waitFor(() => {
      expect(screen.getByText('Alert Type')).toBeInTheDocument();
    });

    expect(screen.getByText('Severity')).toBeInTheDocument();
    expect(screen.getByText('Message')).toBeInTheDocument();
    expect(screen.getByText('Triggered At')).toBeInTheDocument();
    expect(screen.getByText('Resolved At')).toBeInTheDocument();
    expect(screen.getByText('Status')).toBeInTheDocument();
    expect(screen.getByText('Channels')).toBeInTheDocument();
  });

  it('handles API error gracefully by showing no alerts', async () => {
    global.fetch = vi.fn(() =>
      Promise.resolve({
        ok: false,
        status: 500,
      } as Response)
    );

    renderWithProviders(<AlertHistoryTab />);

    await waitFor(() => {
      expect(screen.getByText('No alerts')).toBeInTheDocument();
    });
  });
});
