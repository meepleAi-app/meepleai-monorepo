import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ActivityFeed } from '@/components/game-night/ActivityFeed';

describe('ActivityFeed', () => {
  it('renders empty state when no events', () => {
    render(<ActivityFeed events={[]} />);
    expect(screen.getByText(/nessuna attività/i)).toBeInTheDocument();
  });

  it('renders score events with player name and points', () => {
    render(
      <ActivityFeed
        events={[
          {
            id: '1',
            type: 'score',
            playerName: 'Alice',
            value: 10,
            dimension: 'default',
            timestamp: new Date().toISOString(),
          },
        ]}
      />
    );
    expect(screen.getByText(/Alice ha segnato 10 punti/)).toBeInTheDocument();
  });

  it('renders score events with dimension when not default', () => {
    render(
      <ActivityFeed
        events={[
          {
            id: '2',
            type: 'score',
            playerName: 'Bob',
            value: 5,
            dimension: 'military',
            timestamp: new Date().toISOString(),
          },
        ]}
      />
    );
    expect(screen.getByText(/Bob ha segnato 5 punti \(military\)/)).toBeInTheDocument();
  });

  it('renders turn_advance events', () => {
    render(
      <ActivityFeed
        events={[
          {
            id: '3',
            type: 'turn_advance',
            timestamp: new Date().toISOString(),
          },
        ]}
      />
    );
    expect(screen.getByText('Turno avanzato')).toBeInTheDocument();
  });

  it('renders pause and resume events', () => {
    render(
      <ActivityFeed
        events={[
          { id: '4', type: 'pause', timestamp: new Date().toISOString() },
          { id: '5', type: 'resume', timestamp: new Date().toISOString() },
        ]}
      />
    );
    expect(screen.getByText('Partita in pausa')).toBeInTheDocument();
    expect(screen.getByText('Partita ripresa')).toBeInTheDocument();
  });
});
