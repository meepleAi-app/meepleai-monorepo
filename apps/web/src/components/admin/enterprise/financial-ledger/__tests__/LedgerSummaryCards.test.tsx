/**
 * LedgerSummaryCards Tests
 * Issue #3722 - Manual Ledger CRUD
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { LedgerSummaryCards } from '../LedgerSummaryCards';

describe('LedgerSummaryCards', () => {
  const defaultProps = {
    totalIncome: 5000,
    totalExpense: 1200,
    netBalance: 3800,
    loading: false,
  };

  it('renders all three summary cards', () => {
    render(<LedgerSummaryCards {...defaultProps} />);
    expect(screen.getByTestId('ledger-summary-cards')).toBeInTheDocument();
    expect(screen.getByTestId('summary-income')).toBeInTheDocument();
    expect(screen.getByTestId('summary-expense')).toBeInTheDocument();
    expect(screen.getByTestId('summary-net-balance')).toBeInTheDocument();
  });

  it('displays formatted income amount', () => {
    render(<LedgerSummaryCards {...defaultProps} />);
    const incomeCard = screen.getByTestId('summary-income');
    expect(incomeCard.textContent).toContain('5,000.00');
  });

  it('displays formatted expense amount', () => {
    render(<LedgerSummaryCards {...defaultProps} />);
    const expenseCard = screen.getByTestId('summary-expense');
    expect(expenseCard.textContent).toContain('1,200.00');
  });

  it('displays formatted net balance', () => {
    render(<LedgerSummaryCards {...defaultProps} />);
    const balanceCard = screen.getByTestId('summary-net-balance');
    expect(balanceCard.textContent).toContain('3,800.00');
  });

  it('displays negative net balance correctly', () => {
    render(<LedgerSummaryCards {...defaultProps} netBalance={-500} />);
    const balanceCard = screen.getByTestId('summary-net-balance');
    expect(balanceCard.textContent).toContain('500.00');
  });

  it('shows loading skeleton when loading', () => {
    render(<LedgerSummaryCards {...defaultProps} loading={true} />);
    const container = screen.getByTestId('ledger-summary-cards');
    const pulseElements = container.querySelectorAll('.animate-pulse');
    expect(pulseElements.length).toBe(3);
  });

  it('shows labels for each card', () => {
    render(<LedgerSummaryCards {...defaultProps} />);
    expect(screen.getByText('Total Income')).toBeInTheDocument();
    expect(screen.getByText('Total Expense')).toBeInTheDocument();
    expect(screen.getByText('Net Balance')).toBeInTheDocument();
  });

  it('renders zero values correctly', () => {
    render(<LedgerSummaryCards totalIncome={0} totalExpense={0} netBalance={0} loading={false} />);
    const incomeCard = screen.getByTestId('summary-income');
    expect(incomeCard.textContent).toContain('0.00');
  });
});
