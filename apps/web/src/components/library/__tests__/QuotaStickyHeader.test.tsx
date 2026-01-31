/**
 * QuotaStickyHeader Component Tests (Issue #2869)
 *
 * Test Coverage:
 * - Rendering with different quota states
 * - Progress bar color coding (green/yellow/red)
 * - X/Y games text display
 * - Percentage display
 * - Upgrade link visibility for different tiers
 * - Sticky CSS classes applied
 *
 * Target: >= 90% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QuotaStickyHeader, type QuotaStickyHeaderProps } from '../QuotaStickyHeader';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    className,
    'data-testid': testId,
  }: {
    children: React.ReactNode;
    href: string;
    className?: string;
    'data-testid'?: string;
  }) => (
    <a href={href} className={className} data-testid={testId}>
      {children}
    </a>
  ),
}));

// ============================================================================
// Test Data Factory
// ============================================================================

const createQuotaProps = (overrides: Partial<QuotaStickyHeaderProps> = {}): QuotaStickyHeaderProps => ({
  currentCount: 10,
  maxAllowed: 20,
  percentageUsed: 50,
  userTier: 'normal',
  ...overrides,
});

// ============================================================================
// Rendering Tests
// ============================================================================

describe('QuotaStickyHeader - Rendering', () => {
  it('renders with correct testid', () => {
    render(<QuotaStickyHeader {...createQuotaProps()} />);

    expect(screen.getByTestId('quota-sticky-header')).toBeInTheDocument();
  });

  it('renders progress bar', () => {
    render(<QuotaStickyHeader {...createQuotaProps()} />);

    expect(screen.getByTestId('quota-sticky-header-progress')).toBeInTheDocument();
  });

  it('displays X/Y games count', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ currentCount: 15, maxAllowed: 30 })} />);

    expect(screen.getByTestId('quota-sticky-header-current')).toHaveTextContent('15');
    expect(screen.getByTestId('quota-sticky-header-max')).toHaveTextContent('30');
  });

  it('displays percentage', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 75 })} />);

    expect(screen.getByTestId('quota-sticky-header-percentage')).toHaveTextContent('75%');
  });

  it('applies custom className', () => {
    render(<QuotaStickyHeader {...createQuotaProps()} className="custom-class" />);

    expect(screen.getByTestId('quota-sticky-header')).toHaveClass('custom-class');
  });
});

// ============================================================================
// Sticky Positioning Tests
// ============================================================================

describe('QuotaStickyHeader - Sticky Positioning', () => {
  it('has sticky positioning class below main header', () => {
    render(<QuotaStickyHeader {...createQuotaProps()} />);

    const header = screen.getByTestId('quota-sticky-header');
    expect(header).toHaveClass('sticky');
    expect(header).toHaveClass('top-16'); // Below UnifiedHeader (z-50 at top-0)
    expect(header).toHaveClass('z-40');
  });

  it('has backdrop blur styling', () => {
    render(<QuotaStickyHeader {...createQuotaProps()} />);

    const header = screen.getByTestId('quota-sticky-header');
    expect(header).toHaveClass('backdrop-blur-[16px]');
  });

  it('has border and shadow', () => {
    render(<QuotaStickyHeader {...createQuotaProps()} />);

    const header = screen.getByTestId('quota-sticky-header');
    expect(header).toHaveClass('border-b');
    expect(header).toHaveClass('shadow-sm');
  });
});

// ============================================================================
// Progress Bar Color Tests
// ============================================================================

describe('QuotaStickyHeader - Progress Bar Colors', () => {
  it('uses default color (green) when under 70%', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 50 })} />);

    const progress = screen.getByTestId('quota-sticky-header-progress');
    // Default color - no override class
    expect(progress).not.toHaveClass('[&>div]:bg-destructive');
    expect(progress).not.toHaveClass('[&>div]:bg-amber-500');
  });

  it('uses amber color when at 70%', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 70 })} />);

    const progress = screen.getByTestId('quota-sticky-header-progress');
    expect(progress).toHaveClass('[&>div]:bg-amber-500');
  });

  it('uses amber color when between 70-90%', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 85 })} />);

    const progress = screen.getByTestId('quota-sticky-header-progress');
    expect(progress).toHaveClass('[&>div]:bg-amber-500');
  });

  it('uses destructive color when above 90%', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 91 })} />);

    const progress = screen.getByTestId('quota-sticky-header-progress');
    expect(progress).toHaveClass('[&>div]:bg-destructive');
  });

  it('uses destructive color at 100%', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 100 })} />);

    const progress = screen.getByTestId('quota-sticky-header-progress');
    expect(progress).toHaveClass('[&>div]:bg-destructive');
  });
});

// ============================================================================
// Text Color Tests
// ============================================================================

describe('QuotaStickyHeader - Percentage Text Color', () => {
  it('uses muted color when under 70%', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 50 })} />);

    const percentage = screen.getByTestId('quota-sticky-header-percentage');
    expect(percentage).toHaveClass('text-muted-foreground');
  });

  it('uses amber color when at 70%', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 70 })} />);

    const percentage = screen.getByTestId('quota-sticky-header-percentage');
    expect(percentage).toHaveClass('text-amber-600');
  });

  it('uses amber color when between 70-90%', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 80 })} />);

    const percentage = screen.getByTestId('quota-sticky-header-percentage');
    expect(percentage).toHaveClass('text-amber-600');
  });

  it('uses destructive color when above 90%', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 95 })} />);

    const percentage = screen.getByTestId('quota-sticky-header-percentage');
    expect(percentage).toHaveClass('text-destructive');
  });
});

// ============================================================================
// Upgrade Link Tests
// ============================================================================

describe('QuotaStickyHeader - Upgrade Link', () => {
  it('shows upgrade link for free tier', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ userTier: 'free' })} />);

    const link = screen.getByTestId('quota-sticky-header-upgrade-link');
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/account/subscription');
  });

  it('shows upgrade link for normal tier', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ userTier: 'normal' })} />);

    const link = screen.getByTestId('quota-sticky-header-upgrade-link');
    expect(link).toBeInTheDocument();
  });

  it('does not show upgrade link for premium tier', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ userTier: 'premium' })} />);

    expect(screen.queryByTestId('quota-sticky-header-upgrade-link')).not.toBeInTheDocument();
  });

  it('shows sparkles icon and "Upgrade" text when near limit', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 75, userTier: 'normal' })} />);

    const link = screen.getByTestId('quota-sticky-header-upgrade-link');
    expect(link).toHaveTextContent('Upgrade');
  });

  it('shows chevron icon and "Impostazioni" text when not near limit', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 50, userTier: 'normal' })} />);

    const link = screen.getByTestId('quota-sticky-header-upgrade-link');
    expect(link).toHaveTextContent('Impostazioni');
  });
});

// ============================================================================
// Compact Display Tests
// ============================================================================

describe('QuotaStickyHeader - Compact Display', () => {
  it('has mini progress bar height', () => {
    render(<QuotaStickyHeader {...createQuotaProps()} />);

    const progress = screen.getByTestId('quota-sticky-header-progress');
    expect(progress).toHaveClass('h-1.5');
  });

  it('has compact padding', () => {
    render(<QuotaStickyHeader {...createQuotaProps()} />);

    const header = screen.getByTestId('quota-sticky-header');
    expect(header).toHaveClass('px-4');
    expect(header).toHaveClass('py-2');
  });
});

// ============================================================================
// Percentage Rounding Tests
// ============================================================================

describe('QuotaStickyHeader - Percentage Rounding', () => {
  it('rounds percentage to whole number', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 33.33 })} />);

    expect(screen.getByTestId('quota-sticky-header-percentage')).toHaveTextContent('33%');
  });

  it('rounds up when >= .5', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 66.7 })} />);

    expect(screen.getByTestId('quota-sticky-header-percentage')).toHaveTextContent('67%');
  });

  it('rounds down when < .5', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 66.3 })} />);

    expect(screen.getByTestId('quota-sticky-header-percentage')).toHaveTextContent('66%');
  });
});

// ============================================================================
// Edge Cases
// ============================================================================

describe('QuotaStickyHeader - Edge Cases', () => {
  it('handles zero games', () => {
    render(<QuotaStickyHeader {...createQuotaProps({
      currentCount: 0,
      maxAllowed: 10,
      percentageUsed: 0,
    })} />);

    expect(screen.getByTestId('quota-sticky-header-current')).toHaveTextContent('0');
    expect(screen.getByTestId('quota-sticky-header-percentage')).toHaveTextContent('0%');
  });

  it('handles full library', () => {
    render(<QuotaStickyHeader {...createQuotaProps({
      currentCount: 20,
      maxAllowed: 20,
      percentageUsed: 100,
    })} />);

    expect(screen.getByTestId('quota-sticky-header-current')).toHaveTextContent('20');
    expect(screen.getByTestId('quota-sticky-header-max')).toHaveTextContent('20');
    expect(screen.getByTestId('quota-sticky-header-percentage')).toHaveTextContent('100%');
  });

  it('handles exactly 70% usage (yellow boundary)', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 70 })} />);

    const progress = screen.getByTestId('quota-sticky-header-progress');
    const percentage = screen.getByTestId('quota-sticky-header-percentage');

    expect(progress).toHaveClass('[&>div]:bg-amber-500');
    expect(percentage).toHaveClass('text-amber-600');
  });

  it('handles exactly 69% usage (just under yellow)', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 69 })} />);

    const progress = screen.getByTestId('quota-sticky-header-progress');
    const percentage = screen.getByTestId('quota-sticky-header-percentage');

    expect(progress).not.toHaveClass('[&>div]:bg-amber-500');
    expect(percentage).toHaveClass('text-muted-foreground');
  });

  it('handles exactly 90% usage (still yellow)', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 90 })} />);

    const progress = screen.getByTestId('quota-sticky-header-progress');
    expect(progress).toHaveClass('[&>div]:bg-amber-500');
  });

  it('handles exactly 91% usage (red)', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 91 })} />);

    const progress = screen.getByTestId('quota-sticky-header-progress');
    expect(progress).toHaveClass('[&>div]:bg-destructive');
  });

  it('handles large numbers', () => {
    render(<QuotaStickyHeader {...createQuotaProps({
      currentCount: 999,
      maxAllowed: 1000,
      percentageUsed: 99.9,
    })} />);

    expect(screen.getByTestId('quota-sticky-header-current')).toHaveTextContent('999');
    expect(screen.getByTestId('quota-sticky-header-max')).toHaveTextContent('1000');
    expect(screen.getByTestId('quota-sticky-header-percentage')).toHaveTextContent('100%');
  });

  it('displays infinity symbol for admin unlimited quota', () => {
    render(<QuotaStickyHeader {...createQuotaProps({
      currentCount: 42,
      maxAllowed: 2147483647,
      percentageUsed: 0,
      userTier: 'premium',
    })} />);

    expect(screen.getByTestId('quota-sticky-header-current')).toHaveTextContent('42');
    expect(screen.getByTestId('quota-sticky-header-max')).toHaveTextContent('∞');
  });

  it('displays infinity symbol for values greater than Int32.MaxValue', () => {
    render(<QuotaStickyHeader {...createQuotaProps({
      currentCount: 100,
      maxAllowed: 9999999999,
      percentageUsed: 0,
      userTier: 'premium',
    })} />);

    expect(screen.getByTestId('quota-sticky-header-max')).toHaveTextContent('∞');
  });

  it('displays normal number for non-admin quota', () => {
    render(<QuotaStickyHeader {...createQuotaProps({
      currentCount: 15,
      maxAllowed: 50,
      percentageUsed: 30,
    })} />);

    expect(screen.getByTestId('quota-sticky-header-max')).toHaveTextContent('50');
    expect(screen.queryByText(/∞/)).not.toBeInTheDocument();
  });
});

// ============================================================================
// Accessibility Tests
// ============================================================================

describe('QuotaStickyHeader - Accessibility', () => {
  it('has accessible progress bar', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ percentageUsed: 50 })} />);

    const progressBar = document.querySelector('[role="progressbar"]');
    expect(progressBar).toBeInTheDocument();
    expect(progressBar).toHaveAttribute('aria-valuenow', '50');
  });

  it('upgrade link is keyboard accessible', () => {
    render(<QuotaStickyHeader {...createQuotaProps({ userTier: 'normal' })} />);

    const link = screen.getByTestId('quota-sticky-header-upgrade-link');
    expect(link.tagName).toBe('A');
    expect(link).toHaveAttribute('href');
  });
});
