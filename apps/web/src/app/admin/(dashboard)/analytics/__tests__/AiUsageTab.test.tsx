import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetPdfAnalytics = vi.fn();
const mockGetChatAnalytics = vi.fn();
const mockGetModelPerformance = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getPdfAnalytics: (...args: unknown[]) => mockGetPdfAnalytics(...args),
      getChatAnalytics: (...args: unknown[]) => mockGetChatAnalytics(...args),
      getModelPerformance: (...args: unknown[]) => mockGetModelPerformance(...args),
    },
  },
}));

import { AiUsageTab } from '../AiUsageTab';

describe('AiUsageTab', () => {
  beforeEach(() => {
    mockGetPdfAnalytics.mockResolvedValue({
      totalUploaded: 120,
      successCount: 110,
      failedCount: 10,
      successRate: 0.917,
      avgProcessingTime: null,
      p95ProcessingTime: null,
      totalStorageBytes: 52428800,
      storageByTier: {},
      uploadsByDay: [],
    });
    mockGetChatAnalytics.mockResolvedValue({
      totalThreads: 45,
      activeThreads: 12,
      closedThreads: 33,
      totalMessages: 890,
      avgMessagesPerThread: 19.8,
      uniqueUsers: 28,
      threadsByAgentType: {},
      threadsByDay: [],
    });
    mockGetModelPerformance.mockResolvedValue({
      totalRequests: 5000,
      totalCost: 42.5,
      totalTokens: 1200000,
      avgLatencyMs: 350,
      successRate: 0.985,
      models: [],
      dailyStats: [],
    });
  });

  it('renders heading after loading', async () => {
    render(<AiUsageTab />);
    await waitFor(() => {
      expect(screen.getByText('AI Usage Analytics')).toBeInTheDocument();
    });
  });

  it('renders three metric cards', async () => {
    render(<AiUsageTab />);
    await waitFor(() => {
      expect(screen.getByText('PDF Processing')).toBeInTheDocument();
      expect(screen.getByText('Chat Activity')).toBeInTheDocument();
      expect(screen.getByText('Model Performance')).toBeInTheDocument();
    });
  });

  it('displays pdf stats', async () => {
    render(<AiUsageTab />);
    await waitFor(() => {
      expect(screen.getByText('120')).toBeInTheDocument();
    });
  });

  it('displays chat stats', async () => {
    render(<AiUsageTab />);
    await waitFor(() => {
      expect(screen.getByText('45')).toBeInTheDocument();
    });
  });

  it('displays model cost', async () => {
    render(<AiUsageTab />);
    await waitFor(() => {
      expect(screen.getByText('$42.50')).toBeInTheDocument();
    });
  });
});
