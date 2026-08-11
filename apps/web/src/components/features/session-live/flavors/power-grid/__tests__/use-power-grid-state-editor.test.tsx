import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { usePowerGridStateEditor } from '../use-power-grid-state-editor';
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
    | import('../power-grid-state').PowerGridGameState
    | null;
}

describe('usePowerGridStateEditor', () => {
  it('initializeState seeds 8 null slots + 0 resources', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.initializeState());
    expect(current()?.plants.current).toEqual([null, null, null, null]);
    expect(current()?.resources).toEqual({ coal: 0, oil: 0, garbage: 0, uranium: 0 });
  });

  it('bumpResource clamps at 0', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.bumpResource('coal', -1));
    expect(current()?.resources.coal).toBe(0);
    act(() => result.current.bumpResource('coal', 1));
    expect(current()?.resources.coal).toBe(1);
  });

  it('setPlant sets a number, clears with null, clamps negative to 0', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setPlant('current', 1, 15));
    expect(current()?.plants.current[1]).toBe(15);
    act(() => result.current.setPlant('current', 1, null));
    expect(current()?.plants.current[1]).toBeNull();
    act(() => result.current.setPlant('future', 0, -4));
    expect(current()?.plants.future[0]).toBe(0);
  });

  it('setPlant out-of-range index is a no-op', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setPlant('current', 9, 5));
    expect(current()?.plants.current).toEqual([null, null, null, null]);
  });

  it('setPlant PUTs immediately (no debounce wait)', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.initializeState());
    mutateMock.mockClear();
    act(() => result.current.setPlant('current', 0, 7));
    expect(mutateMock).toHaveBeenCalled(); // immediate, without advancing timers
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => usePowerGridStateEditor(SID));
    act(() => result.current.bumpResource('coal', 1));
    act(() => result.current.setPlant('current', 0, 3));
    expect(current()).toBeNull();
  });
});
