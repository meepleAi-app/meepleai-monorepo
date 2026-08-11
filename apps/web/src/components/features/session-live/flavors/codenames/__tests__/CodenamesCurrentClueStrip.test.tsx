import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CodenamesCurrentClueStrip } from '../CodenamesCurrentClueStrip';

const labels = {
  noClue: 'Nessun indizio',
  wordPlaceholder: 'Parola',
  numberAria: 'Numero',
  giveClue: 'Dai indizio',
  endTurn: 'Fine turno',
};

describe('CodenamesCurrentClueStrip', () => {
  it('read-only shows the active clue as WORD : NUMBER', () => {
    render(
      <CodenamesCurrentClueStrip
        clue={{ word: 'MARE', number: 3 }}
        currentTeam="red"
        editable={false}
        labels={labels}
      />
    );
    expect(screen.getByText(/MARE/)).toBeInTheDocument();
    expect(screen.getByText(/3/)).toBeInTheDocument();
  });

  it('read-only with no clue shows the empty label + no inputs', () => {
    render(
      <CodenamesCurrentClueStrip clue={null} currentTeam="red" editable={false} labels={labels} />
    );
    expect(screen.getByText('Nessun indizio')).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).toBeNull();
  });

  it('host: typing a word + clicking give fires onSetClue', async () => {
    const onSetClue = vi.fn();
    render(
      <CodenamesCurrentClueStrip
        clue={null}
        currentTeam="red"
        editable
        onSetClue={onSetClue}
        labels={labels}
      />
    );
    await userEvent.type(screen.getByRole('textbox'), 'MARE');
    await userEvent.click(screen.getByRole('button', { name: 'Dai indizio' }));
    expect(onSetClue).toHaveBeenCalled();
    expect(onSetClue.mock.calls[0][0]).toBe('MARE');
  });

  it('host: end-turn button fires onSwitchTeam', async () => {
    const onSwitchTeam = vi.fn();
    render(
      <CodenamesCurrentClueStrip
        clue={null}
        currentTeam="red"
        editable
        onSwitchTeam={onSwitchTeam}
        labels={labels}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Fine turno' }));
    expect(onSwitchTeam).toHaveBeenCalledOnce();
  });
});
