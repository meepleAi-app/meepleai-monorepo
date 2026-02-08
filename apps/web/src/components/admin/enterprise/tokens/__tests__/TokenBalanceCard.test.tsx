/**
 * TokenBalanceCard Tests
 * Issue #3692 - Token Management System
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { TokenBalanceCard } from '../TokenBalanceCard';

const defaultProps = {
  currentBalance: 450,
  totalBudget: 1000,
  currency: 'EUR',
  usagePercent: 45,
  projectedDaysUntilDepletion: 12,
};

describe('TokenBalanceCard', () => {
  it('renders balance amount', () => {
    render(<TokenBalanceCard {...defaultProps} />);
    expect(screen.getByTestId('token-balance-amount')).toBeInTheDocument();
  });

  it('renders usage percentage', () => {
    render(<TokenBalanceCard {...defaultProps} />);
    expect(screen.getByTestId('token-usage-percent')).toHaveTextContent('45.0% used');
  });

  it('renders depletion projection', () => {
    render(<TokenBalanceCard {...defaultProps} />);
    expect(screen.getByTestId('token-depletion-projection')).toHaveTextContent('~12 days remaining');
  });

  it('renders progress bar', () => {
    render(<TokenBalanceCard {...defaultProps} />);
    expect(screen.getByTestId('token-usage-bar')).toBeInTheDocument();
  });

  it('shows loading skeleton', () => {
    render(<TokenBalanceCard {...defaultProps} loading />);
    const card = screen.getByTestId('token-balance-card');
    expect(card.className).toContain('animate-pulse');
  });

  it('shows warning color when usage >= 80%', () => {
    render(<TokenBalanceCard {...defaultProps} usagePercent={85} />);
    const card = screen.getByTestId('token-balance-card');
    expect(card.className).toContain('amber');
  });

  it('shows critical color when usage >= 95%', () => {
    render(<TokenBalanceCard {...defaultProps} usagePercent={97} />);
    const card = screen.getByTestId('token-balance-card');
    expect(card.className).toContain('red');
  });

  it('handles null depletion projection', () => {
    render(<TokenBalanceCard {...defaultProps} projectedDaysUntilDepletion={null} />);
    expect(screen.queryByTestId('token-depletion-projection')).not.toBeInTheDocument();
  });

  it('renders OpenRouter Balance header', () => {
    render(<TokenBalanceCard {...defaultProps} />);
    expect(screen.getByText('OpenRouter Balance')).toBeInTheDocument();
  });
});
