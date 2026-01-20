/**
 * QuotaStatusBar Component Tests (Issue #2610)
 *
 * Test Coverage:
 * - Rendering with different tiers (free, normal, premium)
 * - Progress bar percentage display
 * - Warning state when near limit (≥80%)
 * - Danger state when at limit (100%)
 * - Upgrade link visibility and content
 * - Tier labels display
 * - Remaining slots display
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QuotaStatusBar, type QuotaStatusBarProps } from '../QuotaStatusBar';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('next/link', () => ({
  default: ({ children, href, className }: { children: React.ReactNode; href: string; className?: string }) => (
    <a href={href} className={className}>{children}</a>
  ),
}));

// ============================================================================
// Test Data Factory
// ============================================================================

const createQuotaProps = (overrides: Partial<QuotaStatusBarProps> = {}): QuotaStatusBarProps => ({
  currentCount: 5,
  maxAllowed: 10,
  userTier: 'free',
  percentageUsed: 50,
  remainingSlots: 5,
  showUpgradeLink: true,
  ...overrides,
});

// ============================================================================
// Rendering Tests
// ============================================================================

describe('QuotaStatusBar - Rendering', () => {
  it('renders with basic quota information', () => {
    render(<QuotaStatusBar {...createQuotaProps()} />);

    expect(screen.getByText(/La tua libreria: 5\/10 giochi/)).toBeInTheDocument();
  });

  it('displays correct count and max', () => {
    render(<QuotaStatusBar {...createQuotaProps({ currentCount: 8, maxAllowed: 20 })} />);

    expect(screen.getByText(/8\/20 giochi/)).toBeInTheDocument();
  });

  it('renders progress bar', () => {
    render(<QuotaStatusBar {...createQuotaProps()} />);

    const progressBar = document.querySelector('[role="progressbar"]');
    expect(progressBar).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(
      <QuotaStatusBar {...createQuotaProps()} className="custom-class" />
    );

    const wrapper = container.firstChild;
    expect(wrapper).toHaveClass('custom-class');
  });
});

// ============================================================================
// Tier Display Tests
// ============================================================================

describe('QuotaStatusBar - Tier Display', () => {
  it('displays Free tier label', () => {
    render(<QuotaStatusBar {...createQuotaProps({ userTier: 'free' })} />);

    expect(screen.getByText(/Piano Free/)).toBeInTheDocument();
  });

  it('displays Normal tier label', () => {
    render(<QuotaStatusBar {...createQuotaProps({ userTier: 'normal' })} />);

    expect(screen.getByText(/Piano Normal/)).toBeInTheDocument();
  });

  it('displays Premium tier label', () => {
    render(<QuotaStatusBar {...createQuotaProps({ userTier: 'premium' })} />);

    expect(screen.getByText(/Piano Premium/)).toBeInTheDocument();
  });

  it('displays remaining slots when available', () => {
    render(<QuotaStatusBar {...createQuotaProps({ remainingSlots: 7 })} />);

    expect(screen.getByText(/7 slot disponibili/)).toBeInTheDocument();
  });

  it('does not display remaining slots when zero', () => {
    render(<QuotaStatusBar {...createQuotaProps({ remainingSlots: 0 })} />);

    expect(screen.queryByText(/slot disponibili/)).not.toBeInTheDocument();
  });
});

// ============================================================================
// Upgrade Link Tests
// ============================================================================

describe('QuotaStatusBar - Upgrade Link', () => {
  it('shows upgrade link for free tier', () => {
    render(<QuotaStatusBar {...createQuotaProps({ userTier: 'free' })} />);

    const link = screen.getByRole('link', { name: /Passa a Normal/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/account/subscription');
  });

  it('shows correct additional slots for free to normal upgrade', () => {
    render(<QuotaStatusBar {...createQuotaProps({ userTier: 'free' })} />);

    expect(screen.getByText(/\+15/)).toBeInTheDocument();
  });

  it('shows upgrade link for normal tier', () => {
    render(<QuotaStatusBar {...createQuotaProps({ userTier: 'normal' })} />);

    const link = screen.getByRole('link', { name: /Passa a Premium/i });
    expect(link).toBeInTheDocument();
  });

  it('shows correct additional slots for normal to premium upgrade', () => {
    render(<QuotaStatusBar {...createQuotaProps({ userTier: 'normal' })} />);

    expect(screen.getByText(/\+30/)).toBeInTheDocument();
  });

  it('does not show upgrade link for premium tier', () => {
    render(<QuotaStatusBar {...createQuotaProps({ userTier: 'premium' })} />);

    expect(screen.queryByText(/Passa a/)).not.toBeInTheDocument();
  });

  it('hides upgrade link when showUpgradeLink is false', () => {
    render(<QuotaStatusBar {...createQuotaProps({ showUpgradeLink: false })} />);

    expect(screen.queryByRole('link', { name: /Passa a/i })).not.toBeInTheDocument();
  });
});

// ============================================================================
// Warning State Tests (≥80% usage)
// ============================================================================

describe('QuotaStatusBar - Warning State', () => {
  it('shows warning styling when usage is at 80%', () => {
    const { container } = render(
      <QuotaStatusBar {...createQuotaProps({ percentageUsed: 80 })} />
    );

    const wrapper = container.firstChild;
    expect(wrapper).toHaveClass('border-yellow-500/50');
  });

  it('shows warning styling when usage is between 80-99%', () => {
    const { container } = render(
      <QuotaStatusBar {...createQuotaProps({ percentageUsed: 90 })} />
    );

    const wrapper = container.firstChild;
    expect(wrapper).toHaveClass('border-yellow-500/50');
  });

  it('shows warning message when near limit', () => {
    render(
      <QuotaStatusBar
        {...createQuotaProps({
          percentageUsed: 85,
          currentCount: 8,
          maxAllowed: 10,
        })}
      />
    );

    expect(
      screen.getByText(/Stai per raggiungere il limite della tua libreria/)
    ).toBeInTheDocument();
  });

  it('does not show warning message when usage is below 80%', () => {
    render(<QuotaStatusBar {...createQuotaProps({ percentageUsed: 70 })} />);

    expect(
      screen.queryByText(/Stai per raggiungere il limite/)
    ).not.toBeInTheDocument();
  });
});

// ============================================================================
// Danger State Tests (At Limit)
// ============================================================================

describe('QuotaStatusBar - Danger State', () => {
  it('shows danger styling when at limit', () => {
    const { container } = render(
      <QuotaStatusBar
        {...createQuotaProps({
          currentCount: 10,
          maxAllowed: 10,
          percentageUsed: 100,
          remainingSlots: 0,
        })}
      />
    );

    const wrapper = container.firstChild;
    expect(wrapper).toHaveClass('border-destructive/50');
    expect(wrapper).toHaveClass('bg-destructive/5');
  });

  it('shows "Limite raggiunto" indicator', () => {
    render(
      <QuotaStatusBar
        {...createQuotaProps({
          currentCount: 10,
          maxAllowed: 10,
          percentageUsed: 100,
        })}
      />
    );

    expect(screen.getByText('Limite raggiunto')).toBeInTheDocument();
  });

  it('shows danger message with upgrade link when at limit', () => {
    render(
      <QuotaStatusBar
        {...createQuotaProps({
          currentCount: 10,
          maxAllowed: 10,
          percentageUsed: 100,
          userTier: 'free',
        })}
      />
    );

    expect(screen.getByText(/Hai raggiunto il limite di 10 giochi/)).toBeInTheDocument();
  });

  it('shows upgrade link in danger message for non-premium users', () => {
    render(
      <QuotaStatusBar
        {...createQuotaProps({
          currentCount: 10,
          maxAllowed: 10,
          percentageUsed: 100,
          userTier: 'free',
        })}
      />
    );

    const dangerLinks = screen.getAllByRole('link', { name: /Passa a Normal/i });
    expect(dangerLinks.length).toBeGreaterThan(0);
  });

  it('does not show warning message when at limit (shows danger instead)', () => {
    render(
      <QuotaStatusBar
        {...createQuotaProps({
          currentCount: 10,
          maxAllowed: 10,
          percentageUsed: 100,
        })}
      />
    );

    expect(
      screen.queryByText(/Stai per raggiungere il limite/)
    ).not.toBeInTheDocument();
  });
});

// ============================================================================
// Normal State Tests
// ============================================================================

describe('QuotaStatusBar - Normal State', () => {
  it('does not show warning or danger styling when under 80%', () => {
    const { container } = render(
      <QuotaStatusBar {...createQuotaProps({ percentageUsed: 50 })} />
    );

    const wrapper = container.firstChild;
    expect(wrapper).not.toHaveClass('border-yellow-500/50');
    expect(wrapper).not.toHaveClass('border-destructive/50');
  });

  it('has default card styling', () => {
    const { container } = render(
      <QuotaStatusBar {...createQuotaProps({ percentageUsed: 30 })} />
    );

    const wrapper = container.firstChild;
    expect(wrapper).toHaveClass('rounded-lg');
    expect(wrapper).toHaveClass('border');
    expect(wrapper).toHaveClass('bg-card');
  });
});

// ============================================================================
// Edge Cases
// ============================================================================

describe('QuotaStatusBar - Edge Cases', () => {
  it('handles zero games', () => {
    render(
      <QuotaStatusBar
        {...createQuotaProps({
          currentCount: 0,
          maxAllowed: 10,
          percentageUsed: 0,
          remainingSlots: 10,
        })}
      />
    );

    expect(screen.getByText(/0\/10 giochi/)).toBeInTheDocument();
    expect(screen.getByText(/10 slot disponibili/)).toBeInTheDocument();
  });

  it('handles premium tier with high limits', () => {
    render(
      <QuotaStatusBar
        {...createQuotaProps({
          currentCount: 150,
          maxAllowed: 500,
          percentageUsed: 30,
          remainingSlots: 350,
          userTier: 'premium',
        })}
      />
    );

    expect(screen.getByText(/150\/500 giochi/)).toBeInTheDocument();
    expect(screen.getByText(/350 slot disponibili/)).toBeInTheDocument();
    expect(screen.getByText(/Piano Premium/)).toBeInTheDocument();
  });

  it('handles exactly 80% usage (boundary)', () => {
    const { container } = render(
      <QuotaStatusBar
        {...createQuotaProps({
          currentCount: 8,
          maxAllowed: 10,
          percentageUsed: 80,
          remainingSlots: 2,
        })}
      />
    );

    const wrapper = container.firstChild;
    expect(wrapper).toHaveClass('border-yellow-500/50');
    expect(
      screen.getByText(/Stai per raggiungere il limite/)
    ).toBeInTheDocument();
  });

  it('handles exactly 79% usage (just under warning)', () => {
    const { container } = render(
      <QuotaStatusBar
        {...createQuotaProps({
          percentageUsed: 79,
        })}
      />
    );

    const wrapper = container.firstChild;
    expect(wrapper).not.toHaveClass('border-yellow-500/50');
    expect(
      screen.queryByText(/Stai per raggiungere il limite/)
    ).not.toBeInTheDocument();
  });
});
