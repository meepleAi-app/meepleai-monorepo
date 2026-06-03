import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { BudgetKpiStrip } from '../BudgetKpiStrip';

describe('BudgetKpiStrip', () => {
  it('renders 4 KPI boxes with BE-pending placeholders', () => {
    render(<BudgetKpiStrip />);

    expect(screen.getByTestId('budget-kpi-strip')).toBeInTheDocument();
    expect(screen.getByTestId('budget-kpi-spesa-oggi')).toHaveTextContent('—');
    expect(screen.getByTestId('budget-kpi-spesa-mese')).toHaveTextContent('—');
    expect(screen.getByTestId('budget-kpi-budget-residuo')).toHaveTextContent('—');
    expect(screen.getByTestId('budget-kpi-proiezione-fine-mese')).toHaveTextContent('—');
  });

  it('shows endpoint hint in tooltip', () => {
    render(<BudgetKpiStrip />);

    const oggi = screen.getByTestId('budget-kpi-spesa-oggi');
    expect(oggi).toHaveAttribute('title', expect.stringMatching(/breakdown/i));
  });
});
