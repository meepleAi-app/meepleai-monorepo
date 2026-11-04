/**
 * #4169: i KPI della pagina dei costi vengono dal registro dei costi, non da `AppBudget`, e l'unico
 * budget mostrato è il tetto mensile del gateway LLM.
 */
import { screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { CostKpiStrip, summarizeCosts } from '../CostKpiStrip';

const useCostBreakdownMock = vi.hoisted(() => vi.fn());
const useLlmSystemConfigMock = vi.hoisted(() => vi.fn());

vi.mock('@/hooks/useCostBreakdown', () => ({
  useCostBreakdown: () => useCostBreakdownMock(),
}));
vi.mock('@/hooks/queries/useProviders', () => ({
  useLlmSystemConfig: () => useLlmSystemConfigMock(),
}));

const day = (date: string, total: number) => ({ date: `${date}T00:00:00Z`, providers: [], total });
const NOW = new Date('2026-10-11T15:00:00Z');
const DAYS = [
  day('2026-09-30', 5),
  day('2026-10-01', 1),
  day('2026-10-10', 2),
  day('2026-10-11', 0.5),
];

describe('summarizeCosts', () => {
  it('sums today and the month to date, and projects the month linearly', () => {
    const summary = summarizeCosts(DAYS, NOW);

    expect(summary.spentToday).toBe(0.5);
    expect(summary.spentMonth).toBe(3.5); // settembre escluso
    expect(summary.projectedMonthEnd).toBeCloseTo((3.5 / 11) * 31, 6);
    expect(summary.daysLeft).toBe(20);
  });
});

describe('CostKpiStrip', () => {
  it('shows the spend from the cost log and the monthly cap of the gateway', () => {
    useCostBreakdownMock.mockReturnValue({ data: { days: DAYS }, isLoading: false });
    useLlmSystemConfigMock.mockReturnValue({ data: { monthlyBudgetUsd: 20 } });

    renderWithQuery(<CostKpiStrip now={NOW} />);

    expect(screen.getByTestId('cost-kpi-today')).toHaveTextContent('0.50');
    expect(screen.getByTestId('cost-kpi-month')).toHaveTextContent('3.50');
    expect(screen.getByTestId('cost-kpi-caps')).toHaveTextContent('20.00');
    expect(screen.getByRole('link', { name: /ai → budget/i })).toHaveAttribute(
      'href',
      '/admin/ai?tab=config'
    );
  });

  it('says so when the cost log does not answer', () => {
    useCostBreakdownMock.mockReturnValue({ data: undefined, isLoading: false });
    useLlmSystemConfigMock.mockReturnValue({ data: undefined });

    renderWithQuery(<CostKpiStrip now={NOW} />);

    expect(screen.getByTestId('cost-kpi-unavailable')).toBeInTheDocument();
    expect(screen.getByTestId('cost-kpi-caps')).toHaveTextContent('—');
  });
});
