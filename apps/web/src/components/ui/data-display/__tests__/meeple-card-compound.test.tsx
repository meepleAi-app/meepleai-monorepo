import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { MeepleCards } from '../meeple-card-compound';

describe('MeepleCards compound component', () => {
  it('renders MeepleCards.Game with entity="game"', () => {
    render(<MeepleCards.Game title="Catan" variant="grid" data-testid="card" />);
    const card = screen.getByTestId('card');
    expect(card).toBeInTheDocument();
  });

  it('renders MeepleCards.Player with entity="player"', () => {
    render(<MeepleCards.Player title="Marco" variant="list" data-testid="card" />);
    expect(screen.getByTestId('card')).toBeInTheDocument();
  });

  it('renders MeepleCards.Agent with entity="agent"', () => {
    render(<MeepleCards.Agent title="RAG Bot" variant="compact" data-testid="card" />);
    expect(screen.getByTestId('card')).toBeInTheDocument();
  });

  it('renders MeepleCards.Session with entity="session"', () => {
    render(<MeepleCards.Session title="Friday Night" variant="grid" data-testid="card" />);
    expect(screen.getByTestId('card')).toBeInTheDocument();
  });

  it('renders MeepleCards.Event with entity="event"', () => {
    render(<MeepleCards.Event title="Tournament" variant="featured" data-testid="card" />);
    expect(screen.getByTestId('card')).toBeInTheDocument();
  });

  it('renders MeepleCards.Document with entity="document"', () => {
    render(<MeepleCards.Document title="Rulebook.pdf" variant="compact" data-testid="card" />);
    expect(screen.getByTestId('card')).toBeInTheDocument();
  });

  it('renders MeepleCards.Toolkit with entity="toolkit"', () => {
    render(<MeepleCards.Toolkit title="Catan Tools" variant="grid" data-testid="card" />);
    expect(screen.getByTestId('card')).toBeInTheDocument();
  });

  it('renders MeepleCards.Base with dynamic entity', () => {
    render(<MeepleCards.Base entity="custom" title="Custom" variant="grid" data-testid="card" />);
    expect(screen.getByTestId('card')).toBeInTheDocument();
  });

  it('passes through all props to underlying MeepleCard', () => {
    render(
      <MeepleCards.Game
        title="Azul"
        subtitle="Next Move Games"
        variant="grid"
        rating={4.2}
        ratingMax={5}
        data-testid="card"
      />
    );
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Next Move Games')).toBeInTheDocument();
  });

  it('has correct display names for debugging', () => {
    expect(MeepleCards.Game.displayName).toBe('MeepleCards.Game');
    expect(MeepleCards.Player.displayName).toBe('MeepleCards.Player');
    expect(MeepleCards.Session.displayName).toBe('MeepleCards.Session');
    expect(MeepleCards.Agent.displayName).toBe('MeepleCards.Agent');
    expect(MeepleCards.Event.displayName).toBe('MeepleCards.Event');
  });

  it('exposes all entity types', () => {
    const keys = Object.keys(MeepleCards);
    expect(keys).toContain('Game');
    expect(keys).toContain('Player');
    expect(keys).toContain('Session');
    expect(keys).toContain('Agent');
    expect(keys).toContain('Document');
    expect(keys).toContain('ChatSession');
    expect(keys).toContain('Event');
    expect(keys).toContain('Toolkit');
    expect(keys).toContain('KbCard');
    expect(keys).toContain('Base');
  });
});
