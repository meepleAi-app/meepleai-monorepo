import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { WingspanRoundTracker } from '../WingspanRoundTracker';

const labels = {
  heading: 'Round',
  roundTemplate: 'Round {n}/4',
  turnBudgetTemplate: '{n} turni',
  goalsHeading: 'Obiettivi',
  goalPlaceholderTemplate: 'Obiettivo round {n}',
  advanceRoundLabel: 'Avanza round',
};
const state = {
  v: 1 as const,
  game: 'wingspan' as const,
  round: 2,
  roundGoals: [{ label: 'Nidi' }],
};

describe('WingspanRoundTracker', () => {
  it('shows the current round and its turn budget', () => {
    render(<WingspanRoundTracker state={state} editable={false} labels={labels} />);
    expect(screen.getByText('Round 2/4')).toBeInTheDocument();
    expect(screen.getByText('7 turni')).toBeInTheDocument(); // budget[1] = 7
  });

  it('read-only mode exposes no controls', () => {
    render(<WingspanRoundTracker state={state} editable={false} labels={labels} />);
    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.queryByRole('textbox')).toBeNull();
  });

  it('host mode: advance-round button fires onAdvanceRound', async () => {
    const onAdvanceRound = vi.fn();
    render(
      <WingspanRoundTracker
        state={state}
        editable
        onAdvanceRound={onAdvanceRound}
        labels={labels}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Avanza round' }));
    expect(onAdvanceRound).toHaveBeenCalledOnce();
  });

  it('host mode: editing a goal input fires onSetRoundGoal', async () => {
    const onSetRoundGoal = vi.fn();
    render(
      <WingspanRoundTracker
        state={{ ...state, roundGoals: [] }}
        editable
        onSetRoundGoal={onSetRoundGoal}
        labels={labels}
      />
    );
    const firstGoal = screen.getAllByRole('textbox')[0];
    await userEvent.type(firstGoal, 'X');
    expect(onSetRoundGoal).toHaveBeenCalledWith(0, 'X');
  });
});
