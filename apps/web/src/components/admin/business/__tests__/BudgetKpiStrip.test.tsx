import { screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

const useBudgetMock = vi.hoisted(() => vi.fn());
const useCostBreakdownMock = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/useBudget', () => ({
  useBudget: () => useBudgetMock(),
  APP_BUDGET_QUERY_KEY: ['admin', 'budget'],
}));

vi.mock('@/hooks/useCostBreakdown', () => ({
  useCostBreakdown: () => useCostBreakdownMock(),
  COST_BREAKDOWN_QUERY_KEY: () => ['admin', 'business', 'breakdown'],
}));

import { BudgetKpiStrip } from '../BudgetKpiStrip';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

describe('BudgetKpiStrip', () => {
  it('renders 4 skeleton placeholders while budget query is loading', () => {
    useBudgetMock.mockReturnValue({ budget: null, isLoading: true, isError: false });
    useCostBreakdownMock.mockReturnValue({ data: undefined });

    renderWithQuery(<BudgetKpiStrip />);

    const strip = screen.getByTestId('budget-kpi-strip');
    expect(strip).toHaveAttribute('aria-busy', 'true');
    expect(screen.getByTestId('budget-kpi-skeleton-today')).toBeInTheDocument();
    expect(screen.getByTestId('budget-kpi-skeleton-month')).toBeInTheDocument();
    expect(screen.getByTestId('budget-kpi-skeleton-remaining')).toBeInTheDocument();
    expect(screen.getByTestId('budget-kpi-skeleton-projected')).toBeInTheDocument();
  });

  it('renders 4 empty-state cards with "—" placeholders when budget is not configured', () => {
    useBudgetMock.mockReturnValue({ budget: null, isLoading: false, isError: false });
    useCostBreakdownMock.mockReturnValue({ data: undefined });

    renderWithQuery(<BudgetKpiStrip />);

    const today = screen.getByTestId('budget-kpi-empty-today');
    expect(today).toBeInTheDocument();
    expect(today).toHaveTextContent('—');
    expect(today).toHaveAttribute('title', expect.stringMatching(/imposta budget/i));
    expect(screen.getByTestId('budget-kpi-empty-month')).toHaveTextContent('—');
    expect(screen.getByTestId('budget-kpi-empty-remaining')).toHaveTextContent('—');
    expect(screen.getByTestId('budget-kpi-empty-projected')).toHaveTextContent('—');
  });
});
