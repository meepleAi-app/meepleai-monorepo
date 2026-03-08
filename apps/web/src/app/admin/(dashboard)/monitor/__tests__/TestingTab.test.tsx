import { vi, describe, it, expect, beforeEach } from 'vitest';

const mockGetAccessibilityMetrics = vi.hoisted(() => vi.fn());
const mockGetPerformanceMetrics = vi.hoisted(() => vi.fn());
const mockGetE2EMetrics = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      getAccessibilityMetrics: mockGetAccessibilityMetrics,
      getPerformanceMetrics: mockGetPerformanceMetrics,
      getE2EMetrics: mockGetE2EMetrics,
    },
  },
}));

import { render, screen, waitFor } from '@testing-library/react';

import { TestingTab } from '../TestingTab';

describe('TestingTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAccessibilityMetrics.mockResolvedValue({
      lighthouseScore: 92,
      axeViolations: 3,
      wcagCompliance: { levelA: 98, levelAA: 85, levelAAA: 60 },
      testedPages: 25,
      criticalIssues: [],
      lastRun: '2026-01-01T00:00:00Z',
    });
    mockGetPerformanceMetrics.mockResolvedValue({
      lighthouseScore: 88,
      coreWebVitals: { lcp: 2.5, fid: 100, cls: 0.1 },
      budgetStatus: {
        js: { current: 250, budget: 300, unit: 'KB' },
        css: { current: 50, budget: 100, unit: 'KB' },
        images: { current: 500, budget: 800, unit: 'KB' },
      },
      slowestPages: [],
      lastRun: '2026-01-01T00:00:00Z',
    });
    mockGetE2EMetrics.mockResolvedValue({
      coverage: 85,
      passRate: 97,
      flakyRate: 2,
      totalTests: 150,
      executionTime: 12,
      criticalJourneys: [],
      lastRun: '2026-01-01T00:00:00Z',
    });
  });

  it('renders "Testing Metrics" heading', async () => {
    render(<TestingTab />);

    await waitFor(() => {
      expect(screen.getByText('Testing Metrics')).toBeInTheDocument();
    });
  });

  it('renders all three metric sections', async () => {
    render(<TestingTab />);

    await waitFor(() => {
      expect(screen.getByText('Accessibility')).toBeInTheDocument();
    });

    expect(screen.getByText('Performance')).toBeInTheDocument();
    expect(screen.getByText('E2E')).toBeInTheDocument();
  });

  it('renders structured accessibility metrics', async () => {
    render(<TestingTab />);

    await waitFor(() => {
      expect(screen.getByText('Axe Violations')).toBeInTheDocument();
    });

    expect(screen.getAllByText('Lighthouse Score')).toHaveLength(2);
    expect(screen.getByText('WCAG A')).toBeInTheDocument();
    expect(screen.getByText('Pages Tested')).toBeInTheDocument();
  });

  it('renders structured E2E metrics', async () => {
    render(<TestingTab />);

    await waitFor(() => {
      expect(screen.getByText('Coverage')).toBeInTheDocument();
    });

    expect(screen.getByText('Pass Rate')).toBeInTheDocument();
    expect(screen.getByText('Total Tests')).toBeInTheDocument();
  });

  it('handles API errors gracefully', async () => {
    mockGetAccessibilityMetrics.mockRejectedValue(new Error('fail'));
    mockGetPerformanceMetrics.mockRejectedValue(new Error('fail'));
    mockGetE2EMetrics.mockRejectedValue(new Error('fail'));

    render(<TestingTab />);

    await waitFor(() => {
      expect(screen.getByText('Testing Metrics')).toBeInTheDocument();
    });

    const noDataMessages = screen.getAllByText('No data available.');
    expect(noDataMessages).toHaveLength(3);
  });
});
