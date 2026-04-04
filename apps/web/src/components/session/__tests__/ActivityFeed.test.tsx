import { render, screen } from '@testing-library/react';
import { describe, expect, it, beforeEach } from 'vitest';
import { useSessionStore } from '@/stores/session';
import { ActivityFeed } from '../ActivityFeed';

describe('ActivityFeed', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('shows empty state when no events', () => {
    render(<ActivityFeed />);
    expect(screen.getByText(/sessione appena iniziata/i)).toBeInTheDocument();
  });

  it('renders events from store', () => {
    useSessionStore.getState().addEvent({
      id: 'e1',
      type: 'note',
      data: { playerName: 'Alice', text: 'Nice!' },
      timestamp: new Date().toISOString(),
    });
    render(<ActivityFeed />);
    expect(screen.getByText('Nice!')).toBeInTheDocument();
  });

  it('renders events in reverse chronological order', () => {
    useSessionStore.getState().addEvent({
      id: 'e1',
      type: 'note',
      data: { playerName: 'A', text: 'First' },
      timestamp: '2026-03-11T19:00:00Z',
    });
    useSessionStore.getState().addEvent({
      id: 'e2',
      type: 'note',
      data: { playerName: 'B', text: 'Second' },
      timestamp: '2026-03-11T19:01:00Z',
    });
    render(<ActivityFeed />);
    const events = screen.getAllByTestId('activity-event');
    expect(events).toHaveLength(2);
  });
});
