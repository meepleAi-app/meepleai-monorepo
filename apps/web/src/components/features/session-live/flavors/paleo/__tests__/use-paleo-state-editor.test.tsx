import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { usePaleoStateEditor } from '../use-paleo-state-editor';
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
  return useLiveSessionStore.getState().gameState as import('../paleo-state').PaleoGameState | null;
}

describe('usePaleoStateEditor', () => {
  it('initializeState seeds resources 0 + players alive', () => {
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1', 'p2']));
    act(() => result.current.initializeState());
    expect(current()?.resources).toEqual({ wood: 0, stone: 0, food: 0, knowledge: 0 });
    expect(current()?.survivors).toEqual({ p1: 'alive', p2: 'alive' });
  });

  it('bumpResource clamps at 0', () => {
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.bumpResource('wood', -1));
    expect(current()?.resources.wood).toBe(0);
    act(() => result.current.bumpResource('wood', 1));
    expect(current()?.resources.wood).toBe(1);
  });

  it('cycleSurvivorStatus advances alive → wounded', () => {
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.cycleSurvivorStatus('p1'));
    expect(current()?.survivors.p1).toBe('wounded');
  });

  it('cycleSurvivorStatus folds in a missing player (defaults alive → wounded)', () => {
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => result.current.cycleSurvivorStatus('pX'));
    expect(current()?.survivors.pX).toBe('wounded');
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1']));
    act(() => result.current.bumpResource('wood', 1));
    expect(current()).toBeNull();
  });

  it('eventually PUTs (debounced)', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => usePaleoStateEditor(SID, ['p1']));
    act(() => result.current.initializeState());
    act(() => vi.advanceTimersByTime(600));
    expect(mutateMock).toHaveBeenCalled();
    vi.useRealTimers();
  });
});
