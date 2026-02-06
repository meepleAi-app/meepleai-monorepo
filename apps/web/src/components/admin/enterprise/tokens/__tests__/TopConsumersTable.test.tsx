/**
 * TopConsumersTable Tests
 * Issue #3692 - Token Management System
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { TopConsumersTable } from '../TopConsumersTable';

const mockConsumers = [
  { userId: 'user-1', displayName: 'Alice Demo', email: 'alice@example.com', tier: 'Pro' as const, tokensUsed: 185000, percentOfTierLimit: 92.5 },
  { userId: 'user-2', displayName: 'Bob Test', email: 'bob@example.com', tier: 'Basic' as const, tokensUsed: 48000, percentOfTierLimit: 96 },
  { userId: 'user-3', displayName: 'Charlie Dev', email: 'charlie@example.com', tier: 'Enterprise' as const, tokensUsed: 1200000, percentOfTierLimit: 0 },
];

describe('TopConsumersTable', () => {
  it('renders table container', () => {
    render(<TopConsumersTable consumers={mockConsumers} />);
    expect(screen.getByTestId('top-consumers-table')).toBeInTheDocument();
  });

  it('renders header', () => {
    render(<TopConsumersTable consumers={mockConsumers} />);
    expect(screen.getByText('Top Consumers')).toBeInTheDocument();
  });

  it('renders consumer rows', () => {
    render(<TopConsumersTable consumers={mockConsumers} />);
    expect(screen.getByTestId('consumer-row-0')).toBeInTheDocument();
    expect(screen.getByTestId('consumer-row-1')).toBeInTheDocument();
    expect(screen.getByTestId('consumer-row-2')).toBeInTheDocument();
  });

  it('renders display names', () => {
    render(<TopConsumersTable consumers={mockConsumers} />);
    expect(screen.getByText('Alice Demo')).toBeInTheDocument();
    expect(screen.getByText('Bob Test')).toBeInTheDocument();
    expect(screen.getByText('Charlie Dev')).toBeInTheDocument();
  });

  it('renders emails', () => {
    render(<TopConsumersTable consumers={mockConsumers} />);
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
  });

  it('renders tier badges', () => {
    render(<TopConsumersTable consumers={mockConsumers} />);
    expect(screen.getByText('Pro')).toBeInTheDocument();
    expect(screen.getByText('Basic')).toBeInTheDocument();
    expect(screen.getByText('Enterprise')).toBeInTheDocument();
  });

  it('formats token values', () => {
    render(<TopConsumersTable consumers={mockConsumers} />);
    expect(screen.getByText('185.0K')).toBeInTheDocument();
    expect(screen.getByText('48.0K')).toBeInTheDocument();
    expect(screen.getByText('1.2M')).toBeInTheDocument();
  });

  it('shows percentage of tier limit', () => {
    render(<TopConsumersTable consumers={mockConsumers} />);
    expect(screen.getByText('93% of limit')).toBeInTheDocument();
    expect(screen.getByText('96% of limit')).toBeInTheDocument();
    expect(screen.getByText('0% of limit')).toBeInTheDocument();
  });

  it('shows rank numbers', () => {
    render(<TopConsumersTable consumers={mockConsumers} />);
    expect(screen.getByText('#1')).toBeInTheDocument();
    expect(screen.getByText('#2')).toBeInTheDocument();
    expect(screen.getByText('#3')).toBeInTheDocument();
  });

  it('shows empty state', () => {
    render(<TopConsumersTable consumers={[]} />);
    expect(screen.getByText('No consumer data available')).toBeInTheDocument();
  });

  it('shows loading skeleton', () => {
    render(<TopConsumersTable consumers={[]} loading />);
    const table = screen.getByTestId('top-consumers-table');
    expect(table.className).toContain('animate-pulse');
  });
});
