import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ZombicideSurvivorsPanel } from '../ZombicideSurvivorsPanel';

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
  heading: 'Sopravvissuti',
  healthy: 'Illeso',
  wounded: 'Ferito',
  down: 'A terra',
  cycleAria: '{name}: cambia ferite',
};

describe('ZombicideSurvivorsPanel', () => {
  it('renders a row per player with a wound badge', () => {
    const { container } = render(
      <ZombicideSurvivorsPanel
        players={players}
        survivors={{ p1: 0, p2: 1 }}
        editable={false}
        labels={labels}
      />
    );
    expect(container.querySelectorAll('[data-slot="zc-survivor-row"]')).toHaveLength(2);
    expect(screen.getByText('Marco')).toBeInTheDocument();
  });

  it('defaults a missing player to healthy (0)', () => {
    render(
      <ZombicideSurvivorsPanel
        players={players}
        survivors={{ p1: 2 }}
        editable={false}
        labels={labels}
      />
    );
    expect(screen.getByText('Illeso')).toBeInTheDocument(); // p2 absent → healthy
  });

  it('flags a down survivor (wounds=2)', () => {
    const { container } = render(
      <ZombicideSurvivorsPanel
        players={players}
        survivors={{ p1: 2, p2: 0 }}
        editable={false}
        labels={labels}
      />
    );
    expect(
      container.querySelector('[data-slot="zc-survivor-row"][data-down="true"]')
    ).not.toBeNull();
  });

  it('read-only exposes no buttons', () => {
    render(
      <ZombicideSurvivorsPanel
        players={players}
        survivors={{ p1: 0, p2: 0 }}
        editable={false}
        labels={labels}
      />
    );
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host: tapping a row fires onCycle with the player id', async () => {
    const onCycle = vi.fn();
    render(
      <ZombicideSurvivorsPanel
        players={players}
        survivors={{ p1: 0, p2: 0 }}
        editable
        onCycle={onCycle}
        labels={labels}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Marco: cambia ferite' }));
    expect(onCycle).toHaveBeenCalledWith('p1');
  });
});
