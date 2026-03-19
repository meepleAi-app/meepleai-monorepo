/**
 * TavoloLayout, TavoloSection, ActiveSessionCard — Unit Tests
 * Tests for the "Il Tavolo" dashboard layout components.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { TavoloLayout } from '@/components/dashboard-v2/tavolo/TavoloLayout';
import { TavoloSection } from '@/components/dashboard-v2/tavolo/TavoloSection';
import { ActiveSessionCard } from '@/components/dashboard-v2/tavolo/ActiveSessionCard';

// ─── Mocks ───────────────────────────────────────────────────────────────────

// Mock ActivityFeed to isolate TavoloLayout tests from its API hook
vi.mock('@/components/dashboard-v2/tavolo/ActivityFeed', () => ({
  ActivityFeed: () => <div data-testid="activity-feed-mock">Activity Feed</div>,
}));

// Mock MeepleCard to avoid complex rendering in unit tests
vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: ({
    title,
    subtitle,
    badge,
  }: {
    title: string;
    subtitle?: string;
    badge?: string;
  }) => (
    <div data-testid="meeple-card-mock">
      <span data-testid="meeple-card-title">{title}</span>
      {subtitle && <span data-testid="meeple-card-subtitle">{subtitle}</span>}
      {badge && <span data-testid="meeple-card-badge">{badge}</span>}
    </div>
  ),
}));

// ─── TavoloLayout Tests ───────────────────────────────────────────────────────

describe('TavoloLayout', () => {
  it('renders children in the main content area', () => {
    render(
      <TavoloLayout>
        <div data-testid="child-content">Main Content</div>
      </TavoloLayout>
    );

    expect(screen.getByTestId('child-content')).toBeInTheDocument();
    expect(screen.getByText('Main Content')).toBeInTheDocument();
  });

  it('renders the activity feed sidebar', () => {
    render(
      <TavoloLayout>
        <div>Content</div>
      </TavoloLayout>
    );

    expect(screen.getByTestId('activity-feed-mock')).toBeInTheDocument();
  });

  it('renders the feed sidebar heading', () => {
    render(
      <TavoloLayout>
        <div>Content</div>
      </TavoloLayout>
    );

    expect(screen.getByText(/Feed Attività/i)).toBeInTheDocument();
  });

  it('has grid layout classes on the container', () => {
    const { container } = render(
      <TavoloLayout>
        <div>Content</div>
      </TavoloLayout>
    );

    const grid = container.firstChild as HTMLElement;
    expect(grid).toHaveClass('grid');
    expect(grid).toHaveClass('grid-cols-1');
  });

  it('renders multiple children correctly', () => {
    render(
      <TavoloLayout>
        <div data-testid="child-1">First</div>
        <div data-testid="child-2">Second</div>
      </TavoloLayout>
    );

    expect(screen.getByTestId('child-1')).toBeInTheDocument();
    expect(screen.getByTestId('child-2')).toBeInTheDocument();
  });
});

// ─── TavoloSection Tests ──────────────────────────────────────────────────────

describe('TavoloSection', () => {
  it('renders the icon', () => {
    render(
      <TavoloSection icon="🎲" title="Sessioni Recenti">
        <div>content</div>
      </TavoloSection>
    );

    expect(screen.getByText('🎲')).toBeInTheDocument();
  });

  it('renders the title', () => {
    render(
      <TavoloSection icon="🎲" title="Sessioni Recenti">
        <div>content</div>
      </TavoloSection>
    );

    expect(screen.getByText('Sessioni Recenti')).toBeInTheDocument();
  });

  it('renders children', () => {
    render(
      <TavoloSection icon="📚" title="Giochi">
        <div data-testid="section-child">Child Content</div>
      </TavoloSection>
    );

    expect(screen.getByTestId('section-child')).toBeInTheDocument();
  });

  it('renders as a section element', () => {
    const { container } = render(
      <TavoloSection icon="📚" title="Giochi">
        <div>content</div>
      </TavoloSection>
    );

    expect(container.querySelector('section')).toBeInTheDocument();
  });

  it('renders the divider line', () => {
    const { container } = render(
      <TavoloSection icon="📚" title="Giochi">
        <div>content</div>
      </TavoloSection>
    );

    const divider = container.querySelector('.h-px.flex-1');
    expect(divider).toBeInTheDocument();
  });
});

// ─── ActiveSessionCard Tests ──────────────────────────────────────────────────

describe('ActiveSessionCard', () => {
  const mockSession = {
    id: 'session-1',
    gameName: 'Catan',
    gameImageUrl: '/images/catan.jpg',
    playerCount: 4,
    duration: '45 min',
    gameId: 'game-1',
  };

  it('renders the game name via MeepleCard', () => {
    render(<ActiveSessionCard session={mockSession} />);

    expect(screen.getByTestId('meeple-card-title')).toHaveTextContent('Catan');
  });

  it('renders subtitle with player count and duration', () => {
    render(<ActiveSessionCard session={mockSession} />);

    const subtitle = screen.getByTestId('meeple-card-subtitle');
    expect(subtitle).toHaveTextContent('4 giocatori');
    expect(subtitle).toHaveTextContent('45 min');
  });

  it('renders the active badge', () => {
    render(<ActiveSessionCard session={mockSession} />);

    expect(screen.getByTestId('meeple-card-badge')).toHaveTextContent('Attiva');
  });

  it('applies the emerald left border class', () => {
    const { container } = render(<ActiveSessionCard session={mockSession} />);

    const wrapper = container.firstChild as HTMLElement;
    expect(wrapper).toHaveClass('border-l-emerald-500');
  });

  it('works without optional fields (gameImageUrl, gameId)', () => {
    const minimalSession = {
      id: 'session-2',
      gameName: 'Chess',
      playerCount: 2,
      duration: '30 min',
    };

    render(<ActiveSessionCard session={minimalSession} />);

    expect(screen.getByTestId('meeple-card-title')).toHaveTextContent('Chess');
  });
});
