import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ActivityFeedEvent } from '../ActivityFeedEvent';

describe('ActivityFeedEvent', () => {
  it('renders a dice roll event', () => {
    render(
      <ActivityFeedEvent
        event={{
          id: 'e1',
          type: 'dice_roll',
          playerId: 'p1',
          data: { values: [3, 4], total: 7, playerName: 'Alice' },
          timestamp: '2026-03-11T19:00:00Z',
        }}
      />
    );
    expect(screen.getByText(/7/)).toBeInTheDocument();
    expect(screen.getByText(/Alice/)).toBeInTheDocument();
  });

  it('renders a score update event', () => {
    render(
      <ActivityFeedEvent
        event={{
          id: 'e2',
          type: 'score_update',
          playerId: 'p1',
          data: { playerName: 'Bob', action: '+5', newScore: 15 },
          timestamp: '2026-03-11T19:05:00Z',
        }}
      />
    );
    expect(screen.getByText(/Bob/)).toBeInTheDocument();
    expect(screen.getByText(/15/)).toBeInTheDocument();
  });

  it('renders a note event', () => {
    render(
      <ActivityFeedEvent
        event={{
          id: 'e3',
          type: 'note',
          playerId: 'p1',
          data: { playerName: 'Carol', text: 'Great round!' },
          timestamp: '2026-03-11T19:10:00Z',
        }}
      />
    );
    expect(screen.getByText('Great round!')).toBeInTheDocument();
  });

  it('renders an AI tip event', () => {
    render(
      <ActivityFeedEvent
        event={{
          id: 'e4',
          type: 'ai_tip',
          data: { text: 'Remember the trading rule!' },
          timestamp: '2026-03-11T19:15:00Z',
        }}
      />
    );
    expect(screen.getByText(/trading rule/)).toBeInTheDocument();
  });

  it('shows formatted timestamp', () => {
    render(
      <ActivityFeedEvent
        event={{
          id: 'e5',
          type: 'turn_change',
          data: { from: 1, to: 2, playerName: 'Dan' },
          timestamp: '2026-03-11T19:20:00Z',
        }}
      />
    );
    expect(screen.getByTestId('activity-event')).toBeInTheDocument();
  });
});
