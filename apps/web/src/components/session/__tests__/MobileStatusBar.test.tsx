import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, beforeEach } from 'vitest';

import { useSessionStore } from '@/stores/session';
import { MobileStatusBar } from '../MobileStatusBar';

describe('MobileStatusBar', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('renders game name', () => {
    render(<MobileStatusBar gameName="Catan" currentPlayer="Alice" />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('shows LIVE indicator when session is live', () => {
    useSessionStore.getState().startSession('s1', 'g1');
    render(<MobileStatusBar gameName="Catan" currentPlayer="Alice" />);
    expect(screen.getByText('LIVE')).toBeInTheDocument();
  });

  it('shows current turn', () => {
    useSessionStore.getState().startSession('s1', 'g1');
    render(<MobileStatusBar gameName="Catan" currentPlayer="Alice" />);
    expect(screen.getByText(/turno 1/i)).toBeInTheDocument();
  });

  it('shows pause button', () => {
    useSessionStore.getState().startSession('s1', 'g1');
    render(<MobileStatusBar gameName="Catan" currentPlayer="Alice" />);
    expect(screen.getByRole('button', { name: /pausa/i })).toBeInTheDocument();
  });

  it('toggles pause on click', async () => {
    const user = userEvent.setup();
    useSessionStore.getState().startSession('s1', 'g1');
    render(<MobileStatusBar gameName="Catan" currentPlayer="Alice" />);
    await user.click(screen.getByRole('button', { name: /pausa/i }));
    expect(useSessionStore.getState().isPaused).toBe(true);
  });
});
