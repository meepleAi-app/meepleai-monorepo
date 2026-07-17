import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PuertoRicoPlayerMatSummary } from '../PuertoRicoPlayerMatSummary';
import { emptyPuertoRicoPlayerState } from '../puerto-rico-state';

const player = {
  id: 'p1',
  userId: null,
  displayName: 'Marco',
  avatarUrl: null,
  color: 'Red',
  role: 'Host',
  teamId: null,
  totalScore: 0,
  currentRank: 1,
  joinedAt: '',
  isActive: true,
} as const;
const labels = {
  doubloonsLabel: 'Dobloni',
  colonistsLabel: 'Coloni',
  plantationsLabel: 'Piantagioni',
  quarriesLabel: 'Cave',
  buildingsLabel: 'Edifici',
  incAria: '{field} +1',
  decAria: '{field} -1',
};

describe('PuertoRicoPlayerMatSummary', () => {
  it('renders the name + all 5 goods', () => {
    const { container } = render(
      <PuertoRicoPlayerMatSummary
        player={player}
        state={{
          ...emptyPuertoRicoPlayerState(),
          storehouse: { corn: 2, indigo: 0, sugar: 1, tobacco: 0, coffee: 3 },
        }}
        editable={false}
        labels={labels}
      />
    );
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(container.querySelectorAll('[data-good]')).toHaveLength(5);
  });

  it('read-only mode exposes no steppers', () => {
    render(
      <PuertoRicoPlayerMatSummary
        player={player}
        state={emptyPuertoRicoPlayerState()}
        editable={false}
        labels={labels}
      />
    );
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: doubloons +1 fires onBumpCounter', async () => {
    const onBumpCounter = vi.fn();
    render(
      <PuertoRicoPlayerMatSummary
        player={player}
        state={emptyPuertoRicoPlayerState()}
        editable
        onBumpCounter={onBumpCounter}
        labels={labels}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Dobloni +1' }));
    expect(onBumpCounter).toHaveBeenCalledWith('doubloons', 1);
  });

  it('host: a good stepper fires onBumpGood', async () => {
    const onBumpGood = vi.fn();
    const { container } = render(
      <PuertoRicoPlayerMatSummary
        player={player}
        state={emptyPuertoRicoPlayerState()}
        editable
        onBumpGood={onBumpGood}
        labels={labels}
      />
    );
    const cornInc = container.querySelector('[data-good="corn"] [data-dir="inc"]') as HTMLElement;
    await userEvent.click(cornInc);
    expect(onBumpGood).toHaveBeenCalledWith('corn', 1);
  });
});
