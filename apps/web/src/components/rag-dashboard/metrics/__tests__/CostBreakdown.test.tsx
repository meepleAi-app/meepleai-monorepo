import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { CostBreakdown } from '../CostBreakdown';
import type { CostMetrics } from '../types';

const mockCostData: CostMetrics = {
  currentSession: 0.45,
  projectedMonthly: 125,
  avgPerQuery: 0.0023,
  byStrategy: {
    Hybrid: 0.003,
    Semantic: 0.0025,
    Keyword: 0.001,
    Contextual: 0.0028,
    MultiQuery: 0.004,
    Agentic: 0.005,
  },
  budgetUsed: 85,
  budgetLimit: 200,
};

describe('CostBreakdown', () => {
  it('renders the component title', () => {
    render(<CostBreakdown data={mockCostData} />);
    expect(screen.getByText('Cost Analysis')).toBeInTheDocument();
  });

  it('displays session cost', () => {
    render(<CostBreakdown data={mockCostData} />);
    expect(screen.getByText('Session Cost')).toBeInTheDocument();
    expect(screen.getByText('$0.45')).toBeInTheDocument();
  });

  it('shows projected monthly cost', () => {
    render(<CostBreakdown data={mockCostData} />);
    expect(screen.getByText('Monthly Projected')).toBeInTheDocument();
    expect(screen.getByText('$125')).toBeInTheDocument();
  });

  it('displays average cost per query', () => {
    render(<CostBreakdown data={mockCostData} />);
    expect(screen.getByText('Average Cost Per Query')).toBeInTheDocument();
    expect(screen.getByText('$0.0023')).toBeInTheDocument();
  });

  it('shows budget usage', () => {
    render(<CostBreakdown data={mockCostData} />);
    expect(screen.getByText('Budget Usage')).toBeInTheDocument();
    expect(screen.getByText('$85 / $200')).toBeInTheDocument();
  });

  it('displays budget percentage', () => {
    render(<CostBreakdown data={mockCostData} />);
    // 85 / 200 = 42.5% → rounds to 43%
    expect(screen.getByText(/43.*% used/)).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(
      <CostBreakdown data={mockCostData} className="custom-class" />
    );
    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('shows alert badge when budget exceeds 90%', () => {
    const highBudgetData: CostMetrics = {
      ...mockCostData,
      budgetUsed: 185,
    };
    render(<CostBreakdown data={highBudgetData} />);
    expect(screen.getByText('Alert')).toBeInTheDocument();
  });

  it('does not show alert badge when budget is under 90%', () => {
    render(<CostBreakdown data={mockCostData} />);
    expect(screen.queryByText('Alert')).not.toBeInTheDocument();
  });

  it('handles low budget usage', () => {
    const lowBudgetData: CostMetrics = {
      ...mockCostData,
      budgetUsed: 20,
    };
    render(<CostBreakdown data={lowBudgetData} />);
    expect(screen.getByText('$20 / $200')).toBeInTheDocument();
    expect(screen.getByText(/10.*% used/)).toBeInTheDocument();
  });

  it('renders budget progress bar', () => {
    const { container } = render(<CostBreakdown data={mockCostData} />);
    const progressBar = container.querySelector('.bg-muted.rounded-full');
    expect(progressBar).toBeInTheDocument();
  });
});
