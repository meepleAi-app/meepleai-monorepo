import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CatanPlayerCard } from '../CatanPlayerCard';
import { emptyCatanPlayerState } from '../catan-state';

const player = {
  id: 'p1',
  userId: null,
  displayName: 'Marco',
  avatarUrl: null,
  color: 'Red',
  role: 'Host',
  teamId: null,
  totalScore: 8,
  currentRank: 1,
  joinedAt: '',
  isActive: true,
} as const;
const labels = {
  vpLabel: 'PV',
  handLabel: 'Mano',
  devLabel: 'Sviluppo',
  settlementsLabel: 'Insediamenti',
  citiesLabel: 'Città',
  roadsLabel: 'Strade',
  longestRoadLabel: 'Strada+',
  largestArmyLabel: 'Armata+',
  incAriaTemplate: '{field} +1',
  decAriaTemplate: '{field} -1',
};

describe('CatanPlayerCard', () => {
  it('shows name, VP and hand size (read-only)', () => {
    render(
      <CatanPlayerCard
        player={player}
        state={{ ...emptyCatanPlayerState(), handSize: 7 }}
        vp={8}
        editable={false}
        labels={labels}
      />
    );
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument(); // VP
    expect(screen.getByText('7')).toBeInTheDocument(); // hand
  });

  it('read-only mode exposes no steppers', () => {
    render(
      <CatanPlayerCard
        player={player}
        state={emptyCatanPlayerState()}
        vp={0}
        editable={false}
        labels={labels}
      />
    );
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('host mode: stepper fires onBumpBuilt', async () => {
    const onBumpBuilt = vi.fn();
    render(
      <CatanPlayerCard
        player={player}
        state={emptyCatanPlayerState()}
        vp={0}
        editable
        onBumpBuilt={onBumpBuilt}
        labels={labels}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Insediamenti +1' }));
    expect(onBumpBuilt).toHaveBeenCalledWith('settlements', 1);
  });

  it('host mode: badge toggle fires onToggleBadge', async () => {
    const onToggleBadge = vi.fn();
    render(
      <CatanPlayerCard
        player={player}
        state={emptyCatanPlayerState()}
        vp={0}
        editable
        onToggleBadge={onToggleBadge}
        labels={labels}
      />
    );
    await userEvent.click(screen.getByRole('button', { name: 'Strada+' }));
    expect(onToggleBadge).toHaveBeenCalledWith('longestRoad');
  });
});
