/**
 * MetricsKpiCards Tests
 * Issue #3382: Agent Metrics Dashboard
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { MetricsKpiCards } from '../MetricsKpiCards';

const mockMetrics = {
  totalInvocations: 12500,
  totalTokensUsed: 4500000,
  totalCost: 125.50,
  avgLatencyMs: 850,
  avgConfidenceScore: 0.87,
  userSatisfactionRate: 0.92,
  topQueries: [],
  costBreakdown: [],
  usageOverTime: [],
};

describe('MetricsKpiCards', () => {
  it('renders loading state', () => {
    render(<MetricsKpiCards isLoading={true} />);

    // Should show 4 skeleton cards
    const skeletons = document.querySelectorAll('[class*="animate-pulse"]');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('renders metrics data correctly', () => {
    render(<MetricsKpiCards metrics={mockMetrics} isLoading={false} />);

    // Check total invocations (formatted as 12.5K)
    expect(screen.getByText('12.5K')).toBeInTheDocument();
    expect(screen.getByText('Total Invocations')).toBeInTheDocument();

    // Check total cost
    expect(screen.getByText('$125.50')).toBeInTheDocument();
    expect(screen.getByText('Total Cost')).toBeInTheDocument();

    // Check average confidence (87%)
    expect(screen.getByText('87%')).toBeInTheDocument();
    expect(screen.getByText('Avg Confidence')).toBeInTheDocument();

    // Check average latency (850ms)
    expect(screen.getByText('850ms')).toBeInTheDocument();
    expect(screen.getByText('Avg Latency')).toBeInTheDocument();
  });

  it('formats large numbers correctly', () => {
    const largeMetrics = {
      ...mockMetrics,
      totalInvocations: 1500000,
      totalTokensUsed: 500000000,
    };

    render(<MetricsKpiCards metrics={largeMetrics} isLoading={false} />);

    // Should show 1.5M
    expect(screen.getByText('1.5M')).toBeInTheDocument();
  });

  it('formats small costs correctly', () => {
    const smallCostMetrics = {
      ...mockMetrics,
      totalCost: 0.0125,
    };

    render(<MetricsKpiCards metrics={smallCostMetrics} isLoading={false} />);

    // Should show $0.0125
    expect(screen.getByText('$0.0125')).toBeInTheDocument();
  });

  it('handles zero metrics', () => {
    const zeroMetrics = {
      ...mockMetrics,
      totalInvocations: 0,
      totalCost: 0,
      avgLatencyMs: 0,
      avgConfidenceScore: 0,
    };

    render(<MetricsKpiCards metrics={zeroMetrics} isLoading={false} />);

    // Should render without crashing
    expect(screen.getByText('Total Invocations')).toBeInTheDocument();
  });
});
