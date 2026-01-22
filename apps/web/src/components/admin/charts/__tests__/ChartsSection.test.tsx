/**
 * ChartsSection Component Tests - Issue #2794
 *
 * Test suite for ChartsSection component covering:
 * - API Requests Chart rendering
 * - AI Usage Donut Chart rendering
 * - Data transformation from useDashboardData hook
 * - Loading states
 * - Empty states
 * - Responsive layout
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { DashboardMetrics, DashboardTrends } from '@/lib/api';

import { ChartsSection, calculateAiUsageBreakdown } from '../ChartsSection';

// Mock the useDashboardData hook
const mockMetrics: DashboardMetrics = {
  totalUsers: 100,
  totalGames: 50,
  activeUsersToday: 25,
  totalRagRequests: 150,
  totalChatMessages: 200,
  totalPdfDocuments: 30,
};

const mockTrends: DashboardTrends = {
  apiRequest: [
    { date: '2026-01-15', count: 10 },
    { date: '2026-01-16', count: 15 },
    { date: '2026-01-17', count: 20 },
    { date: '2026-01-18', count: 18 },
    { date: '2026-01-19', count: 22 },
    { date: '2026-01-20', count: 25 },
    { date: '2026-01-21', count: 30 },
  ],
};

const mockUseDashboardData = vi.fn(() => ({
  metrics: mockMetrics,
  trends: mockTrends,
  isLoading: false,
  error: null,
}));

vi.mock('@/hooks/queries/useDashboardData', () => ({
  useDashboardData: () => mockUseDashboardData(),
}));

// Mock the chart components to simplify testing
vi.mock('../APIRequestsChart', () => ({
  APIRequestsChart: ({ data, isLoading }: any) => (
    <div data-testid="api-requests-chart">
      {isLoading ? (
        <div>Loading API Requests...</div>
      ) : (
        <div>
          <div>API Requests Chart</div>
          <div data-testid="api-requests-data-count">{data.length}</div>
        </div>
      )}
    </div>
  ),
}));

vi.mock('../AIUsageDonut', () => ({
  AIUsageDonut: ({ data, isLoading }: any) => (
    <div data-testid="ai-usage-donut">
      {isLoading ? (
        <div>Loading AI Usage...</div>
      ) : (
        <div>
          <div>AI Usage Donut</div>
          <div data-testid="ai-usage-categories">{data.length}</div>
        </div>
      )}
    </div>
  ),
}));

describe('ChartsSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders the analytics heading', () => {
      render(<ChartsSection />);
      expect(screen.getByRole('heading', { name: 'Analytics' })).toBeInTheDocument();
    });

    it('renders both chart components', () => {
      render(<ChartsSection />);
      expect(screen.getByTestId('api-requests-chart')).toBeInTheDocument();
      expect(screen.getByTestId('ai-usage-donut')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = render(<ChartsSection className="custom-class" />);
      const section = container.querySelector('section');
      expect(section).toHaveClass('custom-class');
    });
  });

  describe('Data Transformation', () => {
    it('transforms apiRequestTrend data correctly', () => {
      render(<ChartsSection />);
      const dataCountElement = screen.getByTestId('api-requests-data-count');
      expect(dataCountElement).toHaveTextContent('7'); // 7 days of data
    });

    it('calculates AI usage breakdown from metrics', () => {
      render(<ChartsSection />);
      const categoriesElement = screen.getByTestId('ai-usage-categories');
      expect(categoriesElement).toHaveTextContent('3'); // Embeddings, Completions, OCR
    });

    it('handles empty trends data', () => {
      mockUseDashboardData.mockReturnValueOnce({
        metrics: mockMetrics,
        trends: { apiRequest: [] },
        isLoading: false,
        error: null,
      });

      render(<ChartsSection />);
      const dataCountElement = screen.getByTestId('api-requests-data-count');
      expect(dataCountElement).toHaveTextContent('0');
    });

    it('handles null metrics for AI usage', () => {
      mockUseDashboardData.mockReturnValueOnce({
        metrics: null,
        trends: mockTrends,
        isLoading: false,
        error: null,
      });

      render(<ChartsSection />);
      const categoriesElement = screen.getByTestId('ai-usage-categories');
      expect(categoriesElement).toHaveTextContent('0'); // Empty array when metrics is null
    });
  });

  describe('Loading State', () => {
    it('shows loading state in both charts', () => {
      mockUseDashboardData.mockReturnValueOnce({
        metrics: null,
        trends: { apiRequest: [] },
        isLoading: true,
        error: null,
      });

      render(<ChartsSection />);
      expect(screen.getByText('Loading API Requests...')).toBeInTheDocument();
      expect(screen.getByText('Loading AI Usage...')).toBeInTheDocument();
    });
  });

  describe('Responsive Layout', () => {
    it('has responsive grid classes', () => {
      const { container } = render(<ChartsSection />);
      const gridContainer = container.querySelector('.grid');
      expect(gridContainer).toHaveClass('grid-cols-1');
      expect(gridContainer).toHaveClass('lg:grid-cols-2');
    });

    it('has proper gap spacing', () => {
      const { container } = render(<ChartsSection />);
      const gridContainer = container.querySelector('.grid');
      expect(gridContainer).toHaveClass('gap-6');
    });
  });

  describe('AI Usage Calculation Logic', () => {
    it('calculates embedding calls correctly (RAG + PDF)', () => {
      // Embedding calls = totalRagRequests + totalPdfDocuments
      // Expected: 150 + 30 = 180
      const result = calculateAiUsageBreakdown(mockMetrics);
      const embeddings = result.find(stat => stat.category === 'Embeddings');
      expect(embeddings).toBeDefined();
      expect(embeddings?.count).toBe(180);
    });

    it('calculates completion calls correctly (RAG + Chat)', () => {
      // Completion calls = totalRagRequests + totalChatMessages
      // Expected: 150 + 200 = 350
      const result = calculateAiUsageBreakdown(mockMetrics);
      const completions = result.find(stat => stat.category === 'Completions');
      expect(completions).toBeDefined();
      expect(completions?.count).toBe(350);
    });

    it('calculates OCR calls correctly (PDF only)', () => {
      // OCR calls = totalPdfDocuments
      // Expected: 30
      const result = calculateAiUsageBreakdown(mockMetrics);
      const ocr = result.find(stat => stat.category === 'OCR');
      expect(ocr).toBeDefined();
      expect(ocr?.count).toBe(30);
    });

    it('returns empty array when metrics is null', () => {
      const result = calculateAiUsageBreakdown(null);
      expect(result).toEqual([]);
    });

    it('returns all three categories in correct order', () => {
      const result = calculateAiUsageBreakdown(mockMetrics);
      expect(result).toHaveLength(3);
      expect(result[0].category).toBe('Embeddings');
      expect(result[1].category).toBe('Completions');
      expect(result[2].category).toBe('OCR');
    });
  });

  describe('Integration with useDashboardData', () => {
    it('uses data from useDashboardData hook', () => {
      render(<ChartsSection />);
      expect(mockUseDashboardData).toHaveBeenCalled();
    });

    it('passes loading state to child components', () => {
      mockUseDashboardData.mockReturnValueOnce({
        metrics: null,
        trends: { apiRequest: [] },
        isLoading: true,
        error: null,
      });

      render(<ChartsSection />);
      expect(screen.getByText('Loading API Requests...')).toBeInTheDocument();
      expect(screen.getByText('Loading AI Usage...')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has proper heading structure', () => {
      render(<ChartsSection />);
      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveTextContent('Analytics');
    });

    it('uses semantic HTML section element', () => {
      const { container } = render(<ChartsSection />);
      expect(container.querySelector('section')).toBeInTheDocument();
    });
  });
});
