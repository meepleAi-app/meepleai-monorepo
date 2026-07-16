import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { useCatanStateEditor } from '../use-catan-state-editor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

const mutateMock = vi.fn<[unknown], void>();
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: mutateMock }),
}));

const SID = 'sess-1';

beforeEach(() => {
  mutateMock.mockReset();
  useLiveSessionStore.getState().reset();
});

function current() {
  return useLiveSessionStore.getState().gameState as import('../catan-state').CatanGameState | null;
}

describe('useCatanStateEditor', () => {
  it('initializeState seeds a board + zeroed players and writes the store optimistically', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1', 'p2']));
    act(() => result.current.initializeState());
    const s = current();
    expect(s?.game).toBe('catan');
    expect(s?.board.hexes).toHaveLength(19);
    expect(Object.keys(s?.players ?? {})).toEqual(['p1', 'p2']);
    expect(s?.players.p1?.handSize).toBe(0);
  });

  it('setDiceRoll sets last + prepends history', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.setDiceRoll(8));
    act(() => result.current.setDiceRoll(6));
    expect(current()?.dice.last).toBe(6);
    expect(current()?.dice.history).toEqual([6, 8]);
  });

  it('moveRobber updates robberHexId', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.moveRobber('h5'));
    expect(current()?.board.robberHexId).toBe('h5');
  });

  it('bumpBuilt clamps to [0, total]', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.bumpBuilt('p1', 'cities', -1));
    expect(current()?.players.p1?.built.cities).toBe(0); // clamp at 0
    for (let i = 0; i < 6; i++) act(() => result.current.bumpBuilt('p1', 'cities', 1));
    expect(current()?.players.p1?.built.cities).toBe(4); // clamp at total
  });

  it('toggleBadge is exclusive across players', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1', 'p2']));
    act(() => result.current.initializeState());
    act(() => result.current.toggleBadge('p1', 'longestRoad'));
    expect(current()?.players.p1?.badges.longestRoad).toBe(true);
    act(() => result.current.toggleBadge('p2', 'longestRoad'));
    expect(current()?.players.p1?.badges.longestRoad).toBe(false);
    expect(current()?.players.p2?.badges.longestRoad).toBe(true);
  });

  it('mutators are no-ops when state is null (host has not initialized)', () => {
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1']));
    act(() => result.current.setDiceRoll(8));
    expect(current()).toBeNull();
  });

  it('eventually PUTs the state (debounced) via the mutation', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => useCatanStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => vi.advanceTimersByTime(600));
    expect(mutateMock).toHaveBeenCalled();
    const lastArg = mutateMock.mock.calls.at(-1)?.[0] as import('../catan-state').CatanGameState;
    expect(lastArg.game).toBe('catan');
    vi.useRealTimers();
  });
});
