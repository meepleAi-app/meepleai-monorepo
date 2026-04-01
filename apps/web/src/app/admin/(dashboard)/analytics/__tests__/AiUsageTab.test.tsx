import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockPdfData = {
  totalUploaded: 10,
  successRate: 0.9,
  failedCount: 1,
  totalStorageBytes: 1024,
};
const mockChatData = { totalThreads: 5, activeThreads: 2, totalMessages: 50, uniqueUsers: 3 };
const mockModelData = { totalRequests: 100, totalCost: 5.5, avgLatencyMs: 230, successRate: 0.95 };

const mocks = vi.hoisted(() => ({
  getPdfAnalytics: vi.fn(),
  getChatAnalytics: vi.fn(),
  getModelPerformance: vi.fn(),
}));

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getPdfAnalytics: mocks.getPdfAnalytics,
      getChatAnalytics: mocks.getChatAnalytics,
      getModelPerformance: mocks.getModelPerformance,
    },
  },
}));

import { AiUsageTab } from '../AiUsageTab';

describe('AiUsageTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getPdfAnalytics.mockResolvedValue(mockPdfData);
    mocks.getChatAnalytics.mockResolvedValue(mockChatData);
    mocks.getModelPerformance.mockResolvedValue(mockModelData);
  });

  it('mostra il selettore periodo 7d/30d/90d', async () => {
    render(<AiUsageTab />);
    expect(await screen.findByRole('button', { name: '7d' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '30d' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '90d' })).toBeInTheDocument();
  });

  it('30d è il periodo default', async () => {
    render(<AiUsageTab />);
    expect(await screen.findByRole('button', { name: '7d' })).toBeInTheDocument();
    expect(mocks.getPdfAnalytics).toHaveBeenCalledWith(30);
  });

  it('cambiare periodo a 7d ricarica i dati con periodo corretto', async () => {
    render(<AiUsageTab />);
    await screen.findByRole('button', { name: '7d' });
    fireEvent.click(screen.getByRole('button', { name: '7d' }));
    expect(mocks.getPdfAnalytics).toHaveBeenCalledWith(7);
  });
});
