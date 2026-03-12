import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, beforeEach } from 'vitest';

import { useSessionStore } from '@/store/session';

import { SimpleDiceRoller } from '../SimpleDiceRoller';

describe('SimpleDiceRoller', () => {
  beforeEach(() => {
    useSessionStore.setState(useSessionStore.getInitialState());
  });

  it('renders dice type selector', () => {
    render(<SimpleDiceRoller playerId="p1" playerName="Alice" />);
    expect(screen.getByLabelText(/tipo dadi/i)).toBeInTheDocument();
  });

  it('shows roll button', () => {
    render(<SimpleDiceRoller playerId="p1" playerName="Alice" />);
    expect(screen.getByRole('button', { name: /lancia/i })).toBeInTheDocument();
  });

  it('adds dice_roll event to store on roll', async () => {
    const user = userEvent.setup();
    render(<SimpleDiceRoller playerId="p1" playerName="Alice" />);
    await user.click(screen.getByRole('button', { name: /lancia/i }));
    const events = useSessionStore.getState().events;
    expect(events).toHaveLength(1);
    expect(events[0].type).toBe('dice_roll');
  });

  it('displays the result after rolling', async () => {
    const user = userEvent.setup();
    render(<SimpleDiceRoller playerId="p1" playerName="Alice" />);
    await user.click(screen.getByRole('button', { name: /lancia/i }));
    expect(screen.getByTestId('dice-result')).toBeInTheDocument();
  });
});
