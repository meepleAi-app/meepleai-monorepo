/**
 * ConsumptionChart Tests
 * Issue #3692 - Token Management System
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ConsumptionChart } from '../ConsumptionChart';

const mockPoints = Array.from({ length: 30 }, (_, i) => ({
  date: `2026-01-${String(i + 1).padStart(2, '0')}`,
  tokens: 3000 + Math.floor(i * 100),
  cost: 0.45 + i * 0.015,
}));

const defaultProps = {
  points: mockPoints,
  totalTokens: 156000,
  avgDailyTokens: 5200,
  avgDailyCost: 0.78,
};

describe('ConsumptionChart', () => {
  it('renders chart container', () => {
    render(<ConsumptionChart {...defaultProps} />);
    expect(screen.getByTestId('consumption-chart')).toBeInTheDocument();
  });

  it('renders header text', () => {
    render(<ConsumptionChart {...defaultProps} />);
    expect(screen.getByText('Token Consumption')).toBeInTheDocument();
  });

  it('renders period toggle buttons', () => {
    render(<ConsumptionChart {...defaultProps} />);
    expect(screen.getByTestId('consumption-period-7')).toHaveTextContent('7 days');
    expect(screen.getByTestId('consumption-period-30')).toHaveTextContent('30 days');
  });

  it('renders bars container', () => {
    render(<ConsumptionChart {...defaultProps} />);
    expect(screen.getByTestId('consumption-bars')).toBeInTheDocument();
  });

  it('defaults to 7-day view with 7 bars', () => {
    render(<ConsumptionChart {...defaultProps} />);
    const bars = screen.getByTestId('consumption-bars');
    // 7-day view should show 7 groups
    expect(bars.children.length).toBe(7);
  });

  it('switches to 30-day view on click', () => {
    render(<ConsumptionChart {...defaultProps} />);
    fireEvent.click(screen.getByTestId('consumption-period-30'));
    const bars = screen.getByTestId('consumption-bars');
    expect(bars.children.length).toBe(30);
  });

  it('renders total tokens stat', () => {
    render(<ConsumptionChart {...defaultProps} />);
    expect(screen.getByTestId('consumption-total')).toHaveTextContent('156.0K');
  });

  it('renders avg daily tokens stat', () => {
    render(<ConsumptionChart {...defaultProps} />);
    expect(screen.getByTestId('consumption-avg-daily')).toHaveTextContent('5.2K');
  });

  it('renders avg daily cost stat', () => {
    render(<ConsumptionChart {...defaultProps} />);
    expect(screen.getByTestId('consumption-avg-cost')).toHaveTextContent('€0.78');
  });

  it('shows loading skeleton', () => {
    render(<ConsumptionChart {...defaultProps} loading />);
    const chart = screen.getByTestId('consumption-chart');
    expect(chart.className).toContain('animate-pulse');
  });
});
