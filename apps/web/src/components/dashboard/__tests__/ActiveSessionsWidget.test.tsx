/**
 * ActiveSessionsWidget Unit Tests (Issue #3309)
 *
 * Coverage areas:
 * - Rendering with sessions
 * - Empty state
 * - Loading state
 * - Session card details (players, turn, duration)
 * - Navigation links
 * - "View All" visibility logic
 *
 * Target: 90%+ coverage
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { ActiveSessionsWidget, type ActiveSession } from '../ActiveSessionsWidget';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: { children: React.ReactNode; href: string; [key: string]: unknown }) => (
    <a href={href} {...props}>{children}</a>
  ),
}));

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: any) => <div {...props}>{children}</div>,
  },
}));

// ============================================================================
// Test Data
// ============================================================================

const mockSessions: ActiveSession[] = [
  {
    id: 'session-1',
    gameName: 'Catan',
    gameId: 'game-1',
    startDate: '2026-01-20T14:00:00Z',
    players: { current: 3, max: 4 },
    turn: 12,
    duration: 45,
  },
  {
    id: 'session-2',
    gameName: 'Ticket to Ride',
    gameId: 'game-2',
    startDate: '2026-01-19T10:00:00Z',
    players: { current: 2, max: 5 },
    turn: 8,
    duration: 30,
  },
];

const threeSessions: ActiveSession[] = [
  ...mockSessions,
  {
    id: 'session-3',
    gameName: 'Wingspan',
    gameId: 'game-3',
    startDate: '2026-01-18T16:00:00Z',
    players: { current: 4, max: 4 },
    turn: 5,
    duration: 60,
  },
];

describe('ActiveSessionsWidget', () => {
  // ============================================================================
  // Rendering Tests
  // ============================================================================

  describe('Rendering', () => {
    it('renders widget container', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      expect(screen.getByTestId('active-sessions-widget')).toBeInTheDocument();
    });

    it('renders widget title with session count', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} totalCount={2} />);

      expect(screen.getByTestId('active-sessions-title')).toHaveTextContent('2 Sessioni Attive');
    });

    it('renders singular title for 1 session', () => {
      render(<ActiveSessionsWidget sessions={[mockSessions[0]]} totalCount={1} />);

      expect(screen.getByTestId('active-sessions-title')).toHaveTextContent('1 Sessione Attiva');
    });

    it('renders max 2 session cards', () => {
      render(<ActiveSessionsWidget sessions={threeSessions} />);

      expect(screen.getByTestId('session-card-session-1')).toBeInTheDocument();
      expect(screen.getByTestId('session-card-session-2')).toBeInTheDocument();
      expect(screen.queryByTestId('session-card-session-3')).not.toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = render(
        <ActiveSessionsWidget sessions={mockSessions} className="custom-widget" />
      );

      expect(container.querySelector('.custom-widget')).toBeInTheDocument();
    });
  });

  // ============================================================================
  // Session Card Tests
  // ============================================================================

  describe('Session Card', () => {
    it('displays game name', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      expect(screen.getByTestId('session-name-session-1')).toHaveTextContent('Catan');
      expect(screen.getByTestId('session-name-session-2')).toHaveTextContent('Ticket to Ride');
    });

    it('displays players count', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      expect(screen.getByTestId('session-players-session-1')).toHaveTextContent('3/4 giocatori');
      expect(screen.getByTestId('session-players-session-2')).toHaveTextContent('2/5 giocatori');
    });

    it('displays turn number', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      expect(screen.getByTestId('session-turn-session-1')).toHaveTextContent('Turno 12');
      expect(screen.getByTestId('session-turn-session-2')).toHaveTextContent('Turno 8');
    });

    it('displays duration', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      expect(screen.getByTestId('session-duration-session-1')).toHaveTextContent('45min');
      expect(screen.getByTestId('session-duration-session-2')).toHaveTextContent('30min');
    });

    it('displays formatted date', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      // Date formatting: DD/MM
      const card1 = screen.getByTestId('session-card-session-1');
      expect(card1).toHaveTextContent('Partita del');
    });
  });

  // ============================================================================
  // Navigation Tests
  // ============================================================================

  describe('Navigation', () => {
    it('continue button links to session page', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      const continueBtn = screen.getByTestId('session-continue-session-1');
      // Button is wrapped by Link, so we check parent anchor
      const linkElement = continueBtn.closest('a');
      expect(linkElement).toHaveAttribute('href', '/toolkit/session-1');
    });

    it('view all link appears when more than 2 sessions', () => {
      render(<ActiveSessionsWidget sessions={threeSessions} totalCount={3} />);

      const viewAll = screen.getByTestId('view-all-sessions');
      expect(viewAll).toBeInTheDocument();
      expect(viewAll).toHaveAttribute('href', '/toolkit/history');
    });

    it('view all link hidden when 2 or fewer sessions', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} totalCount={2} />);

      expect(screen.queryByTestId('view-all-sessions')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Empty State Tests
  // ============================================================================

  describe('Empty State', () => {
    it('renders empty state when no sessions', () => {
      render(<ActiveSessionsWidget sessions={[]} />);

      expect(screen.getByTestId('active-sessions-empty')).toBeInTheDocument();
    });

    it('shows empty message', () => {
      render(<ActiveSessionsWidget sessions={[]} />);

      expect(screen.getByText('Nessuna sessione attiva')).toBeInTheDocument();
    });

    it('shows start new session CTA', () => {
      render(<ActiveSessionsWidget sessions={[]} />);

      const cta = screen.getByTestId('start-new-session-cta');
      expect(cta).toBeInTheDocument();
      // Button is wrapped by Link, so we check parent anchor
      const linkElement = cta.closest('a');
      expect(linkElement).toHaveAttribute('href', '/toolkit/new');
    });

    it('does not show session cards in empty state', () => {
      render(<ActiveSessionsWidget sessions={[]} />);

      expect(screen.queryByTestId('session-card-session-1')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Loading State Tests
  // ============================================================================

  describe('Loading State', () => {
    it('renders skeleton when isLoading', () => {
      render(<ActiveSessionsWidget isLoading />);

      expect(screen.getByTestId('active-sessions-widget-skeleton')).toBeInTheDocument();
    });

    it('does not render content when loading', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} isLoading />);

      expect(screen.queryByTestId('active-sessions-widget')).not.toBeInTheDocument();
      expect(screen.queryByTestId('session-card-session-1')).not.toBeInTheDocument();
    });

    it('skeleton has glassmorphic styling', () => {
      render(<ActiveSessionsWidget isLoading />);

      const skeleton = screen.getByTestId('active-sessions-widget-skeleton');
      expect(skeleton).toHaveClass('backdrop-blur-xl');
    });
  });

  // ============================================================================
  // Default Props Tests
  // ============================================================================

  describe('Default Props', () => {
    it('uses mock data when sessions not provided', () => {
      render(<ActiveSessionsWidget />);

      // Should render with default mock data (Catan, Ticket to Ride)
      expect(screen.getByTestId('active-sessions-widget')).toBeInTheDocument();
      expect(screen.getByTestId('session-name-session-1')).toHaveTextContent('Catan');
    });

    it('isLoading defaults to false', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      expect(screen.queryByTestId('active-sessions-widget-skeleton')).not.toBeInTheDocument();
    });

    it('totalCount defaults to sessions length', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      // 2 sessions, no "View All" because totalCount defaults to 2
      expect(screen.queryByTestId('view-all-sessions')).not.toBeInTheDocument();
    });
  });

  // ============================================================================
  // Styling Tests
  // ============================================================================

  describe('Styling', () => {
    it('widget has glassmorphic styling', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      const widget = screen.getByTestId('active-sessions-widget');
      expect(widget).toHaveClass('backdrop-blur-xl');
      expect(widget).toHaveClass('rounded-2xl');
    });

    it('continue button has emerald styling', () => {
      render(<ActiveSessionsWidget sessions={mockSessions} />);

      const continueBtn = screen.getByTestId('session-continue-session-1');
      expect(continueBtn).toHaveClass('bg-emerald-600');
    });
  });
});
