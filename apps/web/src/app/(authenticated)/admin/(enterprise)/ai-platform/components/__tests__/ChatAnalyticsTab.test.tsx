/**
 * ChatAnalyticsTab Tests (Issue #3714)
 *
 * Tests:
 * 1. Loading state
 * 2. Renders stat cards with real data
 * 3. Time range selector works
 * 4. Error state
 * 5. Empty data
 * 6. Agent type breakdown
 * 7. Daily activity table
 * 8. Active rate calculation
 * 9. Handles zero threads gracefully
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { ChatAnalyticsTab } from '../ChatAnalyticsTab';

// Mock the API
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getChatAnalytics: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
const mockGetChatAnalytics = vi.mocked(api.admin.getChatAnalytics);

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
  };
}

const mockAnalytics = {
  totalThreads: 456,
  activeThreads: 320,
  closedThreads: 136,
  totalMessages: 8900,
  avgMessagesPerThread: 19.5,
  uniqueUsers: 78,
  threadsByAgentType: {
    auto: 200,
    tutor: 150,
    arbitro: 80,
    decisore: 26,
  },
  threadsByDay: [
    { date: '2026-02-14', totalCount: 15, activeCount: 12, closedCount: 3, messageCount: 290 },
    { date: '2026-02-15', totalCount: 22, activeCount: 18, closedCount: 4, messageCount: 410 },
  ],
};

describe('ChatAnalyticsTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders loading state initially', () => {
    mockGetChatAnalytics.mockReturnValue(new Promise(() => {})); // never resolves
    render(<ChatAnalyticsTab />, { wrapper: createWrapper() });

    const loadingElements = screen.getAllByText('...');
    expect(loadingElements.length).toBeGreaterThan(0);
  });

  it('renders stat cards with data', async () => {
    mockGetChatAnalytics.mockResolvedValue(mockAnalytics);
    render(<ChatAnalyticsTab />, { wrapper: createWrapper() });

    // Wait for data to load
    expect(await screen.findByText('320 active / 136 closed')).toBeInTheDocument();
    expect(screen.getByText('Total Threads')).toBeInTheDocument();
    expect(screen.getByText('Total Messages')).toBeInTheDocument();
    expect(screen.getByText('Avg 19.5 per thread')).toBeInTheDocument();
    expect(screen.getByText('Unique Users')).toBeInTheDocument();
  });

  it('calculates active rate correctly', async () => {
    mockGetChatAnalytics.mockResolvedValue(mockAnalytics);
    render(<ChatAnalyticsTab />, { wrapper: createWrapper() });

    // 320/456 ≈ 70%
    expect(await screen.findByText('70%')).toBeInTheDocument();
  });

  it('renders agent type breakdown', async () => {
    mockGetChatAnalytics.mockResolvedValue(mockAnalytics);
    render(<ChatAnalyticsTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('Threads by Agent Type')).toBeInTheDocument();
    expect(screen.getByText('Auto')).toBeInTheDocument();
    expect(screen.getByText('Tutor')).toBeInTheDocument();
    expect(screen.getByText('Arbitro')).toBeInTheDocument();
    expect(screen.getByText('Decisore')).toBeInTheDocument();
    expect(screen.getByText('200')).toBeInTheDocument();
    expect(screen.getByText('150')).toBeInTheDocument();
  });

  it('renders daily activity table', async () => {
    mockGetChatAnalytics.mockResolvedValue(mockAnalytics);
    render(<ChatAnalyticsTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('2026-02-14')).toBeInTheDocument();
    expect(screen.getByText('2026-02-15')).toBeInTheDocument();
    expect(screen.getByText('290')).toBeInTheDocument();
    expect(screen.getByText('410')).toBeInTheDocument();
  });

  it('shows time range selector with active state', () => {
    mockGetChatAnalytics.mockResolvedValue(mockAnalytics);
    render(<ChatAnalyticsTab />, { wrapper: createWrapper() });

    const btn30 = screen.getByText('30 days');
    expect(btn30.className).toContain('bg-amber-100');

    const btn7 = screen.getByText('7 days');
    expect(btn7.className).not.toContain('bg-amber-100');
  });

  it('changes time range on button click', async () => {
    mockGetChatAnalytics.mockResolvedValue(mockAnalytics);
    render(<ChatAnalyticsTab />, { wrapper: createWrapper() });

    fireEvent.click(screen.getByText('7 days'));
    expect(mockGetChatAnalytics).toHaveBeenCalledWith(7);
  });

  it('renders error state', async () => {
    mockGetChatAnalytics.mockRejectedValue(new Error('Network error'));
    render(<ChatAnalyticsTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('Failed to load chat analytics', {}, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.getByText('Network error')).toBeInTheDocument();
  });

  it('shows empty data message when no threads', async () => {
    mockGetChatAnalytics.mockResolvedValue({
      ...mockAnalytics,
      totalThreads: 0,
      activeThreads: 0,
      closedThreads: 0,
      totalMessages: 0,
      avgMessagesPerThread: 0,
      uniqueUsers: 0,
      threadsByAgentType: {},
      threadsByDay: [],
    });
    render(<ChatAnalyticsTab />, { wrapper: createWrapper() });

    expect(await screen.findByText('No chat data for this period')).toBeInTheDocument();
  });

  it('handles zero total threads for active rate', async () => {
    mockGetChatAnalytics.mockResolvedValue({
      ...mockAnalytics,
      totalThreads: 0,
      activeThreads: 0,
      closedThreads: 0,
    });
    render(<ChatAnalyticsTab />, { wrapper: createWrapper() });

    // Should show 0% when totalThreads is 0 (no division by zero)
    expect(await screen.findByText('0%')).toBeInTheDocument();
  });
});
