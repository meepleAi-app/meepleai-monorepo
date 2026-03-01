/**
 * RequestTimelineChart unit tests.
 * Issue #5078: Admin usage page — request timeline chart.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { RequestTimelineChart } from '../RequestTimelineChart';

// ─── Recharts mock ────────────────────────────────────────────────────────────

vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="responsive-container">{children}</div>
  ),
  ComposedChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="composed-chart">{children}</div>
  ),
  Area:          () => <div data-testid="area" />,
  XAxis:         () => <div data-testid="x-axis" />,
  YAxis:         () => <div data-testid="y-axis" />,
  CartesianGrid: () => <div data-testid="cartesian-grid" />,
  Tooltip:       () => <div data-testid="tooltip" />,
  Legend:        () => <div data-testid="legend" />,
}));

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const mockData = {
  buckets: [
    {
      bucket:         '2026-02-22T10:00:00Z',
      label:          '10:00',
      manual:         5,
      ragPipeline:    3,
      eventDriven:    1,
      automatedTest:  0,
      agentTask:      2,
      adminOperation: 0,
      totalCostUsd:   0.0012,
    },
    {
      bucket:         '2026-02-22T11:00:00Z',
      label:          '11:00',
      manual:         8,
      ragPipeline:    2,
      eventDriven:    0,
      automatedTest:  1,
      agentTask:      3,
      adminOperation: 1,
      totalCostUsd:   0.0025,
    },
  ],
  period:        '24h',
  groupedByHour: true,
  totalRequests: 26,
  totalCostUsd:  0.0037,
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('RequestTimelineChart', () => {
  const noop = vi.fn();

  it('renders chart title', () => {
    render(<RequestTimelineChart data={mockData} period="24h" onPeriodChange={noop} />);
    expect(screen.getByText('Request Timeline')).toBeInTheDocument();
  });

  it('renders period selector buttons', () => {
    render(<RequestTimelineChart data={mockData} period="24h" onPeriodChange={noop} />);
    expect(screen.getByRole('button', { name: '24h' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '7d' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '30d' })).toBeInTheDocument();
  });

  it('calls onPeriodChange with "7d" when 7d button clicked', () => {
    const onChange = vi.fn();
    render(<RequestTimelineChart data={mockData} period="24h" onPeriodChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: '7d' }));
    expect(onChange).toHaveBeenCalledWith('7d');
  });

  it('calls onPeriodChange with "30d" when 30d button clicked', () => {
    const onChange = vi.fn();
    render(<RequestTimelineChart data={mockData} period="24h" onPeriodChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: '30d' }));
    expect(onChange).toHaveBeenCalledWith('30d');
  });

  it('shows total requests in summary', () => {
    render(<RequestTimelineChart data={mockData} period="24h" onPeriodChange={noop} />);
    expect(screen.getByText(/26/)).toBeInTheDocument();
  });

  it('shows total cost in summary', () => {
    render(<RequestTimelineChart data={mockData} period="24h" onPeriodChange={noop} />);
    expect(screen.getByText(/0\.0037/)).toBeInTheDocument();
  });

  it('renders recharts ResponsiveContainer when data available', () => {
    render(<RequestTimelineChart data={mockData} period="24h" onPeriodChange={noop} />);
    expect(screen.getByTestId('responsive-container')).toBeInTheDocument();
    expect(screen.getByTestId('composed-chart')).toBeInTheDocument();
  });

  it('shows loading skeleton when isLoading', () => {
    render(<RequestTimelineChart data={null} period="24h" onPeriodChange={noop} isLoading />);
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when data is null', () => {
    render(<RequestTimelineChart data={null} period="24h" onPeriodChange={noop} />);
    expect(screen.getByText('No requests in this period')).toBeInTheDocument();
  });

  it('shows empty state when buckets array is empty', () => {
    const emptyData = { ...mockData, buckets: [], totalRequests: 0 };
    render(<RequestTimelineChart data={emptyData} period="24h" onPeriodChange={noop} />);
    expect(screen.getByText('No requests in this period')).toBeInTheDocument();
  });

  it('does not show summary when data is null', () => {
    render(<RequestTimelineChart data={null} period="24h" onPeriodChange={noop} />);
    expect(screen.queryByText(/requests ·/)).not.toBeInTheDocument();
  });
});
