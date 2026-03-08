import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/components/admin/charts/ChartsSection', () => ({
  ChartsSection: () => <div data-testid="charts-section" />,
}));

import { OverviewTab } from '../OverviewTab';

describe('OverviewTab', () => {
  it('renders heading', () => {
    render(<OverviewTab />);
    expect(screen.getByText('Analytics Overview')).toBeInTheDocument();
  });

  it('renders ChartsSection component', () => {
    render(<OverviewTab />);
    expect(screen.getByTestId('charts-section')).toBeInTheDocument();
  });
});
