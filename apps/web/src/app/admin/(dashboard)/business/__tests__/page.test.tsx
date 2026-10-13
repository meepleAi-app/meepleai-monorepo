/**
 * #4169 (ADR-095 D6/D8): la pagina dei costi non offre più un secondo budget.
 *
 * `AppBudget` era un budget mensile che il gateway LLM non applicava: la pagina lo faceva impostare
 * («Imposta budget») e misurare (`BudgetGauge`) come se contasse. L'unico budget è quello del gateway,
 * nel tab Budget dell'AI.
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/components/admin/business/CostKpiStrip', () => ({
  CostKpiStrip: () => <div data-testid="cost-kpi-strip" />,
}));
vi.mock('@/components/admin/business/BudgetKpiStrip', () => ({
  BudgetKpiStrip: () => <div data-testid="budget-kpi-strip" />,
}));
vi.mock('@/components/admin/business/BudgetGauge', () => ({
  BudgetGauge: () => <div data-testid="budget-gauge" />,
}));
vi.mock('@/components/admin/business/SetBudgetDialog', () => ({
  SetBudgetDialog: () => <div data-testid="set-budget-dialog" />,
}));
vi.mock('@/components/admin/business/CostStackedArea', () => ({
  CostStackedArea: () => <div data-testid="cost-stacked-area" />,
}));
vi.mock('@/components/admin/business/CostSimulator', () => ({
  CostSimulator: () => <div data-testid="cost-simulator" />,
}));
vi.mock('@/components/admin/business/FeatureCostTable', () => ({
  FeatureCostTable: () => <div data-testid="feature-cost-table" />,
}));

import BudgetPage from '../page';

describe('/admin/business (#4169)', () => {
  it('offers no second budget to set or measure', () => {
    render(<BudgetPage />);

    expect(screen.queryByRole('button', { name: /imposta budget/i })).not.toBeInTheDocument();
    expect(screen.queryByTestId('set-budget-dialog')).not.toBeInTheDocument();
    expect(screen.queryByTestId('budget-gauge')).not.toBeInTheDocument();
  });

  it('shows the costs, and points to the caps the gateway applies', () => {
    render(<BudgetPage />);

    expect(screen.getByTestId('cost-kpi-strip')).toBeInTheDocument();
    expect(screen.getByTestId('cost-stacked-area')).toBeInTheDocument();
    expect(screen.getByTestId('feature-cost-table')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /ai → budget/i })).toHaveAttribute(
      'href',
      '/admin/ai?tab=config'
    );
  });
});
