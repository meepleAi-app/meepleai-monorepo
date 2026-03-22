import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { ExplorationView } from '../exploration/ExplorationView';

// Mock child components so tests are isolated from their internals
vi.mock('../exploration/HeroCompact', () => ({
  HeroCompact: ({ userName }: { userName: string }) => (
    <div data-testid="hero-compact">{userName}</div>
  ),
}));

vi.mock('../exploration/ActiveSessionBanner', () => ({
  ActiveSessionBanner: ({ gameName }: { gameName: string }) => (
    <div data-testid="active-session-banner">{gameName}</div>
  ),
}));

vi.mock('../exploration/CarouselSection', () => ({
  CarouselSection: ({ title, children }: { title: string; children: React.ReactNode }) => (
    <section data-testid={`carousel-${title}`}>
      <h2>{title}</h2>
      {children}
    </section>
  ),
}));

vi.mock('../exploration/QuickStats', () => ({
  QuickStats: ({ totalGames }: { totalGames: number }) => (
    <div data-testid="quick-stats">{totalGames}</div>
  ),
}));

// next/link is needed by child components indirectly; mock it to avoid navigation errors
vi.mock('next/link', () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

describe('ExplorationView', () => {
  const defaultProps = {
    userName: 'Luca',
    gamesThisWeek: 3,
    hoursPlayed: 5,
    avgRating: 7.8,
    totalGames: 42,
    totalSessions: 18,
  };

  it('renders HeroCompact with the provided userName', () => {
    render(<ExplorationView {...defaultProps} />);
    expect(screen.getByTestId('hero-compact')).toHaveTextContent('Luca');
  });

  it('renders all three carousel sections', () => {
    render(<ExplorationView {...defaultProps} />);
    expect(screen.getByTestId('carousel-Giochi Recenti')).toBeInTheDocument();
    expect(screen.getByTestId('carousel-Suggeriti per te')).toBeInTheDocument();
    expect(screen.getByTestId('carousel-Sessioni Recenti')).toBeInTheDocument();
  });

  it('renders QuickStats', () => {
    render(<ExplorationView {...defaultProps} />);
    expect(screen.getByTestId('quick-stats')).toBeInTheDocument();
  });

  it('does NOT render ActiveSessionBanner when activeSession is null', () => {
    render(<ExplorationView {...defaultProps} activeSession={null} />);
    expect(screen.queryByTestId('active-session-banner')).not.toBeInTheDocument();
  });

  it('does NOT render ActiveSessionBanner when activeSession is undefined', () => {
    render(<ExplorationView {...defaultProps} />);
    expect(screen.queryByTestId('active-session-banner')).not.toBeInTheDocument();
  });

  it('renders ActiveSessionBanner when activeSession is provided', () => {
    render(
      <ExplorationView
        {...defaultProps}
        activeSession={{ sessionId: 's1', gameName: 'Catan', elapsed: '1h 20m' }}
      />
    );
    expect(screen.getByTestId('active-session-banner')).toHaveTextContent('Catan');
  });
});
