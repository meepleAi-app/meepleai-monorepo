/**
 * SessionQuotaBar Component Tests (Issue #3075)
 *
 * Test Coverage:
 * - Rendering with different tiers (free, normal, premium)
 * - Progress bar percentage display
 * - Warning state when near limit (≥80%)
 * - Danger state when at limit (100%)
 * - Upgrade link visibility and content
 * - Unlimited sessions display (admin)
 * - Loading state
 * - Compact mode
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { SessionQuotaBar, type SessionQuotaBarProps } from '../SessionQuotaBar';

// ============================================================================
// Mocks
// ============================================================================

vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    className,
  }: {
    children: React.ReactNode;
    href: string;
    className?: string;
  }) => (
    <a href={href} className={className}>
      {children}
    </a>
  ),
}));

// ============================================================================
// Test Data Factory
// ============================================================================

const createQuotaProps = (overrides: Partial<SessionQuotaBarProps> = {}): SessionQuotaBarProps => ({
  currentSessions: 2,
  maxSessions: 5,
  userTier: 'free',
  remainingSlots: 3,
  canCreateNew: true,
  isUnlimited: false,
  showUpgradeLink: true,
  isLoading: false,
  compact: false,
  ...overrides,
});

// ============================================================================
// Rendering Tests
// ============================================================================

describe('SessionQuotaBar - Rendering', () => {
  it('renders with basic quota information', () => {
    render(<SessionQuotaBar {...createQuotaProps()} />);

    expect(screen.getByText(/Sessioni attive: 2\/5/)).toBeInTheDocument();
  });

  it('displays correct count and max', () => {
    render(<SessionQuotaBar {...createQuotaProps({ currentSessions: 3, maxSessions: 10 })} />);

    expect(screen.getByText(/3\/10/)).toBeInTheDocument();
  });

  it('renders progress bar', () => {
    render(<SessionQuotaBar {...createQuotaProps()} />);

    const progressBar = document.querySelector('[role="progressbar"]');
    expect(progressBar).toBeInTheDocument();
  });

  it('applies custom className', () => {
    const { container } = render(
      <SessionQuotaBar {...createQuotaProps()} className="custom-class" />
    );

    const wrapper = container.firstChild;
    expect(wrapper).toHaveClass('custom-class');
  });
});

// ============================================================================
// Loading State Tests
// ============================================================================

describe('SessionQuotaBar - Loading State', () => {
  it('shows loading state when isLoading is true', () => {
    render(<SessionQuotaBar {...createQuotaProps({ isLoading: true })} />);

    expect(screen.getByText(/Caricamento quota sessioni/)).toBeInTheDocument();
  });

  it('does not show quota information when loading', () => {
    render(<SessionQuotaBar {...createQuotaProps({ isLoading: true })} />);

    expect(screen.queryByText(/Sessioni attive:/)).not.toBeInTheDocument();
  });
});

// ============================================================================
// Tier Display Tests
// ============================================================================

describe('SessionQuotaBar - Tier Display', () => {
  it('displays Free tier label', () => {
    render(<SessionQuotaBar {...createQuotaProps({ userTier: 'free' })} />);

    expect(screen.getByText(/Piano Free/)).toBeInTheDocument();
  });

  it('displays Normal tier label', () => {
    render(<SessionQuotaBar {...createQuotaProps({ userTier: 'normal' })} />);

    expect(screen.getByText(/Piano Normal/)).toBeInTheDocument();
  });

  it('displays Premium tier label', () => {
    render(<SessionQuotaBar {...createQuotaProps({ userTier: 'premium' })} />);

    expect(screen.getByText(/Piano Premium/)).toBeInTheDocument();
  });

  it('displays remaining slots when available', () => {
    render(<SessionQuotaBar {...createQuotaProps({ remainingSlots: 4 })} />);

    expect(screen.getByText(/4 slot disponibili/)).toBeInTheDocument();
  });

  it('does not display remaining slots when zero', () => {
    render(<SessionQuotaBar {...createQuotaProps({ remainingSlots: 0, canCreateNew: false })} />);

    expect(screen.queryByText(/slot disponibili/)).not.toBeInTheDocument();
  });
});

// ============================================================================
// Unlimited Sessions Tests
// ============================================================================

describe('SessionQuotaBar - Unlimited Sessions', () => {
  it('shows unlimited message for admins', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          isUnlimited: true,
          currentSessions: 5,
        })}
      />
    );

    expect(screen.getByText(/Sessioni illimitate/)).toBeInTheDocument();
  });

  it('shows session count for unlimited users', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          isUnlimited: true,
          currentSessions: 8,
        })}
      />
    );

    expect(screen.getByText(/8 sessioni attive/)).toBeInTheDocument();
  });

  it('does not show progress bar for unlimited users', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          isUnlimited: true,
        })}
      />
    );

    expect(document.querySelector('[role="progressbar"]')).not.toBeInTheDocument();
  });

  it('does not show upgrade link for unlimited users', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          isUnlimited: true,
        })}
      />
    );

    expect(screen.queryByText(/Passa a/)).not.toBeInTheDocument();
  });
});

// ============================================================================
// Upgrade Link Tests
// ============================================================================

describe('SessionQuotaBar - Upgrade Link', () => {
  it('shows upgrade link for free tier', () => {
    render(<SessionQuotaBar {...createQuotaProps({ userTier: 'free' })} />);

    const link = screen.getByRole('link', { name: /Passa a Normal/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/account/subscription');
  });

  it('shows upgrade link for normal tier', () => {
    render(<SessionQuotaBar {...createQuotaProps({ userTier: 'normal' })} />);

    const link = screen.getByRole('link', { name: /Passa a Premium/i });
    expect(link).toBeInTheDocument();
  });

  it('does not show upgrade link for premium tier', () => {
    render(<SessionQuotaBar {...createQuotaProps({ userTier: 'premium' })} />);

    expect(screen.queryByText(/Passa a/)).not.toBeInTheDocument();
  });

  it('hides upgrade link when showUpgradeLink is false', () => {
    render(<SessionQuotaBar {...createQuotaProps({ showUpgradeLink: false })} />);

    expect(screen.queryByRole('link', { name: /Passa a/i })).not.toBeInTheDocument();
  });
});

// ============================================================================
// Warning State Tests (≥80% usage)
// ============================================================================

describe('SessionQuotaBar - Warning State', () => {
  it('shows warning styling when usage is at 80%', () => {
    const { container } = render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 4,
          maxSessions: 5,
          remainingSlots: 1,
        })}
      />
    );

    const wrapper = container.firstChild;
    expect(wrapper).toHaveClass('border-yellow-500/50');
  });

  it('shows warning message when near limit', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 4,
          maxSessions: 5,
          remainingSlots: 1,
        })}
      />
    );

    expect(
      screen.getByText(/Stai per raggiungere il limite di sessioni attive/)
    ).toBeInTheDocument();
  });

  it('does not show warning message when usage is below 80%', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 2,
          maxSessions: 10,
          remainingSlots: 8,
        })}
      />
    );

    expect(screen.queryByText(/Stai per raggiungere il limite/)).not.toBeInTheDocument();
  });
});

// ============================================================================
// Danger State Tests (At Limit)
// ============================================================================

describe('SessionQuotaBar - Danger State', () => {
  it('shows danger styling when at limit', () => {
    const { container } = render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 5,
          maxSessions: 5,
          remainingSlots: 0,
          canCreateNew: false,
        })}
      />
    );

    const wrapper = container.firstChild;
    expect(wrapper).toHaveClass('border-destructive/50');
    expect(wrapper).toHaveClass('bg-destructive/5');
  });

  it('shows "Limite raggiunto" indicator', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 5,
          maxSessions: 5,
          remainingSlots: 0,
          canCreateNew: false,
        })}
      />
    );

    expect(screen.getByText('Limite raggiunto')).toBeInTheDocument();
  });

  it('shows danger message with upgrade link when at limit', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 5,
          maxSessions: 5,
          remainingSlots: 0,
          canCreateNew: false,
          userTier: 'free',
        })}
      />
    );

    expect(screen.getByText(/Hai raggiunto il limite di 5 sessioni attive/)).toBeInTheDocument();
  });

  it('shows upgrade link in danger message for non-premium users', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 5,
          maxSessions: 5,
          remainingSlots: 0,
          canCreateNew: false,
          userTier: 'free',
        })}
      />
    );

    const dangerLinks = screen.getAllByRole('link', { name: /Passa a Normal/i });
    expect(dangerLinks.length).toBeGreaterThan(0);
  });

  it('does not show warning message when at limit (shows danger instead)', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 5,
          maxSessions: 5,
          remainingSlots: 0,
          canCreateNew: false,
        })}
      />
    );

    expect(screen.queryByText(/Stai per raggiungere il limite/)).not.toBeInTheDocument();
  });
});

// ============================================================================
// Compact Mode Tests
// ============================================================================

describe('SessionQuotaBar - Compact Mode', () => {
  it('renders in compact mode', () => {
    const { container } = render(<SessionQuotaBar {...createQuotaProps({ compact: true })} />);

    const wrapper = container.firstChild;
    expect(wrapper).toHaveClass('p-3');
  });

  it('does not show warning message in compact mode', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          compact: true,
          currentSessions: 4,
          maxSessions: 5,
          remainingSlots: 1,
        })}
      />
    );

    expect(screen.queryByText(/Stai per raggiungere il limite/)).not.toBeInTheDocument();
  });

  it('shows shorter upgrade text in compact mode', () => {
    render(<SessionQuotaBar {...createQuotaProps({ compact: true, userTier: 'free' })} />);

    expect(screen.getByText('Upgrade')).toBeInTheDocument();
  });
});

// ============================================================================
// Normal State Tests
// ============================================================================

describe('SessionQuotaBar - Normal State', () => {
  it('does not show warning or danger styling when under 80%', () => {
    const { container } = render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 2,
          maxSessions: 10,
          remainingSlots: 8,
        })}
      />
    );

    const wrapper = container.firstChild;
    expect(wrapper).not.toHaveClass('border-yellow-500/50');
    expect(wrapper).not.toHaveClass('border-destructive/50');
  });

  it('has default card styling', () => {
    const { container } = render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 1,
          maxSessions: 10,
        })}
      />
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

describe('SessionQuotaBar - Edge Cases', () => {
  it('handles zero sessions', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 0,
          maxSessions: 5,
          remainingSlots: 5,
        })}
      />
    );

    expect(screen.getByText(/0\/5/)).toBeInTheDocument();
    expect(screen.getByText(/5 slot disponibili/)).toBeInTheDocument();
  });

  it('handles unknown tier gracefully', () => {
    render(<SessionQuotaBar {...createQuotaProps({ userTier: 'unknown_tier' })} />);

    // Should display the raw tier value
    expect(screen.getByText(/Piano unknown_tier/)).toBeInTheDocument();
  });

  it('handles single session available', () => {
    render(
      <SessionQuotaBar
        {...createQuotaProps({
          currentSessions: 4,
          maxSessions: 5,
          remainingSlots: 1,
        })}
      />
    );

    expect(screen.getByText(/1 slot disponibili/)).toBeInTheDocument();
  });
});
