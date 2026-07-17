import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { usePuertoRicoStateEditor } from '../use-puerto-rico-state-editor';
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
  return useLiveSessionStore.getState().gameState as
    | import('../puerto-rico-state').PuertoRicoGameState
    | null;
}

describe('usePuertoRicoStateEditor', () => {
  it('initializeState seeds players + 3 galleons', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1', 'p2']));
    act(() => result.current.initializeState());
    expect(Object.keys(current()?.players ?? {})).toEqual(['p1', 'p2']);
    expect(current()?.galleons).toHaveLength(3);
  });

  it('bumpPlayerCounter clamps at 0', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.bumpPlayerCounter('p1', 'doubloons', -1));
    expect(current()?.players.p1?.doubloons).toBe(0);
    act(() => result.current.bumpPlayerCounter('p1', 'doubloons', 1));
    expect(current()?.players.p1?.doubloons).toBe(1);
  });

  it('bumpPlayerGood clamps at 0 and targets the right good', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.bumpPlayerGood('p1', 'sugar', 1));
    expect(current()?.players.p1?.storehouse.sugar).toBe(1);
    expect(current()?.players.p1?.storehouse.corn).toBe(0);
  });

  it('setGalleonGood resets that ship loaded to 0; bumpGalleonLoaded caps at cap', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1', 'p2'])); // caps [3,4,5]
    act(() => result.current.initializeState());
    act(() => result.current.bumpGalleonLoaded(0, 1));
    act(() => result.current.setGalleonGood(0, 'corn'));
    expect(current()?.galleons[0]).toEqual({ good: 'corn', loaded: 0, cap: 3 });
    for (let i = 0; i < 9; i++) act(() => result.current.bumpGalleonLoaded(0, 1));
    expect(current()?.galleons[0]?.loaded).toBe(3); // capped at cap
  });

  it('setTradingSlot writes the good at the slot', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.setTradingSlot(2, 'coffee'));
    expect(current()?.tradingHouse.slots).toEqual([null, null, 'coffee', null]);
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1']));
    act(() => result.current.bumpPlayerCounter('p1', 'doubloons', 1));
    expect(current()).toBeNull();
  });

  it('eventually PUTs (debounced)', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => usePuertoRicoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => vi.advanceTimersByTime(600));
    expect(mutateMock).toHaveBeenCalled();
    vi.useRealTimers();
  });
});
