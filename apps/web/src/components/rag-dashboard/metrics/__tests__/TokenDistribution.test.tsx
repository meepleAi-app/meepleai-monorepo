import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { TokenDistribution } from '../TokenDistribution';
import type { TokenUsage } from '../types';

const mockTokenData: TokenUsage = {
  input: 1500,
  output: 800,
  context: 3500,
  total: 5800,
  costEstimate: 0.023,
};

describe('TokenDistribution', () => {
  it('renders the component title', () => {
    render(<TokenDistribution data={mockTokenData} />);
    expect(screen.getByText('Token Usage')).toBeInTheDocument();
  });

  it('displays total tokens (formatted)', () => {
    render(<TokenDistribution data={mockTokenData} />);
    // formatTokens shows "5.8K" for 5800
    expect(screen.getByText('5.8K')).toBeInTheDocument();
  });

  it('shows token distribution labels', () => {
    render(<TokenDistribution data={mockTokenData} />);
    expect(screen.getByText('Input')).toBeInTheDocument();
    expect(screen.getByText('Output')).toBeInTheDocument();
    expect(screen.getByText('Context')).toBeInTheDocument();
  });

  it('displays individual token counts (formatted)', () => {
    render(<TokenDistribution data={mockTokenData} />);
    // formatTokens shows "1.5K", "800", "3.5K"
    expect(screen.getByText('1.5K')).toBeInTheDocument();
    expect(screen.getByText('800')).toBeInTheDocument();
    expect(screen.getByText('3.5K')).toBeInTheDocument();
  });

  it('shows cost estimate', () => {
    render(<TokenDistribution data={mockTokenData} />);
    expect(screen.getByText('$0.023')).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(
      <TokenDistribution data={mockTokenData} className="custom-class" />
    );
    expect(container.firstChild).toHaveClass('custom-class');
  });

  it('handles large token counts gracefully', () => {
    const highUsageData: TokenUsage = {
      input: 300000,
      output: 250000,
      context: 400000,
      total: 950000,
      costEstimate: 0.38,
    };
    render(<TokenDistribution data={highUsageData} />);
    // formatTokens shows "950.0K" for 950000
    expect(screen.getByText('950.0K')).toBeInTheDocument();
  });

  it('handles million-level token counts', () => {
    const millionUsageData: TokenUsage = {
      input: 500000,
      output: 250000,
      context: 750000,
      total: 1500000,
      costEstimate: 0.60,
    };
    render(<TokenDistribution data={millionUsageData} />);
    // formatTokens shows "1.5M" for 1500000
    expect(screen.getByText('1.5M')).toBeInTheDocument();
  });

  it('renders the stacked bar visualization', () => {
    const { container } = render(<TokenDistribution data={mockTokenData} />);
    // Check for the stacked bar container
    const stackedBar = container.querySelector('.flex.h-8.rounded-lg');
    expect(stackedBar).toBeInTheDocument();
  });
});
