/**
 * Dashboard Hub Accessibility Tests (Issue #3980)
 *
 * WCAG 2.1 AA compliance validation using jest-axe for all
 * Dashboard Hub components from Epic #3901.
 *
 * Components tested:
 * - LibrarySnapshot (quota, top games, empty state)
 * - ActivityFeed (events, empty state)
 * - QuickActionsGrid (action buttons, navigation)
 *
 * @see Epic #3901 - Dashboard Hub Core (MVP)
 */

import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, it, expect, vi } from 'vitest';

import { LibrarySnapshot } from '../LibrarySnapshot';
import { ActivityFeed } from '../ActivityFeed';
import { QuickActionsGrid } from '../QuickActionsGrid';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({
    children,
    href,
    ...props
  }: {
    children: React.ReactNode;
    href: string;
    [key: string]: unknown;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

// Mock next/image
vi.mock('next/image', () => ({
  default: ({
    src,
    alt,
    ...props
  }: {
    src: string;
    alt: string;
    [key: string]: unknown;
  }) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img src={src} alt={alt} {...props} />
  ),
}));

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({
      children,
      ...props
    }: {
      children: React.ReactNode;
      [key: string]: unknown;
    }) => <div {...props}>{children}</div>,
  },
}));

// Mock analytics
vi.mock('@/lib/analytics/track-event', () => ({
  trackEvent: vi.fn(),
}));

// ============================================================================
// Test Data
// ============================================================================

const mockQuota = { used: 127, total: 200 };
const mockTopGames = [
  {
    id: 'game-1',
    title: 'Catan',
    coverUrl: '/images/catan.jpg',
    rating: 5,
    playCount: 45,
  },
  {
    id: 'game-2',
    title: 'Ticket to Ride',
    coverUrl: '/images/ticket.jpg',
    rating: 4,
    playCount: 32,
  },
];

const mockEvents = [
  {
    id: 'evt-1',
    type: 'game_added' as const,
    title: 'Aggiunto Wingspan alla collezione',
    timestamp: new Date().toISOString(),
    icon: 'plus' as const,
    color: 'emerald' as const,
  },
  {
    id: 'evt-2',
    type: 'session_completed' as const,
    title: 'Sessione di Catan completata',
    timestamp: new Date(Date.now() - 86400000).toISOString(),
    icon: 'gamepad' as const,
    color: 'blue' as const,
  },
];

// ============================================================================
// LibrarySnapshot Accessibility Tests
// ============================================================================

describe('LibrarySnapshot - Accessibility', () => {
  it('should have no accessibility violations with data', async () => {
    const { container } = render(
      <LibrarySnapshot quota={mockQuota} topGames={mockTopGames} />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations in empty state', async () => {
    const { container } = render(
      <LibrarySnapshot quota={mockQuota} topGames={[]} />
    );

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations in loading state', async () => {
    const { container } = render(<LibrarySnapshot isLoading />);

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});

// ============================================================================
// ActivityFeed Accessibility Tests
// ============================================================================

describe('ActivityFeed - Accessibility', () => {
  it('should have no accessibility violations with events', async () => {
    const { container } = render(<ActivityFeed events={mockEvents} />);

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations in empty state', async () => {
    const { container } = render(<ActivityFeed events={[]} />);

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});

// ============================================================================
// QuickActionsGrid Accessibility Tests
// ============================================================================

describe('QuickActionsGrid - Accessibility', () => {
  it('should have no accessibility violations with default actions', async () => {
    const { container } = render(<QuickActionsGrid />);

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('should have no accessibility violations in loading state', async () => {
    const { container } = render(<QuickActionsGrid isLoading />);

    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
