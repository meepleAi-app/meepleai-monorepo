import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { useWingspanStateEditor } from '../use-wingspan-state-editor';
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
    | import('../wingspan-state').WingspanGameState
    | null;
}

describe('useWingspanStateEditor', () => {
  it('initializeState writes round 1 + empty goals optimistically', () => {
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.initializeState());
    expect(current()).toEqual({ v: 1, game: 'wingspan', round: 1, roundGoals: [] });
  });

  it('advanceRound increments and caps at 4', () => {
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.advanceRound());
    expect(current()?.round).toBe(2);
    act(() => result.current.setRound(4));
    act(() => result.current.advanceRound());
    expect(current()?.round).toBe(4);
  });

  it('setRound clamps to 1..4', () => {
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setRound(9));
    expect(current()?.round).toBe(4);
    act(() => result.current.setRound(0));
    expect(current()?.round).toBe(1);
  });

  it('setRoundGoal writes the label at the index (padding earlier slots)', () => {
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setRoundGoal(1, 'Uova nel forest'));
    expect(current()?.roundGoals).toEqual([{ label: '' }, { label: 'Uova nel forest' }]);
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.advanceRound());
    expect(current()).toBeNull();
  });

  it('eventually PUTs the state (debounced)', () => {
    vi.useFakeTimers();
    const { result } = renderHook(() => useWingspanStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => vi.advanceTimersByTime(600));
    expect(mutateMock).toHaveBeenCalled();
    vi.useRealTimers();
  });
});
