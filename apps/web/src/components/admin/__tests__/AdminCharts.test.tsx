/**
 * AdminCharts Component Tests - Issue #887
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  EndpointDistributionChart,
  LatencyDistributionChart,
  RequestsTimeSeriesChart,
  FeedbackChart,
  buildEndpointData,
  buildLatencyBins,
  buildTimeSeries,
  buildFeedbackData,
} from '../AdminCharts';

describe('EndpointDistributionChart', () => {
  it('renders empty state when no data is provided', () => {
    render(<EndpointDistributionChart endpointCounts={{}} />);

    expect(screen.getByText('No endpoint data available')).toBeInTheDocument();
  });

  it('maps endpoints to expected colors', () => {
    const data = buildEndpointData({ qa: 10, explain: 5, setup: 2, chess: 1, other: 4 });
    expect(data).toHaveLength(5);
    const colorByName = Object.fromEntries(data.map(d => [d.name, d.color]));
    expect(colorByName.qa).toBe('#1a73e8');
    expect(colorByName.explain).toBe('#f9ab00');
    expect(colorByName.setup).toBe('#a142f4');
    expect(colorByName.chess).toBe('#34a853');
    expect(colorByName.other).toBe('#64748b');
  });
});

describe('LatencyDistributionChart', () => {
  it('renders empty message when no requests exist', () => {
    render(<LatencyDistributionChart requests={[]} />);
    expect(screen.getByText('No latency data available')).toBeInTheDocument();
  });

  it('groups requests into latency bins', () => {
    const data = buildLatencyBins([
      { latencyMs: 50 },
      { latencyMs: 150 },
      { latencyMs: 250 },
      { latencyMs: 450 },
      { latencyMs: 900 },
    ]);
    expect(data).toHaveLength(6);
    expect(data.find((b: any) => b.range === '0-100ms').count).toBe(1);
    expect(data.find((b: any) => b.range === '100-200ms').count).toBe(1);
    expect(data.find((b: any) => b.range === '200-300ms').count).toBe(1);
    expect(data.find((b: any) => b.range === '400-500ms').count).toBe(1);
    expect(data[data.length - 1].count).toBe(1); // 500-1000ms bucket
  });
});

describe('RequestsTimeSeriesChart', () => {
  it('renders empty state when no request history exists', () => {
    render(<RequestsTimeSeriesChart requests={[]} />);
    expect(screen.getByText('No time series data available')).toBeInTheDocument();
  });

  it('aggregates success and error counts per hour', () => {
    const data = buildTimeSeries([
      { createdAt: '2025-12-10T08:15:00Z', status: 'Success' },
      { createdAt: '2025-12-10T08:30:00Z', status: 'Error' },
      { createdAt: '2025-12-10T09:05:00Z', status: 'Success' },
    ]);
    expect(data).toHaveLength(2);
    const firstHour = data[0];
    expect(firstHour.total).toBe(2);
    expect(firstHour.success).toBe(1);
    expect(firstHour.error).toBe(1);

    const secondHour = data[1];
    expect(secondHour.total).toBe(1);
    expect(secondHour.success).toBe(1);
    expect(secondHour.error).toBe(0);
  });
});

describe('FeedbackChart', () => {
  it('shows empty state when counts are zero', () => {
    render(<FeedbackChart feedbackCounts={{ helpful: 0, notHelpful: 0 }} />);
    expect(screen.getByText('No feedback data available')).toBeInTheDocument();
  });

  it('renders bars with mapped colors for feedback', () => {
    const data = buildFeedbackData({ helpful: 6, notHelpful: 2 });
    expect(data).toEqual([
      { name: 'Helpful', value: 6, color: '#34a853' },
      { name: 'Not Helpful', value: 2, color: '#ea4335' },
    ]);
  });
});
