import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { PaleoTribePanel } from '../PaleoTribePanel';

const players = [
  {
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
  },
  {
    id: 'p2',
    userId: null,
    displayName: 'Anna',
    avatarUrl: null,
    color: 'Blue',
    role: 'Player',
    teamId: null,
    totalScore: 0,
    currentRank: 2,
    joinedAt: '',
    isActive: true,
  },
] as const;
const labels = {
  heading: 'Tribù',
  statusAlive: 'Vivo',
  statusWounded: 'Ferito',
  statusDead: 'Morto',
  cycleAria: '{name}: cambia stato',
};

describe('PaleoTribePanel', () => {
  it('renders a row per player with a status badge', () => {
    const { container } = render(
      <PaleoTribePanel
        players={players}
        survivors={{ p1: 'alive', p2: 'wounded' }}
        editable={false}
        labels={labels}
      />
    );
    expect(container.querySelectorAll('[data-slot="paleo-tribe-row"]')).toHaveLength(2);
    expect(screen.getByText('Marco')).toBeInTheDocument();
  });

  it('defaults a missing player to alive', () => {
    render(
      <PaleoTribePanel
        players={players}
        survivors={{ p1: 'dead' }}
        editable={false}
        labels={labels}
      />
    );
    // p2 absent from survivors → shows the alive label
    expect(screen.getByText('Vivo')).toBeInTheDocument();
  });

  it('read-only exposes no buttons', () => {
    render(
      <PaleoTribePanel
        players={players}
        survivors={{ p1: 'alive', p2: 'alive' }}
        editable={false}
        labels={labels}
      />
    );
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: tapping a row fires onCycle with the player id', async () => {
    const onCycle = vi.fn();
    render(
      <PaleoTribePanel
        players={players}
        survivors={{ p1: 'alive', p2: 'alive' }}
        editable
        onCycle={onCycle}
        labels={labels}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Marco: cambia stato' }));
    expect(onCycle).toHaveBeenCalledWith('p1');
  });
});
