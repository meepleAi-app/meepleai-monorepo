import { vi, describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

const mockGetActiveAiUsers = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getActiveAiUsers: (...args: unknown[]) => mockGetActiveAiUsers(...args),
    },
  },
}));

// ---------------------------------------------------------------------------
// Component under test
// ---------------------------------------------------------------------------

import { MauDashboard } from '../MauDashboard';

// ---------------------------------------------------------------------------
// Test Data
// ---------------------------------------------------------------------------

const mockMauData = {
  totalActiveUsers: 150,
  aiChatUsers: 100,
  pdfUploadUsers: 60,
  agentUsers: 30,
  periodDays: 30,
  periodStart: '2026-02-13T00:00:00Z',
  periodEnd: '2026-03-13T00:00:00Z',
  dailyBreakdown: [
    { date: '2026-03-12T00:00:00Z', activeUsers: 12, aiChatUsers: 8, pdfUploadUsers: 4 },
    { date: '2026-03-13T00:00:00Z', activeUsers: 15, aiChatUsers: 10, pdfUploadUsers: 5 },
  ],
};

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('MauDashboard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetActiveAiUsers.mockResolvedValue(mockMauData);
  });

  it('renders KPI cards with data', async () => {
    render(<MauDashboard />);

    await waitFor(() => {
      expect(screen.getByText('Total Active Users')).toBeInTheDocument();
      expect(screen.getByText('150')).toBeInTheDocument();
    });

    expect(screen.getByText('AI Chat Users')).toBeInTheDocument();
    expect(screen.getByText('100')).toBeInTheDocument();
    expect(screen.getByText('PDF Upload Users')).toBeInTheDocument();
    expect(screen.getByText('60')).toBeInTheDocument();
    expect(screen.getByText('Agent Users')).toBeInTheDocument();
    expect(screen.getByText('30')).toBeInTheDocument();
  });

  it('fetches data with default 30-day period', async () => {
    render(<MauDashboard />);

    await waitFor(() => {
      expect(mockGetActiveAiUsers).toHaveBeenCalledWith(30);
    });
  });

  it('changes period when buttons are clicked', async () => {
    render(<MauDashboard />);

    await waitFor(() => {
      expect(mockGetActiveAiUsers).toHaveBeenCalledWith(30);
    });

    const sevenDayButton = screen.getByText('7d');
    fireEvent.click(sevenDayButton);

    await waitFor(() => {
      expect(mockGetActiveAiUsers).toHaveBeenCalledWith(7);
    });
  });

  it('displays daily breakdown table', async () => {
    render(<MauDashboard />);

    await waitFor(() => {
      expect(screen.getByText('Daily Active Users Trend')).toBeInTheDocument();
    });
  });

  it('shows error state and retry button', async () => {
    mockGetActiveAiUsers.mockRejectedValue(new Error('Network error'));

    render(<MauDashboard />);

    await waitFor(() => {
      expect(screen.getByText('Network error')).toBeInTheDocument();
    });

    expect(screen.getByText('Retry')).toBeInTheDocument();
  });

  it('retries data fetch on retry button click', async () => {
    mockGetActiveAiUsers.mockRejectedValueOnce(new Error('Network error'));
    mockGetActiveAiUsers.mockResolvedValueOnce(mockMauData);

    render(<MauDashboard />);

    await waitFor(() => {
      expect(screen.getByText('Retry')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Retry'));

    await waitFor(() => {
      expect(mockGetActiveAiUsers).toHaveBeenCalledTimes(2);
    });
  });

  it('renders period selector buttons', () => {
    render(<MauDashboard />);

    expect(screen.getByText('7d')).toBeInTheDocument();
    expect(screen.getByText('30d')).toBeInTheDocument();
    expect(screen.getByText('90d')).toBeInTheDocument();
  });
});
