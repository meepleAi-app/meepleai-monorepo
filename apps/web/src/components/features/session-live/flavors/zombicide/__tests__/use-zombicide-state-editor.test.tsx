import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { useZombicideStateEditor } from '../use-zombicide-state-editor';
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
    | import('../zombicide-state').ZombicideGameState
    | null;
}

describe('useZombicideStateEditor', () => {
  it('initializeState seeds zombies 0 + players 0 wounds', () => {
    const { result } = renderHook(() => useZombicideStateEditor(SID, ['p1', 'p2']));
    act(() => result.current.initializeState());
    expect(current()?.zombies.walker).toBe(0);
    expect(current()?.survivors).toEqual({ p1: 0, p2: 0 });
  });

  it('bumpZombie clamps at 0', () => {
    const { result } = renderHook(() => useZombicideStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.bumpZombie('walker', -1));
    expect(current()?.zombies.walker).toBe(0);
    act(() => result.current.bumpZombie('walker', 1));
    expect(current()?.zombies.walker).toBe(1);
  });

  it('cycleWound advances 0 → 1 and PUTs immediately', () => {
    const { result } = renderHook(() => useZombicideStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    mutateMock.mockClear();
    act(() => result.current.cycleWound('p1'));
    expect(current()?.survivors.p1).toBe(1);
    expect(mutateMock).toHaveBeenCalled(); // immediate, no timer advance
  });

  it('cycleWound folds in a missing player (0 → 1)', () => {
    const { result } = renderHook(() => useZombicideStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.cycleWound('pX'));
    expect(current()?.survivors.pX).toBe(1);
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => useZombicideStateEditor(SID, ['p1']));
    act(() => result.current.bumpZombie('walker', 1));
    act(() => result.current.cycleWound('p1'));
    expect(current()).toBeNull();
  });
});
