/**
 * CostBreakdownPanel unit tests.
 * Issue #5080: Admin usage page — cost breakdown panel.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { CostBreakdownPanel } from '../CostBreakdownPanel';

// ─── Recharts mock ────────────────────────────────────────────────────────────

vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="responsive-container">{children}</div>
  ),
  PieChart: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="pie-chart">{children}</div>
  ),
  Pie:     () => <div data-testid="pie" />,
  Cell:    () => <div data-testid="cell" />,
  Tooltip: () => <div data-testid="tooltip" />,
  Legend:  () => <div data-testid="legend" />,
}));

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const mockData = {
  byModel: [
    { modelId: 'openai/gpt-4o',     costUsd: 0.05231, requests: 12, totalTokens: 5000 },
    { modelId: 'anthropic/claude-3', costUsd: 0.01234, requests:  5, totalTokens: 2000 },
  ],
  bySource: [
    { source: 'RagPipeline', costUsd: 0.04,  requests: 10 },
    { source: 'Manual',      costUsd: 0.025, requests:  7 },
  ],
  byTier: [
    { tier: 'premium', costUsd: 0.05, requests:  8 },
    { tier: 'free',    costUsd: 0.0,  requests:  9 },
  ],
  totalCostUsd:  0.06465,
  totalRequests: 17,
  period:        '7d',
};

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('CostBreakdownPanel', () => {
  const noop = vi.fn();

  it('renders panel title', () => {
    render(<CostBreakdownPanel data={mockData} period="7d" onPeriodChange={noop} />);
    expect(screen.getByText('Cost Breakdown')).toBeInTheDocument();
  });

  it('renders period selector buttons', () => {
    render(<CostBreakdownPanel data={mockData} period="7d" onPeriodChange={noop} />);
    expect(screen.getByRole('button', { name: '1d' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '7d' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '30d' })).toBeInTheDocument();
  });

  it('calls onPeriodChange with "1d" when 1d button clicked', () => {
    const onChange = vi.fn();
    render(<CostBreakdownPanel data={mockData} period="7d" onPeriodChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: '1d' }));
    expect(onChange).toHaveBeenCalledWith('1d');
  });

  it('calls onPeriodChange with "30d" when 30d button clicked', () => {
    const onChange = vi.fn();
    render(<CostBreakdownPanel data={mockData} period="7d" onPeriodChange={onChange} />);
    fireEvent.click(screen.getByRole('button', { name: '30d' }));
    expect(onChange).toHaveBeenCalledWith('30d');
  });

  it('shows total requests in summary', () => {
    render(<CostBreakdownPanel data={mockData} period="7d" onPeriodChange={noop} />);
    expect(screen.getByText(/17/)).toBeInTheDocument();
  });

  it('renders model tab by default — shows model names', () => {
    render(<CostBreakdownPanel data={mockData} period="7d" onPeriodChange={noop} />);
    expect(screen.getByText('gpt-4o')).toBeInTheDocument();
    expect(screen.getByText('claude-3')).toBeInTheDocument();
  });

  it('switches to source tab and shows source rows', () => {
    render(<CostBreakdownPanel data={mockData} period="7d" onPeriodChange={noop} />);
    // Tab buttons capitalize the first letter: "Source" not "source"
    fireEvent.click(screen.getByText('Source'));
    expect(screen.getByText('RagPipeline')).toBeInTheDocument();
    expect(screen.getByText('Manual')).toBeInTheDocument();
  });

  it('switches to tier tab and shows tier rows', () => {
    render(<CostBreakdownPanel data={mockData} period="7d" onPeriodChange={noop} />);
    // Tab buttons capitalize the first letter: "Tier" not "tier"
    fireEvent.click(screen.getByText('Tier'));
    expect(screen.getByText('premium')).toBeInTheDocument();
    expect(screen.getByText('free')).toBeInTheDocument();
  });

  it('renders donut PieChart when data available', () => {
    render(<CostBreakdownPanel data={mockData} period="7d" onPeriodChange={noop} />);
    expect(screen.getByTestId('pie-chart')).toBeInTheDocument();
  });

  it('shows loading skeleton when isLoading', () => {
    render(<CostBreakdownPanel data={null} period="7d" onPeriodChange={noop} isLoading />);
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when byModel is empty', () => {
    render(
      <CostBreakdownPanel
        data={{ ...mockData, byModel: [] }}
        period="7d"
        onPeriodChange={noop}
      />
    );
    expect(screen.getByText('No cost data in this period')).toBeInTheDocument();
  });

  it('shows empty state when data is null', () => {
    render(<CostBreakdownPanel data={null} period="7d" onPeriodChange={noop} />);
    expect(screen.getByText('No cost data in this period')).toBeInTheDocument();
  });
});
