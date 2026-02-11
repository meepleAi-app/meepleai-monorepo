/**
 * RevenueVsCostsChart Tests
 * Issue #3723 - Ledger Dashboard and Visualization
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import type { MonthlyRevenueData } from '@/lib/api/schemas/financial-ledger.schemas';

import { RevenueVsCostsChart } from '../RevenueVsCostsChart';

const MOCK_DATA: MonthlyRevenueData[] = [
  { month: 'Jan 26', revenue: 2000, costs: 1000 },
  { month: 'Feb 26', revenue: 2500, costs: 1100 },
  { month: 'Mar 26', revenue: 2800, costs: 1200 },
  { month: 'Apr 26', revenue: 3000, costs: 1300 },
  { month: 'May 26', revenue: 3200, costs: 1400 },
  { month: 'Jun 26', revenue: 3500, costs: 1500 },
];

describe('RevenueVsCostsChart', () => {
  it('renders chart container with data', () => {
    render(<RevenueVsCostsChart data={MOCK_DATA} loading={false} />);
    expect(screen.getByTestId('revenue-vs-costs-chart')).toBeInTheDocument();
  });

  it('renders chart title', () => {
    render(<RevenueVsCostsChart data={MOCK_DATA} loading={false} />);
    expect(screen.getByText('Revenue vs Costs (12 Months)')).toBeInTheDocument();
  });

  it('displays total revenue in footer', () => {
    render(<RevenueVsCostsChart data={MOCK_DATA} loading={false} />);
    expect(screen.getByText('Total Revenue')).toBeInTheDocument();
  });

  it('displays total costs in footer', () => {
    render(<RevenueVsCostsChart data={MOCK_DATA} loading={false} />);
    expect(screen.getByText('Total Costs')).toBeInTheDocument();
  });

  it('displays profit trend label', () => {
    render(<RevenueVsCostsChart data={MOCK_DATA} loading={false} />);
    expect(screen.getByText('Profit Trend')).toBeInTheDocument();
  });

  it('shows loading skeleton when loading', () => {
    render(<RevenueVsCostsChart data={[]} loading={true} />);
    expect(screen.getByTestId('revenue-chart-skeleton')).toBeInTheDocument();
  });

  it('shows empty state when no data', () => {
    render(<RevenueVsCostsChart data={[]} loading={false} />);
    expect(screen.getByText('No revenue data available')).toBeInTheDocument();
  });

  it('renders Recharts container with data', () => {
    const { container } = render(<RevenueVsCostsChart data={MOCK_DATA} loading={false} />);
    expect(container.querySelector('.recharts-responsive-container')).toBeInTheDocument();
  });

  it('renders with single data point without error', () => {
    const singleData: MonthlyRevenueData[] = [{ month: 'Jan 26', revenue: 1000, costs: 500 }];
    render(<RevenueVsCostsChart data={singleData} loading={false} />);
    expect(screen.getByTestId('revenue-vs-costs-chart')).toBeInTheDocument();
  });

  it('handles zero revenue and costs', () => {
    const zeroData: MonthlyRevenueData[] = [
      { month: 'Jan 26', revenue: 0, costs: 0 },
      { month: 'Feb 26', revenue: 0, costs: 0 },
    ];
    render(<RevenueVsCostsChart data={zeroData} loading={false} />);
    expect(screen.getByTestId('revenue-vs-costs-chart')).toBeInTheDocument();
  });
});
