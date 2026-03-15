import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { HeroZone } from '../hero-zone';

vi.mock('../session-hero', () => ({
  DashboardSessionHero: ({ lastSession }: any) => (
    <div data-testid="session-hero">{lastSession?.gameName ?? 'active'}</div>
  ),
}));
vi.mock('../game-night-hero', () => ({
  GameNightHero: ({ gameNight }: any) => <div data-testid="game-night-hero">{gameNight.title}</div>,
}));
vi.mock('../incomplete-session-hero', () => ({
  IncompleteSessionHero: ({ session }: any) => (
    <div data-testid="incomplete-session-hero">{session.gameName}</div>
  ),
}));

describe('HeroZone', () => {
  it('renders active session hero when context type is active-session', () => {
    render(
      <HeroZone
        hero={{ type: 'active-session', priority: 100, data: { id: 's1' } }}
        lastSession={undefined}
      />
    );
    expect(screen.getByTestId('session-hero')).toBeInTheDocument();
  });

  it('renders game night hero when context type is upcoming-game-night', () => {
    render(
      <HeroZone
        hero={{
          type: 'upcoming-game-night',
          priority: 90,
          data: { id: 'gn1', title: 'Venerdì nerd', scheduledAt: '2026-03-16T20:00:00Z' },
        }}
        lastSession={undefined}
      />
    );
    expect(screen.getByTestId('game-night-hero')).toHaveTextContent('Venerdì nerd');
  });

  it('renders incomplete session hero when context type is incomplete-session', () => {
    render(
      <HeroZone
        hero={{
          type: 'incomplete-session',
          priority: 80,
          data: { id: 's2', gameName: 'Catan', sessionDate: '2026-03-12T20:00:00Z' },
        }}
        lastSession={undefined}
      />
    );
    expect(screen.getByTestId('incomplete-session-hero')).toHaveTextContent('Catan');
  });

  it('renders session hero (empty state) for last-played context', () => {
    const last = { gameName: 'Catan', sessionDate: '2026-03-12T20:00:00Z' };
    render(
      <HeroZone
        hero={{ type: 'last-played', priority: 50, data: { id: 'g1', title: 'Catan' } }}
        lastSession={last as any}
      />
    );
    expect(screen.getByTestId('session-hero')).toBeInTheDocument();
  });

  it('renders nothing for welcome context with no sessions', () => {
    const { container } = render(
      <HeroZone hero={{ type: 'welcome', priority: 10, data: null }} lastSession={undefined} />
    );
    expect(container.firstChild).toBeNull();
  });
});
