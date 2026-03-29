import { describe, expect, it, beforeEach } from 'vitest';
import { useGameNightStore } from '../store';

describe('useGameNightStore', () => {
  beforeEach(() => {
    useGameNightStore.setState(useGameNightStore.getInitialState());
  });

  it('starts with empty game nights list', () => {
    const state = useGameNightStore.getState();
    expect(state.gameNights).toEqual([]);
    expect(state.isLoading).toBe(false);
  });

  it('sets game nights', () => {
    const nights = [
      { id: '1', title: 'Friday Night', status: 'upcoming' as const, date: '2026-03-15' },
    ];
    useGameNightStore.getState().setGameNights(nights as any);
    expect(useGameNightStore.getState().gameNights).toHaveLength(1);
  });

  it('sets selected game night', () => {
    useGameNightStore.getState().selectGameNight('gn-1');
    expect(useGameNightStore.getState().selectedId).toBe('gn-1');
  });

  it('manages selected games for a night', () => {
    useGameNightStore.getState().addGame({ id: 'g1', title: 'Catan' } as any);
    expect(useGameNightStore.getState().selectedGames).toHaveLength(1);
    useGameNightStore.getState().removeGame('g1');
    expect(useGameNightStore.getState().selectedGames).toHaveLength(0);
  });

  it('manages players', () => {
    useGameNightStore.getState().addPlayer({ id: 'p1', name: 'Alice' } as any);
    expect(useGameNightStore.getState().players).toHaveLength(1);
    useGameNightStore.getState().removePlayer('p1');
    expect(useGameNightStore.getState().players).toHaveLength(0);
  });
});
