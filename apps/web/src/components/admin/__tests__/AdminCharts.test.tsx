/**
 * AdminCharts Component Tests - Issue #887
 */

import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import {
  EndpointDistributionChart,
  LatencyDistributionChart,
  RequestsTimeSeriesChart,
  FeedbackChart,
} from '../AdminCharts';

type CapturedProps = Record<string, any>;
const captured: CapturedProps = {};

vi.mock('recharts', () => {
  const React = require('react');
  const capture = (key: string, props: any) => {
    captured[key] = props;
    return (
      <div data-testid={key}>
        {typeof props.children === 'function' ? props.children() : props.children}
      </div>
    );
  };

  return {
    ResponsiveContainer: ({ children, width = 800, height = 300 }: any) => (
      <div data-testid="responsive-container" style={{ width, height }}>
        {typeof children === 'function' ? children({ width, height }) : children}
      </div>
    ),
    PieChart: (props: any) => capture('pie-chart', props),
    Pie: (props: any) => capture('pie', props),
    Cell: ({ fill }: any) => <div data-testid="cell" data-fill={fill} />,
    Tooltip: () => <div data-testid="tooltip" />,
    Legend: () => <div data-testid="legend" />,
    BarChart: (props: any) => capture('bar-chart', props),
    Bar: (props: any) => capture('bar', props),
    CartesianGrid: () => <div data-testid="grid" />,
    XAxis: (props: any) => <div data-testid={`x-axis-${props.dataKey ?? 'default'}`} />,
    YAxis: () => <div data-testid="y-axis" />,
    LineChart: (props: any) => capture('line-chart', props),
    Line: (props: any) => <div data-testid={`line-${props.dataKey}`} />,
  };
});

beforeEach(() => {
  for (const key of Object.keys(captured)) {
    delete captured[key];
  }
});

describe('EndpointDistributionChart', () => {
  it('renders empty state when no data is provided', () => {
    render(<EndpointDistributionChart endpointCounts={{}} />);

    expect(screen.getByText('No endpoint data available')).toBeInTheDocument();
  });

  it('maps endpoints to expected colors', () => {
    render(
      <EndpointDistributionChart
        endpointCounts={{ qa: 10, explain: 5, setup: 2, chess: 1, other: 4 }}
      />
    );

    const data = captured['pie'].data;
    expect(data).toHaveLength(5);
    const colorByName = Object.fromEntries(data.map((d: any) => [d.name, d.color]));
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
    render(
      <LatencyDistributionChart
        requests={[
          { latencyMs: 50, endpoint: 'qa', createdAt: '2025-12-10T08:00:00Z' },
          { latencyMs: 150, endpoint: 'qa', createdAt: '2025-12-10T08:01:00Z' },
          { latencyMs: 250, endpoint: 'qa', createdAt: '2025-12-10T08:02:00Z' },
          { latencyMs: 450, endpoint: 'qa', createdAt: '2025-12-10T08:03:00Z' },
          { latencyMs: 900, endpoint: 'qa', createdAt: '2025-12-10T08:04:00Z' },
        ]}
      />
    );

    const data = captured['bar-chart'].data;
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
    render(
      <RequestsTimeSeriesChart
        requests={[
          { createdAt: '2025-12-10T08:15:00Z', status: 'Success' },
          { createdAt: '2025-12-10T08:30:00Z', status: 'Error' },
          { createdAt: '2025-12-10T09:05:00Z', status: 'Success' },
        ]}
      />
    );

    const data = captured['line-chart'].data;
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
    render(<FeedbackChart feedbackCounts={{ helpful: 6, notHelpful: 2 }} />);

    const data = captured['bar-chart'].data;
    expect(data).toEqual([
      { name: 'Helpful', value: 6, color: '#34a853' },
      { name: 'Not Helpful', value: 2, color: '#ea4335' },
    ]);

    const cells = screen.getAllByTestId('cell').map(cell => cell.getAttribute('data-fill'));
    expect(cells).toContain('#34a853');
    expect(cells).toContain('#ea4335');
  });
});
