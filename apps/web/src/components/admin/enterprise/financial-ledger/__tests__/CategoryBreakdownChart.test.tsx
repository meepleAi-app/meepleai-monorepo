/**
 * CategoryBreakdownChart Tests
 * Issue #3723 - Ledger Dashboard and Visualization
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import type { CategoryBreakdownItem } from '@/lib/api/schemas/financial-ledger.schemas';

import { CategoryBreakdownChart } from '../CategoryBreakdownChart';

const MOCK_DATA: CategoryBreakdownItem[] = [
  { category: 'Subscription', amount: 5000, percentage: 40.0, type: 'Income' },
  { category: 'TokenPurchase', amount: 3000, percentage: 24.0, type: 'Income' },
  { category: 'Infrastructure', amount: 2500, percentage: 35.0, type: 'Expense' },
  { category: 'TokenUsage', amount: 1500, percentage: 21.0, type: 'Expense' },
  { category: 'Other', amount: 500, percentage: 7.0, type: 'Expense' },
];

describe('CategoryBreakdownChart', () => {
  it('renders chart container', () => {
    render(<CategoryBreakdownChart data={MOCK_DATA} loading={false} />);
    expect(screen.getByTestId('category-breakdown-chart')).toBeInTheDocument();
  });

  it('renders chart title', () => {
    render(<CategoryBreakdownChart data={MOCK_DATA} loading={false} />);
    expect(screen.getByText('Breakdown by Category')).toBeInTheDocument();
  });

  it('renders all category rows', () => {
    render(<CategoryBreakdownChart data={MOCK_DATA} loading={false} />);
    expect(screen.getByTestId('category-row-Subscription')).toBeInTheDocument();
    expect(screen.getByTestId('category-row-TokenPurchase')).toBeInTheDocument();
    expect(screen.getByTestId('category-row-Infrastructure')).toBeInTheDocument();
    expect(screen.getByTestId('category-row-TokenUsage')).toBeInTheDocument();
    expect(screen.getByTestId('category-row-Other')).toBeInTheDocument();
  });

  it('displays formatted category labels', () => {
    render(<CategoryBreakdownChart data={MOCK_DATA} loading={false} />);
    expect(screen.getByText('Subscription')).toBeInTheDocument();
    expect(screen.getByText('Token Purchase')).toBeInTheDocument();
    expect(screen.getByText('Infrastructure')).toBeInTheDocument();
  });

  it('displays type badges for income and expense', () => {
    render(<CategoryBreakdownChart data={MOCK_DATA} loading={false} />);
    const incomeBadges = screen.getAllByText('Income');
    const expenseBadges = screen.getAllByText('Expense');
    expect(incomeBadges.length).toBe(2);
    expect(expenseBadges.length).toBe(3);
  });

  it('displays percentage values', () => {
    render(<CategoryBreakdownChart data={MOCK_DATA} loading={false} />);
    expect(screen.getByText('40.0%')).toBeInTheDocument();
    expect(screen.getByText('24.0%')).toBeInTheDocument();
    expect(screen.getByText('35.0%')).toBeInTheDocument();
  });

  it('sorts categories by amount descending', () => {
    render(<CategoryBreakdownChart data={MOCK_DATA} loading={false} />);
    const rows = screen.getAllByTestId(/^category-row-/);
    expect(rows[0]).toHaveAttribute('data-testid', 'category-row-Subscription');
    expect(rows[1]).toHaveAttribute('data-testid', 'category-row-TokenPurchase');
    expect(rows[2]).toHaveAttribute('data-testid', 'category-row-Infrastructure');
  });

  it('renders progress bars with aria attributes', () => {
    render(<CategoryBreakdownChart data={MOCK_DATA} loading={false} />);
    const progressBars = screen.getAllByRole('progressbar');
    expect(progressBars.length).toBe(5);
    expect(progressBars[0]).toHaveAttribute('aria-valuenow', '40');
  });

  it('shows loading skeleton when loading', () => {
    render(<CategoryBreakdownChart data={[]} loading={true} />);
    expect(screen.getByTestId('category-chart-skeleton')).toBeInTheDocument();
  });

  it('shows empty state when no data', () => {
    render(<CategoryBreakdownChart data={[]} loading={false} />);
    expect(screen.getByText('No category data available')).toBeInTheDocument();
  });

  it('renders single category without error', () => {
    const singleData: CategoryBreakdownItem[] = [
      { category: 'Subscription', amount: 1000, percentage: 100.0, type: 'Income' },
    ];
    render(<CategoryBreakdownChart data={singleData} loading={false} />);
    expect(screen.getByTestId('category-row-Subscription')).toBeInTheDocument();
  });
});
