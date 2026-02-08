/**
 * TierUsageTable Tests
 * Issue #3692 - Token Management System
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { TierUsageTable } from '../TierUsageTable';

const mockTiers = [
  { tier: 'Free' as const, limitPerMonth: 10000, currentUsage: 2300, userCount: 2500, usagePercent: 23 },
  { tier: 'Basic' as const, limitPerMonth: 50000, currentUsage: 24000, userCount: 300, usagePercent: 48 },
  { tier: 'Pro' as const, limitPerMonth: 200000, currentUsage: 134000, userCount: 45, usagePercent: 67 },
  { tier: 'Enterprise' as const, limitPerMonth: 0, currentUsage: 1500000, userCount: 2, usagePercent: 0 },
];

describe('TierUsageTable', () => {
  it('renders table container', () => {
    render(<TierUsageTable tiers={mockTiers} />);
    expect(screen.getByTestId('tier-usage-table')).toBeInTheDocument();
  });

  it('renders header', () => {
    render(<TierUsageTable tiers={mockTiers} />);
    expect(screen.getByText('Token Pool per Tier')).toBeInTheDocument();
  });

  it('renders all tier rows', () => {
    render(<TierUsageTable tiers={mockTiers} />);
    expect(screen.getByTestId('tier-row-Free')).toBeInTheDocument();
    expect(screen.getByTestId('tier-row-Basic')).toBeInTheDocument();
    expect(screen.getByTestId('tier-row-Pro')).toBeInTheDocument();
    expect(screen.getByTestId('tier-row-Enterprise')).toBeInTheDocument();
  });

  it('renders tier names', () => {
    render(<TierUsageTable tiers={mockTiers} />);
    expect(screen.getByText('Free')).toBeInTheDocument();
    expect(screen.getByText('Basic')).toBeInTheDocument();
    expect(screen.getByText('Pro')).toBeInTheDocument();
    expect(screen.getByText('Enterprise')).toBeInTheDocument();
  });

  it('shows Unlimited for zero limit', () => {
    render(<TierUsageTable tiers={mockTiers} />);
    expect(screen.getByText('Unlimited')).toBeInTheDocument();
  });

  it('formats large token values', () => {
    render(<TierUsageTable tiers={mockTiers} />);
    expect(screen.getByText('1.5M')).toBeInTheDocument();
  });

  it('renders column headers', () => {
    render(<TierUsageTable tiers={mockTiers} />);
    expect(screen.getByText('Tier')).toBeInTheDocument();
    expect(screen.getByText('Limit/Month')).toBeInTheDocument();
    expect(screen.getByText('Current Usage')).toBeInTheDocument();
    expect(screen.getByText('Users')).toBeInTheDocument();
    expect(screen.getByText('Usage %')).toBeInTheDocument();
  });

  it('does not render edit buttons without onEditTier', () => {
    render(<TierUsageTable tiers={mockTiers} />);
    expect(screen.queryByText('Action')).not.toBeInTheDocument();
    expect(screen.queryByTestId('edit-tier-Free')).not.toBeInTheDocument();
  });

  it('renders edit buttons with onEditTier', () => {
    const onEditTier = vi.fn();
    render(<TierUsageTable tiers={mockTiers} onEditTier={onEditTier} />);
    expect(screen.getByTestId('edit-tier-Free')).toBeInTheDocument();
    expect(screen.getByTestId('edit-tier-Pro')).toBeInTheDocument();
  });

  it('calls onEditTier with tier name', () => {
    const onEditTier = vi.fn();
    render(<TierUsageTable tiers={mockTiers} onEditTier={onEditTier} />);
    fireEvent.click(screen.getByTestId('edit-tier-Pro'));
    expect(onEditTier).toHaveBeenCalledWith('Pro');
  });

  it('shows loading skeleton', () => {
    render(<TierUsageTable tiers={[]} loading />);
    const table = screen.getByTestId('tier-usage-table');
    expect(table.className).toContain('animate-pulse');
  });
});
