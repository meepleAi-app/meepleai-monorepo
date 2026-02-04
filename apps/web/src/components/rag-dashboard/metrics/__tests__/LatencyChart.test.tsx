import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { LatencyChart } from '../LatencyChart';
import type { LatencyMetrics } from '../types';

const mockLatencyData: LatencyMetrics = {
  p50: 250,
  p95: 480,
  p99: 720,
  avg: 300,
  trend: -5, // 5% improvement
};

describe('LatencyChart', () => {
  it('renders the component title', () => {
    render(<LatencyChart data={mockLatencyData} />);
    expect(screen.getByText('Latency')).toBeInTheDocument();
  });

  it('displays all percentile labels', () => {
    render(<LatencyChart data={mockLatencyData} />);
    expect(screen.getByText('P50')).toBeInTheDocument();
    expect(screen.getByText('P95')).toBeInTheDocument();
    expect(screen.getByText('P99')).toBeInTheDocument();
  });

  it('shows latency values with ms suffix', () => {
    render(<LatencyChart data={mockLatencyData} />);
    expect(screen.getByText('250ms')).toBeInTheDocument();
    expect(screen.getByText('480ms')).toBeInTheDocument();
    expect(screen.getByText('720ms')).toBeInTheDocument();
  });

  it('displays average latency', () => {
    render(<LatencyChart data={mockLatencyData} />);
    expect(screen.getByText('Avg: 300ms')).toBeInTheDocument();
  });

  it('shows negative trend indicator (improvement)', () => {
    render(<LatencyChart data={mockLatencyData} />);
    expect(screen.getByText('5%')).toBeInTheDocument();
  });

  it('shows positive trend indicator (degradation)', () => {
    const dataWithPositiveTrend: LatencyMetrics = {
      ...mockLatencyData,
      trend: 8,
    };
    render(<LatencyChart data={dataWithPositiveTrend} />);
    expect(screen.getByText('8%')).toBeInTheDocument();
  });

  it('hides trend indicator when trend is zero', () => {
    const dataWithNoTrend: LatencyMetrics = {
      ...mockLatencyData,
      trend: 0,
    };
    render(<LatencyChart data={dataWithNoTrend} />);
    // Should not show any percentage trend indicator
    expect(screen.queryByText('0%')).not.toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(
      <LatencyChart data={mockLatencyData} className="custom-class" />
    );
    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('shows target threshold', () => {
    render(<LatencyChart data={mockLatencyData} targetThreshold={400} />);
    expect(screen.getByText('Target: <400ms')).toBeInTheDocument();
  });

  it('uses default threshold when not provided', () => {
    render(<LatencyChart data={mockLatencyData} />);
    expect(screen.getByText('Target: <500ms')).toBeInTheDocument();
  });
});
