import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, beforeEach } from 'vitest';

import { useSessionStore } from '@/store/session';
import { MobileSessionLayout } from '../MobileSessionLayout';

const defaultProps = {
  sessionId: 'sess-1',
  gameName: 'Catan',
  currentPlayer: 'Alice',
  players: [
    { id: 'p1', name: 'Alice', score: 10 },
    { id: 'p2', name: 'Bob', score: 8 },
  ],
  playerId: 'p1',
  playerName: 'Alice',
};

describe('MobileSessionLayout', () => {
  beforeEach(() => {
    useSessionStore.getState().reset();
  });

  it('renders status bar with game name', () => {
    render(<MobileSessionLayout {...defaultProps} />);
    expect(screen.getByTestId('mobile-status-bar')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('renders scorebar with players', () => {
    render(<MobileSessionLayout {...defaultProps} />);
    expect(screen.getByTestId('mobile-scorebar')).toBeInTheDocument();
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('renders input bar with note placeholder', () => {
    render(<MobileSessionLayout {...defaultProps} />);
    expect(screen.getByPlaceholderText('Scrivi una nota...')).toBeInTheDocument();
  });

  it('opens dice bottom sheet when dice button is clicked', async () => {
    const user = userEvent.setup();
    render(<MobileSessionLayout {...defaultProps} />);
    await user.click(screen.getByRole('button', { name: /dadi/i }));
    expect(screen.getByText('Lancio dadi in arrivo')).toBeInTheDocument();
  });

  it('opens camera bottom sheet when camera button is clicked', async () => {
    const user = userEvent.setup();
    render(<MobileSessionLayout {...defaultProps} />);
    await user.click(screen.getByRole('button', { name: /foto/i }));
    expect(screen.getByText('Fotocamera in arrivo')).toBeInTheDocument();
  });

  it('opens AI bottom sheet when AI button is clicked', async () => {
    const user = userEvent.setup();
    render(<MobileSessionLayout {...defaultProps} />);
    await user.click(screen.getByRole('button', { name: /ai/i }));
    expect(screen.getByText('Assistente AI in arrivo')).toBeInTheDocument();
  });
});
