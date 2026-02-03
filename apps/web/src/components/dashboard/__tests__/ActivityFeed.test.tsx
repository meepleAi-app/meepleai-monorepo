/**
 * ActivityFeed Unit Tests (Issue #3311)
 *
 * Coverage areas:
 * - Rendering with events
 * - Empty state
 * - Loading state
 * - Event details (title, description, timestamp)
 * - Navigation links
 * - Activity type icons
 * - "View All" visibility logic
 * - Timeline structure
 *
 * Target: 90%+ coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ActivityFeed, type ActivityEvent, type ActivityEventType } from '../ActivityFeed';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
      <div {...props}>{children}</div>
    ),
  },
}));

// ============================================================================
// Test Data
// ============================================================================

const mockEvents: ActivityEvent[] = [
  {
    id: 'event-1',
    type: 'game_added',
    title: 'Aggiunto "Wingspan"',
    description: 'Aggiunto alla collezione',
    entityId: 'game-1',
    entityType: 'game',
    timestamp: new Date('2026-01-20T14:30:00Z').toISOString(),
  },
  {
    id: 'event-2',
    type: 'session_completed',
    title: 'Giocato "Catan"',
    description: '4 giocatori • 90 min',
    entityId: 'session-1',
    entityType: 'session',
    timestamp: new Date('2026-01-19T20:00:00Z').toISOString(),
  },
  {
    id: 'event-3',
    type: 'chat_saved',
    title: 'Chat "Regole Wingspan"',
    entityId: 'chat-1',
    entityType: 'chat',
    timestamp: new Date('2026-01-18T19:30:00Z').toISOString(),
  },
];

const tenEvents: ActivityEvent[] = Array.from({ length: 10 }, (_, i) => ({
  id: `event-${i + 1}`,
  type: (['game_added', 'session_completed', 'chat_saved', 'wishlist_added', 'achievement_unlocked'] as ActivityEventType[])[i % 5],
  title: `Event ${i + 1}`,
  timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * i).toISOString(),
}));

const elevenEvents: ActivityEvent[] = [
  ...tenEvents,
  {
    id: 'event-11',
    type: 'game_added',
    title: 'Event 11',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 11).toISOString(),
  },
];

describe('ActivityFeed', () => {
  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders widget container', () => {
      render(<ActivityFeed events={mockEvents} />);

      expect(screen.getByTestId('activity-feed-widget')).toBeInTheDocument();
    });

    it('renders widget title', () => {
      render(<ActivityFeed events={mockEvents} />);

      expect(screen.getByTestId('activity-feed-title')).toHaveTextContent('Attività Recente');
    });

    it('renders max 10 event items by default', () => {
      render(<ActivityFeed events={elevenEvents} />);

      expect(screen.getByTestId('activity-event-event-1')).toBeInTheDocument();
      expect(screen.getByTestId('activity-event-event-10')).toBeInTheDocument();
      expect(screen.queryByTestId('activity-event-event-11')).not.toBeInTheDocument();
    });

    it('renders timeline container', () => {
      render(<ActivityFeed events={mockEvents} />);

      expect(screen.getByTestId('activity-timeline')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = render(
        <ActivityFeed events={mockEvents} className="custom-widget" />
      );

      expect(container.querySelector('.custom-widget')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Event Item Tests
  // ============================================================================

  describe('Event Item', () => {
    it('displays event title', () => {
      render(<ActivityFeed events={mockEvents} />);

      expect(screen.getByTestId('activity-title-event-1')).toHaveTextContent('Aggiunto "Wingspan"');
      expect(screen.getByTestId('activity-title-event-2')).toHaveTextContent('Giocato "Catan"');
    });

    it('displays event description when present', () => {
      render(<ActivityFeed events={mockEvents} />);

      expect(screen.getByTestId('activity-description-event-1')).toHaveTextContent('Aggiunto alla collezione');
      expect(screen.getByTestId('activity-description-event-2')).toHaveTextContent('4 giocatori • 90 min');
    });

    it('does not display description when absent', () => {
      render(<ActivityFeed events={mockEvents} />);

      expect(screen.queryByTestId('activity-description-event-3')).not.toBeInTheDocument();
    });

    it('displays timestamp', () => {
      render(<ActivityFeed events={mockEvents} />);

      // Timestamps should be rendered
      expect(screen.getByTestId('activity-time-event-1')).toBeInTheDocument();
      expect(screen.getByTestId('activity-time-event-2')).toBeInTheDocument();
    });

    it('renders activity icon', () => {
      render(<ActivityFeed events={mockEvents} />);

      expect(screen.getByTestId('activity-icon-event-1')).toBeInTheDocument();
      expect(screen.getByTestId('activity-icon-event-2')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Navigation Tests
  // ============================================================================

  describe('Navigation', () => {
    it('game_added event links to library', () => {
      render(<ActivityFeed events={mockEvents} />);

      const eventLink = screen.getByTestId('activity-event-event-1');
      expect(eventLink).toHaveAttribute('href', '/library/game-1');
    });

    it('session_completed event links to toolkit', () => {
      render(<ActivityFeed events={mockEvents} />);

      const eventLink = screen.getByTestId('activity-event-event-2');
      expect(eventLink).toHaveAttribute('href', '/toolkit/session-1');
    });

    it('chat_saved event links to chat', () => {
      render(<ActivityFeed events={mockEvents} />);

      const eventLink = screen.getByTestId('activity-event-event-3');
      expect(eventLink).toHaveAttribute('href', '/chat/chat-1');
    });

    it('wishlist_added event links to wishlist', () => {
      const wishlistEvent: ActivityEvent = {
        id: 'wishlist-event',
        type: 'wishlist_added',
        title: 'Wishlist Event',
        entityId: 'wishlist-1',
        timestamp: new Date().toISOString(),
      };
      render(<ActivityFeed events={[wishlistEvent]} />);

      const eventLink = screen.getByTestId('activity-event-wishlist-event');
      expect(eventLink).toHaveAttribute('href', '/wishlist/wishlist-1');
    });

    it('achievement_unlocked event links to achievements', () => {
      const achievementEvent: ActivityEvent = {
        id: 'achievement-event',
        type: 'achievement_unlocked',
        title: 'Achievement Event',
        entityId: 'achievement-1',
        timestamp: new Date().toISOString(),
      };
      render(<ActivityFeed events={[achievementEvent]} />);

      const eventLink = screen.getByTestId('activity-event-achievement-event');
      expect(eventLink).toHaveAttribute('href', '/achievements/achievement-1');
    });

    it('view all link appears when more than limit events', () => {
      render(<ActivityFeed events={elevenEvents} totalCount={11} />);

      const viewAll = screen.getByTestId('view-all-activity');
      expect(viewAll).toBeInTheDocument();
      expect(viewAll).toHaveAttribute('href', '/activity');
    });

    it('view all link hidden when limit or fewer events', () => {
      render(<ActivityFeed events={tenEvents} totalCount={10} />);

      expect(screen.queryByTestId('view-all-activity')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Activity Type Icon Tests
  // ============================================================================

  describe('Activity Type Icons', () => {
    it('renders correct icon container for game_added', () => {
      const gameEvent: ActivityEvent = {
        id: 'game-event',
        type: 'game_added',
        title: 'Game Added',
        timestamp: new Date().toISOString(),
      };
      render(<ActivityFeed events={[gameEvent]} />);

      const iconContainer = screen.getByTestId('activity-icon-game-event');
      expect(iconContainer).toHaveClass('bg-amber-500/20');
    });

    it('renders correct icon container for session_completed', () => {
      const sessionEvent: ActivityEvent = {
        id: 'session-event',
        type: 'session_completed',
        title: 'Session Completed',
        timestamp: new Date().toISOString(),
      };
      render(<ActivityFeed events={[sessionEvent]} />);

      const iconContainer = screen.getByTestId('activity-icon-session-event');
      expect(iconContainer).toHaveClass('bg-emerald-500/20');
    });

    it('renders correct icon container for chat_saved', () => {
      const chatEvent: ActivityEvent = {
        id: 'chat-event',
        type: 'chat_saved',
        title: 'Chat Saved',
        timestamp: new Date().toISOString(),
      };
      render(<ActivityFeed events={[chatEvent]} />);

      const iconContainer = screen.getByTestId('activity-icon-chat-event');
      expect(iconContainer).toHaveClass('bg-blue-500/20');
    });

    it('renders correct icon container for wishlist_added', () => {
      const wishlistEvent: ActivityEvent = {
        id: 'wishlist-event',
        type: 'wishlist_added',
        title: 'Wishlist Added',
        timestamp: new Date().toISOString(),
      };
      render(<ActivityFeed events={[wishlistEvent]} />);

      const iconContainer = screen.getByTestId('activity-icon-wishlist-event');
      expect(iconContainer).toHaveClass('bg-yellow-500/20');
    });

    it('renders correct icon container for achievement_unlocked', () => {
      const achievementEvent: ActivityEvent = {
        id: 'achievement-event',
        type: 'achievement_unlocked',
        title: 'Achievement Unlocked',
        timestamp: new Date().toISOString(),
      };
      render(<ActivityFeed events={[achievementEvent]} />);

      const iconContainer = screen.getByTestId('activity-icon-achievement-event');
      expect(iconContainer).toHaveClass('bg-purple-500/20');
    });
  });

  // ============================================================================
  // Empty State Tests
  // ============================================================================

  describe('Empty State', () => {
    it('renders empty state when no events', () => {
      render(<ActivityFeed events={[]} />);

      expect(screen.getByTestId('activity-feed-empty')).toBeInTheDocument();
    });

    it('shows empty message', () => {
      render(<ActivityFeed events={[]} />);

      expect(screen.getByText('Nessuna attività recente')).toBeInTheDocument();
    });

    it('shows explore catalog CTA', () => {
      render(<ActivityFeed events={[]} />);

      const cta = screen.getByTestId('start-playing-cta');
      expect(cta).toBeInTheDocument();
      expect(cta.closest('a')).toHaveAttribute('href', '/games/catalog');
    });

    it('does not show timeline in empty state', () => {
      render(<ActivityFeed events={[]} />);

      expect(screen.queryByTestId('activity-timeline')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton when isLoading', () => {
      render(<ActivityFeed isLoading />);

      expect(screen.getByTestId('activity-feed-skeleton')).toBeInTheDocument();
    });

    it('does not render content when loading', () => {
      render(<ActivityFeed events={mockEvents} isLoading />);

      expect(screen.queryByTestId('activity-feed-widget')).not.toBeInTheDocument();
      expect(screen.queryByTestId('activity-event-event-1')).not.toBeInTheDocument();
    });

    it('skeleton has glassmorphic styling', () => {
      render(<ActivityFeed isLoading />);

      const skeleton = screen.getByTestId('activity-feed-skeleton');
      expect(skeleton).toHaveClass('backdrop-blur-xl');
    });
  });

  // ============================================================================
  // Default Props Tests
  // ============================================================================

  describe('Default Props', () => {
    it('uses mock data when events not provided', () => {
      render(<ActivityFeed />);

      // Should render with default mock data
      expect(screen.getByTestId('activity-feed-widget')).toBeInTheDocument();
      expect(screen.getByTestId('activity-event-event-1')).toBeInTheDocument();
    });

    it('isLoading defaults to false', () => {
      render(<ActivityFeed events={mockEvents} />);

      expect(screen.queryByTestId('activity-feed-skeleton')).not.toBeInTheDocument();
    });

    it('limit defaults to 10', () => {
      render(<ActivityFeed events={elevenEvents} />);

      // Should only show 10 events
      expect(screen.getByTestId('activity-event-event-10')).toBeInTheDocument();
      expect(screen.queryByTestId('activity-event-event-11')).not.toBeInTheDocument();
    });

    it('totalCount defaults to events length', () => {
      render(<ActivityFeed events={tenEvents} />);

      // 10 events, no "View All" because totalCount defaults to 10
      expect(screen.queryByTestId('view-all-activity')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Styling Tests
  // ============================================================================

  describe('Styling', () => {
    it('widget has glassmorphic styling', () => {
      render(<ActivityFeed events={mockEvents} />);

      const widget = screen.getByTestId('activity-feed-widget');
      expect(widget).toHaveClass('backdrop-blur-xl');
      expect(widget).toHaveClass('rounded-2xl');
    });

    it('header icon has slate styling', () => {
      const { container } = render(
        <ActivityFeed events={mockEvents} />
      );

      const iconContainer = container.querySelector('.bg-slate-500\\/20');
      expect(iconContainer).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Custom Limit Tests
  // ============================================================================

  describe('Custom Limit', () => {
    it('respects custom limit prop', () => {
      render(<ActivityFeed events={tenEvents} limit={5} />);

      expect(screen.getByTestId('activity-event-event-5')).toBeInTheDocument();
      expect(screen.queryByTestId('activity-event-event-6')).not.toBeInTheDocument();
    });

    it('shows view all when totalCount exceeds custom limit', () => {
      render(<ActivityFeed events={tenEvents} limit={5} totalCount={10} />);

      expect(screen.getByTestId('view-all-activity')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Edge Cases
  // ============================================================================

  describe('Edge Cases', () => {
    it('handles single event', () => {
      const singleEvent = [mockEvents[0]];
      render(<ActivityFeed events={singleEvent} />);

      expect(screen.getByTestId('activity-event-event-1')).toBeInTheDocument();
      expect(screen.queryByTestId('activity-event-event-2')).not.toBeInTheDocument();
    });

    it('truncates long titles in CSS', () => {
      const longTitleEvent: ActivityEvent = {
        id: 'long-event',
        title: 'This is a very long title that should be truncated by CSS',
        type: 'game_added',
        timestamp: new Date().toISOString(),
      };
      render(<ActivityFeed events={[longTitleEvent]} />);

      const titleElement = screen.getByTestId('activity-title-long-event');
      expect(titleElement).toHaveClass('truncate');
    });

    it('handles event without entityId', () => {
      const noEntityEvent: ActivityEvent = {
        id: 'no-entity-event',
        type: 'game_added',
        title: 'Game Added',
        timestamp: new Date().toISOString(),
      };
      render(<ActivityFeed events={[noEntityEvent]} />);

      // Should link to default route without entityId
      const eventLink = screen.getByTestId('activity-event-no-entity-event');
      expect(eventLink).toHaveAttribute('href', '/library');
    });
  });
});
