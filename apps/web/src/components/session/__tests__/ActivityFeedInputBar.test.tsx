import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useSessionStore } from '@/store/session';
import { ActivityFeedInputBar } from '../ActivityFeedInputBar';

describe('ActivityFeedInputBar', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('renders text input', () => {
    render(<ActivityFeedInputBar playerId="p1" playerName="Alice" />);
    expect(screen.getByPlaceholderText(/nota/i)).toBeInTheDocument();
  });

  it('renders quick action buttons', () => {
    render(<ActivityFeedInputBar playerId="p1" playerName="Alice" />);
    expect(screen.getByRole('button', { name: /dadi/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /foto/i })).toBeInTheDocument();
  });

  it('adds note event on submit', async () => {
    const user = userEvent.setup();
    render(<ActivityFeedInputBar playerId="p1" playerName="Alice" />);
    const input = screen.getByPlaceholderText(/nota/i);
    await user.type(input, 'Great move!');
    await user.click(screen.getByRole('button', { name: /invia/i }));
    expect(useSessionStore.getState().events).toHaveLength(1);
    expect(useSessionStore.getState().events[0].type).toBe('note');
  });
});
